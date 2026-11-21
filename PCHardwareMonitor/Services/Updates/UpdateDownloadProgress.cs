using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal enum UpdateDownloadStage { Preparing, Downloading, Verifying, Ready }
internal sealed record UpdateDownloadProgress(UpdateDownloadStage Stage, long ReceivedBytes, long? TotalBytes,
    double BytesPerSecond, TimeSpan IdleFor)
{
    internal int? Percent => Stage == UpdateDownloadStage.Ready ? 100 : TotalBytes is > 0
        ? (int)Math.Clamp(ReceivedBytes * 100d / TotalBytes.Value, 0, 99) : null;
}

// The heartbeat continues while ReadAsync is waiting, so a stalled connection stays visible.
internal sealed class UpdateDownloadReporter : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IProgress<int>? _legacy;
    private readonly IProgress<UpdateDownloadProgress>? _details;
    private readonly Func<TimeSpan> _clock;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _heartbeat;
    private UpdateDownloadStage _stage;
    private long _received, _sampleBytes;
    private long? _total;
    private TimeSpan _sampleTime, _lastData;
    private int _lastPercent = -1;

    internal UpdateDownloadReporter(IProgress<int>? legacy, IProgress<UpdateDownloadProgress>? details,
        Func<TimeSpan>? clock = null, bool heartbeat = true)
    {
        _legacy = legacy; _details = details;
        var timer = Stopwatch.StartNew(); _clock = clock ?? (() => timer.Elapsed);
        _sampleTime = _lastData = _clock();
        _heartbeat = heartbeat && (legacy != null || details != null) ? HeartbeatAsync() : Task.CompletedTask;
        Publish();
    }
    internal void SetStage(UpdateDownloadStage stage, long? total = null)
    {
        lock (_gate)
        {
            _stage = stage;
            if (total.HasValue) _total = total > 0 ? total : null;
            _sampleTime = _lastData = _clock(); _sampleBytes = _received;
            Publish();
        }
    }
    internal void Received(long count)
    {
        lock (_gate)
        {
            if (count < _received) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > _received) _lastData = _clock();
            _received = count;
        }
    }
    internal UpdateDownloadProgress Sample()
    {
        lock (_gate)
        {
            TimeSpan now = _clock();
            double seconds = (now - _sampleTime).TotalSeconds;
            double speed = _stage == UpdateDownloadStage.Downloading && seconds > 0 ? (_received - _sampleBytes) / seconds : 0;
            _sampleTime = now; _sampleBytes = _received;
            return new(_stage, _received, _total, Math.Max(0, speed), now >= _lastData ? now - _lastData : TimeSpan.Zero);
        }
    }
    private void Publish()
    {
        lock (_gate)
        {
            var value = Sample();
            if (value.Percent is int percent && percent != _lastPercent)
            { _lastPercent = percent; _legacy?.Report(percent); }
            _details?.Report(value);
        }
    }
    private async Task HeartbeatAsync()
    {
        try
        {
            while (true) { await Task.Delay(500, _stop.Token).ConfigureAwait(false); Publish(); }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); await _heartbeat.ConfigureAwait(false); _stop.Dispose();
    }
}
