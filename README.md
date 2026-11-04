# M6 Akkuwächter

Kleiner Akkuwächter für Windows 10/11 x64 und die hier geprüfte **Keychron M6 (3434:D03F) am Keychron Link Type A (3434:D030)**. Andere M6-Varianten und Bluetooth werden von dieser Version nicht unterstützt.

## Benutzen

`M6Akku.exe` öffnen. Die Überwachung beginnt und richtet beim ersten Start den gewünschten Autostart für den aktuellen Benutzer ein. Danach kann das Fenster geschlossen werden: Der Akkuwächter läuft mit einem Batteriesymbol im Windows-Infobereich weiter. Beim Windows-Start erscheint kein Einstellungsfenster. Windows kann das Symbol im aufklappbaren Bereich neben der Uhr einsortieren.

Mauszeiger über dem Tray-Symbol: letzter gemeldeter Akkustand und Zeitpunkt der Messung. Ältere oder nicht mehr bestätigte Werte werden als „älterer Wert“ gekennzeichnet; das Symbol wird grau. Grün bedeutet über 30 %, Gelb bis 30 %, Rot bis 10 %, Blau bedeutet Laden. Doppelklick öffnet die Einstellungen. Das Rechtsklick-Menü enthält **Jetzt prüfen**, **Einstellungen** und **Beenden**. Es gibt keine Animation und keinen zusätzlichen Abfragetimer.

Zum Einstellen oder Beenden die EXE erneut öffnen oder im Startmenü nach **M6 Akkuwächter** suchen. Es wird nur eine Hintergrundinstanz gestartet. Im Fenster lässt sich der Autostart ausschalten; „Beenden“ stoppt den laufenden Prozess. Beenden entfernt den Autostart nicht.

Unter **Prüfintervall** sind ganze Minuten von **1 bis 1440** einstellbar. Mit **Übernehmen** wird der Wert gespeichert und der nächste Prüftermin ab diesem Zeitpunkt neu geplant. Standard ist eine Minute. Die erste Prüfung nach dem Programmstart bzw. dem Aufwachen erfolgt weiterhin zeitnah; spätere Prüfungen folgen dem gewählten Intervall. „Jetzt prüfen“ ist jederzeit zusätzlich möglich.

Die EXE benötigt die in Windows integrierte .NET-Framework-4.8-Laufzeit. Sie braucht weder einen Installer noch Administratorrechte. Die Quelldateien und das Build-Skript sind nur für eigene Änderungen nötig.

## Warnungen

- Windows-Mitteilungen bei 30 %, 20 %, 10 % und 5 %; bei 5 % ausdrücklich „sehr niedrig“.
- Beim Start mit bereits niedrigem Akku oder einem Sprung über mehrere Schwellen erscheint nur die dringendste Meldung.
- Eine bereits gemeldete Schwelle wird gespeichert und erst wieder aktiviert, nachdem der Akku mindestens fünf Prozentpunkte darüber gestiegen ist.
- Beim gemeldeten Laden werden keine Akkuwarnungen ausgegeben.
- Kein zusätzlicher Ton. Windows-Einstellungen wie „Nicht stören“ können die Darstellung zurückhalten oder unterdrücken. Einmal an Windows übergebene Warnungen werden als ausgegeben gespeichert; eine sichtbare Anzeige kann die App nicht erzwingen.
- Über „Testmitteilung“ lässt sich die Ausgabe prüfen, ohne den echten Warnstatus zu verändern.

## Hintergrundprüfung und Leistung

Ein Windows-Timer plant die Prüfungen mit einer monotonen Uhr im eingestellten Minutenintervall. Eine zweite gleichzeitige Abfrage wird verhindert. Derselbe Timer wacht spätestens alle 30 Sekunden kurz auf, um Inaktivität und Wiederaufnahme zu erkennen; nur bei fälliger Prüfung wird USB abgefragt. Zwischen den Prüfungen gibt es keine aktive Warteschleife. Der Gerätepfad wird zwischengespeichert. Nur Anschlussereignisse des passenden Keychron-Empfängers markieren den Messwert als veraltet und lösen eine zeitnahe neue Prüfung aus.

Nach fünf Minuten ohne Benutzereingabe werden die USB-Abfragen ausgesetzt. Sobald wieder Eingaben erfolgen, wird innerhalb von etwa 30 Sekunden eine neue Prüfung gestartet; das volle Minutenintervall wird dafür nicht abgewartet. Dabei zählen auch Tastatureingaben: Ob genau die Maus aktiv ist, wird absichtlich nicht mit einem Eingabe-Hook überwacht. Der Timer weckt den PC nicht aus dem Standby. Nach dem Aufwachen wird zeitnah erneut geprüft, sofern keine längere Inaktivität vorliegt. Fehlgeschlagene Abfragen werden nach einer Minute erneut versucht. Nach Erfolg gilt wieder das gewählte Intervall. Ein längeres Intervall kann eine Akkuwarnung entsprechend verzögern.

