using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DuoFlow.Camera;
using DuoFlow.Vision;

namespace DuoFlow.App;

/// <summary>
/// M3.1 acceptance probe (`--camera-probe`): enumerate, open the selected
/// camera, take a fixed number of frames, report what actually happened, close.
///
/// <para>
/// <b>Privacy (PROJECT_SPEC 铁律 5): this never persists a pixel.</b> A frame is
/// reduced to a handful of scalars - mean luminance, its standard deviation,
/// the alpha mean, the byte range - the moment it arrives, and only those
/// scalars leave the callback. None of them carries a position, so none of them
/// can be turned back into an image. They are enough to prove the frame is a
/// real decoded picture rather than a zeroed allocation.
/// </para>
/// <para>
/// Why a probe at all instead of a unit test: "can this machine open its camera
/// and receive frames" is a property of the machine, not of the code. It has to
/// be measured on the machine, and it has to be measured the same way on any
/// machine - which is what the JSON + exit code give us.
/// </para>
/// <para>
/// <b>What this gates on, and what it deliberately does not.</b> The verdict is
/// structural: device found, opened, format negotiated, N frames arrived, every
/// frame carries a plausible BGRA alpha channel, health classified. It is NOT
/// gated on the picture having contrast - that depends on the room, and a
/// locked screen with the lens capped produces a perfectly uniform frame from a
/// perfectly working camera. Conflating the two would make the test fail for a
/// reason it cannot fix. Scene contrast is reported instead, with a hint.
/// </para>
/// </summary>
internal static class CameraProbe
{
    public static async Task<int> RunAsync(string outputDir, double targetFps, int frameCount)
    {
        Directory.CreateDirectory(outputDir);
        string jsonPath = Path.Combine(outputDir, "camera-probe-result.json");

        var report = new Dictionary<string, object?>();
        var failures = new List<string>();

        IReadOnlyList<CameraDeviceInfo> devices = Array.Empty<CameraDeviceInfo>();
        CameraDeviceInfo? chosen = null;
        CameraDeviceInfo? opened = null;
        var luminance = new List<double>();
        var contrast = new List<double>();
        var alpha = new List<double>();
        var stamps = new List<double>();
        byte byteMin = 255;
        byte byteMax = 0;
        long offered = 0;
        long accepted = 0;
        long dropped = 0;
        double offeredSpan = 0.0;
        string? error = null;
        CameraFailure health = CameraFailure.None;

        try
        {
            devices = await MediaCaptureCamera.EnumerateAsync();
            Trace.Log($"camera-probe: enumerated {devices.Count} device(s)");
            foreach (CameraDeviceInfo d in devices)
            {
                Trace.Log($"camera-probe:   [{d.Enclosure}] {d.Name} enabled={d.IsEnabled} id={d.Id}");
            }

            chosen = CameraSelection.Choose(devices);
            if (chosen is null)
            {
                health = CameraFailure.NoDevice;
                failures.Add("no capture device enumerated");
            }
            else
            {
                Trace.Log($"camera-probe: chosen '{chosen.Name}' ({chosen.Enclosure})");

                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var camera = new MediaCaptureCamera(targetFps);
                camera.FrameArrived += frame =>
                {
                    (double mean, double std, double a, byte lo, byte hi) = BrightnessStats(frame.Bgra.Span);
                    luminance.Add(mean);
                    contrast.Add(std);
                    alpha.Add(a);
                    byteMin = Math.Min(byteMin, lo);
                    byteMax = Math.Max(byteMax, hi);
                    stamps.Add(frame.TimestampSeconds);
                    if (luminance.Count >= frameCount)
                    {
                        done.TrySetResult(true);
                    }
                };

                var clock = Stopwatch.StartNew();
                await camera.OpenAsync(chosen.Id, targetFps);
                opened = camera.Device;
                Trace.Log($"camera-probe: opened {opened?.Width}x{opened?.Height} " +
                          $"@{opened?.FrameRate:0.##}fps fmt={opened?.PixelFormat}");

                // Wait for the frames, but never forever: a camera that opens
                // and then delivers nothing is a real failure mode (privacy
                // prompt pending, driver stuck) and must be reported, not hung.
                Task completed = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(20)));
                if (completed != done.Task)
                {
                    failures.Add($"timed out after 20 s with {luminance.Count}/{frameCount} frames");
                }

                clock.Stop();
                double elapsed = Math.Max(clock.Elapsed.TotalSeconds, 1e-6);
                Trace.Log($"camera-probe: {luminance.Count} frames in {elapsed:0.00}s " +
                          $"(accepted={camera.FramesAccepted} dropped={camera.FramesDropped})");

                await camera.CloseAsync();
                offered = camera.FramesOffered;
                accepted = camera.FramesAccepted;
                dropped = camera.FramesDropped;
                offeredSpan = camera.OfferedSpanSeconds;
                if (camera.IsRunning)
                {
                    failures.Add("camera still running after CloseAsync");
                }
            }
        }
        catch (Exception ex)
        {
            health = CameraHealth.Classify(ex);
            error = $"{ex.GetType().Name}: {ex.Message} (hresult=0x{ex.HResult:X8})";
            Trace.Log($"camera-probe: FAILED {error}");
            failures.Add(CameraHealth.Describe(health));
        }

        double measuredFps = 0.0;
        if (stamps.Count >= 2)
        {
            double span = stamps[^1] - stamps[0];
            measuredFps = span > 1e-6 ? (stamps.Count - 1) / span : 0.0;
        }

        double lumMin = double.MaxValue, lumMax = double.MinValue, lumMean = 0.0;
        foreach (double l in luminance)
        {
            lumMin = Math.Min(lumMin, l);
            lumMax = Math.Max(lumMax, l);
            lumMean += l;
        }
        if (luminance.Count > 0)
        {
            lumMean /= luminance.Count;
        }

        // Contrast (std within a frame) and how much it moves between frames.
        // A FROZEN stream re-delivers the same buffer, so the std delta is
        // exactly 0; a live sensor in a dark room still jitters by a
        // thousandth. That is the cheapest way to tell "camera works" from
        // "camera returns a stale frame", and it says nothing about what is
        // in the picture.
        double stdMin = double.MaxValue, stdMax = double.MinValue, stdDelta = 0.0;
        for (int i = 0; i < contrast.Count; i++)
        {
            stdMin = Math.Min(stdMin, contrast[i]);
            stdMax = Math.Max(stdMax, contrast[i]);
            if (i > 0)
            {
                stdDelta = Math.Max(stdDelta, Math.Abs(contrast[i] - contrast[i - 1]));
            }
        }
        if (contrast.Count == 0)
        {
            stdMin = stdMax = 0.0;
        }

        // Camera-side rate: frames the sensor delivered over the span between
        // its first and last delivery. Compared against MeasuredFps this is the
        // whole story - if the two agree, nothing was dropped by us and the
        // rate is whatever the camera decided to give (a dim room slows a
        // webcam down by lengthening exposure; there is nothing to fix in code).
        double cameraFps = offeredSpan > 1e-6 ? (offered - 1) / offeredSpan : 0.0;

        double alphaMean = 0.0;
        foreach (double a in alpha)
        {
            alphaMean += a;
        }
        if (alpha.Count > 0)
        {
            alphaMean /= alpha.Count;
        }

        // A frame where every sampled pixel has the same value is not evidence
        // of a broken pipeline - it is evidence of a scene with nothing in it
        // (lens capped, room dark, or the privacy shutter closed). Recorded,
        // not gated: see the class comment.
        bool sceneFlat = contrast.Count == 0 || stdMax < 0.5;

        report["GeneratedAt"] = DateTimeOffset.Now.ToString("s");
        report["TargetFps"] = targetFps;
        report["RequestedFrames"] = frameCount;
        report["Devices"] = devices;
        report["ChosenDevice"] = chosen;
        report["OpenedDevice"] = opened;
        report["FramesReceived"] = luminance.Count;
        report["FramesOffered"] = offered;
        report["FramesAccepted"] = accepted;
        report["FramesDropped"] = dropped;
        report["OfferedSpanSeconds"] = Math.Round(offeredSpan, 3);
        report["MeasuredFps"] = Math.Round(measuredFps, 3);
        report["CameraFps"] = Math.Round(cameraFps, 3);
        report["LuminanceMean"] = luminance.Count > 0 ? Math.Round(lumMean, 2) : 0.0;
        report["LuminanceMin"] = luminance.Count > 0 ? Math.Round(lumMin, 2) : 0.0;
        report["LuminanceMax"] = luminance.Count > 0 ? Math.Round(lumMax, 2) : 0.0;
        report["ContrastMin"] = Math.Round(stdMin, 4);
        report["ContrastMax"] = Math.Round(stdMax, 4);
        report["ContrastDelta"] = Math.Round(stdDelta, 6);
        report["AlphaMean"] = Math.Round(alphaMean, 2);
        report["ByteMin"] = (int)byteMin;
        report["ByteMax"] = (int)byteMax;
        report["SceneFlat"] = sceneFlat;
        report["Health"] = health.ToString();
        report["HealthHint"] = CameraHealth.Describe(health);
        report["Error"] = error;
        report["Failures"] = failures;
        // Deliberately NOT reported: any pixel, any image path, any frame data.
        report["StoredPixels"] = false;

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        Trace.Log($"camera-probe: wrote {jsonPath}");

        // Structural proof that the buffer holds a decoded image rather than a
        // zeroed allocation. NV12 has no alpha, so the BGRA conversion has to
        // synthesise one: a real frame is alpha 255 everywhere. If alpha came
        // back as anything else we are reading bytes that are not BGRA, and
        // every statistic above is meaningless.
        bool alphaSane = alpha.Count > 0 && alphaMean >= 250.0;
        bool bytesSane = byteMax > 0 && byteMin <= byteMax;

        bool ok = error is null
                  && failures.Count == 0
                  && luminance.Count >= frameCount
                  && measuredFps > 0.0
                  && alphaSane
                  && bytesSane;

        if (!alphaSane && alpha.Count > 0)
        {
            failures.Add($"alpha mean {alphaMean:0.0} - buffer is not BGRA8 (expected 255)");
        }

        if (sceneFlat && ok)
        {
            Trace.Log("camera-probe: scene is uniform (lens capped / dark room / shutter closed) " +
                      "- pipeline is fine, contrast is not measurable in this state");
        }

        Trace.Log($"camera-probe: OVERALL={(ok ? "PASS" : "FAIL")}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// Reduces a frame to four scalars: mean luminance (0..255), its standard
    /// deviation, the alpha mean, and the byte range.
    ///
    /// Sparse on purpose: the numbers only have to prove a live decoded frame,
    /// and touching every pixel of a 1080p frame in managed code would cost
    /// more than the vision stage it is proving exists.
    ///
    /// The alpha mean is the load-bearing one. NV12 carries no alpha, so a
    /// correct BGRA conversion sets it to 255; a mis-read buffer does not.
    /// </summary>
    private static (double Mean, double Std, double Alpha, byte Min, byte Max) BrightnessStats(ReadOnlySpan<byte> bgra)
    {
        const int stride = 64;   // every 16th pixel
        int pixels = bgra.Length / 4;
        if (pixels == 0)
        {
            return (0.0, 0.0, 0.0, 0, 0);
        }

        double sum = 0.0;
        double sumSq = 0.0;
        double sumA = 0.0;
        byte min = 255;
        byte max = 0;
        int n = 0;
        for (int i = 0; i < pixels; i += stride)
        {
            int o = i * 4;
            byte b = bgra[o];
            byte g = bgra[o + 1];
            byte r = bgra[o + 2];
            sum += 0.114 * b + 0.587 * g + 0.299 * r;
            sumSq += (0.114 * b + 0.587 * g + 0.299 * r) * (0.114 * b + 0.587 * g + 0.299 * r);
            sumA += bgra[o + 3];
            min = Math.Min(min, Math.Min(b, Math.Min(g, r)));
            max = Math.Max(max, Math.Max(b, Math.Max(g, r)));
            n++;
        }

        if (n == 0)
        {
            return (0.0, 0.0, 0.0, 0, 0);
        }

        double mean = sum / n;
        double variance = Math.Max(0.0, sumSq / n - mean * mean);
        return (mean, Math.Sqrt(variance), sumA / n, min, max);
    }
}
