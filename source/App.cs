using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    internal static readonly string WindowName="M6Akku.Control."+WindowsIdentity.GetCurrent().User.Value;
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string className,string title);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int msg,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)] internal struct CopyData { public IntPtr Tag; public int Size; public IntPtr Data; }
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr window,int msg,IntPtr w,ref CopyData data,uint flags,uint timeout,out IntPtr result);
    [STAThread] static int Main(string[] args)
    {
        try {
            if(args.Length>1 && args[0]=="--status") {
                IntPtr target=FindWindow(null,WindowName);
                if(target==IntPtr.Zero) throw new InvalidOperationException("Der Akkuwächter läuft nicht.");
                string path=Path.GetFullPath(args[1]); IntPtr data=Marshal.StringToHGlobalUni(path);
                try {
                    var packet=new CopyData { Tag=new IntPtr(0x4D36),Size=(path.Length+1)*2,Data=data }; IntPtr result;
                    if(SendMessageTimeout(target,0x4A,IntPtr.Zero,ref packet,2,2000,out result)==IntPtr.Zero || result==IntPtr.Zero)
                        throw new InvalidOperationException("Statusdiagnose konnte nicht geschrieben werden.");
                } finally { Marshal.FreeHGlobal(data); }
                return 0;
            }
            if(args.Length>0 && args[0]=="--self-test") { File.WriteAllText(args[1],Tests.Run()); return 0; }
            if(args.Length>0 && args[0]=="--diagnose") {
                var report=new System.Text.StringBuilder();
                foreach(var device in Hid.Enumerate()) report.AppendLine(device.ToString());
                try { var reading=new BatteryReader().Poll(); report.AppendLine(String.Format("M6: {0}% | Charging={1} | {2:O}",reading.Percent,reading.Charging,reading.Time)); }
                catch(Exception ex) { report.AppendLine("Abfrage: "+ex.Message); }
                File.WriteAllText(args[1],report.ToString()); return 0;
            }
            if(args.Length>0 && args[0]=="--toast-test") {
                var notification=new Notifications();
                bool ok=notification.Show("M6 Akkuwächter – Test","Die Windows-Benachrichtigung funktioniert. Warnungen erfolgen bei 30, 20, 10 und 5 Prozent.",true);
                File.WriteAllText(args[1],ok?"Windows hat die Testmitteilung angenommen.":notification.LastError); return ok?0:1;
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool created;
            using(var mutex=new Mutex(true,"Local\\"+WindowName,out created)) {
                if(!created) {
                    if(args.Length>0 && args[0]=="--background") return 0;
                    IntPtr window=IntPtr.Zero;
                    for(int i=0;i<20 && window==IntPtr.Zero;i++) { window=FindWindow(null,WindowName); if(window==IntPtr.Zero) Thread.Sleep(100); }
                    if(window!=IntPtr.Zero) PostMessage(window,args.Length>0 && args[0]=="--stop"?0x8002:0x8001,IntPtr.Zero,IntPtr.Zero);
                    return 0;
                }
                if(args.Length>0 && args[0]=="--stop") return 0;
                using(var context=new MonitorContext(args.Length>0 && args[0]=="--background")) Application.Run(context);
            }
            return 0;
        } catch(Exception ex) {
            if(args.Length>1 && args[0].StartsWith("--")) { File.WriteAllText(args[1],ex.ToString()); return 1; }
            MessageBox.Show(ex.Message,"M6 Akkuwächter",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1;
        }
    }
}

internal sealed class MonitorContext : ApplicationContext
{
    readonly ControllerWindow controller;
    readonly System.Windows.Forms.Timer timer;
    readonly BatteryReader reader=new BatteryReader();
    readonly Notifications notifications=new Notifications();
    readonly SavedState saved;
    readonly WarningPolicy warnings;
    readonly TrayView tray;
    readonly CheckSchedule schedule;
    readonly Stopwatch clock=Stopwatch.StartNew();
    SettingsWindow settings;
    bool busy,closing,suspended,invalidate,readingValid;
    public BatteryReading Reading;
    public string Status="Überwachung startet …", Error;
    public DateTime LastAttempt;
    int attempts,successes;

    [StructLayout(LayoutKind.Sequential)] struct LastInput { public uint Size,Time; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput input);
    static bool Idle {
        get { var input=new LastInput(); input.Size=(uint)Marshal.SizeOf(input); return GetLastInputInfo(ref input) && unchecked((uint)Environment.TickCount-input.Time)>=300000; }
    }
    public MonitorContext(bool background)
    {
        saved=Storage.Load(); warnings=new WarningPolicy(saved.WarningMask);
        schedule=new CheckSchedule(saved.PollIntervalMinutes,clock.ElapsedMilliseconds);
        controller=new ControllerWindow(this); IntPtr unused=controller.Handle;
        try {
            if(!saved.Initialized) { Storage.SetAutostart(true); saved.Initialized=true; Storage.Save(saved); }
            notifications.Initialize();
        } catch(Exception ex) { Error="Einrichtung: "+ex.Message; }
        tray=new TrayView(ShowSettings,()=>Poll(true),Stop,RefreshTray);
        timer=new System.Windows.Forms.Timer();
        timer.Tick+=(sender,args)=>OnTimer();
        ArmTimer();
        if(!background) ShowSettings();
    }
    public void ShowSettings()
    {
        if(closing) return;
        if(settings==null || settings.IsDisposed) settings=new SettingsWindow(this);
        settings.Show(); if(settings.WindowState==FormWindowState.Minimized) settings.WindowState=FormWindowState.Normal;
        settings.Activate();
    }
    public void DeviceChanged() { invalidate=true; readingValid=false; RefreshSettings(); }
    public void ReceiverChanged()
    {
        DeviceChanged(); schedule.RefreshSoon(clock.ElapsedMilliseconds); ArmTimer();
    }
    void ArmTimer()
    {
        if(closing || suspended || busy) return;
        long now=clock.ElapsedMilliseconds;
        int delay=schedule.TimerDelay(now);
        // Reuse the timer to redraw at the freshness boundary, including while paused.
        if(Reading!=null && readingValid && now<=schedule.NextDueMilliseconds+5000)
            delay=(int)Math.Min(delay,Math.Max(1,schedule.NextDueMilliseconds+5001-now));
        timer.Stop(); timer.Interval=delay; timer.Start();
    }
    void OnTimer()
    {
        timer.Stop();
        if(closing || suspended || busy) return;
        var decision=schedule.Evaluate(clock.ElapsedMilliseconds,Idle);
        if(decision==CheckDecision.Poll) { Poll(false); return; }
        if(decision==CheckDecision.Paused) {
            Status="Pausiert: seit 5 Minuten keine Eingabe. Nach Nutzung Fortsetzung binnen 30 Sekunden.";
            RefreshSettings();
        }
        else RefreshTray();
        ArmTimer();
    }
    public string NextCheck {
        get {
            if(suspended) return "nach dem Aufwachen";
            if(busy) return "läuft gerade";
            if(schedule.Paused) return "binnen 30 Sekunden nach erneuter Nutzung";
            return DateTime.Now.AddMilliseconds(Math.Max(0,schedule.NextDueMilliseconds-clock.ElapsedMilliseconds)).ToString("dd.MM. HH:mm:ss");
        }
    }
    public string DiagnosticStatus()
    {
        return "Zeit: "+DateTime.Now.ToString("O")+"\r\nStatus: "+Status+"\r\nIntervall: "+PollIntervalMinutes+" Minuten"+
            "\r\nLetzter Versuch: "+(attempts==0?"keiner":LastAttempt.ToString("O"))+
            "\r\nLetzte erfolgreiche Messung: "+(Reading==null?"keine":Reading.Time.ToString("O"))+
            "\r\nAkku: "+(Reading==null?"unbekannt":Reading.Percent+" %")+
            "\r\nMesswert aktuell: "+TrayView.IsReadingCurrent(Reading,readingValid,clock.ElapsedMilliseconds,schedule.NextDueMilliseconds)+
            "\r\nNächste Prüfung: "+NextCheck+"\r\nPausiert: "+schedule.Paused+
            "\r\nBeschäftigt: "+busy+"\r\nVersuche: "+attempts+"\r\nErfolgreich: "+successes+"\r\n";
    }
    public int PollIntervalMinutes { get { return saved.PollIntervalMinutes; } }
    public void SetPollInterval(int minutes)
    {
        if(minutes<1 || minutes>1440) throw new ArgumentOutOfRangeException("minutes","Bitte 1 bis 1440 Minuten wählen.");
        if(minutes==saved.PollIntervalMinutes) return;
        int previous=saved.PollIntervalMinutes;
        saved.PollIntervalMinutes=minutes;
        try { Storage.Save(saved); } catch { saved.PollIntervalMinutes=previous; throw; }
        schedule.SetInterval(minutes,clock.ElapsedMilliseconds); ArmTimer();
        if(!busy) Status="Prüfintervall gespeichert. Nächste Prüfung in "+minutes+(minutes==1?" Minute.":" Minuten.");
        RefreshSettings();
    }
    public void PowerChanged(bool sleep)
    {
        suspended=sleep; invalidate=true;
        timer.Stop();
        RefreshSettings();
        if(!sleep) { schedule.RefreshSoon(clock.ElapsedMilliseconds); ArmTimer(); }
    }
    public async void Poll(bool manual)
    {
        if(busy || closing || suspended) return;
        if(!manual && Idle) { schedule.Evaluate(clock.ElapsedMilliseconds,true); Status="Pausiert: seit 5 Minuten keine Eingabe."; RefreshSettings(); ArmTimer(); return; }
        timer.Stop(); busy=true; attempts++; LastAttempt=DateTime.Now; Status="Akkustand wird geprüft …"; RefreshSettings();
        bool reset=invalidate; invalidate=false;
        bool successful=false;
        try {
            var result=await Task.Run(()=> { if(reset) reader.Invalidate(); return reader.Poll(); });
            if(closing) return;
            Reading=result; readingValid=!invalidate; successful=true; successes++; Status=(result.Charging?"Maus lädt. ":"Überwachung aktiv. ")+"Prüfintervall: "+PollIntervalMinutes+(PollIntervalMinutes==1?" Minute.":" Minuten.");
            int oldMask=warnings.Mask;
            int? warning=warnings.Evaluate(result.Percent,result.Charging);
            if(warning.HasValue) {
                string title=warning.Value==5?"M6: Akku sehr niedrig – bitte laden":"M6: Akku wird knapp";
                if(notifications.Show(title,"Deine Keychron M6 meldet "+result.Percent+" % Akku. Bitte laden.",false)) warnings.MarkDelivered(result.Percent);
            }
            if(oldMask!=warnings.Mask) { saved.WarningMask=warnings.Mask; Storage.Save(saved); }
        } catch(Exception ex) { readingValid=false; Status=ex.Message; }
        finally {
            busy=false;
            if(!closing) {
                schedule.Completed(clock.ElapsedMilliseconds,successful);
                if(invalidate) schedule.RefreshSoon(clock.ElapsedMilliseconds);
                RefreshSettings(); ArmTimer();
            }
        }
    }
    public void TestNotification()
    {
        if(!notifications.Show("M6 Akkuwächter – Test","Warnungen sind bei 30 %, 20 %, 10 % und 5 % eingerichtet.",true))
            MessageBox.Show(notifications.LastError,"Benachrichtigung",MessageBoxButtons.OK,MessageBoxIcon.Information);
        RefreshSettings();
    }
    public string NotificationError { get { return notifications.LastError; } }
    void RefreshTray() { if(!closing && tray!=null) tray.Update(Reading,readingValid,busy,PollIntervalMinutes,clock.ElapsedMilliseconds,schedule.NextDueMilliseconds,(schedule.Paused || suspended) && !busy); }
    void RefreshSettings() { RefreshTray(); if(settings!=null && !settings.IsDisposed) settings.UpdateStatus(); }
    public void Stop() { ExitThread(); }
    protected override void ExitThreadCore()
    {
        if(closing) return; closing=true; timer.Stop();
        tray.Dispose();
        if(settings!=null) settings.Dispose(); controller.Dispose(); base.ExitThreadCore();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) { closing=true; timer.Dispose(); tray.Dispose(); controller.Dispose(); if(settings!=null) settings.Dispose(); }
        base.Dispose(disposing);
    }
}

