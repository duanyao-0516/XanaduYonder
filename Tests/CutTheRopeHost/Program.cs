using System;
using XanaduYonder.Runtime.CutTheRope;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void Main()
    {
        var native = new Bridge();
        var session = new CutTheRopeSession(native);
        int returns = 0;
        string returnError = null;
        session.Returned += error => { returns++; returnError = error; };
        Check(session.TryOpen(false) && session.IsBusy && !native.SoundEnabled,
            "Accepted launch immediately owns input and passes the saved sound preference");
        Check(!session.TryOpen(true) && native.Opens == 1,
            "Duplicate input cannot call Android open twice");
        session.Tick(1, true, false);
        Check(session.IsBusy && returns == 0, "Queued or running Activity retains ownership");
        native.Running = false;
        session.Tick(2, false, false);
        session.Tick(20, false, false);
        Check(session.IsBusy && returns == 0, "Finishing while backgrounded cannot restore hall input");
        session.Tick(21, true, false);
        Check(session.IsBusy, "First resumed frame cannot pass its Back through to the hall");
        session.Tick(22, true, true);
        session.Tick(22.1, true, false);
        Check(session.IsBusy, "Return Back/touch must be released and settle before input resumes");
        session.Tick(22.4, true, false);
        Check(!session.IsBusy && returns == 1 && returnError == null,
            "Clean foreground restores input and emits one successful return");
        session.Tick(23, true, false);
        Check(returns == 1, "A completed session cannot emit duplicate returns");

        native.Reject = true; native.Error = "Activity unavailable";
        Check(!session.TryOpen(true) && !session.IsBusy && session.LastError == native.Error,
            "Synchronous rejection releases ownership and retains native diagnostics");
        native.Reject = false; native.ThrowOpen = true;
        Check(!session.TryOpen(true) && !session.IsBusy && session.LastError.Contains("JNI open"),
            "JNI launch exceptions release input for the host failure dialog");
        native.ThrowOpen = false; native.Error = null;
        Check(session.TryOpen(true) && native.SoundEnabled, "A failed launch does not prevent a later launch");
        native.Running = false; native.Error = "WebView renderer failed";
        session.Tick(30, true, false);
        session.Tick(31, true, false);
        Check(!session.IsBusy && returns == 2 && returnError == native.Error,
            "Asynchronous native failure returns once with its diagnostic");

        Check(session.TryOpen(true), "Another session starts after asynchronous failure");
        native.ThrowStatus = true;
        session.Tick(40, true, false);
        session.Tick(41, true, false);
        Check(!session.IsBusy && returns == 3 && returnError.Contains("JNI status"),
            "Status bridge failure cannot leave hall input permanently disabled");
        native.ThrowStatus = false;
        Check(session.TryOpen(true), "Session can reopen after a status exception");
        native.Running = false;
        session.Tick(50, true, false);
        session.Tick(51, false, false);
        session.Tick(51.1, true, false);
        Check(session.IsBusy, "A new focus loss restarts the pending return input drain");
        session.Tick(51.4, true, false);
        Check(!session.IsBusy && returns == 4, "Return proceeds after renewed foreground stability");

        Check(session.TryOpen(true), "Lifecycle disposal covers an active session");
        session.Dispose();
        native.Running = false;
        session.Tick(60, true, false);
        Check(!session.IsBusy && returns == 4 && !session.TryOpen(true),
            "Disposal releases ownership and prevents stale callbacks or reopening");
        session.Dispose();
        Console.WriteLine("Cut the Rope host checks: " + checks + " passed");
    }

    // Android JNI is the only substituted boundary; all lifecycle transitions are production code.
    private sealed class Bridge : ICutTheRopeBridge
    {
        public int Opens;
        public bool SoundEnabled, Running, Reject, ThrowOpen, ThrowStatus;
        public string Error;
        public bool Open(bool soundEnabled)
        {
            Opens++; SoundEnabled = soundEnabled;
            if (ThrowOpen) throw new InvalidOperationException("JNI open failed");
            Running = !Reject;
            return !Reject;
        }
        public bool IsRunning()
        {
            if (ThrowStatus) throw new InvalidOperationException("JNI status failed");
            return Running;
        }
        public string GetLastError() { return Error; }
    }
}
