using System;

// New report-only rules. Inputs are decoded goals plus SERVER receipt time/geometry/
// entitlement, never client grounded flags, velocity or elapsed-time claims.
internal sealed class ReceivedMovementObservation
{
    internal enum Support { Unknown, NearGeometry, Clear }
    internal struct Sample
    {
        internal double Time, X, Y, Z, Speed, Launch, Gravity, Step, LevelTolerance;
        internal Support Ground;
    }
    internal struct Evidence
    {
        internal string Rule;
        internal double Observed, Allowed, Elapsed, Distance;
        internal int Samples;
    }
    internal const double Interval = .25, MaximumAirGap = 1, EventWindow = 15, EventSeparation = 1;
    internal const int RepeatedEvents = 3;
    internal static bool IsNewTick(bool havePrevious, uint previous, uint incoming) => !havePrevious || incoming > previous;
    private readonly double[] _events = new double[RepeatedEvents];
    private int _eventIndex, _eventCount;
    private double _lastEvent;
    private bool _has, _air;
    private Sample _previous;
    private double _ascentAt, _ascentY, _peakUp, _levelAt, _levelMin, _levelMax, _levelDistance;
    private int _ascentSamples, _levelSamples;
    internal string Quality { get; private set; } = "awaiting-fresh-position";

    internal void Reset()
    {
        _has = _air = false; _eventIndex = _eventCount = 0;
        _lastEvent = double.NegativeInfinity; Array.Clear(_events, 0, _events.Length);
        Quality = "lifecycle-reset";
    }
    internal void InvalidateAir(string quality) { _air = false; Quality = quality; }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static double Distance(double x, double y, double z) => Math.Sqrt(x*x + y*y + z*z);
    internal static double UpwardCeiling(double launch, double speed, double slopeDegrees) =>
        Math.Max(launch, speed * Math.Tan(slopeDegrees * Math.PI / 180));

    // Launch includes the conservative legal slope ascent rate selected by the adapter.
    internal Evidence? Observe(Sample sample)
    {
        if (!Finite(sample.Time) || !Finite(sample.X) || !Finite(sample.Y) || !Finite(sample.Z) ||
            !Finite(sample.Speed) || sample.Speed <= 0 || !Finite(sample.Launch) || sample.Launch <= 0 ||
            !Finite(sample.Gravity) || sample.Gravity >= 0 || !Finite(sample.Step) || sample.Step < 0 ||
            !Finite(sample.LevelTolerance) || sample.LevelTolerance < 0)
        { Reset(); Quality = "invalid-sample-or-server-configuration"; return null; }
        if (!_has) { _previous = sample; _has = true; Quality = "baseline"; return null; }
        double elapsed = sample.Time - _previous.Time;
        if (elapsed <= 0) { InvalidateAir("duplicate-or-backward-time"); return null; }
        if (elapsed < Interval) { Quality = "coalesced"; return null; }
        double dx = sample.X - _previous.X, dy = sample.Y - _previous.Y, dz = sample.Z - _previous.Z;
        double horizontal = Distance(dx, 0, dz);
        double moved = Distance(dx, dy, dz);
        double speed = Math.Max(sample.Speed, _previous.Speed);
        double launch = Math.Max(sample.Launch, _previous.Launch);
        _previous = sample;
        if (!Finite(moved)) { Reset(); Quality = "invalid-distance"; return null; }
        if (moved <= .001) { InvalidateAir("unchanged-position-not-hover-evidence"); return null; }
        double displacement = speed * elapsed * HorizontalMovementObservation.SpeedMargin +
            HorizontalMovementObservation.DisplacementSlack + speed * HorizontalMovementObservation.BurstSeconds;
        double upwardDisplacement = launch * elapsed * HorizontalMovementObservation.SpeedMargin +
            HorizontalMovementObservation.DisplacementSlack + launch * HorizontalMovementObservation.BurstSeconds;
        if (horizontal > displacement || dy > upwardDisplacement)
        {
            InvalidateAir("unexplained-displacement-rebased");
            if (sample.Time - _lastEvent < EventSeparation) return null;
            _lastEvent = sample.Time; _events[_eventIndex] = sample.Time;
            _eventIndex = (_eventIndex + 1) % RepeatedEvents; _eventCount = Math.Min(RepeatedEvents, _eventCount + 1);
            if (_eventCount == RepeatedEvents && sample.Time - _events[_eventIndex] <= EventWindow)
                return new Evidence { Rule = "repeated-unexplained-displacement", Observed = RepeatedEvents,
                    Allowed = RepeatedEvents - 1, Elapsed = sample.Time - _events[_eventIndex],
                    Distance = Math.Max(horizontal, dy), Samples = RepeatedEvents };
            return null;
        }
        if (elapsed > MaximumAirGap) { InvalidateAir("sparse-arrivals-no-air-inference"); return null; }
        if (sample.Ground != Support.Clear) { InvalidateAir(sample.Ground == Support.Unknown ? "support-ambiguous" : "near-collision-support"); return null; }
        Quality = "fresh-changed-position-clear-support-volume";
        if (!_air)
        {
            _air = true; _ascentAt = _levelAt = sample.Time; _ascentY = _levelMin = _levelMax = sample.Y;
            _peakUp = sample.Launch; _ascentSamples = _levelSamples = 1; _levelDistance = 0; return null;
        }
        _peakUp = Math.Max(_peakUp, sample.Launch); _ascentSamples++; _levelSamples++;
        _levelDistance += horizontal;
        _levelMin = Math.Min(_levelMin, sample.Y); _levelMax = Math.Max(_levelMax, sample.Y);
        if (_levelMax - _levelMin > sample.LevelTolerance)
        { _levelAt = sample.Time; _levelMin = _levelMax = sample.Y; _levelSamples = 1; _levelDistance = 0; }
        double window = sample.Time - _ascentAt;
        // Bounds legal launch/slope rise, with the existing burst allowance ONCE/window.
        double allowance = _peakUp * window + _peakUp * HorizontalMovementObservation.BurstSeconds +
            sample.Step + HorizontalMovementObservation.DistanceSlack;
        if (window >= 1.5 && _ascentSamples >= 6)
        {
            double rise = sample.Y - _ascentY; int count = _ascentSamples;
            _ascentAt = sample.Time; _ascentY = sample.Y; _ascentSamples = 1;
            // Retain entitlement only for this completed window, not indefinitely in air.
            _peakUp = sample.Launch;
            if (rise > allowance) return new Evidence { Rule = "excessive-unsupported-ascent",
                Observed = rise, Allowed = allowance, Elapsed = window, Distance = rise, Samples = count };
        }
        // This deliberately does NOT infer hovering from stationary/rotation-only goals.
        double levelTime = sample.Time - _levelAt;
        double required = Math.Max(2 * HorizontalMovementObservation.WindowDuration, 4 * _peakUp / -sample.Gravity);
        if (levelTime >= required && _levelSamples >= 8)
        {
            int count = _levelSamples; double span = _levelMax - _levelMin, distance = _levelDistance;
            _levelAt = sample.Time; _levelMin = _levelMax = sample.Y; _levelSamples = 1; _levelDistance = 0;
            return new Evidence { Rule = "unsupported-level-motion", Observed = span, Allowed = sample.LevelTolerance,
                Elapsed = levelTime, Distance = distance, Samples = count };
        }
        return null;
    }
}
