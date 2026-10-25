# Handoff: English Steam Trainer

Stand: 2026-10-25. Die Bedienung und Pflege beschreibt `README.md`. Hier steht nur,
was nicht offensichtlich ist.

## Status

`main` ist gepusht, zuletzt veröffentlicht: **v1.2.0** (Tags v1.0.0–v1.2.0).

### Fortschrittsbericht für Eltern (in v1.2.0, Einrichtung offen)

Ziel: Vom Entwickler-/Admin-PC sehen, ob und wie viel gelernt wurde. Gewählt
wurde: Upload in ein privates GitHub-Repo. Die anderen Optionen waren eine
Netzwerkfreigabe oder ein Bericht nur lokal. Inhalt: nur die Tagesgrafik.

Fertig (Build + Tests grün):
- `Core/ProgressReport.cs`: Markdown pro Konto mit Mermaid-Grafik
  (`xychart-beta`), Tabelle der letzten 30 Tage und Zusammenfassung.
- `Core/ReportUpload.cs`: `report-upload.json` (Repo + Token) lesen,
  Upload über die GitHub-Contents-API, nur wenn sich der Inhalt geändert hat.
- Watchdog: lädt im 30-Min.-Takt nach dem Inhaltsabgleich pro Konto
  `<PC>/<konto>.md` hoch. Fehler werden nur bei Änderung geloggt.
- `Install.ps1 -ReportToken/-ReportRepository`: legt `report-upload.json` an.
  Die Datei ist nur für SYSTEM/Admins lesbar. Ohne `-TargetUser` werden nur die
  Berichte eingerichtet, ohne Neuinstallation.
- `Update.ps1`: `report-upload.json` steht bei den Datendateien, damit sie
  nicht ins für Benutzer lesbare Backup kopiert wird.
- README: Abschnitt „Fortschrittsbericht ansehen“. Tests:
  `ProgressReportTests.cs`.

Offen, in dieser Reihenfolge:
1. GitHub-Repo `sebastianhockmann/karteikarten-berichte` anlegen: **privat**,
   mit README. Auf dem Entwickler-PC ist `gh` nicht installiert. Plan:
   `winget install GitHub.cli`, dann `gh auth login`, dann
   `gh repo create karteikarten-berichte --private --add-readme`.
   Alternativ von Hand auf github.com/new.
2. Fine-grained Token erstellen. Das geht nur von Hand auf GitHub: nur dieses
   Repo, Recht *Contents: Read and write*.
3. ~~Committen, Version 1.2.0, Tag pushen~~ – erledigt am 2026-10-25.
4. Auf den Kind-PCs (DESKTOP-NO7VIKD/tiago, Groot/leand) als Admin
   `Install.ps1 -ReportToken ...` ausführen. Das geht erst, wenn v1.2.0
   installiert ist, sonst kennt der Watchdog die Datei nicht.
5. Ersten Upload prüfen: Watchdog-Log „Fortschrittsbericht hochgeladen“.
   Zeigt GitHub die Mermaid-Grafik an? Sonst bleibt die Tabelle, oder es
   braucht eine andere Darstellung.

### Unregelmäßige Verben (in v1.2.0, 2026-10-25)

- `content/en/verbs.csv` (Infinitive;SimplePast;PastParticiple;German), nur
  für Sprachen mit `HasVerbForms` (Englisch) – Spanisch lädt keine Datei.
- Eine Form + Deutsch vorgegeben, zwei Felder eintippen. Beide richtig = richtig,
  Tippfehler wie bei Vokabeln. Journal-Art `verbs`, Wiederholung wie Grammatik.
- `verbFormQuestionsPerSession` in `config.json` (Standard 5). Ältere
  Programmversionen ignorieren das Feld und die Datei.
- Build + 82 Tests grün. **UI nicht gestartet** (Layout der drei Eingabefelder
  auf echtem Bildschirm prüfen).
- Entscheidung: **kein grafischer Admin-Editor.** Inhalte werden weiter auf
  GitHub bearbeitet (oder die CSV-Dateien in Excel, Trennzeichen `;`), CI prüft.
  Sollte doch einer nötig werden: statische Seite über GitHub Pages, die per
  Commit speichert, keine Desktop-App.

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
- Lernverlauf liegt lokal pro PC. Zentral gibt es nur den optionalen
  Tagesbericht (README „Fortschrittsbericht ansehen“). Dafür liegt ein
  Fine-grained-Token auf dem Kind-PC, nur für SYSTEM/Admins lesbar und nur für
  das Berichte-Repo gültig. Läuft nach max. 1 Jahr ab, dann im Watchdog-Log
  „HTTP 401“. Noch nicht gegen das echte GitHub getestet (nur gegen eine
  nachgebaute API in den Tests). Ob GitHub `xychart-beta` als Grafik zeigt, beim
  ersten Upload prüfen. Die Tabelle darunter funktioniert in jedem Fall.
