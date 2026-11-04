using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Win32;

[Serializable] public sealed class SavedState
{
    public int WarningMask;
    public bool Initialized;
    public int PollIntervalMinutes = 1;
}

internal static class Storage
{
    public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"M6Akku");
    static string FileName { get { return Path.Combine(Folder,"state.xml"); } }
    public static SavedState Load()
    {
        if(!File.Exists(FileName)) return new SavedState();
        try {
            using(var stream=File.OpenRead(FileName)) {
                var state=(SavedState)new XmlSerializer(typeof(SavedState)).Deserialize(stream);
                if(state.PollIntervalMinutes<1 || state.PollIntervalMinutes>1440) state.PollIntervalMinutes=1;
                return state;
            }
        }
        catch(InvalidOperationException) { return new SavedState(); }
        catch(IOException) { return new SavedState(); }
    }
    public static void Save(SavedState value)
    {
        Directory.CreateDirectory(Folder);
        string temp=FileName+".tmp";
        using(var stream=File.Create(temp)) new XmlSerializer(typeof(SavedState)).Serialize(stream,value);
        if(File.Exists(FileName)) File.Replace(temp,FileName,null); else File.Move(temp,FileName);
    }
    public static bool AutostartEnabled {
        get {
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key!=null && String.Equals(key.GetValue("M6Akku") as string,AutostartCommand,StringComparison.OrdinalIgnoreCase);
        }
    }
    static string AutostartCommand { get { return "\""+System.Windows.Forms.Application.ExecutablePath+"\" --background"; } }
    public static void SetAutostart(bool enable)
    {
        using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
            if(enable) key.SetValue("M6Akku",AutostartCommand); else key.DeleteValue("M6Akku",false);
        }
    }
}
