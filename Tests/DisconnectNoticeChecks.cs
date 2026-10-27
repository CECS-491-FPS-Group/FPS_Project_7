using System;

internal static class DisconnectNoticeChecks
{
    private static int _passed;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        _passed++;
    }
    public static void Main()
    {
        var state = new DisconnectNoticeState();
        Check(state.Message == null && !state.Recovering, "initial quiet state");
        Check(!state.BeginRecovery(false), "failed join is not established loss");
        state.ReceiveReason(DisconnectNoticeState.Integrity);
        state.Authenticate();
        Check(state.BeginRecovery(false), "established loss recovers");
        Check(state.Message == "Connection to the server was lost.", "unauthenticated reason ignored");
        Check(!state.BeginRecovery(false), "duplicate stop suppressed");
        state.ReceiveReason(DisconnectNoticeState.Movement);
        Check(state.Message == "Connection to the server was lost.", "late reason ignored");
        state.CompleteRecovery();
        Check(state.Message != null, "reason survives local scene recovery");
        state.Dismiss();
        Check(state.Message == null, "dismiss clears reason");
        state.BeginAttempt(); state.Authenticate(); state.ReceiveReason(DisconnectNoticeState.Integrity);
        Check(state.BeginRecovery(false), "new connection can recover");
        Check(state.Message == "Disconnected: game file integrity check failed. Restore the original game files and try again.", "static exact message");
        state.CompleteRecovery(); state.BeginAttempt();
        Check(state.Message == null && !state.Connected, "new attempt clears old message");
        state.Authenticate(); state.ReceiveReason(255); state.BeginRecovery(false);
        Check(state.Message == "Connection to the server was lost.", "unknown reason falls back");
        state.CompleteRecovery(); state.BeginAttempt(); state.Authenticate();
        state.ReceiveReason(DisconnectNoticeState.Movement); state.BeginRecovery(false);
        Check(state.Message == "Disconnected: movement exceeded the server's allowed limits.", "dynamic exact message");
        state.CompleteRecovery(); state.BeginAttempt(); state.Authenticate();
        state.ReceiveReason(DisconnectNoticeState.Integrity);
        Check(state.BeginRecovery(true), "intentional return recovers once");
        Check(state.Message == null, "intentional return never displays kick");
        Check(!state.BeginRecovery(false), "intentional callback cannot create second recovery");
        state.CompleteRecovery();
        Check(state.BeginRecovery(true), "offline intentional return allowed");
        Check(state.Message == null, "offline return quiet");
        Console.WriteLine("Passed " + _passed + " disconnect notice checks.");
    }
}
