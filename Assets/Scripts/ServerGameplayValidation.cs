using System;

// Small pure admission helpers; no verdicts, enforcement, networking or Unity state.
internal static class ServerGameplayValidation
{
    // Generous implementation safety bounds, not weapon IDs or detector thresholds.
    internal const int MaximumWeaponDefinitions = 64;
    internal static string WeaponConfigurationError(int damage, int magazine, int pellets,
        double rate, double reload, double range, double spread, double zoom)
    {
        if (damage < 1 || damage > int.MaxValue / 2) return "damage must fit a positive integer including the 2x Damage boost";
        if (magazine < 1 || magazine > 1000000) return "magazine must be 1..1000000";
        if (pellets < 1 || pellets > 16) return "pellets must be 1..16 (ray work bound)";
        if (!Finite(rate) || rate < .1 || rate > 1000) return "rate must be finite, 0.1..1000 shots/s";
        if (!Finite(reload) || reload < .1 || reload > 3600) return "reload must be finite, 0.1..3600 seconds";
        if (!Finite(range) || range < .1 || range > 100000) return "range must be finite, 0.1..100000 metres";
        if (!Finite(spread) || spread < 0 || spread > 90) return "spread must be finite, 0..90 degrees";
        if (!Finite(zoom) || zoom < 1 || zoom > 1000) return "zoom must be finite, 1..1000";
        return null;
    }
    internal static bool ValidAimConfiguration(double yaw, double pitch) =>
        Finite(yaw) && yaw >= 0 && yaw <= 180 && Finite(pitch) && pitch >= 0 && pitch <= 89.9f;

    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    internal static bool WithinPickupRange(double dx, double dy, double dz, double radius)
    {
        if (!Finite(dx) || !Finite(dy) || !Finite(dz) || !Finite(radius) || radius < 0d) return false;
        double squared = dx * dx + dy * dy + dz * dz;
        double limit = radius * radius;
        return Finite(squared) && Finite(limit) && squared <= limit;
    }

    // Generous processing allowance, separate from the authoritative shot cooldown.
    // Uses the server's fastest configured weapon, so 60 is a floor, not a weapon ceiling.
    internal static double CombatRequestRate(double fastestServerWeaponRate)
        => Finite(fastestServerWeaponRate) && fastestServerWeaponRate > 0d
            ? Math.Max(60d, fastestServerWeaponRate * 2d + 8d) : 60d;

    internal sealed class RequestBudget
    {
        private readonly double _rate, _capacity;
        private double _tokens, _last, _nextLog;
        private bool _started;

        internal RequestBudget(double rate, double capacity)
        {
            if (!Finite(rate) || rate <= 0d || !Finite(capacity) || capacity < 1d)
                throw new ArgumentOutOfRangeException(nameof(rate));
            _rate = rate; _capacity = capacity; _tokens = capacity;
        }

        // Caller supplies monotonic SERVER seconds and uses one bucket per connection reference.
        // Rejection is ordinary admission control, never evidence for automatic punishment.
        internal bool TryConsume(double now)
        {
            if (!Finite(now) || now < 0d) return false;
            if (!_started) { _last = now; _started = true; }
            if (now < _last) return false; // A bad clock value cannot refill the bucket.
            _tokens = Math.Min(_capacity, _tokens + (now - _last) * _rate);
            _last = now;
            if (_tokens < 1d) return false;
            _tokens -= 1d;
            return true;
        }

        internal bool PermitRejectionLog(double now)
        {
            if (!Finite(now) || now < _nextLog) return false;
            _nextLog = now + 5d;
            return true;
        }
    }
}
