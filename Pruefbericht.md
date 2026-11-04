# Prüfbericht – M6 Akkuwächter 1.3

Stand: 31.10.2026, Europe/Berlin. Windows x64 auf dem Rechner des Nutzers.

## Hintergrundkorrektur 1.3

- Die frühere Wiederaufnahme konnte nach Inaktivität bis zum nächsten vollen Minutenintervall warten. Die Planung verwendet jetzt monotone Zeit und denselben Timer mit maximal 30 Sekunden bis zur nächsten Aktivitätskontrolle. USB wird weiterhin nur zum eigentlichen Prüftermin abgefragt. Wiederaufnahme erfolgt innerhalb von etwa 30 Sekunden.
- Allgemeine Windows-Geräteereignisse markieren den M6-Messwert nicht mehr als veraltet. Nur die passende Empfängerkennung löst eine Neubewertung aus.
- Im Hardwarevergleich fiel eine erste Statusanfrage ohne Antwort aus; die unmittelbar folgende Anfrage erhielt eine frische Ready-Bestätigung und 99 %. Daher wurde die Wartezeit auf 1500 ms je Versuch erweitert und eine einmalige Wiederholung ergänzt. Alte Cache-Antworten ohne frische Bestätigung werden weiterhin verworfen. Nach einem Fehlschlag erfolgt der nächste automatische Versuch nach einer Minute.
- **46 Tests bestanden**, einschließlich regelmäßiger Folgetermine, Wiederaufnahme vor dem bisherigen 10-Minuten-Termin, begrenzter Fehlerwiederholung, Intervalländerung, Gerätefilter und Wiederholung einer verlorenen ersten Statusanfrage.
- Tatsächlicher automatischer Hintergrundlauf ohne Einstellungsfenster: Versuch um **31.10.2026 15:22:33**, erfolgreiche Messung um **15:22:34**, **99 %**, nächster regulärer Termin **15:32:34** bei erhaltenem 10-Minuten-Intervall. Status über die laufende Anwendung ausgelesen, keine separate manuelle Hardwareabfrage.
- Neue Diagnose `--status <Datei>` und Anzeige des nächsten Prüftermins. Keine permanenten Logdateien. Die historische Ressourcenmessung unten ist kein neuer Leistungsvergleich für Version 1.3. Pause und Wiederaufnahme wurden mit virtueller monotoner Zeit getestet; ein vollständiger zehnminütiger Live-Zyklus oder echter Windows-Standby wurde für diese Korrektur nicht erneut abgewartet.

## Frühere Prüfungen

Version 1.2 ergänzt ein WinForms-NotifyIcon mit letztem Akkustand, Messzeit, Kennzeichnung älterer Werte, Doppelklick für Einstellungen und Kontextmenü. Das bestehende Prüfintervall wird weiterverwendet. Es gibt keinen zusätzlichen Timer und keine Animation. Die Ressourcenmessungen unten stammen aus Version 1.0 ohne Tray-Symbol.

Die Tray-Version wurde kompiliert und erfolgreich im Hintergrund gestartet; alle 30 bestehenden Prüfungen bestehen weiterhin. Das gespeicherte Intervall von 10 Minuten und der Warnstatus blieben erhalten. Direkt nach dem Start wurden etwa 44,7 MiB Arbeitssatz beobachtet; dieser Einzelwert ist kein kontrollierter Vergleich mit der früheren Version. Die Tray-Interaktionen selbst wurden aufgrund der bereits fehlenden App-Freigabe nicht automatisiert über Computer Use angeklickt.

## Nachgewiesen

- Keychron-Link-Type-A-Empfänger `3434:D030`, Statuscollection `008C:0001` erkannt.
- Das echte Gerät bestätigt die Statusabfrage mit `54 E4 00` und anschließend `54 E4 01`.
- Die frische Antwort `51 06 01 00 34 34 3F D0 07 01 09 02 00 ...` enthält die M6-Kennung `3434:D03F` und meldet **2 % Akku, nicht ladend**. Das ist eine Gerätemeldung, keine unabhängige Messung der Zellkapazität.
- Keychrons öffentliche Produktdaten bestätigen `3434:D03F` als Keychron M6. Die Paketbelegung stimmt mit der Statusabfrage des offiziellen Launchers überein. Ein zusätzlicher visueller Vergleich mit dessen Akkuanzeige erfolgte nicht.
- Die Endversion hat die Hardwareabfrage ebenfalls bestanden; letzte Abfrage vor Auslieferung um 23:21 Uhr meldete weiterhin 2 %.
- Eine Windows-Testmitteilung wurde vom System angenommen und vom Nutzer ausdrücklich als sichtbar bestätigt.
- Autostartwert zeigt auf die ausgelieferte EXE mit `--background`. Die Endversion läuft bereits ohne sichtbares Hauptfenster. Ein zweiter Start mit `--background` erzeugt keine zweite Instanz.
- Die Beenden-Funktion über `--stop` wurde beim Versionswechsel erfolgreich verwendet.

