using System;

// Pure temporal correlation, not a verdict. No policy, networking, targets or damage.
public sealed class OccludedAimTracking
{
    public enum Visibility { Uncertain, Visible, Covered }
    public struct Sample
    {
        public double Time, AimYaw, AimPitch, TargetYaw, TargetPitch;
        public Visibility Cover;
        public bool Fresh, PossibleCue;
    }
    public const double Grace = 2, Window = 6, MaximumGap = .65;
    public int Samples { get; private set; }
    public double Duration { get; private set; }
    public double MatchedTime { get; private set; }
    public double AimTravel { get; private set; }
    public double TargetTravel { get; private set; }
    private double _graceUntil, _start;
    private Sample _previous;
    private bool _havePrevious;
    public void Reset(double now)
    {
        _graceUntil = now + Grace; ClearWindow();
    }
    private void ClearWindow()
    {
        Samples = 0; Duration = MatchedTime = AimTravel = TargetTravel = 0; _havePrevious = false;
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public static double Delta(double from, double to) => ((to - from + 540) % 360 + 360) % 360 - 180;
    public bool Observe(Sample s)
    {
        if (!Finite(s.Time)) { Reset(0); return false; }
        if (!s.Fresh || s.PossibleCue || s.Cover != Visibility.Covered ||
            !Finite(s.AimYaw) || !Finite(s.AimPitch) || !Finite(s.TargetYaw) || !Finite(s.TargetPitch) ||
            Math.Abs(s.AimPitch) > 80 || Math.Abs(s.TargetPitch) > 80)
        { Reset(s.Time); return false; }
        if (s.Time < _graceUntil) return false;
        if (_havePrevious && (s.Time <= _previous.Time || s.Time - _previous.Time > MaximumGap))
        { Reset(s.Time); return false; }
        double errorYaw = Delta(s.AimYaw, s.TargetYaw), errorPitch = s.AimPitch - s.TargetPitch;
        if (Math.Sqrt(errorYaw * errorYaw + errorPitch * errorPitch) > 3)
        { ClearWindow(); return false; }
        if (!_havePrevious) { ClearWindow(); _start = s.Time; _previous = s; _havePrevious = true; Samples = 1; return false; }
        double ay = Delta(_previous.AimYaw, s.AimYaw), ap = s.AimPitch - _previous.AimPitch;
        double ty = Delta(_previous.TargetYaw, s.TargetYaw), tp = s.TargetPitch - _previous.TargetPitch;
        double a = Math.Sqrt(ay * ay + ap * ap), t = Math.Sqrt(ty * ty + tp * tp);
        double dt = s.Time - _previous.Time;
        if (a > .1 && t > .1 && a / t >= .5 && a / t <= 1.5 && (ay * ty + ap * tp) / (a * t) >= .9)
            MatchedTime += dt;
        AimTravel += a; TargetTravel += t; Samples++; Duration = s.Time - _start; _previous = s;
        if (Duration < Window) return false;
        bool report = Samples >= 20 && MatchedTime >= Duration * .8 && AimTravel >= 12 && TargetTravel >= 12;
        // Caller can capture metrics until next Observe; a completed window never grows unbounded.
        _havePrevious = false;
        return report;
    }
}
