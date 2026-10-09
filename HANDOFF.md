# Handoff: English Steam Trainer

Stand: 2026-10-09. Die Bedienung und Pflege beschreibt `README.md`. Hier steht nur,
was nicht offensichtlich ist.

## Status

Noch **nichts committet/gepusht**, auch nicht die Arbeit vom 2026-09-25. Die
Online-Funktionen greifen erst, wenn das hier passiert ist:

1. Push nach `main` → `content/` ist unter raw.githubusercontent.com erreichbar
   (vorher: Watchdog loggt HTTP 404, App nutzt mitgelieferte Inhalte).
2. Tag `v1.0.0` pushen → GitHub-Action baut das erste Release. Ohne Release
   schlägt `irm .../Install.ps1 | iex` fehl („releases/latest“ = 404). Bis dahin
   nur `Install.ps1 -FromSource`.

## Umgesetzt am 2026-10-09

- Mehrsprachig (Englisch/Spanisch) pro Konto über `settings.json` (ersetzt
  `blocked-users.txt`, wird migriert).
- Inhalte und Sperrliste online in `content/`, SYSTEM-Watchdog lädt sie in
  `C:\ProgramData\EnglishSteamTrainer\cache` (admin-only). Validierung beim
  Laden *und* beim Download, damit eine kaputte/leere Online-Datei nichts freischaltet.
- Updater über GitHub-Releases (`Update.ps1`, SYSTEM-Aufgabe), Installer
  `Install.ps1` (ersetzt `Deploy.ps1` und `Install-Autostart.ps1`).
- Punkte: Lernjournal mit HMAC-Kette ersetzt die frei editierbare
  `progress.json` (alte XP werden einmalig als sichtbarer Eintrag übernommen).
  Verlaufsfenster in der App.
- Antwortprüfung: Akzente/Artikel egal, Tippfehler erst ab 5 Buchstaben
  (vorher war z. B. „Mund“ richtig für „Hund“), Tippfehler gibt nur +5 XP.
- Wiederholung: zuletzt falsch beantwortete Wörter kommen zuerst.
- .NET 8 → .NET 10 (8 läuft am 10.11.2026 aus); Paket ist self-contained.
- Nur eine UI-Instanz pro Konto. Unit-Tests (53) inkl. Prüfung von `content/`.

## Verifiziert / nicht verifiziert

- Build, alle Tests und `Build-Package.ps1 -Zip` laufen. Die UI startet in
  Englisch und Spanisch ohne Fehler (Smoke-Test ohne Watchdog, Spuren danach
  entfernt).
- **Nicht ausgeführt:** `Install.ps1` und `Update.ps1` (brauchen Admin bzw.
  ein echtes Release und verändern die laufende Installation auf diesem PC);
  der Watchdog als SYSTEM inkl. Online-Abgleich. Beides nach dem ersten Release
  auf einem PC testen.

## Bewusste Grenzen

- Signaturschlüssel stehen im Programm: schützt gegen Bearbeiten im Editor,
  nicht gegen jemanden, der den Code liest.
- Ist das Kind Admin, hilft nichts davon.
- Lernverlauf liegt nur lokal pro PC. Eine zentrale Übersicht für Eltern über
  alle PCs bräuchte Schreibzugriff auf einen Server/ein Repo von den Kind-PCs
  aus (Token auf dem PC) – bewusst nicht umgesetzt.
