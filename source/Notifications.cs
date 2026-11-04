using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

internal sealed class Notifications
{
    const string AppId="M6Akku.Desktop";
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    public string LastError;
    ToastNotifier notifier;
    public void Initialize()
    {
        // Windows 10/11 unpackaged desktop-toast identity, scoped to this user.
        using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\"+AppId)) {
            key.SetValue("DisplayName","M6 Akkuwächter");
            key.SetValue("ShowInSettings",1,RegistryValueKind.DWord);
        }
        Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppId));
        ShellIdentity.Ensure(AppId);
        notifier=ToastNotificationManager.CreateToastNotifier(AppId);
    }
    public bool Show(string title,string message,bool test)
    {
        try {
            if(notifier==null) Initialize();
            var xml=new XmlDocument();
            xml.LoadXml("<toast duration='long'><visual><binding template='ToastGeneric'><text>"+SecurityElement.Escape(title)+"</text><text>"+SecurityElement.Escape(message)+"</text></binding></visual><audio silent='true'/></toast>");
            var toast=new ToastNotification(xml);
            toast.Tag=test?"test":"battery"; toast.Group="M6Akku";
            toast.ExpirationTime=DateTimeOffset.Now.AddMinutes(10);
            toast.Failed+=(sender,args)=> { LastError="Windows konnte die Mitteilung nicht anzeigen: "+args.ErrorCode.Message; };
            notifier.Show(toast); LastError=null; return true;
        } catch(Exception ex) { LastError=ex.ToString(); return false; }
    }
}
