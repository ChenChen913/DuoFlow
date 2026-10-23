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
    /// M3.2 acceptance probe: open the camera, run screen detection on every
    /// frame, report what the detector decided.
    ///
    /// <para>
    /// <b>Privacy is unchanged.</b> The frame is turned into a
    /// <see cref="GrayImage"/> inside the callback, the detector consumes it,
    /// and only the resulting quad (normalized coordinates) and confidence
    /// leave. Nothing is written to disk but those numbers. A quad is
    /// geometry, not a picture - it cannot be turned back into a frame.
    /// </para>
    /// <para>
    /// <b>What is gated and what is not.</b> The gate is "the detector never
    /// threw and every frame produced a decision". It is NOT "the screen was
    /// found": on a machine whose lens is capped or whose room is dark there
    /// is nothing to find, and a detector that invented a quad there would be
    /// the bug, not the feature. Detection quality is reported, with the
    /// reason breakdown that explains a low rate.
    /// </para>
    /// <para>
    /// Cost note: each frame allocates one luminance buffer and one downscaled
    /// copy (~1 MB for 720p). At the 10 fps the governor allows that is fine
    /// for a probe; the shipping path will reuse buffers the way
    /// <see cref="MediaCaptureCamera"/> reuses its frame array.
    /// </para>
    /// </summary>
    public static async Task<int> RunDetectAsync(string outputDir, double targetFps, int frameCount)
    {
        Directory.CreateDirectory(outputDir);
        string jsonPath = Path.Combine(outputDir, "camera-detect-result.json");

        var report = new Dictionary<string, object?>();
        var failures = new List<string>();

        IReadOnlyList<CameraDeviceInfo> devices = Array.Empty<CameraDeviceInfo>();
        CameraDeviceInfo? chosen = null;
        CameraDeviceInfo? opened = null;

        var confidences = new List<double>();
        var reasons = new Dictionary<string, int>();
        var polarities = new Dictionary<string, int>();
        ScreenDetection? best = null;
        int usableFrames = 0;
        int detectorThrows = 0;
        string? lastThrow = null;
        string? error = null;
        CameraFailure health = CameraFailure.None;

        try
        {
            devices = await MediaCaptureCamera.EnumerateAsync();
            chosen = CameraSelection.Choose(devices);
            if (chosen is null)
            {
                health = CameraFailure.NoDevice;
                failures.Add("no capture device enumerated");
            }
            else
            {
                Trace.Log($"camera-detect: chosen '{chosen.Name}' ({chosen.Enclosure})");

                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var camera = new MediaCaptureCamera(targetFps);
                camera.FrameArrived += frame =>
                {
                    ScreenDetection detection;
                    try
                    {
                        GrayImage gray = GrayImage.FromBgra(frame.Width, frame.Height, frame.Bgra.Span);
                        detection = ScreenDetector.Detect(gray);
                    }
                    catch (Exception ex)
                    {
                        // A detector that throws on one frame must not take the
                        // whole probe down - but it must be counted, because
                        // "never throws" is the actual contract (M3.2).
                        detectorThrows++;
                        lastThrow = $"{ex.GetType().Name}: {ex.Message}";
                        return;
                    }

                    confidences.Add(detection.Confidence);
                    string reason = detection.Reason.ToString();
                    reasons[reason] = reasons.TryGetValue(reason, out int n) ? n + 1 : 1;
                    string polarity = detection.Polarity.ToString();
                    polarities[polarity] = polarities.TryGetValue(polarity, out int p) ? p + 1 : 1;

                    if (detection.IsUsable)
                    {
                        usableFrames++;
                    }

                    if (best is null || detection.Confidence > best.Confidence)
                    {
                        best = detection;
                    }

                    if (confidences.Count >= frameCount)
                    {
                        done.TrySetResult(true);
                    }
                };

                await camera.OpenAsync(chosen.Id, targetFps);
                opened = camera.Device;

                Task completed = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(20)));
                if (completed != done.Task)
                {
                    failures.Add($"timed out after 20 s with {confidences.Count}/{frameCount} frames");
                }

                await camera.CloseAsync();
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
            Trace.Log($"camera-detect: FAILED {error}");
            failures.Add(CameraHealth.Describe(health));
        }

        double confMin = double.MaxValue, confMax = double.MinValue, confMean = 0.0;
        foreach (double c in confidences)
        {
            confMin = Math.Min(confMin, c);
            confMax = Math.Max(confMax, c);
            confMean += c;
        }

        if (confidences.Count > 0)
        {
            confMean /= confidences.Count;
        }
        else
        {
            confMin = confMax = 0.0;
        }

        report["GeneratedAt"] = DateTimeOffset.Now.ToString("s");
        report["TargetFps"] = targetFps;
        report["RequestedFrames"] = frameCount;
        report["Devices"] = devices;
        report["ChosenDevice"] = chosen;
        report["OpenedDevice"] = opened;
        report["FramesDetected"] = confidences.Count;
        report["DetectorThrows"] = detectorThrows;
        report["LastThrow"] = lastThrow;
        report["UsableFrames"] = usableFrames;
        report["UsableRate"] = confidences.Count > 0 ? Math.Round((double)usableFrames / confidences.Count, 3) : 0.0;
        report["ConfidenceMin"] = Math.Round(confMin, 4);
        report["ConfidenceMax"] = Math.Round(confMax, 4);
        report["ConfidenceMean"] = Math.Round(confMean, 4);
        report["Reasons"] = reasons;
        report["Polarities"] = polarities;
        report["BestQuad"] = best?.Quad?.Flatten();
        report["BestConfidence"] = best is null ? 0.0 : Math.Round(best.Confidence, 4);
        report["BestReason"] = best?.Reason.ToString();
        report["BestPolarity"] = best?.Polarity.ToString();
        report["BestAreaFraction"] = best is null ? 0.0 : Math.Round(best.AreaFraction, 4);
        report["BestAspect"] = best is null ? 0.0 : Math.Round(best.AspectRatio, 4);
        report["BestEdgeContrast"] = best is null ? 0.0 : Math.Round(best.EdgeContrast, 2);
        report["BestSceneStd"] = best is null ? 0.0 : Math.Round(best.SceneStd, 3);
        report["BestClipped"] = best?.TouchesFrameEdge ?? false;
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
        Trace.Log($"camera-detect: wrote {jsonPath}");
        if (best is not null)
        {
            Trace.Log($"camera-detect: best -> {best.Describe()}");
        }

        bool ok = error is null
                  && failures.Count == 0
                  && confidences.Count >= frameCount
                  && detectorThrows == 0;

        if (detectorThrows > 0)
        {
            failures.Add($"detector threw on {detectorThrows} frame(s); last {lastThrow}");
        }

        if (ok && usableFrames == 0)
        {
            Trace.Log("camera-detect: no usable detection - expected when the lens is capped " +
                      "or the room is dark; the contract here is 'decides, never throws'");
        }

        Trace.Log($"camera-detect: OVERALL={(ok ? "PASS" : "FAIL")}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// M3.3 acceptance probe: run the REAL provider end to end and report what
    /// it produced.
    ///
    /// <para>
    /// Deliberately uses <see cref="CameraLidProvider"/> rather than repeating
    /// its steps: a probe that re-implements the pipeline proves the probe
    /// works, not the provider. This one exercises the same object the app will
    /// construct, including device selection, the frame governor, the coverage
    /// estimate and the progress mapping.
    /// </para>
    /// <para>
    /// <b>What is gated and what is not.</b> The gate is structural - the
    /// provider started, it delivered N frames, the vision chain never threw,
    /// and it reported a plausible confidence. It is NOT "the progress moved":
    /// the probe runs with the laptop sitting still, so a moving progress would
    /// be the bug. Stability is reported instead, because it is the number that
    /// says whether M3.4 has anything to smooth.
    /// </para>
    /// <para>
    /// Privacy is unchanged: <see cref="GrayImage"/> is built and consumed
    /// inside the frame callback, and only coverage, progress and confidence
    /// scalars reach this method.
    /// </para>
    /// </summary>
    public static async Task<int> RunLidAsync(string outputDir, double targetFps, int frameCount)
    {
        Directory.CreateDirectory(outputDir);
        string jsonPath = Path.Combine(outputDir, "camera-lid-result.json");

        var report = new Dictionary<string, object?>();
        var failures = new List<string>();

        IReadOnlyList<CameraDeviceInfo> devices = Array.Empty<CameraDeviceInfo>();
        var coverages = new List<double>();
        var nearTops = new List<double>();
        var progresses = new List<double>();
        var confidences = new List<double>();
        var progressReasons = new Dictionary<string, int>();
        var deckReasons = new Dictionary<string, int>();
        int fillsFrame = 0;
        int touchesTop = 0;
        double openBaseline = 0.0;
        bool hasLearnedBaseline = false;
        string? lastCoverageText = null;
        string? error = null;

        try
        {
            devices = await MediaCaptureCamera.EnumerateAsync();
            Trace.Log($"camera-lid: enumerated {devices.Count} device(s)");

            await using var provider = new CameraLidProvider(targetFps);
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            provider.StateChanged += (_, state) =>
            {
                LidProgressEstimate? estimate = provider.LastEstimate;
                DeckCoverage? coverage = provider.LastCoverage;

                progresses.Add(state.Progress);
                confidences.Add(state.Confidence);

                string progressReason = estimate?.Reason.ToString() ?? "null";
                progressReasons[progressReason] =
                    progressReasons.TryGetValue(progressReason, out int n) ? n + 1 : 1;

                if (coverage is not null)
                {
                    coverages.Add(coverage.Coverage);
                    nearTops.Add(coverage.NearEdgeTop);
                    if (coverage.FillsFrame)
                    {
                        fillsFrame++;
                    }

                    if (coverage.TouchesTop)
                    {
                        touchesTop++;
                    }

                    string deckReason = coverage.Reason.ToString();
                    deckReasons[deckReason] = deckReasons.TryGetValue(deckReason, out int m) ? m + 1 : 1;
                    lastCoverageText = coverage.Describe();
                }

                if (progresses.Count >= frameCount)
                {
                    done.TrySetResult(true);
                }
            };

            await provider.StartAsync();
            if (!provider.IsAvailable)
            {
                failures.Add($"provider did not start: {provider.LastError ?? "unknown"}");
            }
            else
            {
                Trace.Log($"camera-lid: opened {provider.Device?.Width}x{provider.Device?.Height} " +
                          $"@30fps max, '{provider.Device?.Name}' ({provider.Device?.Enclosure})");

                Task completed = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(20)));
                if (completed != done.Task)
                {
                    failures.Add($"timed out after 20 s with {progresses.Count}/{frameCount} states");
                }

                await provider.StopAsync();
                if (provider.IsAvailable)
                {
                    failures.Add("provider still available after StopAsync");
                }
            }

            openBaseline = provider.OpenBaseline;
            hasLearnedBaseline = provider.HasLearnedBaseline;

            if (provider.FrameErrors > 0)
            {
                failures.Add($"vision chain threw on {provider.FrameErrors} frame(s): {provider.LastError}");
            }
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message} (hresult=0x{ex.HResult:X8})";
            Trace.Log($"camera-lid: FAILED {error}");
            failures.Add(CameraHealth.Describe(CameraHealth.Classify(ex)));
        }

        static (double Min, double Max, double Mean, double Std) Summarise(List<double> values)
        {
            if (values.Count == 0)
            {
                return (0.0, 0.0, 0.0, 0.0);
            }

            double min = double.MaxValue, max = double.MinValue, sum = 0.0;
            foreach (double v in values)
            {
                min = Math.Min(min, v);
                max = Math.Max(max, v);
                sum += v;
            }

            double mean = sum / values.Count;
            double variance = 0.0;
            foreach (double v in values)
            {
                double d = v - mean;
                variance += d * d;
            }

            return (min, max, mean, Math.Sqrt(variance / values.Count));
        }

        (double covMin, double covMax, double covMean, double covStd) = Summarise(coverages);
        (double topMin, double topMax, double topMean, double _) = Summarise(nearTops);
        (double progMin, double progMax, double progMean, double progStd) = Summarise(progresses);
        (double confMin, double confMax, double confMean, double confStd) = Summarise(confidences);

        report["GeneratedAt"] = DateTimeOffset.Now.ToString("s");
        report["TargetFps"] = targetFps;
        report["RequestedFrames"] = frameCount;
        report["Devices"] = devices;
        report["States"] = progresses.Count;
        report["OpenBaseline"] = Math.Round(openBaseline, 4);
        report["HasLearnedBaseline"] = hasLearnedBaseline;
        report["CoverageMin"] = Math.Round(covMin, 4);
        report["CoverageMax"] = Math.Round(covMax, 4);
        report["CoverageMean"] = Math.Round(covMean, 4);
        report["CoverageStd"] = Math.Round(covStd, 4);
        report["NearEdgeTopMin"] = Math.Round(topMin, 4);
        report["NearEdgeTopMax"] = Math.Round(topMax, 4);
        report["NearEdgeTopMean"] = Math.Round(topMean, 4);
        report["FillsFrameFrames"] = fillsFrame;
        report["TouchesTopFrames"] = touchesTop;
        report["ProgressMin"] = Math.Round(progMin, 4);
        report["ProgressMax"] = Math.Round(progMax, 4);
        report["ProgressMean"] = Math.Round(progMean, 4);
        report["ProgressStd"] = Math.Round(progStd, 4);
        report["ConfidenceMin"] = Math.Round(confMin, 4);
        report["ConfidenceMax"] = Math.Round(confMax, 4);
        report["ConfidenceMean"] = Math.Round(confMean, 4);
        report["ConfidenceStd"] = Math.Round(confStd, 4);
        report["ProgressReasons"] = progressReasons;
        report["DeckReasons"] = deckReasons;
        report["LastCoverage"] = lastCoverageText;
        report["Error"] = error;
        report["Failures"] = failures;
        // Deliberately NOT reported: any pixel, any image path, any frame data.
        report["StoredPixels"] = false;

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        Trace.Log($"camera-lid: wrote {jsonPath}");
        if (lastCoverageText is not null)
        {
            Trace.Log($"camera-lid: last coverage -> {lastCoverageText}");
        }

        // Gate on the PIPELINE, not on the scene - the M3.1/M3.2 lesson. "Did we
        // measure anything" depends on where the lid is standing and where the
        // camera is pointing; on a laptop with the lid wide open the surface
        // this provider is supposed to watch is simply not in the frame, and
        // that is reported (DeckReasons / TouchesTopFrames), not failed.
        bool ok = error is null
                  && failures.Count == 0
                  && progresses.Count >= frameCount;

        if (ok && confMax <= 0.0)
        {
            Trace.Log("camera-lid: no usable measurement - expected with the lid open, " +
                      "capped lens or dark room; the contract here is 'runs and decides', " +
                      "not 'finds the deck'");
        }

        if (ok && confMax > 0.0 && progStd < 1e-9)
        {
            Trace.Log("camera-lid: progress is perfectly still - expected, the lid did not move; " +
                      "this probe proves the chain runs, not that it tracks a closing lid");
        }

        Trace.Log($"camera-lid: OVERALL={(ok ? "PASS" : "FAIL")}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// M3.3 sampling run: record the lid-progress series while the user
    /// actually closes the lid.
    ///
    /// <para>
    /// <b>Why this is not a gate but a recorder.</b> Every previous camera
    /// probe ran with the laptop stationary, so it could only prove "the
    /// pipeline runs and refuses to invent numbers". Whether the series
    /// actually TRACKS a closing lid cannot be established without a lid that
    /// closes, and that is a physical action no test can perform. So this mode
    /// does not judge - it writes down what happened, sample by sample, and
    /// leaves the judging to whoever reads the file.
    /// </para>
    /// <para>
    /// <b>Triggered by motion, not by a stopwatch.</b> The first attempt gave
    /// the user a 60-second window and asked them to act inside it; the whole
    /// series came back flat because they were still reading the instructions
    /// when the window closed. A fixed window assumes the human is synchronised
    /// with the process, and they never are. So this waits for the scene to
    /// actually change, keeps a rolling pre-roll of the moments before, and
    /// records a fixed span from the moment of the trigger. The user can take
    /// as long as they like to get ready.
    /// </para>
    /// <para>
    /// <b>Written incrementally, on purpose.</b> Closing a laptop makes Windows
    /// turn the display off and usually puts the machine to sleep, which can
    /// suspend or kill this process mid-run. A summary written only at the end
    /// would then be lost - so every recorded sample is appended to a JSONL
    /// file and flushed immediately. Whatever happened before the machine slept
    /// is on disk; the summary at the end is a convenience, not the data.
    /// </para>
    /// <para>
    /// Privacy: one line per frame, scalars only. No pixel, no image path.
    /// </para>
    /// </summary>
    /// <param name="maxWaitSeconds">How long to wait for the lid to start moving.</param>
    /// <param name="recordSeconds">How much to record after the motion starts.</param>
    public static async Task<int> RecordLidAsync(
        string outputDir, double targetFps, double maxWaitSeconds, double recordSeconds)
    {
        Directory.CreateDirectory(outputDir);
        string jsonlPath = Path.Combine(outputDir, "lid-series.jsonl");
        string summaryPath = Path.Combine(outputDir, "lid-series-summary.json");

        var failures = new List<string>();
        var recorded = new List<LidSample>();
        var reasons = new Dictionary<string, int>();
        var deckReasons = new Dictionary<string, int>();
        int measured = 0;
        int touchesTop = 0;
        int fillsFrame = 0;
        int armed = 0;
        string? error = null;
        double triggerAt = -1.0;

        // AutoFlush, not a buffer: the whole point is that a sleep in the
        // middle of the run must not take the earlier samples with it.
        using var writer = new StreamWriter(new FileStream(
            jsonlPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };

        try
        {
            await using var provider = new CameraLidProvider(targetFps);
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var clock = Stopwatch.StartNew();

            // Pre-roll: the moments just before the motion started. Without it
            // the series begins mid-gesture and the "before" value is missing.
            const int preRollSamples = 30;
            var preRoll = new Queue<LidSample>();
            var recentCoverage = new Queue<double>();
            var recentTop = new Queue<double>();

            void Write(LidSample sample)
            {
                recorded.Add(sample);
                writer.WriteLine(sample.ToJson());

                reasons[sample.Reason] = reasons.TryGetValue(sample.Reason, out int n) ? n + 1 : 1;
                deckReasons[sample.Deck] = deckReasons.TryGetValue(sample.Deck, out int m) ? m + 1 : 1;
                if (sample.Reason != nameof(LidProgressReason.NoMeasurement))
                {
                    measured++;
                }

                if (sample.TouchesTop)
                {
                    touchesTop++;
                }

                if (sample.Fills)
                {
                    fillsFrame++;
                }
            }

            provider.StateChanged += (_, state) =>
            {
                LidProgressEstimate? estimate = provider.LastEstimate;
                DeckCoverage? coverage = provider.LastCoverage;
                if (estimate is null || coverage is null)
                {
                    return;
                }

                var sample = new LidSample(
                    T: Math.Round(clock.Elapsed.TotalSeconds, 3),
                    Progress: Math.Round(state.Progress, 4),
                    Confidence: Math.Round(state.Confidence, 4),
                    Reason: estimate.Reason.ToString(),
                    Coverage: Math.Round(coverage.Coverage, 4),
                    Deck: coverage.Reason.ToString(),
                    NearTop: Math.Round(coverage.NearEdgeTop, 4),
                    TouchesTop: coverage.TouchesTop,
                    Fills: coverage.FillsFrame,
                    Std: Math.Round(coverage.SceneStd, 2),
                    Separability: Math.Round(coverage.Separability, 3),
                    Near: Math.Round(coverage.NearMean, 1),
                    Far: Math.Round(coverage.FarMean, 1));

                if (triggerAt < 0.0)
                {
                    // Idle: keep a rolling reference so a slow lighting drift
                    // does not look like a lid, then look for a real change.
                    recentCoverage.Enqueue(sample.Coverage);
                    recentTop.Enqueue(sample.NearTop);
                    while (recentCoverage.Count > preRollSamples)
                    {
                        recentCoverage.Dequeue();
                        recentTop.Dequeue();
                    }

                    preRoll.Enqueue(sample);
                    while (preRoll.Count > preRollSamples)
                    {
                        preRoll.Dequeue();
                    }

                    if (recentCoverage.Count >= 10)
                    {
                        armed = recentCoverage.Count;
                        double medianCoverage = Median(recentCoverage);
                        double medianTop = Median(recentTop);

                        // 0.04 is ~13x the measured stationary jitter (0.003),
                        // so a still scene cannot arm this; a 1 cm lid movement
                        // can.
                        if (Math.Abs(sample.Coverage - medianCoverage) > 0.04 ||
                            Math.Abs(sample.NearTop - medianTop) > 0.06)
                        {
                            triggerAt = sample.T;
                            Trace.Log($"lid-record: motion detected at t={triggerAt:0.00}s " +
                                      $"(coverage {medianCoverage:0.000} -> {sample.Coverage:0.000})");
                            foreach (LidSample buffered in preRoll)
                            {
                                Write(buffered);
                            }
                        }
                    }

                    return;
                }

                Write(sample);

                if (sample.T - triggerAt >= recordSeconds)
                {
                    done.TrySetResult(true);
                }
            };

            await provider.StartAsync();
            if (!provider.IsAvailable)
            {
                failures.Add($"provider did not start: {provider.LastError ?? "unknown"}");
            }
            else
            {
                Trace.Log($"lid-record: waiting up to {maxWaitSeconds:0}s for the lid to move");

                Task completed = await Task.WhenAny(
                    done.Task, Task.Delay(TimeSpan.FromSeconds(maxWaitSeconds + 10)));
                if (completed != done.Task)
                {
                    failures.Add(triggerAt < 0.0
                        ? $"no lid motion within {maxWaitSeconds:0}s (armed after {armed} samples)"
                        : $"recording stopped early at {recorded.Count} samples");
                }

                await provider.StopAsync();
            }

            if (provider.FrameErrors > 0)
            {
                failures.Add($"vision chain threw on {provider.FrameErrors} frame(s): {provider.LastError}");
            }
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message} (hresult=0x{ex.HResult:X8})";
            Trace.Log($"lid-record: FAILED {error}");
            failures.Add(CameraHealth.Describe(CameraHealth.Classify(ex)));
        }

        // ---- what the series says about tracking -------------------------
        // Backwards steps are the number M3.4 has to fix, so they are measured
        // here rather than eyeballed later. A 0.02 dead band keeps a stationary
        // scene (which jitters by ~0.002) from being counted as a reversal.
        var progresses = recorded.Select(s => s.Progress).ToList();
        var coverages = recorded.Select(s => s.Coverage).ToList();

        int backward = 0;
        int forward = 0;
        double maxBackward = 0.0;
        double maxForward = 0.0;
        for (int i = 1; i < progresses.Count; i++)
        {
            double delta = progresses[i] - progresses[i - 1];
            if (delta < -0.02)
            {
                backward++;
                maxBackward = Math.Max(maxBackward, -delta);
            }
            else if (delta > 0.02)
            {
                forward++;
                maxForward = Math.Max(maxForward, delta);
            }
        }

        double progMin = progresses.Count > 0 ? progresses.Min() : 0.0;
        double progMax = progresses.Count > 0 ? progresses.Max() : 0.0;
        double covMin = coverages.Count > 0 ? coverages.Min() : 0.0;
        double covMax = coverages.Count > 0 ? coverages.Max() : 0.0;
        double duration = recorded.Count > 1 ? recorded[^1].T - recorded[0].T : 0.0;
        string sparkline = Sparkline(progresses, 60);

        var summary = new Dictionary<string, object?>
        {
            ["GeneratedAt"] = DateTimeOffset.Now.ToString("s"),
            ["TargetFps"] = targetFps,
            ["MaxWaitSeconds"] = maxWaitSeconds,
            ["RequestedRecordSeconds"] = recordSeconds,
            ["TriggerAtSeconds"] = triggerAt < 0.0 ? null : Math.Round(triggerAt, 2),
            ["Samples"] = recorded.Count,
            ["DurationSeconds"] = Math.Round(duration, 2),
            ["ProgressFirst"] = progresses.Count > 0 ? Math.Round(progresses[0], 4) : 0.0,
            ["ProgressLast"] = progresses.Count > 0 ? Math.Round(progresses[^1], 4) : 0.0,
            ["ProgressMin"] = Math.Round(progMin, 4),
            ["ProgressMax"] = Math.Round(progMax, 4),
            ["CoverageMin"] = Math.Round(covMin, 4),
            ["CoverageMax"] = Math.Round(covMax, 4),
            ["MeasuredSamples"] = measured,
            ["MeasuredRate"] = recorded.Count > 0 ? Math.Round((double)measured / recorded.Count, 3) : 0.0,
            ["TouchesTopSamples"] = touchesTop,
            ["FillsFrameSamples"] = fillsFrame,
            ["ForwardSteps"] = forward,
            ["BackwardSteps"] = backward,
            ["MaxForwardStep"] = Math.Round(maxForward, 4),
            ["MaxBackwardStep"] = Math.Round(maxBackward, 4),
            ["ProgressReasons"] = reasons,
            ["DeckReasons"] = deckReasons,
            ["Sparkline"] = sparkline,
            ["Error"] = error,
            ["Failures"] = failures,
            ["StoredPixels"] = false,
        };

        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));

        Trace.Log($"lid-record: {recorded.Count} samples over {duration:0.0}s " +
                  $"progress {progMin:0.000}..{progMax:0.000} " +
                  $"forward={forward} backward={backward}");
        Trace.Log($"lid-record: progress {sparkline}");
        Trace.Log($"lid-record: wrote {jsonlPath}");

        bool ok = error is null && failures.Count == 0 && recorded.Count >= 10;

        Trace.Log($"lid-record: OVERALL={(ok ? "PASS" : "FAIL")}");
        return ok ? 0 : 1;
    }

    /// <summary>One recorded sample. A record so the series can be buffered before it is written.</summary>
    private sealed record LidSample(
        double T,
        double Progress,
        double Confidence,
        string Reason,
        double Coverage,
        string Deck,
        double NearTop,
        bool TouchesTop,
        bool Fills,
        double Std,
        double Separability,
        double Near,
        double Far)
    {
        public string ToJson() => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["t"] = T,
            ["progress"] = Progress,
            ["confidence"] = Confidence,
            ["reason"] = Reason,
            ["coverage"] = Coverage,
            ["deck"] = Deck,
            ["nearTop"] = NearTop,
            ["touchesTop"] = TouchesTop,
            ["fills"] = Fills,
            ["std"] = Std,
            ["sep"] = Separability,
            ["near"] = Near,
            ["far"] = Far,
        });
    }

    /// <summary>Median of a small window, used as the "before" reference for the trigger.</summary>
    private static double Median(Queue<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sorted = new List<double>(values);
        sorted.Sort();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) * 0.5;
    }

    /// <summary>
    /// A 60-cell picture of the progress series, for reading a whole run at a
    /// glance in a log. Resampled, not truncated: 600 samples in 60 cells means
    /// each cell is the peak of its bucket, so a short spike still shows.
    /// </summary>
    private static string Sparkline(List<double> values, int cells)
    {
        const string ramp = " .:-=+*#%@";
        if (values.Count == 0)
        {
            return "(no samples)";
        }

        var text = new char[cells];
        for (int c = 0; c < cells; c++)
        {
            int from = (int)((long)c * values.Count / cells);
            int to = (int)((long)(c + 1) * values.Count / cells);
            if (to <= from)
            {
                to = from + 1;
            }

            double peak = 0.0;
            for (int i = from; i < to && i < values.Count; i++)
            {
                peak = Math.Max(peak, values[i]);
            }

            int index = (int)Math.Round(Math.Clamp(peak, 0.0, 1.0) * (ramp.Length - 1));
            text[c] = ramp[index];
        }

        return new string(text);
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
