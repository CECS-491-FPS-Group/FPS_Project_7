using System;

// Pure connection-local report bookkeeping. Receipt is not verification/admission.
internal sealed class IntegrityReportLifecycle
{
    internal readonly double Deadline;
    private readonly double _interval;
    private double _next;
    private bool _received;
    private uint _sequence;
    internal bool Late { get; private set; }
    internal string Result { get; private set; } = "Pending";
    internal bool Verified => Result == "Verified";
    internal IntegrityReportLifecycle(double now, double deadline = 60, double interval = 15)
    {
        if (!ServerGameplayValidation.Finite(now) || now < 0 || !ServerGameplayValidation.Finite(deadline) || deadline <= 0 ||
            !ServerGameplayValidation.Finite(interval) || interval <= 0) throw new ArgumentOutOfRangeException(nameof(now));
        Deadline = now + deadline; _interval = interval;
    }
    internal bool Expire(double now, bool configured)
    {
        if (_received || Late || !ServerGameplayValidation.Finite(now) || now < Deadline) return false;
        Late = true;
        Result = configured ? "MissingReport (not verified)" : "ServerConfigurationError; MissingReport (not verified)";
        return true;
    }
    internal bool TryAccept(uint sequence, double now, bool allowRechecks, out string rejection)
    {
        rejection = null;
        if (!ServerGameplayValidation.Finite(now) || now < 0 ||
            (!_received && sequence != 0) || (_received && (sequence <= _sequence || now < _next)))
            rejection = "InvalidReport frequency/sequence";
        else if (!allowRechecks && sequence != 0) rejection = "InvalidReport recheck unavailable in release server";
        if (rejection != null) return false;
        Late |= !_received && now >= Deadline;
        _received = true; _sequence = sequence; _next = now + _interval;
        return true;
    }
    internal void Complete(string result)
    {
        Result = Late ? result == "Verified" ? "LateReport (matching hashes; not verified)" : "LateReport result=" + result : result;
    }
}
