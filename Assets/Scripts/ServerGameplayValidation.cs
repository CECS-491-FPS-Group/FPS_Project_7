using System;

// Small pure admission helpers; no verdicts, enforcement, networking or Unity state.
internal static class ServerGameplayValidation
{
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