Der Worker sendet ausschließlich den Status-Lesebefehl `Feature 0x51 / Befehl 0x06`. Er wartet pro Versuch maximal 1500 ms auf die frische Ready-Bestätigung `54 E4 01` und liest erst dann die Statusantwort. Bei fehlender Antwort oder den vorübergehenden Windows-Fehlern 31/121 wird die Statusanfrage höchstens einmal wiederholt, ebenfalls mit erforderlicher frischer Bestätigung. Einzelne Feature-Transfers selbst unterliegen den Windows-HID-Treiberzeitlimits. Ohne passende Bestätigung oder bei falscher Mauskennung wird kein neuer Messwert übernommen. Es werden keine Einstellungen der Maus geschrieben und keine normalen Maus-/Tastatur-Eingabekanäle mitgelesen.

Gemessen am Entwicklungssystem: etwa 40 MiB Arbeitssatz, 24 MiB privater Speicher und 31,25 ms CPU-Zeit in einem 90-Sekunden-Fenster mit einer regulären Abfrage. Das entspricht etwa 0,035 % eines CPU-Kerns. Startvorgang und geöffnetes Einstellungsfenster können höhere Werte verursachen. Die EXE ist deutlich kleiner als 100 KB. Details stehen in `Pruefbericht.md`.

## Daten und Entfernen

Keine Netzwerkverbindungen, Telemetrie oder laufenden Logdateien. Einstellungen und Warnstatus liegen unter `%LOCALAPPDATA%\M6Akku\state.xml`. Das Tool erstellt außerdem:

- den Autostartwert `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\M6Akku`;
- eine Startmenü-Verknüpfung `M6 Akkuwächter.lnk` für Einstellungen und die Windows-Mitteilungsidentität;
- die eigene Registrierung `HKCU\Software\Classes\AppUserModelId\M6Akku.Desktop`.

Zum Entfernen zuerst Autostart ausschalten und das Tool beenden. Danach können der Programmordner und die eigene Startmenü-Verknüpfung gelöscht werden. Der Einstellungsordner und die oben genannte Mitteilungsregistrierung können ebenfalls entfernt werden.

Nach einem Verschieben der EXE die Anwendung am neuen Ort öffnen und den Autostart im Fenster wieder aktivieren, damit der Pfad aktualisiert wird. Die Startmenü-Verknüpfung wird beim Start angepasst.

## Selbst bauen und prüfen

In PowerShell `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` ausführen. Verwendet werden der Windows-.NET-Framework-Compiler sowie die lokal vorhandenen WinRT-Metadaten; es werden keine Pakete heruntergeladen.

Optionale Aufrufe (Ausgabepfade als absolute Pfade angeben):

```text
M6Akku.exe --background
M6Akku.exe --stop
M6Akku.exe --status C:\Pfad\hintergrund-status.txt
M6Akku.exe --self-test C:\Pfad\tests.txt
M6Akku.exe --diagnose C:\Pfad\diagnose.txt
M6Akku.exe --toast-test C:\Pfad\toast-test.txt
```

`--diagnose` fragt das Gerät einmal ab und sollte bei beendeter Überwachung und geschlossenem Keychron Launcher verwendet werden. Die Diagnosedatei enthält Keychron-Gerätekennungen und Produktnamen, keine Seriennummern oder Bluetooth-Adressen. `--toast-test` richtet gegebenenfalls die Mitteilungsidentität ein, aber keinen Autostart.

`--status` liest den Zustand der laufenden Überwachung aus, ohne selbst eine USB-Abfrage auszulösen: letzter Versuch, letzte erfolgreiche Messung, nächster Prüftermin, Pause und Anzahl der Versuche. Eine Datei wird dabei nur auf ausdrücklichen Aufruf geschrieben; es gibt keine dauernde Protokollierung. Der nächste Termin ist auch im Einstellungsfenster sichtbar.

Die Bedeutung der Statuspakete wurde mit dem öffentlichen [Keychron Launcher](https://launcher.keychron.com/) abgeglichen und an der angeschlossenen Maus getestet. Die Zuordnung von D03F zur M6 wurde über Keychrons öffentliche Produktdaten bestätigt. Der lokale Quelltext ist eine eigenständige Windows-Implementierung.
