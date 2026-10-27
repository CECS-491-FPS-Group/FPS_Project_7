using System;
using System.Collections.Generic;

// Standalone deterministic checks of the SAME measurement class used by SpeedHackDetector.
// These are not substitutes for FishNet/Unity runtime tests. See Docs/MovementObserver.md.
internal static class MovementObserverChecks
{
    private static int _passed;
    private static readonly double[] Steps = { 0.017d, 0.043d, 0.081d, 0.029d };
    private static double BaseSpeed => ApprovedMovementLimits.SprintSpeed;
    private static double Boosted => BaseSpeed * ApprovedMovementLimits.SpeedMultiplier;

    private static List<HorizontalMovementObservation.Evidence> Run(
        Func<double, double> position, Func<double, double> allowance, double duration = 16d)
    {
        var tracker = new HorizontalMovementObservation();
        tracker.Reset(0d, position(0d), 0d);
        var results = new List<HorizontalMovementObservation.Evidence>();
        double now = 0d;
        int step = 0;
        while (now < duration)
        {
            now = Math.Min(duration, now + Steps[step++ % Steps.Length]);
            var evidence = tracker.Observe(now, position(now), 0d, allowance(now));
            if (evidence.HasValue) results.Add(evidence.Value);
        }
        return results;
    }

    private static void Check(string name, bool passed)
    {
        if (!passed) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        _passed++;
    }

    // An 8-second Speed effect; client receives a replacement/expiry .5s late, then decelerates.
    private static double ExpiringSpeedPosition(double t)
    {
        if (t <= 8.5d) return Boosted * t;
        double after = t - 8.5d;
        return Boosted * 8.5d + BaseSpeed * after + (Boosted - BaseSpeed) * (1d - Math.Exp(-10d * after)) / 10d;
    }