internal sealed class ControllerWindow : Form
{
    readonly MonitorContext context;
    IntPtr registration;
    [StructLayout(LayoutKind.Sequential)] struct DeviceFilter { public int Size,Type,Reserved; public Guid ClassGuid; public short Name; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr RegisterDeviceNotification(IntPtr recipient,ref DeviceFilter filter,int flags);
    [DllImport("user32.dll")] static extern bool UnregisterDeviceNotification(IntPtr registration);
    public ControllerWindow(MonitorContext context)
    {
        this.context=context; Text=Program.WindowName; ShowInTaskbar=false;
    }
    protected override void OnHandleCreated(EventArgs args)
    {
        base.OnHandleCreated(args);
        var filter=new DeviceFilter { Type=5,ClassGuid=new Guid("4D1E55B2-F16F-11CF-88CB-001111000030") };
        filter.Size=Marshal.SizeOf(filter); registration=RegisterDeviceNotification(Handle,ref filter,0);
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x8001) { context.ShowSettings(); return; }
        if(message.Msg==0x8002) { context.Stop(); return; }
        if(message.Msg==0x4A && message.LParam!=IntPtr.Zero) {
            var data=(Program.CopyData)Marshal.PtrToStructure(message.LParam,typeof(Program.CopyData));
            if(data.Tag==new IntPtr(0x4D36) && data.Data!=IntPtr.Zero && data.Size>0 && data.Size<=8192 && data.Size%2==0) {
                try { File.WriteAllText(Marshal.PtrToStringUni(data.Data,data.Size/2).TrimEnd('\0'),context.DiagnosticStatus()); message.Result=new IntPtr(1); }
                catch(Exception) { message.Result=IntPtr.Zero; }
                return;
            }
        }
        if(message.Msg==0x219 && (message.WParam.ToInt32()==0x8000 || message.WParam.ToInt32()==0x8004) && message.LParam!=IntPtr.Zero) {
            if(Marshal.ReadInt32(message.LParam,4)==5) {
                int size=Marshal.ReadInt32(message.LParam);
                if(size>28 && size<=32768) {
                    string path=Marshal.PtrToStringUni(IntPtr.Add(message.LParam,28),(size-28)/2).TrimEnd('\0');
                    if(CheckSchedule.IsReceiverEvent(path)) context.ReceiverChanged();
                }
            }
        }
        if(message.Msg==0x218) {
            int reason=message.WParam.ToInt32();
            if(reason==4) context.PowerChanged(true);
            if(reason==7 || reason==18) context.PowerChanged(false);
        }
        base.WndProc(ref message);
    }
    protected override void Dispose(bool disposing)
    {
        if(registration!=IntPtr.Zero) { UnregisterDeviceNotification(registration); registration=IntPtr.Zero; }
        base.Dispose(disposing);
    }
}

