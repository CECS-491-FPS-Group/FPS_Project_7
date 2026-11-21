using System;
using System.Collections.Generic;

internal static class ReceivedMovementChecks
{
    private static int passed;
    private static void Check(bool value, string label)
    { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
    private static ReceivedMovementObservation.Sample Point(double t, double x, double y = 0,
        ReceivedMovementObservation.Support support = ReceivedMovementObservation.Support.Clear, double speed = 6, double jump = 1.2)
        => new ReceivedMovementObservation.Sample { Time = t, X = x, Y = y, Z = 0, Speed = speed,
            Launch = ReceivedMovementObservation.UpwardCeiling(Math.Sqrt(jump * 30), speed, 45),
            Gravity = -15, Step = .25, LevelTolerance = .05, Ground = support };
    private static List<ReceivedMovementObservation.Evidence> Run(Func<double, ReceivedMovementObservation.Sample> motion, double end = 12)
    {
        var state = new ReceivedMovementObservation(); state.Reset();
        var results = new List<ReceivedMovementObservation.Evidence>();
        for (double t = 0; t <= end; t += .25) { var e = state.Observe(motion(t)); if (e.HasValue) results.Add(e.Value); }
        return results;
    }
    public static void Main()
    {
        Check(ReceivedMovementObservation.IsNewTick(false, 999, 1), "lifecycle reset accepts a new connection's first tick");
        Check(!ReceivedMovementObservation.IsNewTick(true, 100, 100), "duplicate decoded tick not fresh evidence");
        Check(!ReceivedMovementObservation.IsNewTick(true, 100, 99), "older decoded tick not fresh evidence");
        Check(ReceivedMovementObservation.IsNewTick(true, 100, 101), "advancing decoded tick is only a novelty marker");
        foreach (double height in new[] { 1.2, 1.8 })
        {
            double launch = Math.Sqrt(height * 30);
            Check(Math.Abs(launch - (height == 1.2 ? 6 : Math.Sqrt(54))) < .00001, "launch derived from approved height/gravity " + height);
            Check(Run(t => {
                double age = t % 2, y = Math.Max(0, launch * age - 7.5 * age * age);
                return Point(t, 4*t, y, y < .2 ? ReceivedMovementObservation.Support.NearGeometry : ReceivedMovementObservation.Support.Clear, 6, height);
            }).Count == 0, "legal normal/boosted jumps " + height);
        }
        Check(Run(t => Point(t, 4*t, Math.Max(0, 200 - 7.5*t*t), t > 5 ? ReceivedMovementObservation.Support.NearGeometry : ReceivedMovementObservation.Support.Clear)).Count == 0,
            "elevated long fall is not ascent/hover/teleport");
        foreach (double speed in new[] { 6d, 9d })
        {
            Check(Run(t => Point(t, speed*t, speed*t, ReceivedMovementObservation.Support.NearGeometry, speed)).Count == 0, "legal slope supported " + speed);
            Check(Run(t => Point(t, speed*t, .25*Math.Floor(t*4), ReceivedMovementObservation.Support.NearGeometry, speed)).Count == 0, "steps near collision support " + speed);
        }
        Check(Run(t => Point(t, t, 1, ReceivedMovementObservation.Support.Unknown)).Count == 0, "moving/ambiguous support does not imply hover");
        Check(Run(t => Point(t, 0, 20)).Count == 0, "stationary replicated positions are not hover evidence");
        var hover = Run(t => Point(t, Math.Sin(t)*.25, 20));
        Check(hover.Exists(e => e.Rule == "unsupported-level-motion" && e.Elapsed >= 4 && e.Samples >= 8), "fresh moving hover candidate requires time and samples");
        Check(!hover.Exists(e => e.Rule == "excessive-unsupported-ascent"), "level motion is not charged as ascent");
        Check(hover.TrueForAll(e => e.Distance > 0 && e.Distance < 4), "level evidence logs actual changed horizontal distance, not seconds as metres");
        var ascent = Run(t => Point(t, 0, 20*t));
        Check(ascent.Exists(e => e.Rule == "excessive-unsupported-ascent" && e.Observed > e.Allowed && e.Elapsed >= 1.5), "sustained excessive ascent exceeds derived launch envelope");
        Check(!ascent.Exists(e => e.Rule == "unsupported-level-motion"), "ascent is not hovering");
        var single = Run(t => Point(t, t < 2 ? t : t+40, 0, ReceivedMovementObservation.Support.NearGeometry));
        Check(single.Count == 0, "one teleport is not repeated evidence");
        var repeated = Run(t => Point(t, 40*Math.Floor(t/2), 0, ReceivedMovementObservation.Support.NearGeometry));
        Check(repeated.Exists(e => e.Rule == "repeated-unexplained-displacement" && e.Samples == 3), "three independent events within bounded window");
        Check(!repeated.Exists(e => e.Rule == "excessive-unsupported-ascent"), "horizontal events do not contaminate ascent");
        var burst = Run(t => Point(t, t == 2 ? 40 : t > 2 ? 80 : 0, 0, ReceivedMovementObservation.Support.NearGeometry));
        Check(burst.Count == 0, "nearby chunks of one event cannot count as repeated teleports");
        foreach (var report in hover) Check(!HorizontalMovementObservation.CanEnforce(report.Rule, 10, 5), "level motion never qualifies for existing policy");
        foreach (var report in ascent) Check(!HorizontalMovementObservation.CanEnforce(report.Rule, 10, 5), "ascent never qualifies for existing policy");
        foreach (var report in repeated) Check(!HorizontalMovementObservation.CanEnforce(report.Rule, 10, 5), "repeated displacement never qualifies for existing policy");
        var state = new ReceivedMovementObservation(); state.Reset(); state.Observe(Point(0, 0));
        Check(!state.Observe(Point(0, 100)).HasValue, "duplicate timestamp cannot add event");
        Check(!state.Observe(Point(-1, 100)).HasValue, "backward timestamp cannot add event");
        Check(!state.Observe(Point(.1, 0)).HasValue && state.Quality == "coalesced", "delivery burst coalesced before evidence");
        for (int i=1;i<=4;i++) state.Observe(Point(i*.25, i*.02, 20));
        Check(!state.Observe(Point(3, .1, 20)).HasValue && state.Quality.Contains("sparse"), "missing arrival interval breaks air continuity");
        state.Reset(); Check(!state.Observe(Point(100, 10000, 1000)).HasValue, "authorized spawn/respawn/scene/frame-stall reset rebases anywhere");
        Check(Run(t => Point(t, t, 0, ReceivedMovementObservation.Support.NearGeometry)).Count == 0, "crouch feet anchor unchanged while supported");
        Check(Run(t => {
            double y = Math.Max(0, Math.Sqrt(t < 2 ? 54 : 36)*(t%2)-7.5*(t%2)*(t%2));
            return Point(t, 4*t, y, y < .2 ? ReceivedMovementObservation.Support.NearGeometry : ReceivedMovementObservation.Support.Clear, 6, t < .5 ? 1.8 : 1.2);
        }).Count == 0, "Jump expiration/replacement cannot invalidate already legal jump");
        Check(Run(t => Point(t, t < 4 ? 9*t : 36+6*(t-4), 0, ReceivedMovementObservation.Support.NearGeometry, t < 4 ? 9 : 6)).Count == 0,
            "Speed expiration and replacement preserve normal travel");
        Check(Run(t => Point(t, 6*Math.Floor(t/.5)*.5, 0, ReceivedMovementObservation.Support.NearGeometry)).Count == 0, "half-second delivery bursts at legal speed");
        var other = new ReceivedMovementObservation(); other.Reset(); other.Observe(Point(0,0));
        state.Reset(); Check(!other.Observe(Point(.25,1,0,ReceivedMovementObservation.Support.NearGeometry)).HasValue, "separate player state unaffected by reset");
        var bad = Point(1,0); bad.Y=double.NaN;
        Check(!state.Observe(bad).HasValue && state.Quality.Contains("invalid"), "nonfinite vertical samples inconclusive");
        bad = Point(2,0); bad.Gravity=0;
        Check(!state.Observe(bad).HasValue, "invalid server physics configuration is unavailable, not cheating");
        var aged = new ReceivedMovementObservation(); aged.Reset(); aged.Observe(Point(0,0));
        aged.Observe(Point(2,40)); aged.Observe(Point(20,80));
        Check(!aged.Observe(Point(22,120)).HasValue, "old displacement events expire");
        var recovering = new ReceivedMovementObservation(); recovering.Reset();
        for(int i=0;i<=40;i++) recovering.Observe(Point(i*.25,0,i*5));
        recovering.Reset();
        Check(!recovering.Observe(Point(20,0,0,ReceivedMovementObservation.Support.NearGeometry)).HasValue, "normal recovery after ascent rebase");
        Check(Run(t => Point(t, 9*t, 9*t, ReceivedMovementObservation.Support.Clear, 9)).Count == 0,
            "conservative approved Speed slope ceiling even away from support");
        Check(Run(t => Point(t, 0, 9*t, ReceivedMovementObservation.Support.Clear, t < 3 ? 9 : 6))
            .Exists(e => e.Rule == "excessive-unsupported-ascent" && e.Elapsed >= 1.5), "expired Speed cannot grant indefinite old ascent ceiling");
        Check(Run(t => Point(t, 40*Math.Floor(t/2), 0, ReceivedMovementObservation.Support.NearGeometry, 9))
            .Exists(e => e.Rule == "repeated-unexplained-displacement"), "repeated 40m probe exceeds approved boosted displacement budget too");
        var expired = new ReceivedMovementObservation(); expired.Reset(); expired.Observe(Point(0,0));
        expired.Observe(Point(2,40)); expired.Observe(Point(4,80)); expired.Observe(Point(22,80));
        Check(!expired.Observe(Point(24,120)).HasValue, "two old events cannot combine with fresh event past window");
        Check(!expired.Observe(Point(26,160)).HasValue && expired.Observe(Point(28,200)).HasValue, "new independent events can form a fresh bounded window");
        var lifecycle = new ReceivedMovementObservation(); lifecycle.Reset();
        lifecycle.Observe(Point(0,0,0,ReceivedMovementObservation.Support.NearGeometry,9));
        lifecycle.Observe(Point(2,40,0,ReceivedMovementObservation.Support.NearGeometry,9));
        lifecycle.Observe(Point(4,0,0,ReceivedMovementObservation.Support.NearGeometry,9));
        lifecycle.Reset(); // Boosted death/server-approved placement or owner/session change.
        lifecycle.Observe(Point(6,1000)); lifecycle.Observe(Point(8,1040));
        Check(!lifecycle.Observe(Point(10,1000)).HasValue, "boosted lifecycle reset discards previous event history/allowance");
        Check(lifecycle.Observe(Point(12,1040)).HasValue, "new life needs three new independent events");
        Console.WriteLine("Passed " + passed + " received movement checks. Pure samples, not transport/physics/runtime acceptance.");
    }
}
