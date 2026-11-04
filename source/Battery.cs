using System;
using System.Diagnostics;

internal sealed class BatteryReading
{
    public int Percent;
    public bool Charging;
    public DateTime Time;
    public string Receiver;
}

internal sealed class BatteryReader
{
    HidDevice cached;
    public void Invalidate() { cached=null; }
    public BatteryReading Poll()
    {
        if (cached == null) {
            foreach (var device in Hid.Enumerate())
                if (device.Vid == 0x3434 && device.Pid == 0xD030 && device.Page == 0x8C && device.Usage == 1)
                    { cached=device; break; }
        }
        if (cached == null) throw new InvalidOperationException("M6-Empfänger nicht gefunden. Unterstützt: Keychron Link Type A (D030).");
        try {
            using (var connection=new HidConnection(cached)) {
                return Query(connection,cached.Product);
            }
        } catch { cached=null; throw; }
    }
    internal static BatteryReading Query(IBatteryConnection connection,string receiver)
    {
        Exception lastError=null;
        for(int attempt=0;attempt<2;attempt++) {
            try {
                // Drain only the vendor status endpoint, never an input-device collection.
                for(int n=0;n<8;n++) if(connection.Read(5)==null) break;
                var request=new byte[21]; request[0]=0x51; request[1]=0x06;
                connection.SetFeature(request);
                bool ready=false;
                var elapsed=Stopwatch.StartNew();
                while(elapsed.ElapsedMilliseconds<1500) {
                    var packet=connection.Read((int)Math.Max(1,1500-elapsed.ElapsedMilliseconds));
                    if(packet==null) break;
                    if(packet.Length>=3 && packet[0]==0x54 && packet[1]==0xE4 && packet[2]==1) { ready=true; break; }
                }
                // A feature reply can be cached in the receiver. Never accept it without a fresh ready ACK.
                if(!ready) throw new TimeoutException("Maus schläft, ist aus oder antwortet nicht. Kein neuer Akkustand.");
                return Parse(connection.GetFeature(0x51,21),receiver);
            } catch(TimeoutException ex) { lastError=ex; }
            catch(System.ComponentModel.Win32Exception ex) {
                if(ex.NativeErrorCode!=31 && ex.NativeErrorCode!=121) throw;
                lastError=ex;
            }
        }
        throw lastError;
    }
    internal static BatteryReading Parse(byte[] reply,string receiver)
    {
        if(reply==null || reply.Length!=21 || reply[0]!=0x51 || reply[1]!=6)
            throw new InvalidOperationException("Unpassende Statusantwort verworfen.");
        int vid=reply[4]|reply[5]<<8, pid=reply[6]|reply[7]<<8;
        if(vid!=0x3434 || pid!=0xD03F)
            throw new InvalidOperationException(String.Format("Empfänger meldet {0:X4}:{1:X4}, erwartet wird die M6 D03F.",vid,pid));
        if(reply[11]>100 || (reply[12]&0xFC)!=0)
            throw new InvalidOperationException("Ungültige Akkumeldung verworfen.");
        return new BatteryReading { Percent=reply[11],Charging=(reply[12]&3)!=0,Time=DateTime.Now,Receiver=receiver.Trim() };
    }
}

internal sealed class WarningPolicy
{
    internal static readonly int[] Thresholds={30,20,10,5};
    public int Mask;
    public WarningPolicy(int mask) { Mask=mask&15; }
    public int? Evaluate(int percent,bool charging)
    {
        if(percent<0 || percent>100) throw new ArgumentOutOfRangeException("percent");
        for(int i=0;i<Thresholds.Length;i++) if(percent>=Thresholds[i]+5) Mask&=~(1<<i);
        if(charging) return null;
        int? warning=null;
        for(int i=0;i<Thresholds.Length;i++)
            if(percent<=Thresholds[i] && (Mask&(1<<i))==0) warning=Thresholds[i];
        return warning;
    }
    public void MarkDelivered(int percent)
    {
        for(int i=0;i<Thresholds.Length;i++) if(percent<=Thresholds[i]) Mask|=1<<i;
    }
}