## Automatisierte Tests

**30 Assertions bestanden.** Geprüft wurden die vier Warnschwellen, keine Wiederholung, gespeicherter Warnmaskenstand, große Akkusprünge, 0 %, fünf Prozentpunkte Hysterese, Aufladen, falsche Gerätekennung, ungültige Akkustände, falsche Befehle und kurze/fehlende Antworten. Seit Version 1.1 sind zusätzlich die Kompatibilität mit alten Einstellungsdateien (Standardintervall eine Minute) und die gemeinsame Speicherung von Prüfintervall und Warnstatus geprüft.

Version 1.1 ergänzt ein Minutenfeld von 1 bis 1440 und „Übernehmen“. Eine Änderung speichert zuerst die Einstellung und plant dann den bestehenden Timer neu; bei einem Speicherfehler bleibt das alte Intervall aktiv. Die Warnschwellen und die Inaktivitätspause bleiben unverändert. Die folgenden Ressourcenmessungen stammen aus Version 1.0 bei einer Minute Intervall.

Mit einem simulierten Transport wurde außerdem geprüft: Pending gefolgt von Ready ist gültig; fehlende Bestätigung, falsche Reports oder nur Pending erlauben keinen Zugriff auf den möglicherweise alten Feature-Cache. Das erzeugte Status-Lesepaket und die angeforderte Antwortlänge werden überprüft.

Der wirkliche Windows-USB-Lesepfad inklusive Timeout/Abbruch wurde an der Hardware ausgeführt. Er schreibt ausschließlich den Status-Lesebefehl.

## Ressourcen

Messfenster nach dem Programmstart, Einstellungsfenster geschlossen; enthält einen regulären Minutentermin. Gemessen wurde vor dem abschließenden Test-Refactoring und dem Hinzufügen des DPI-Manifests; die Abfragestrategie und das Timerintervall blieben gleich.

| Vergangene Zeit | Zusätzliche CPU-Zeit | Arbeitssatz | Privater Speicher | Handles |
|---|---:|---:|---:|---:|
| 30,03 s | 0 ms | 39,82 MiB | 23,67 MiB | 356 |
| 60,06 s | 31,25 ms | 39,73 MiB | 23,52 MiB | 357 |
| 90,09 s | 31,25 ms | 39,73 MiB | 23,52 MiB | 357 |

Das entspricht im Mittel rund **0,035 % eines CPU-Kerns**; der auf alle Kerne normierte Task-Manager-Wert kann niedriger sein. Beim abschließenden Start der Endversion wurden rund 37,6 MiB Arbeitssatz und 23,4 MiB privater Speicher beobachtet. Die Endversion ist **33.792 Byte (33 KiB)** groß. Messwerte sind Momentaufnahmen, keine Zusicherung für jedes System oder jeden Fehlerfall.

## Grenzen der Prüfung

- Physisches Abziehen/Wiedereinstecken, echter Windows-Standby/Resume, fünf Minuten vollständige Eingabeinaktivität und ein kompletter Ladezyklus wurden nicht als manuelle Langzeittests durchgeführt. Die Behandlung ist implementiert; ein langzeitlicher Einfluss der Abfragen auf die Mauslaufzeit ist nicht gemessen.
- Die automatisierte Sichtprüfung des Einstellungsfensters war wegen fehlender Computer-Use-App-Freigabe blockiert. Die Mitteilung wurde vom Nutzer geprüft.
- Andere Empfänger, andere M6-Revisionen und Bluetooth sind nicht freigegeben.
- Windows kann Warnungen durch eigene Benachrichtigungseinstellungen oder „Nicht stören“ unterdrücken. Die App kann die tatsächliche Wahrnehmung einer Mitteilung nicht nachweisen.

## Reproduzierbare Prüfungen

Die EXE unterstützt `--self-test <Ausgabedatei>`, `--diagnose <Ausgabedatei>` und `--toast-test <Ausgabedatei>`. Absolute Ausgabepfade verwenden. Hardwarediagnose nur bei beendeter Überwachung und geschlossenem Keychron Launcher ausführen. Siehe README.
