using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class TrayView : IDisposable
{
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem battery,time,check;
    Icon icon;
    string iconKey;
    bool disposed;
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    public TrayView(Action openSettings,Action poll,Action stop,Action refresh)
    {
        menu=new ContextMenuStrip();
        battery=new ToolStripMenuItem("Noch kein Akkustand") { Enabled=false };
        time=new ToolStripMenuItem("Noch keine Messung") { Enabled=false };
        check=new ToolStripMenuItem("Jetzt prüfen",null,(sender,args)=>poll());
        menu.Items.AddRange(new ToolStripItem[] { battery,time,new ToolStripSeparator(),check,
            new ToolStripMenuItem("Einstellungen",null,(sender,args)=>openSettings()),
            new ToolStripSeparator(),new ToolStripMenuItem("Beenden",null,(sender,args)=>stop()) });
        menu.Opening+=(sender,args)=>refresh();
        tray=new NotifyIcon { ContextMenuStrip=menu,Text="M6 Akkuwächter – startet" };
        tray.DoubleClick+=(sender,args)=>openSettings();
        tray.MouseMove+=(sender,args)=>refresh();
        Update(null,false,false,1);
        tray.Visible=true;
    }
    public void Update(BatteryReading reading,bool current,bool busy,int intervalMinutes)
    {
        if(reading!=null && DateTime.Now-reading.Time>TimeSpan.FromMinutes(intervalMinutes).Add(TimeSpan.FromSeconds(5))) current=false;
        string suffix=current?"":" · älterer Wert";
        battery.Text=reading==null?"Noch kein Akkustand":"Zuletzt: "+reading.Percent+" %"+(reading.Charging?" · lädt":"")+suffix;
        time.Text=reading==null?"Prüfintervall: "+intervalMinutes+" Min.":"Gemessen: "+reading.Time.ToString("dd.MM.yyyy HH:mm:ss");
        string tip=reading==null?"M6: noch kein Messwert":"M6: zuletzt "+reading.Percent+" %"+(reading.Charging?" (lädt)":"")+"\n"+reading.Time.ToString("dd.MM. HH:mm")+suffix;
        if(tray.Text!=tip) tray.Text=tip;
        check.Enabled=!busy;
        string key=reading==null || !current?"unknown":reading.Charging?"charging":reading.Percent<=10?"low":reading.Percent<=30?"medium":"normal";
        if(key!=iconKey) {
            Color color=key=="unknown"?Color.Gray:key=="charging"?Color.DeepSkyBlue:key=="low"?Color.OrangeRed:key=="medium"?Color.Goldenrod:Color.MediumSeaGreen;
            var next=CreateIcon(color); tray.Icon=next;
            if(icon!=null) icon.Dispose(); icon=next; iconKey=key;
        }
    }
    static Icon CreateIcon(Color color)
    {
        using(var bitmap=new Bitmap(32,32)) {
            using(var graphics=Graphics.FromImage(bitmap)) {
                graphics.SmoothingMode=SmoothingMode.AntiAlias;
                using(var pen=new Pen(Color.DimGray,2)) graphics.DrawRectangle(pen,3,8,23,16);
                using(var brush=new SolidBrush(Color.DimGray)) graphics.FillRectangle(brush,27,12,3,8);
                using(var brush=new SolidBrush(color)) graphics.FillRectangle(brush,6,11,17,10);
            }
            IntPtr handle=bitmap.GetHicon();
            try { using(var temporary=Icon.FromHandle(handle)) return (Icon)temporary.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }
    public void Dispose()
    {
        if(disposed) return; disposed=true;
        tray.Visible=false; tray.Dispose(); menu.Dispose();
        if(icon!=null) { icon.Dispose(); icon=null; }
    }
}
