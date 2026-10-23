using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DuoFlow.Vision;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
// 'Buffer' is ambiguous with System.Buffer once ImplicitUsings pulls in System.
using WinRtBuffer = Windows.Storage.Streams.Buffer;

namespace DuoFlow.Camera;

/// <summary>
/// One delivered frame, in BGRA8.
///
/// <para>
/// <b><see cref="Bgra"/> is only valid inside the callback that received it.</b>
/// The buffer is reused between frames on purpose: a 1920x1080 BGRA frame is
/// 8 MB, and allocating one per frame at 10 Hz would hand the GC 80 MB/s of
/// garbage for data that is consumed in microseconds. A consumer that needs to
/// keep a frame must copy it - and, per the privacy rule, nothing ever does.
/// </para>
/// </summary>
public readonly record struct CameraFrame(
    int Width,
    int Height,
    ReadOnlyMemory<byte> Bgra,
    double TimestampSeconds);

/// <summary>
/// M3.1: the only class in the project that touches a camera device.
///
/// <para>
/// Uses MediaCapture + MediaFrameReader rather than Media Foundation directly
/// because the app is already a WinRT consumer (WGC, Windows.UI.Composition)
/// and MediaFrameReader hands us the two things M3.2 needs: a stable per-frame
/// callback and a choice between CPU and GPU memory. We take CPU
/// (<see cref="MediaCaptureMemoryPreference.Cpu"/>) because screen detection is
/// a CPU vision problem; the GPU→GPU red line (PROJECT_SPEC §2.1) is about the
/// SCREEN capture path, not about the camera.
/// </para>
/// <para>
/// Throttling lives in <see cref="FrameRateGovernor"/> rather than here: the
/// decision "this frame is not needed" is pure logic, and it is the difference
/// between 30 Hz of wasted vision work and 10 Hz of enough.
/// </para>
/// </summary>
public sealed class MediaCaptureCamera : IAsyncDisposable
{
    /// <summary>Angle estimation cannot use more than this; the lid is not that fast.</summary>
    public const double DefaultTargetFps = 10.0;

    private readonly FrameRateGovernor _governor;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private byte[] _frameBytes = Array.Empty<byte>();
    private WinRtBuffer? _winrtBuffer;
    private SoftwareBitmap? _scratch;
    private CameraDeviceInfo? _device;
    private long _offered;
    private double _firstOfferedSeconds;
    private double _lastOfferedSeconds;

    public MediaCaptureCamera(double targetFps = DefaultTargetFps)
    {
        _governor = new FrameRateGovernor(targetFps);
    }

    /// <summary>Raised on the frame-reader thread. Do not block here.</summary>
    public event Action<CameraFrame>? FrameArrived;

    /// <summary>The device currently streaming, with its negotiated format filled in.</summary>
    public CameraDeviceInfo? Device => _device;

    public bool IsRunning => _reader is not null;

    public double TargetFps
    {
        get => _governor.TargetFps;
        set => _governor.TargetFps = value;
    }

    public long FramesAccepted => _governor.Accepted;

    public long FramesDropped => _governor.Dropped;

    /// <summary>
    /// Frames the CAMERA offered, before throttling.
    ///
    /// This is the number that separates "the camera is slow" from "we are
    /// dropping frames". They look identical from the outside (few frames
    /// arrive) but have opposite fixes: a slow camera means the pipeline must
    /// tolerate a low rate (a dim room drops a webcam to 5-7 fps on its own,
    /// via longer exposure), while heavy dropping means the target rate is
    /// wrong. Measured on the development laptop: 4.6 fps offered in a dark
    /// room against a 10 fps target - the camera, not the governor.
    /// </summary>
    public long FramesOffered => _offered;

    /// <summary>
    /// Wall-clock span between the first and last frame the camera offered.
    ///
    /// Exists so the measured camera rate is <c>(FramesOffered - 1) / span</c>
    /// and nothing else. Timing from "OpenAsync returned" instead would charge
    /// the camera for driver start-up and report a rate that is too low by
    /// however long the sensor took to wake - which is exactly the number
    /// someone would then try to "fix".
    /// </summary>
    public double OfferedSpanSeconds => _offered >= 2 ? _lastOfferedSeconds - _firstOfferedSeconds : 0.0;

    /// <summary>
    /// Enumerate capture devices WITHOUT opening any of them.
    ///
    /// <para>
    /// Resolution/frame rate are left at 0 here on purpose: on Windows those
    /// are properties of the negotiated source, and a device that advertises
    /// 1080p may hand out 720p once the sharing mode is applied. Reporting the
    /// advertised number would make the trace lie.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyList<CameraDeviceInfo>> EnumerateAsync()
    {
        DeviceInformationCollection infos = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        var devices = new List<CameraDeviceInfo>(infos.Count);

        foreach (DeviceInformation info in infos)
        {
            devices.Add(new CameraDeviceInfo
            {
                Id = info.Id,
                Name = string.IsNullOrWhiteSpace(info.Name) ? "(unnamed camera)" : info.Name,
                Enclosure = ToEnclosure(info.EnclosureLocation),
                IsEnabled = info.IsEnabled,
            });
        }

        return devices;
    }

