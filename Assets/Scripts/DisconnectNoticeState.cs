// Local presentation state. Reason codes are sent only by the server, never free text.
internal sealed class DisconnectNoticeState
{
    internal const byte Integrity = 1, Movement = 2;
    internal bool Connected { get; private set; }
    internal bool Recovering { get; private set; }
    private byte _reason;
    private bool _show;
    internal string Message => !_show ? null : _reason == Integrity
        ? "Disconnected: game file integrity check failed. Restore the original game files and try again. You CHEATER!"
        : _reason == Movement ? "Disconnected: movement exceeded the server's allowed limits."
        : "Connection to the server was lost. You CHEATER!";

    internal void BeginAttempt() { Connected = false; Dismiss(); }
    internal void Authenticate() { Connected = true; }
    internal void ReceiveReason(byte reason)
    {
        if (Connected && !Recovering && (reason == Integrity || reason == Movement)) _reason = reason;
    }
    internal bool BeginRecovery(bool intentional)
    {
        if (Recovering || (!Connected && !intentional)) return false;
        Recovering = true;
        Connected = false;
        _show = !intentional;
        if (intentional) _reason = 0;
        return true;
    }
    internal void CompleteRecovery() { Recovering = false; }
    internal void Dismiss() { _show = false; _reason = 0; }
}
