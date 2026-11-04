using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

internal sealed class HidDevice
{
    public string Path, Product;
    public ushort Vid, Pid, Page, Usage, InputLength, OutputLength, FeatureLength;
    public override string ToString() { return String.Format("{0} VID={1:X4} PID={2:X4} Usage={3:X4}:{4:X4} In={5} Out={6} Feature={7}", Product, Vid, Pid, Page, Usage, InputLength, OutputLength, FeatureLength); }
}

internal static class Hid
{
    [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int Size; public Guid ClassGuid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] struct Attributes { public int Size; public ushort Vid, Pid, Version; }
    [StructLayout(LayoutKind.Sequential)] struct Caps
    {
        public ushort Usage, Page, InputLength, OutputLength, FeatureLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
        public ushort LinkCollections, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
    }
    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attrs);
    [DllImport("hid.dll")] static extern bool HidD_GetProductString(SafeFileHandle handle, byte[] text, int size);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, out Caps caps);
    [DllImport("hid.dll", SetLastError=true)] internal static extern bool HidD_SetOutputReport(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("hid.dll", SetLastError=true)] internal static extern bool HidD_GetFeature(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("hid.dll", SetLastError=true)] internal static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, int flags);
    [DllImport("setupapi.dll", SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint needed, IntPtr info);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

    public static List<HidDevice> Enumerate()
    {
        var devices = new List<HidDevice>(); Guid guid; HidD_GetHidGuid(out guid);
        IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception();
        try {
            for (uint i=0;; i++) {
                var item = new InterfaceData(); item.Size = Marshal.SizeOf(item);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref item)) break;
                uint needed; SetupDiGetDeviceInterfaceDetail(set, ref item, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                if (needed < 8) continue;
                IntPtr detail = Marshal.AllocHGlobal((int)needed);
                try {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref item, detail, needed, out needed, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    if (path.IndexOf("vid_3434", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
                        if (handle.IsInvalid) continue;
                        var attr = new Attributes(); attr.Size = Marshal.SizeOf(attr);
                        if (!HidD_GetAttributes(handle, ref attr) || attr.Vid != 0x3434) continue;
                        var name = new byte[256]; HidD_GetProductString(handle, name, name.Length);
                        IntPtr parsed;
                        if (!HidD_GetPreparsedData(handle, out parsed)) continue;
                        Caps caps;
                        try { if (HidP_GetCaps(parsed, out caps) < 0) continue; } finally { HidD_FreePreparsedData(parsed); }
                        devices.Add(new HidDevice { Path=path, Product=Encoding.Unicode.GetString(name).TrimEnd('\0'), Vid=attr.Vid, Pid=attr.Pid, Page=caps.Page, Usage=caps.Usage, InputLength=caps.InputLength, OutputLength=caps.OutputLength, FeatureLength=caps.FeatureLength });
                    }
                } finally { Marshal.FreeHGlobal(detail); }
            }
        } finally { SetupDiDestroyDeviceInfoList(set); }
        return devices;
    }
}

// Only a vendor HID collection is opened; mouse/keyboard input collections are never read.
internal interface IBatteryConnection : IDisposable
{
    byte[] Read(int timeout);
    void SetFeature(byte[] buffer);
    byte[] GetFeature(byte id,int size);
}

internal sealed class HidConnection : IBatteryConnection
{
    readonly SafeFileHandle handle;
    readonly FileStream stream;
    readonly int inputLength;
    public HidConnection(HidDevice device)
    {
        inputLength = device.InputLength;
        handle = Hid.CreateFile(device.Path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception();
        stream = new FileStream(handle, FileAccess.ReadWrite, Math.Max(1, inputLength), true);
    }
    public byte[] Read(int timeout)
    {
        if (inputLength == 0) return null;
        var buffer = new byte[inputLength];
        var op = stream.BeginRead(buffer, 0, buffer.Length, null, null);
        using (var done = op.AsyncWaitHandle) {
            bool ready = done.WaitOne(timeout);
            if (!ready) Hid.CancelIoEx(handle, IntPtr.Zero);
            try { int n = stream.EndRead(op); if (!ready || n == 0) return null; Array.Resize(ref buffer, n); return buffer; }
            catch (IOException) { if (!ready) return null; throw; }
            catch (OperationCanceledException) { if (!ready) return null; throw; }
        }
    }
    public void Output(byte[] buffer)
    {
        var op = stream.BeginWrite(buffer, 0, buffer.Length, null, null);
        using (var done = op.AsyncWaitHandle) {
            bool ready = done.WaitOne(700);
            if (!ready) Hid.CancelIoEx(handle, IntPtr.Zero);
            stream.EndWrite(op);
            if (!ready) throw new TimeoutException("USB-Abfrage hat nicht geantwortet.");
        }
    }
    public void SetFeature(byte[] buffer) { if (!Hid.HidD_SetFeature(handle, buffer, buffer.Length)) throw new System.ComponentModel.Win32Exception(); }
    public byte[] GetFeature(byte id, int size) {
        var buffer = new byte[size]; buffer[0]=id;
        if (!Hid.HidD_GetFeature(handle, buffer, buffer.Length)) throw new System.ComponentModel.Win32Exception();
        return buffer;
    }
    public void Dispose() { stream.Dispose(); handle.Dispose(); }
}
