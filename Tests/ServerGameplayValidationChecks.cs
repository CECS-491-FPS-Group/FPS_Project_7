using System;

internal static class ServerGameplayValidationChecks
{
    private static int _passed;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        _passed++;
    }
    private static int Consume(ServerGameplayValidation.RequestBudget budget, double now, int count)
    {
        int accepted = 0;
        for (int i = 0; i < count; i++) if (budget.TryConsume(now)) accepted++;
        return accepted;
    }
    public static void Main()
    {
        var budget = new ServerGameplayValidation.RequestBudget(60, 60);
        Check(Consume(budget, 0, 128) == 60, "bounded initial combat burst");
        Check(!budget.TryConsume(0), "other combat action cannot bypass depleted shared budget");
        Check(Consume(budget, .5, 60) == 30, "refill by actual elapsed time");
        Check(!budget.TryConsume(.5), "repeated timestamp never refills");
        Check(Consume(budget, 100, 128) == 60, "long idle cannot accumulate unbounded burst");
        Check(!budget.TryConsume(99), "backward clock does not refill");
        Check(!budget.TryConsume(double.NaN) && !budget.TryConsume(double.PositiveInfinity), "invalid clock rejected");
        Check(!budget.TryConsume(-1), "negative clock rejected");
        Check(budget.TryConsume(100.5), "valid clock recovers after invalid calls");
        var peer = new ServerGameplayValidation.RequestBudget(60, 60);
        Check(Consume(peer, 100, 128) == 60, "another connection has independent capacity");
        Check(ServerGameplayValidation.CombatRequestRate(10) == 60, "current weapon floor");
        Check(ServerGameplayValidation.CombatRequestRate(100) == 208, "server high-rate weapon not capped at 60");
        Check(ServerGameplayValidation.CombatRequestRate(double.NaN) == 60, "invalid configuration cannot poison budget");
        var query = new ServerGameplayValidation.RequestBudget(2, 4);
        Check(Consume(query, 0, 16) == 4, "bounded snapshot query burst");
        Check(Consume(query, .5, 16) == 1, "snapshot query refill");
        Check(query.PermitRejectionLog(0) && !query.PermitRejectionLog(0), "rejection log emitted once");
        Check(!query.PermitRejectionLog(4.99) && query.PermitRejectionLog(5), "five second rejection log throttle");
        Check(!query.PermitRejectionLog(double.NaN), "invalid clock cannot bypass log throttle");
        var normal = new ServerGameplayValidation.RequestBudget(60, 60);
        bool legitimate = true;
        for (int i = 0; i < 1000; i++) legitimate &= normal.TryConsume(i * .1);
        Check(legitimate, "sustained legitimate 10Hz rifle requests admitted");
        var fast = new ServerGameplayValidation.RequestBudget(208, 208);
        for (int i = 0; i < 1000; i++) if (!fast.TryConsume(i * .01)) throw new Exception("100Hz approved profile rejected");
        Check(true, "future server-approved 100Hz profile admitted");
        Check(ServerGameplayValidation.WithinPickupRange(0, 0, 0, 2.5), "coincident pickup");
        Check(ServerGameplayValidation.WithinPickupRange(1.5, 2, 0, 2.5), "exact three-dimensional boundary");
        Check(!ServerGameplayValidation.WithinPickupRange(1.5, 2.001, 0, 2.5), "outside pickup radius");
        Check(!ServerGameplayValidation.WithinPickupRange(0, 2.6, 0, 2.5), "vertical separation counts");
        Check(!ServerGameplayValidation.WithinPickupRange(double.NaN, 0, 0, 2.5), "NaN cannot collect pickup");
        Check(!ServerGameplayValidation.WithinPickupRange(0, double.PositiveInfinity, 0, 2.5), "infinite Y cannot collect pickup");
        Check(!ServerGameplayValidation.WithinPickupRange(0, 0, double.NegativeInfinity, 2.5), "infinite Z cannot collect pickup");
        Check(!ServerGameplayValidation.WithinPickupRange(double.MaxValue, 0, 0, 2.5), "distance overflow rejected");
        Check(!ServerGameplayValidation.WithinPickupRange(0, 0, 0, double.MaxValue), "radius overflow rejected");
        Check(!ServerGameplayValidation.WithinPickupRange(0, 0, 0, double.NaN) &&
            !ServerGameplayValidation.WithinPickupRange(0, 0, 0, -1), "invalid pickup limit rejected");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, 3, 100, 0, 2) == null, "current sidearm semantics valid");
        Check(ServerGameplayValidation.WeaponConfigurationError(10, 8, 8, 1, 3, 100, 4, 1.3) == null, "current shotgun semantics valid");
        Check(ServerGameplayValidation.WeaponConfigurationError(1, 200, 1, 100, .25, 200, 0, 1) == null, "future high cadence profile remains valid");
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0d, -1d })
        {
            Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, value, 3, 100, 0, 2) != null, "invalid cadence " + value);
            Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, value, 100, 0, 2) != null, "invalid reload " + value);
            Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, 3, value, 0, 2) != null, "invalid range " + value);
        }
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, -1d, 91d })
            Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, 3, 100, value, 2) != null, "invalid spread " + value);
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, 0d, .9d, 1001d })
            Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, 3, 100, 0, value) != null, "invalid zoom " + value);
        Check(ServerGameplayValidation.WeaponConfigurationError(0, 30, 1, 5, 3, 100, 0, 2) != null, "nonpositive damage");
        Check(ServerGameplayValidation.WeaponConfigurationError(int.MaxValue, 30, 1, 5, 3, 100, 0, 2) != null, "damage boost overflow prevented");
        Check(ServerGameplayValidation.WeaponConfigurationError(int.MaxValue / 2, 30, 1, 5, 3, 100, 0, 2) == null, "safe boosted damage boundary");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 0, 1, 5, 3, 100, 0, 2) != null, "empty magazine invalid");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 1000001, 1, 5, 3, 100, 0, 2) != null, "magazine safety bound");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 0, 5, 3, 100, 0, 2) != null, "zero pellets invalid");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 17, 5, 3, 100, 0, 2) != null, "pellets no longer silently clamped");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 1001, 3, 100, 0, 2) != null, "processing cadence bound");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, 3601, 100, 0, 2) != null, "reload safety bound");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, 3, 100001, 0, 2) != null, "range safety bound");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, .05, 3, 100, 0, 2) != null, "rate preserves prior 0.1 minimum semantics");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, .05, 100, 0, 2) != null, "reload preserves prior 0.1 minimum semantics");
        Check(ServerGameplayValidation.WeaponConfigurationError(40, 30, 1, 5, 3, .05, 0, 2) != null, "range preserves prior 0.1 minimum semantics");
        Check(ServerGameplayValidation.ValidAimConfiguration(45, 89), "current aim envelope valid");
        Check(ServerGameplayValidation.ValidAimConfiguration(180, 89.9f), "Inspector float pitch boundary valid");
        Check(!ServerGameplayValidation.ValidAimConfiguration(double.NaN, 89), "nonfinite yaw invalid");
        Check(!ServerGameplayValidation.ValidAimConfiguration(45, double.PositiveInfinity), "nonfinite pitch invalid");
        Check(!ServerGameplayValidation.ValidAimConfiguration(181, 89) && !ServerGameplayValidation.ValidAimConfiguration(45, 90), "aim bounds enforced");
        var ready = new ServerGameplayValidation.RequestBudget(2, 4);
        var ping = new ServerGameplayValidation.RequestBudget(2, 4);
        Check(Consume(ready, 0, 32) == 4, "ready churn bounded before lobby mutation");
        Check(Consume(ping, 0, 32) == 4, "ping independently bounded");
        Check(Consume(ready, .5, 32) == 1 && Consume(ping, .5, 32) == 1, "legitimate retry refills both lobby budgets");
        Console.WriteLine("Passed " + _passed + " server gameplay validation checks.");
    }
}
