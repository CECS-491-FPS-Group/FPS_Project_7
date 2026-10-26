using System;

// Pure measurement state used by the server component. XZ only; no networking or enforcement.
internal sealed class HorizontalMovementObservation
{
    internal const double SampleInterval = 0.25d;
    internal const double WindowDuration = 2d;
    internal const double SpeedMargin = 1.10d;
    internal const double BurstSeconds = 0.35d;
    internal const double DistanceSlack = 0.5d;
    internal const double DisplacementSlack = 12d;
    internal const double TransitionSeconds = 1d;
    internal const double MaxFrameGap = 1d;
    internal const double ReportSuspicion = 4d;

    internal struct Evidence
    {
        internal string Rule;
        internal double Distance, Allowance, Elapsed, Speed;
    }

    internal double Suspicion { get; private set; }
    private bool _hasPosition;
    private double _frameAt, _sampleAt, _x, _z, _previousSpeed, _transitionSpeed, _transitionUntil;
    private double _sampleBudget, _sampleMaxSpeed, _windowTime, _windowDistance, _windowBudget, _windowMaxSpeed;

    internal void Reset(double now, double x, double z)
    {
        Suspicion = 0d;
        _previousSpeed = _transitionSpeed = _transitionUntil = 0d;
        Rebase(now, x, z);
    }

    private void Rebase(double now, double x, double z)
    {
        _hasPosition = Finite(x) && Finite(z);
        _frameAt = _sampleAt = now;
        _x = x; _z = z;
        _sampleBudget = _sampleMaxSpeed = 0d;
        ClearWindow();
    }

    private void ClearWindow()
    {
        _windowTime = _windowDistance = _windowBudget = _windowMaxSpeed = 0d;
    }

    internal Evidence? Observe(double now, double x, double z, double approvedSpeed)
    {
        if (!Finite(x) || !Finite(z))
        {
            Rebase(now, x, z); // Do not poison future baselines with NaN/Infinity.
            return MakeEvidence("invalid-horizontal-position", 0d, 0d, 0d, approvedSpeed);
        }
        double elapsed = now - _frameAt;
        if (!_hasPosition || elapsed <= 0d || elapsed > MaxFrameGap)
        {
            // A stalled server is inconclusive, not a speed accusation. Keep prior suspicion,
            // but discard partial measurements; no catch-up charge on the following frame.
            Rebase(now, x, z);
            _previousSpeed = approvedSpeed;
            return null;
        }

        // A lower authoritative limit starts one bounded tail for delayed replication/deceleration.
        // This is driven by server state changes, never by requests or observed client speed.
        if (approvedSpeed < _previousSpeed)
        {
            _transitionSpeed = Math.Max(_previousSpeed, now < _transitionUntil ? _transitionSpeed : 0d);
            _transitionUntil = now + TransitionSeconds;
        }
        double intervalSpeed = Math.Max(approvedSpeed, _previousSpeed);
        double tailTime = Math.Max(0d, Math.Min(now, _transitionUntil) - _frameAt);
        _sampleBudget += intervalSpeed * elapsed + Math.Max(0d, _transitionSpeed - intervalSpeed) * tailTime;
        _sampleMaxSpeed = Math.Max(_sampleMaxSpeed, Math.Max(intervalSpeed, tailTime > 0d ? _transitionSpeed : 0d));
        _previousSpeed = approvedSpeed;
        _frameAt = now;

        double sampleTime = now - _sampleAt;
        if (sampleTime < SampleInterval) return null;
        double dx = x - _x, dz = z - _z;
        double distance = Math.Sqrt(dx * dx + dz * dz);
        double displacementLimit = _sampleBudget * SpeedMargin + DisplacementSlack + _sampleMaxSpeed * BurstSeconds;
        _x = x; _z = z; _sampleAt = now;
        if (distance > displacementLimit)
        {
            var jump = MakeEvidence("horizontal-displacement", distance, displacementLimit, sampleTime, _sampleMaxSpeed);
            // Report this jump once, then observe from the new position. Never rubberband, and
            // never let one teleport contaminate successive sustained-speed windows.
            Rebase(now, x, z);
            return jump;
        }

        _windowDistance += distance;
        _windowBudget += _sampleBudget;
        _windowMaxSpeed = Math.Max(_windowMaxSpeed, _sampleMaxSpeed);
        _windowTime += sampleTime;
        _sampleBudget = _sampleMaxSpeed = 0d;
        if (_windowTime < WindowDuration) return null;

        double limit = _windowBudget * SpeedMargin + DistanceSlack + _windowMaxSpeed * BurstSeconds;
        bool excessive = _windowDistance > limit;
        Suspicion = excessive ? Math.Min(10d, Suspicion + _windowTime) : Math.Max(0d, Suspicion - _windowTime);
        Evidence? result = excessive && Suspicion >= ReportSuspicion
            ? MakeEvidence("sustained-horizontal-speed", _windowDistance, limit, _windowTime, _windowMaxSpeed)
            : (Evidence?)null;
        ClearWindow();
        return result;
    }

    private static Evidence MakeEvidence(string rule, double distance, double allowance, double elapsed, double speed)
        => new Evidence { Rule = rule, Distance = distance, Allowance = allowance, Elapsed = elapsed, Speed = speed };
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
