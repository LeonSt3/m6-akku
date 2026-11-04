using System;

internal static class Tests
{
    static int count;
    static void Check(bool ok,string name) { if(!ok) throw new Exception("Test fehlgeschlagen: "+name); count++; }
    public static string Run()
    {
        count=0;
        var w=new WarningPolicy(0);
        Check(w.Evaluate(31,false)==null,"über Schwelle");
        Check(w.Evaluate(30,false)==30,"30 Prozent"); w.MarkDelivered(30);
        Check(w.Evaluate(29,false)==null,"keine Wiederholung");
        Check(w.Evaluate(20,false)==20,"20 Prozent"); w.MarkDelivered(20);
        Check(w.Evaluate(10,false)==10,"10 Prozent"); w.MarkDelivered(10);
        Check(w.Evaluate(5,false)==5,"5 Prozent"); w.MarkDelivered(5);
        Check(w.Evaluate(0,false)==null,"0 Prozent nach erfolgter Warnung");
        Check(new WarningPolicy(w.Mask).Evaluate(2,false)==null,"Neustart bewahrt Warnstatus");
        w=new WarningPolicy(0); Check(w.Evaluate(2,false)==5,"Sprung direkt zu kritischem Akku"); w.MarkDelivered(2);
        Check(w.Mask==15,"übersprungene Grenzen erledigt");
        Check(w.Evaluate(9,true)==null && w.Mask==15,"Laden unter Rücksetzgrenze");
        Check(w.Evaluate(10,true)==null && w.Mask==7,"5-Prozent-Grenze wieder scharf");
        Check(w.Evaluate(5,false)==5,"erneute Warnung nach Teilaufladung"); w.MarkDelivered(5);
        w.Evaluate(100,true); Check(w.Mask==0,"alle Grenzen nach Aufladen scharf");
        Check(w.Evaluate(20,true)==null,"keine Warnung beim Laden");
        var reply=new byte[21]; reply[0]=0x51; reply[1]=6; reply[4]=0x34; reply[5]=0x34; reply[6]=0x3F; reply[7]=0xD0; reply[11]=2;
        var r=BatteryReader.Parse(reply,"Test"); Check(r.Percent==2 && !r.Charging,"echtes Antwortformat");
        reply[12]=1; Check(BatteryReader.Parse(reply,"Test").Charging,"Ladestatus");
        reply[11]=255; Reject(reply,"unbekannter Akkustand"); reply[11]=2;
        reply[6]=0x21; Reject(reply,"falsches Gerät"); reply[6]=0x3F;
        reply[1]=0x62; Reject(reply,"falscher Befehl");
        Reject(new byte[4],"zu kurze Antwort"); Reject(null,"fehlende Antwort");
        reply[1]=6;
        using(var fake=new FakeConnection(reply,new byte[] {0x54,0xE4,0},new byte[] {0x54,0xE4,1})) {
            Check(BatteryReader.Query(fake,"Test").Percent==2,"erst Pending, dann frische Bestätigung");
            Check(fake.RequestValid && fake.FeatureRead,"ausschließlich Status-Lesebefehl");
        }
        using(var fake=new FakeConnection(reply)) {
            bool timedOut=false; try { BatteryReader.Query(fake,"Test"); } catch(TimeoutException) { timedOut=true; }
            Check(timedOut && !fake.FeatureRead,"ohne Bestätigung niemals Cache lesen");
        }
        using(var fake=new FakeConnection(reply,new byte[] {0x51,0xE4,1},new byte[] {0x54,0xE2,1})) {
            bool timedOut=false; try { BatteryReader.Query(fake,"Test"); } catch(TimeoutException) { timedOut=true; }
            Check(timedOut && !fake.FeatureRead,"fremde Berichte sind keine Bestätigung");
        }
        using(var fake=new FakeConnection(reply,new byte[] {0x54,0xE4,0})) {
            bool timedOut=false; try { BatteryReader.Query(fake,"Test"); } catch(TimeoutException) { timedOut=true; }
            Check(timedOut && !fake.FeatureRead,"Pending allein reicht nicht");
        }
        using(var fake=new FakeConnection(reply)) {
            fake.RecoverOnRetry=true;
            Check(BatteryReader.Query(fake,"Test").Percent==2 && fake.Requests==2,"verlorene erste Anfrage wird mit frischer Bestätigung wiederholt");
        }
        var serializer=new System.Xml.Serialization.XmlSerializer(typeof(SavedState));
        using(var oldXml=new System.IO.StringReader("<SavedState><WarningMask>15</WarningMask><Initialized>true</Initialized></SavedState>")) {
            var legacy=(SavedState)serializer.Deserialize(oldXml);
            Check(legacy.PollIntervalMinutes==1 && legacy.WarningMask==15 && legacy.Initialized,"vorhandene Einstellungen behalten Standardintervall und Warnstatus");
        }
        using(var xml=new System.IO.StringWriter()) {
            serializer.Serialize(xml,new SavedState { PollIntervalMinutes=15,WarningMask=7,Initialized=true });
            using(var input=new System.IO.StringReader(xml.ToString())) {
                var restored=(SavedState)serializer.Deserialize(input);
                Check(restored.PollIntervalMinutes==15 && restored.WarningMask==7 && restored.Initialized,"Prüfintervall wird zusammen mit bestehendem Zustand gespeichert");
            }
        }
        var schedule=new CheckSchedule(10,0);
        Check(schedule.Evaluate(0,false)==CheckDecision.Wait,"Start wartet auf ersten Termin");
        Check(schedule.Evaluate(1000,false)==CheckDecision.Poll,"automatische Startprüfung");
        schedule.Completed(1100,true);
        Check(schedule.NextDueMilliseconds==601100,"10-Minuten-Abstand nach Erfolg");
        Check(schedule.Evaluate(31100,false)==CheckDecision.Wait,"keine USB-Abfrage durch Aktivitätsprüfung");
        Check(schedule.TimerDelay(31100)==30000,"Aktivitätsprüfung ohne aktives Warten");
        Check(schedule.Evaluate(301100,true)==CheckDecision.Paused,"Pause nach Inaktivität");
        Check(schedule.TimerDelay(601100)==30000,"überfälliger Termin in Pause erzeugt keine schnelle Schleife");
        Check(schedule.Evaluate(331100,false)==CheckDecision.Poll,"Nutzung nach Pause prüft vor altem 10-Minuten-Termin");
        schedule.Completed(331200,false);
        Check(schedule.NextDueMilliseconds==391200,"Fehler wird nach einer Minute erneut geprüft");
        schedule.Completed(331200,true); schedule.RefreshSoon(332000);
        Check(schedule.NextDueMilliseconds==334000,"Wiederverbindung prüft zeitnah");
        schedule.SetInterval(2,400000);
        Check(schedule.NextDueMilliseconds==520000,"Intervalländerung plant neu");
        Check(schedule.Evaluate(519999,false)==CheckDecision.Wait && schedule.Evaluate(520000,false)==CheckDecision.Poll,"reguläre Folgeprüfung am Termin");
        Check(!CheckSchedule.IsReceiverEvent("hid#vid_1234&pid_5678") && !CheckSchedule.IsReceiverEvent(null),"andere Geräte ändern Messstatus nicht");
        Check(CheckSchedule.IsReceiverEvent("\\\\?\\HID#VID_3434&PID_D030&MI_01"),"Keychron-Empfängerereignis erkannt");
        var freshness=new CheckSchedule(10,0);
        freshness.Completed(0,true);
        var lastReading=new BatteryReading { Percent=100,Charging=true,Time=new DateTime(2026,1,1,12,0,0) };
        Check(freshness.Evaluate(300000,true)==CheckDecision.Paused && TrayView.IsReadingCurrent(lastReading,true,300000,freshness.NextDueMilliseconds),"Inaktivität vor Prüftermin behält Akkufarbe");
        Check(TrayView.IsReadingCurrent(lastReading,true,605000,freshness.NextDueMilliseconds),"Messwert bis einschließlich fünf Sekunden Toleranz aktuell");
        Check(!TrayView.IsReadingCurrent(lastReading,true,605001,freshness.NextDueMilliseconds),"überschrittener Prüftermin macht Messwert grau");
        freshness.SetInterval(20,500000);
        Check(TrayView.IsReadingCurrent(lastReading,true,1600000,freshness.NextDueMilliseconds),"verlängertes Intervall gilt ab Änderung statt ab Messzeit");
        Check(!TrayView.IsReadingCurrent(lastReading,true,1705001,freshness.NextDueMilliseconds),"verlängertes Intervall läuft am neuen Termin ab");
        freshness.SetInterval(1,500000);
        Check(TrayView.IsReadingCurrent(lastReading,true,565000,freshness.NextDueMilliseconds) && !TrayView.IsReadingCurrent(lastReading,true,565001,freshness.NextDueMilliseconds),"verkürztes Intervall verschiebt Aktualitätsgrenze");
        Check(!TrayView.IsReadingCurrent(lastReading,false,500001,freshness.NextDueMilliseconds),"Abfrage- oder Verbindungsfehler sofort grau");
        freshness.SetInterval(1440,500000);
        Check(!TrayView.IsReadingCurrent(lastReading,false,500001,freshness.NextDueMilliseconds),"Intervallverlängerung hebt echten Fehler nicht auf");
        Check(!TrayView.IsReadingCurrent(null,true,0,freshness.NextDueMilliseconds),"ohne Messung keine Akkufarbe");
        string pausedTip=TrayView.Tooltip(lastReading,true,true);
        Check(pausedTip.Contains("01.01. 12:00") && pausedTip.Contains("pausiert") && !pausedTip.Contains(" · alt"),"Tooltip zeigt Messzeit und Pause getrennt vom Alter");
        string staleTip=TrayView.Tooltip(lastReading,false,true);
        Check(staleTip.Contains(" · alt") && staleTip.Contains("pausiert") && staleTip.Length<=63,"alter pausierter Ladewert passt in Windows-Tooltip");
        return count+" Tests bestanden.\r\nWarnlogik, Statuspakete, Speicherung und Hintergrundplanung inklusive Pause, Wiederaufnahme und Fehlerwiederholung geprüft.\r\n";
    }
    static void Reject(byte[] packet,string name)
    {
        bool rejected=false; try { BatteryReader.Parse(packet,"Test"); } catch(InvalidOperationException) { rejected=true; }
        Check(rejected,name);
    }
    sealed class FakeConnection : IBatteryConnection
    {
        readonly byte[] reply;
        readonly System.Collections.Generic.Queue<byte[]> reports;
        bool sent;
        public bool RequestValid,FeatureRead;
        public bool RecoverOnRetry;
        public int Requests;
        public FakeConnection(byte[] reply,params byte[][] reports) { this.reply=reply; this.reports=new System.Collections.Generic.Queue<byte[]>(reports); }
        public byte[] Read(int timeout) { return sent && reports.Count>0?reports.Dequeue():null; }
        public void SetFeature(byte[] request) {
            sent=true; Requests++;
            if(RecoverOnRetry && Requests==2) reports.Enqueue(new byte[] {0x54,0xE4,1});
            RequestValid=request.Length==21 && request[0]==0x51 && request[1]==6; for(int i=2;i<request.Length;i++) RequestValid &= request[i]==0;
        }
        public byte[] GetFeature(byte id,int size) { FeatureRead=true; Check(id==0x51 && size==21,"korrektes Feature-Format"); return reply; }
        public void Dispose() { }
    }
}
