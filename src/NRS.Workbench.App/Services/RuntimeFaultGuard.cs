namespace NRS.Workbench.App.Services;

public sealed class NonOverlappingOperationGate
{
    private int _entered;

    public bool TryEnter() => Interlocked.CompareExchange(ref _entered, 1, 0) == 0;

    public void Exit() => Volatile.Write(ref _entered, 0);

    public bool IsEntered => Volatile.Read(ref _entered) != 0;
}

public readonly record struct FaultReportDecision(bool ShouldReport, int SuppressedSinceLastReport);

public sealed class FaultBurstThrottle
{
    private readonly object _gate = new();
    private readonly TimeSpan _cooldown;
    private DateTimeOffset? _lastReportAt;
    private int _suppressed;

    public FaultBurstThrottle(TimeSpan cooldown)
    {
        if (cooldown < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cooldown));
        _cooldown = cooldown;
    }

    public FaultReportDecision Register(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_lastReportAt is DateTimeOffset last && now - last < _cooldown)
            {
                _suppressed++;
                return new FaultReportDecision(false, _suppressed);
            }

            var suppressed = _suppressed;
            _suppressed = 0;
            _lastReportAt = now;
            return new FaultReportDecision(true, suppressed);
        }
    }
}