    public static int Main()
    {
        try
        {
            Check("normal sprint, irregular elapsed time", Run(t => BaseSpeed * t, t => BaseSpeed).Count == 0);
            Check("legitimate boosted sprint", Run(t => Boosted * t, t => Boosted).Count == 0);
            Check("Speed activation midway through a sampling window",
                Run(t => BaseSpeed * Math.Min(t, 3.13d) + Boosted * Math.Max(0d, t - 3.13d),
                    t => t < 3.13d ? BaseSpeed : Boosted).Count == 0);
            Check("Speed expiry: delayed delivery and deceleration",
                Run(ExpiringSpeedPosition, t => t < 8d ? Boosted : BaseSpeed).Count == 0);
            // Shield/Jump/Damage all feed 6, never 9, through GetServerHorizontalSpeed.
            foreach (string kind in new[] { "Shield", "Jump", "Damage" })
            {
                Check("Speed replaced by " + kind + ": bounded tail",
                    Run(ExpiringSpeedPosition, t => t < 8d ? Boosted : BaseSpeed).Count == 0);
                Check(kind + " moving at normal sprint (vertical motion intentionally absent)",
                    Run(t => BaseSpeed * t, t => BaseSpeed).Count == 0);
                Check(kind + " cannot justify permanent 9m/s movement",
                    Run(t => Boosted * t, t => BaseSpeed).Exists(e => e.Rule == "sustained-horizontal-speed"));
            }
            Check("3x speed beyond legitimate boosted speed",
                Run(t => Boosted * 3d * t, t => Boosted).Exists(e => e.Rule == "sustained-horizontal-speed"));
            Check("expired Speed cannot renew tail from continuing client movement",
                Run(t => Boosted * t, t => t < 3d ? Boosted : BaseSpeed)
                    .Exists(e => e.Rule == "sustained-horizontal-speed" && e.Speed == BaseSpeed));
            Check("350ms replication bursts at normal sprint",
                Run(t => Math.Floor(t / 0.35d) * 0.35d * BaseSpeed, t => BaseSpeed).Count == 0);
            Check("350ms replication bursts at boosted sprint",
                Run(t => Math.Floor(t / 0.35d) * 0.35d * Boosted, t => Boosted).Count == 0);
            var jumps = Run(t => t < 4d ? 0d : 30d, t => BaseSpeed);
            Check("30m jump reports exactly once; clean subsequent baseline",
                jumps.Count == 1 && jumps[0].Rule == "horizontal-displacement");

            var observation = new HorizontalMovementObservation();
            observation.Reset(0d, 0d, 0d);
            for (int i = 1; i <= 8; i++) observation.Observe(i * 0.25d, i * 4.5d, 0d, BaseSpeed);
            Check("one excessive window accumulates suspicion without accusation",
                observation.Suspicion >= 2d && observation.Suspicion < HorizontalMovementObservation.ReportSuspicion);
            for (int i = 9; i <= 16; i++) observation.Observe(i * 0.25d, 36d, 0d, BaseSpeed);
            Check("clean window decays suspicion", observation.Suspicion == 0d);

            observation.Reset(5d, 1000d, 0d); // Server lifecycle rebase, not client permission.
            Check("death/respawn reset clears suspicion and old boosted window",
                observation.Suspicion == 0d && !observation.Observe(5.25d, 1001.5d, 0d, BaseSpeed).HasValue);
            Check("server frame gap over 1s is inconclusive", !observation.Observe(8d, 1500d, 0d, BaseSpeed).HasValue);
            Check("baseline clean after server gap", !observation.Observe(8.25d, 1501.5d, 0d, BaseSpeed).HasValue);
            Check("invalid position reported", observation.Observe(8.5d, double.NaN, 0d, BaseSpeed).Value.Rule == "invalid-horizontal-position");
            Check("finite position reacquires baseline", !observation.Observe(8.75d, 2000d, 0d, BaseSpeed).HasValue);

            // Every frame after an identical timestamp must reacquire, not divide by zero.
            Check("nonpositive elapsed time reacquires safely", !observation.Observe(8.75d, 2000d, 0d, BaseSpeed).HasValue);
            Check("enforcement default above report threshold and reachable",
                HorizontalMovementObservation.EnforcementThreshold(8d) > HorizontalMovementObservation.ReportSuspicion &&
                HorizontalMovementObservation.EnforcementThreshold(8d) <= HorizontalMovementObservation.MaxSuspicion);
            Check("unsafe enforcement thresholds bounded",
                HorizontalMovementObservation.EnforcementThreshold(0d) == 5d &&
                HorizontalMovementObservation.EnforcementThreshold(100d) == 10d &&
                HorizontalMovementObservation.EnforcementThreshold(double.NaN) == 8d);
            Check("displacement and invalid positions cannot enforce even at cap",
                !HorizontalMovementObservation.CanEnforce("horizontal-displacement", 10d, 8d) &&
                !HorizontalMovementObservation.CanEnforce("invalid-horizontal-position", 10d, 8d));
            var enforcement = new HorizontalMovementObservation();
            enforcement.Reset(0d, 0d, 0d);
            int eligibleWindows = 0;
            for (int i = 1; i <= 32; i++)
            {
                var result = enforcement.Observe(i * .25d, i * 4.5d, 0d, BaseSpeed);
                if (result.HasValue && HorizontalMovementObservation.CanEnforce(result.Value.Rule, enforcement.Suspicion, 8d)) eligibleWindows++;
                if (i < 32 && eligibleWindows != 0) throw new Exception("Early enforcement before 8 elapsed evidence seconds");
            }
            Check("four disjoint excessive windows reach default once", eligibleWindows == 1 && enforcement.Suspicion == 8d);
            for (int i = 33; i <= 40; i++) enforcement.Observe(i * .25d, i * 4.5d, 0d, BaseSpeed);
            Check("enforcement upper threshold reachable at suspicion cap", enforcement.Suspicion == 10d &&
                HorizontalMovementObservation.CanEnforce("sustained-horizontal-speed", enforcement.Suspicion, 10d));
            for (int i = 41; i <= 48; i++) enforcement.Observe(i * .25d, 180d, 0d, BaseSpeed);
            Check("elapsed clean window decays capped suspicion", enforcement.Suspicion == 8d);
            enforcement.Reset(12d, 180d, 0d);
            Check("lifecycle reset removes enforcement eligibility", !HorizontalMovementObservation.CanEnforce("sustained-horizontal-speed", enforcement.Suspicion, 8d));
            Console.WriteLine(_passed + " movement measurement checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