    private static CameraEnclosure ToEnclosure(EnclosureLocation? location)
    {
        if (location is null)
        {
            // A camera with no enclosure info is almost always USB/external.
            return CameraEnclosure.External;
        }

        return location.Panel switch
        {
            Panel.Front => CameraEnclosure.Front,
            Panel.Back => CameraEnclosure.Back,
            _ => CameraEnclosure.Unknown,
        };
    }

    /// <summary>
    /// Open <paramref name="deviceId"/> and start streaming.
    /// Throws on failure - the caller classifies with <see cref="CameraHealth"/>
    /// so the UI can say what to do about it.
    /// </summary>
    public async Task OpenAsync(string deviceId, double targetFps = DefaultTargetFps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        await CloseAsync();
        TargetFps = targetFps;

        var settings = new MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = StreamingCaptureMode.Video,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            VideoDeviceId = deviceId,
            // SharedReadOnly lets a second DuoFlow instance (or the acceptance
            // probe running beside the app) enumerate/open without kicking the
            // first one out. Not every driver allows it, hence the fallback.
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
        };

        var capture = new MediaCapture();
        try
        {
            await capture.InitializeAsync(settings);
        }
        catch (Exception)
        {
            capture.Dispose();
            settings.SharingMode = MediaCaptureSharingMode.ExclusiveControl;
            capture = new MediaCapture();
            await capture.InitializeAsync(settings);
        }

        _capture = capture;

        MediaFrameSource? source = null;
        foreach (KeyValuePair<string, MediaFrameSource> pair in capture.FrameSources)
        {
            if (pair.Value.Info.SourceKind == MediaFrameSourceKind.Color)
            {
                source = pair.Value;
                break;
            }
        }

        if (source is null)
        {
            await CloseAsync();
            throw new NotSupportedException("camera exposes no color frame source");
        }

        MediaFrameFormat format = source.CurrentFormat;
        _device = new CameraDeviceInfo
        {
            Id = deviceId,
            Name = string.IsNullOrWhiteSpace(source.Info.DeviceInformation?.Name)
                ? "(camera)"
                : source.Info.DeviceInformation.Name,
            Enclosure = ToEnclosure(source.Info.DeviceInformation?.EnclosureLocation),
            IsEnabled = true,
        }.WithFormat(
            (int)format.VideoFormat.Width,
            (int)format.VideoFormat.Height,
            format.FrameRate.Denominator > 0
                ? (double)format.FrameRate.Numerator / format.FrameRate.Denominator
                : 0.0,
            format.Subtype);

        MediaFrameReader reader;
        try
        {
            // Ask for BGRA8 directly: the driver or the media pipeline does the
            // conversion once, instead of us converting every frame in C#.
            reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
        }
        catch (Exception)
        {
            reader = await capture.CreateFrameReaderAsync(source);
        }

        reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
        reader.FrameArrived += OnFrameArrived;
        await reader.StartAsync();
        _reader = reader;
        _governor.ResetClock();
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        double now = _clock.Elapsed.TotalSeconds;
        if (_offered == 0)
        {
            _firstOfferedSeconds = now;
        }
        _lastOfferedSeconds = now;
        _offered++;

        if (!_governor.Accept(now))
        {
            return;
        }

        using MediaFrameReference? frame = sender.TryAcquireLatestFrame();
        VideoMediaFrame? video = frame?.VideoMediaFrame;
        SoftwareBitmap? bitmap = video?.SoftwareBitmap;
        if (bitmap is null)
        {
            return;
        }

        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            _scratch?.Dispose();
            _scratch = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            bitmap = _scratch;
        }

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int needed = width * height * 4;

        if (_frameBytes.Length != needed)
        {
            _frameBytes = new byte[needed];
        }
        if (_winrtBuffer is null || _winrtBuffer.Capacity != (uint)needed)
        {
            _winrtBuffer = new WinRtBuffer((uint)needed);
        }

        bitmap.CopyToBuffer(_winrtBuffer);
        using (DataReader dataReader = DataReader.FromBuffer(_winrtBuffer))
        {
            dataReader.ReadBytes(_frameBytes);
        }

        FrameArrived?.Invoke(new CameraFrame(
            width,
            height,
            new ReadOnlyMemory<byte>(_frameBytes, 0, needed),
            now));
    }

    public async Task CloseAsync()
    {
        MediaFrameReader? reader = _reader;
        _reader = null;
        if (reader is not null)
        {
            reader.FrameArrived -= OnFrameArrived;
            try
            {
                await reader.StopAsync();
            }
            catch
            {
                // A device that vanished mid-stream throws here; the point of
                // Close is to end up with no resources, not to report history.
            }
            reader.Dispose();
        }

        _capture?.Dispose();
        _capture = null;

        _scratch?.Dispose();
        _scratch = null;
        _winrtBuffer = null;
        _frameBytes = Array.Empty<byte>();
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