internal sealed class SettingsWindow : Form
{
    readonly MonitorContext context;
    readonly Label level,status,details;
    readonly System.Windows.Forms.Timer refresh;
    public SettingsWindow(MonitorContext context)
    {
        this.context=context; Text="M6 Akkuwächter"; ClientSize=new Size(540,418);
        AutoScaleMode=AutoScaleMode.Dpi; AutoScaleDimensions=new SizeF(96,96); Font=new Font("Segoe UI",10);
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
        level=new Label { Location=new Point(24,20),Size=new Size(490,50),Font=new Font("Segoe UI",23,FontStyle.Bold) };
        status=new Label { Location=new Point(24,80),Size=new Size(490,58) };
        details=new Label { Location=new Point(24,142),Size=new Size(490,65),ForeColor=SystemColors.GrayText };
        var autostart=new CheckBox { Text="Automatisch mit Windows starten",Location=new Point(24,220),AutoSize=true,Checked=Storage.AutostartEnabled };
        autostart.CheckedChanged+=(sender,args)=> {
            try { Storage.SetAutostart(autostart.Checked); }
            catch(Exception ex) { MessageBox.Show(ex.Message,"Autostart"); }
        };
        var intervalLabel=new Label { Text="Prüfintervall:",Location=new Point(24,260),AutoSize=true };
        var interval=new NumericUpDown { Location=new Point(145,256),Size=new Size(80,28),Minimum=1,Maximum=1440,Value=context.PollIntervalMinutes,AccessibleName="Prüfintervall in Minuten" };
        var minutesLabel=new Label { Text="Minuten",Location=new Point(233,260),AutoSize=true };
        var apply=new Button { Text="Übernehmen",Location=new Point(342,254),Size=new Size(170,34) };
        apply.Click+=(sender,args)=> {
            try { context.SetPollInterval((int)interval.Value); }
            catch(Exception ex) { MessageBox.Show(ex.Message,"Prüfintervall",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        };
        var check=new Button { Text="Jetzt prüfen",Location=new Point(24,308),Size=new Size(145,34) };
        check.Click+=(sender,args)=>context.Poll(true);
        var test=new Button { Text="Testmitteilung",Location=new Point(183,308),Size=new Size(145,34) };
        test.Click+=(sender,args)=>context.TestNotification();
        var stop=new Button { Text="Beenden",Location=new Point(342,308),Size=new Size(170,34) };
        stop.Click+=(sender,args)=>context.Stop();
        var hide=new Button { Text="Im Hintergrund weiter",Location=new Point(24,360),Size=new Size(488,34) };
        hide.Click+=(sender,args)=>Close();
        Controls.AddRange(new Control[] { level,status,details,autostart,intervalLabel,interval,minutesLabel,apply,check,test,stop,hide });
        refresh=new System.Windows.Forms.Timer(); refresh.Interval=2000; refresh.Tick+=(sender,args)=>UpdateStatus(); refresh.Start();
        UpdateStatus();
    }
    public void UpdateStatus()
    {
        var reading=context.Reading;
        level.Text=reading==null?"Akku: noch kein Messwert":reading.Percent+" %"+(reading.Charging?" · lädt":"");
        status.Text=context.Status;
        details.Text="Warnungen: 30 %, 20 %, 10 %, 5 % · ohne zusätzlichen Ton\r\n"+
            (reading==null?"Prüfung alle "+context.PollIntervalMinutes+" Min., Pause bei Inaktivität.":"Letzte erfolgreiche Messung: "+reading.Time.ToString("dd.MM.yyyy HH:mm:ss"))+"\r\n"+
            (context.NotificationError ?? context.Error ?? "Nächste Prüfung: "+context.NextCheck);
    }
    protected override void Dispose(bool disposing) { if(disposing) refresh.Dispose(); base.Dispose(disposing); }
}
