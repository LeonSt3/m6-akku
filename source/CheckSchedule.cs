using System;

internal enum CheckDecision { Wait, Paused, Poll }

// Monotonic milliseconds; wall-clock changes do not move scheduled checks.
internal sealed class CheckSchedule
{
    long intervalMilliseconds;
    public long NextDueMilliseconds { get; private set; }
    public bool Paused { get; private set; }
    public CheckSchedule(int minutes,long now) { SetInterval(minutes,now); NextDueMilliseconds=now+1000; }
    public void SetInterval(int minutes,long now)
    {
        if(minutes<1 || minutes>1440) throw new ArgumentOutOfRangeException("minutes");
        intervalMilliseconds=(long)minutes*60000;
        NextDueMilliseconds=now+intervalMilliseconds;
    }
    public CheckDecision Evaluate(long now,bool idle)
    {
        if(idle) { Paused=true; return CheckDecision.Paused; }
        if(Paused) { Paused=false; NextDueMilliseconds=now; }
        if(now<NextDueMilliseconds) return CheckDecision.Wait;
        return CheckDecision.Poll;
    }
    public void Completed(long now,bool success)
    {
        Paused=false;
        NextDueMilliseconds=now+(success?intervalMilliseconds:Math.Min(intervalMilliseconds,60000));
    }
    public void RefreshSoon(long now) { NextDueMilliseconds=Math.Min(NextDueMilliseconds,now+2000); }
    public int TimerDelay(long now)
    {
        // The same timer cheaply notices resumed input; this never queries USB on its own.
        return Paused?30000:(int)Math.Max(100,Math.Min(30000,NextDueMilliseconds-now));
    }
    public static bool IsReceiverEvent(string path)
    {
        return path!=null && path.IndexOf("vid_3434&pid_d030",StringComparison.OrdinalIgnoreCase)>=0;
    }
}
