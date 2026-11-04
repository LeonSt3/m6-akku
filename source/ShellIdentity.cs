using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;

internal static class ShellIdentity
{
    [ComImport,Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }
    [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellLink
    {
        void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int max,IntPtr data,uint flags);
        void GetIDList(out IntPtr id); void SetIDList(IntPtr id);
        void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder text,int max);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder text,int max);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder text,int max);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short key); void SetHotkey(short key);
        void GetShowCmd(out int cmd); void SetShowCmd(int cmd);
        void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int max,out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path,int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path,uint reserved);
        void Resolve(IntPtr window,uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)] struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit,Size=16)] struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Value; }
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPropertyStore
    {
        void GetCount(out uint count); void GetAt(uint index,out PropertyKey key); void GetValue(ref PropertyKey key,out PropVariant value);
        void SetValue(ref PropertyKey key,ref PropVariant value); void Commit();
    }
    public static void Ensure(string appId)
    {
        string exe=System.Windows.Forms.Application.ExecutablePath;
        string shortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"M6 Akkuwächter.lnk");
        using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\"+appId)) {
            if(File.Exists(shortcut) && String.Equals(key.GetValue("Executable") as string,exe,StringComparison.OrdinalIgnoreCase)) return;
            object shell=new ShellLink();
            try {
                var link=(IShellLink)shell;
                link.SetPath(exe); link.SetWorkingDirectory(Path.GetDirectoryName(exe));
                link.SetDescription("Keychron M6 Akkuwarnungen und Einstellungen"); link.SetIconLocation(exe,0); link.SetShowCmd(1);
                var propKey=new PropertyKey { Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=5 };
                var value=new PropVariant { Type=31,Value=Marshal.StringToCoTaskMemUni(appId) };
                try { var store=(IPropertyStore)shell; store.SetValue(ref propKey,ref value); store.Commit(); }
                finally { Marshal.FreeCoTaskMem(value.Value); }
                ((IPersistFile)shell).Save(shortcut,true);
                key.SetValue("Executable",exe);
            } finally { Marshal.FinalReleaseComObject(shell); }
        }
    }
}
