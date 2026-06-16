# English Steam Trainer

C# / WPF Lernprogramm für Windows.

## Projekte

- `EnglishSteamTrainer.UI`  
  Die Karteikarten-App für das Kind.

- `EnglishSteamTrainer.Core`  
  Gemeinsame Logik: Vokabeln, Fortschritt, XP, Steam-Freischaltung.

- `EnglishSteamTrainer.Watchdog`  
  Hintergrundprogramm. Beendet Steam, solange die 10 Aufgaben nicht erledigt sind.

## Start in Visual Studio Community

1. ZIP entpacken.
2. `EnglishSteamTrainer.sln` öffnen.
3. Rechtsklick auf `EnglishSteamTrainer.UI` > Als Startprojekt festlegen.
4. F5 drücken.

## Watchdog testen

1. Rechtsklick auf `EnglishSteamTrainer.Watchdog` > Als Startprojekt festlegen.
2. Starten.
3. Wenn Steam nicht freigeschaltet ist, wird `steam.exe` beendet.

## Später sinnvoll

- Kind sollte ein Windows-Standardkonto nutzen, kein Admin.
- Watchdog per Windows Aufgabenplanung bei Anmeldung starten.
- UI ebenfalls per Aufgabenplanung bei Anmeldung starten.
- Für sehr starke Sperre später Watchdog als Windows-Dienst umbauen.
