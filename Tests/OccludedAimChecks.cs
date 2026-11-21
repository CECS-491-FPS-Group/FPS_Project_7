using System;

internal static class OccludedAimChecks
{
    private static int passed;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
    private static OccludedAimTracking.Sample Point(double t) => new OccludedAimTracking.Sample {
        Time = t, Fresh = true, Cover = OccludedAimTracking.Visibility.Covered,
        TargetYaw = 5 * Math.Sin(t), AimYaw = 5 * Math.Sin(t) };
    private static int Run(Func<double, OccludedAimTracking.Sample> sample, double end = 14, Action<OccludedAimTracking, double> before = null)
    {
        var state = new OccludedAimTracking(); state.Reset(0); int alerts = 0;
        for (int i = 0; i <= end * 5; i++) { double time = i * .2; before?.Invoke(state, time); if (state.Observe(sample(time))) alerts++; }
        return alerts;
    }
    public static void Main()
    {
        Check(Run(Point) > 0, "moving covered target with matching changing aim produces review signal");
        Check(Run(Point, 7.8) == 0, "visibility-loss grace plus six seconds required");
        Check(Run(t => { var s = Point(t); s.AimYaw = 0; return s; }) == 0, "holding common doorway angle is not tracking");
        Check(Run(t => { var s = Point(t); s.AimYaw = s.TargetYaw = 0; return s; }) == 0, "static target and crosshair are weak evidence");
        Check(Run(t => { var s = Point(t); s.Cover = OccludedAimTracking.Visibility.Visible; return s; }) == 0, "visible target never accumulates");
        Check(Run(t => { var s = Point(t); s.Cover = OccludedAimTracking.Visibility.Uncertain; return s; }) == 0, "uncertain partial exposure never accumulates");
        Check(Run(t => { var s = Point(t); if (t % 5 < 1) s.Cover = OccludedAimTracking.Visibility.Visible; return s; }) == 0, "corner appearances interrupt hidden tracking");
        Check(Run(Point, 14, (s, t) => { if (t % 5 < .19) s.Reset(t); }) == 0, "death/life/round/target reset discards evidence");
        Check(Run(t => { var s = Point(t); s.Fresh = false; return s; }) == 0, "missing and stale aim do not mature evidence");
        Check(Run(t => { var s = Point(t); if (t % 3 < .21) s.Fresh = false; return s; }) == 0, "intermittent stale target clears contiguous window");
        Check(Run(t => { var s = Point(t); s.PossibleCue = true; return s; }) == 0, "possible footsteps suppress observation");
        Check(Run(t => { var s = Point(t); if (t % 5 < .21) s.PossibleCue = true; return s; }) == 0, "accepted-shot or hurt cues renew grace");
        Check(Run(t => { var s = Point(t); s.AimYaw = -s.TargetYaw; return s; }) == 0, "opposed angular changes fail correlation");
        Check(Run(t => { var s = Point(t); s.AimYaw += 8; return s; }) == 0, "nearby unrelated aim is outside alignment cone");
        Check(Run(t => { var s = Point(t); s.AimYaw = s.TargetYaw = .1 * Math.Sin(t); return s; }) == 0, "tiny stationary jitter fails angular travel");
        Check(Run(t => { var s = Point(t); s.AimPitch = double.NaN; return s; }) == 0, "nonfinite data invalidates evidence");
        Check(Run(t => { var s = Point(t); s.AimPitch = s.TargetPitch = 85; return s; }) == 0, "near-vertical angular singularity is uncertain");
        Check(Run(t => { var s = Point(t); s.Time = 0; return s; }) == 0, "duplicate evaluations cannot add elapsed evidence");
        Check(Run(t => { var s = Point(t); s.Time = t * 5; return s; }) == 0, "delivery gaps reset instead of bridging time");
        Check(Run(t => { var s = Point(t); if (t >= 4) s.Cover = OccludedAimTracking.Visibility.Visible; return s; }) == 0, "brief following after disappearance is tolerated");
        Check(Run(t => { var s = Point(t); s.AimYaw += 179; s.TargetYaw += 179; return s; }) > 0, "angle wrapping preserves correlated motion");
        Check(Math.Abs(OccludedAimTracking.Delta(179, -179) - 2) < .001, "signed shortest wrapped delta");
        var noisy = new OccludedAimTracking(); var clean = new OccludedAimTracking(); noisy.Reset(0); clean.Reset(0); int a = 0, b = 0;
        for (int i = 0; i <= 60; i++) { var s = Point(i * .2); if (noisy.Observe(s)) a++; s.AimYaw = 0; if (clean.Observe(s)) b++; }
        Check(a > 0 && b == 0, "separate players have independent state");
        Check(Run(Point, 120) < 21, "completed evidence windows have bounded cadence");
        Console.WriteLine($"OccludedAim: {passed} assertions passed; no policy dependency or punitive output exists.");
    }
}
