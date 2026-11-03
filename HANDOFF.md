# Handoff: English Steam Trainer

Stand: 2026-11-03. Die Bedienung und Pflege beschreibt `README.md`. Hier steht nur,
was nicht offensichtlich ist.

## Status

Zuletzt veröffentlicht: **v1.4.2** (2026-11-03).

### Eingabefelder und App-Icon (in v1.4.2, 2026-11-03)

- `RoundedTextBoxStyle` hat das Padding zusätzlich als Margin im Template
  gesetzt, die TextBox wendet es aber selbst schon an. Doppelt gerechnet fehlte
  Platz, Unterlängen (das y in „cuarenta y seis“) wurden abgeschnitten. Padding
  der Felder jetzt symmetrisch. Offscreen gerendert geprüft.
- `app.ico` (Taskleiste/Fenster) aus `scripts/New-AppIcon.ps1`, als
  `ApplicationIcon` in der UI-csproj. Pflege-Oberfläche hat noch das
  Blazor-Standardicon.

### Zahlen 1–100 für leand (Inhalt, 2026-11-03)

- `es/vocabulary.csv`: Thema „Zahlen“ jetzt 1–100, Ziffer als deutsche
  Alternative („21“ zählt wie „einundzwanzig“).
- `plan.json`: eigener Eintrag für leand (02.–08.11.) mit „Party-Essen“ *und*
  „Zahlen“. Party-Essen muss dort mit drinstehen, weil bei gleichzeitig
  aktiven Einträgen nur der spezifischste gilt – sonst fiele das Thema weg.
- `config.json`: leand braucht 35 statt 30 richtige Antworten.

### Verben im Lernplan (für 1.4.1, 2026-11-02)

- Alle unregelmäßigen Verben haben das Thema „Unregelmäßige Verben“; tiago hat
  es im Lernplan für 02.–08.11. (vorher fehlte den Verben ein Thema, deshalb
  ließen sie sich nicht einplanen).
- `QuizPlanner`: Grammatik/Verben des aktiven Themas sind nicht mehr durch
  `grammarQuestionsPerSession`/`verbFormQuestionsPerSession` begrenzt und
  laufen im Themenanteil mit den Themenvokabeln. Wirkt erst mit Release 1.4.1.
- Oberfläche: Verben-Seite mit Themenfilter und Mehrfachauswahl („Thema
  hinzufügen/entfernen“), Lernplan-Palette weist auf Verben ohne Thema hin.

### Motiv der Woche (für 1.4.1, 2026-11-02)

`Motifs` (Content): 16 Wochenmotive reihum ab Montag 05.01.2026, Vorrang für
Feiertagsmotive (Ostern per Osterformel). `MainWindow.ApplyMotif` setzt
Fensterhintergrund, Kopfzeile, Maskottchen (vorher fest 🦉) und vier
Deko-Emojis in den Kartenecken. Da WPF Emojis einfarbig zeichnet, werden
Maskottchen und Deko in der Kopfzeilenfarbe eingefärbt. Alle Emojis einmal
mit WPF gerendert: nur 🪐 fehlte (ersetzt durch 🌍). Entwicklermodus hat eine
Motiv-Vorschau. Kommt erst mit einem Release bei den Kindern an.

### Entwicklermodus: „Lernen als“ (für 1.4.1, 2026-11-02)

Auswahl im Kopf (nur Entwicklermodus) bzw. `--als <konto>`: Konto und PC des
Kindes aus `config.json` gelten für Lernplan und `RequiredCorrectAnswersFor`
(PC aus dem Eintrag, damit z. B. leand/Groot 30 bekommt). Ein Wechsel öffnet
ein neues `MainWindow` mit neuem Wegwerf-Journal (`App.SwitchLearner`). Geprüft
per UI Automation: tiago → Englisch, „Unregelmäßige Verben“, 15; Wechsel auf
leand → Spanisch, „Party-Essen“. 143 Tests grün.

### Entwicklermodus (für 1.4.1, 2026-11-02)

Anlass: Auf dem Entwickler-PC (ohne Installation) startete die UI im
Normalmodus den Ersatz-Watchdog, der Chrome/Edge des Entwicklerkontos beendete
und auch nach dem Schließen weiterlief. Jetzt gilt für jedes nicht gesperrte
Konto automatisch der Entwicklermodus (früher `--test`); der Watchdog ohne
SYSTEM beendet sich, wenn das Konto nicht gesperrt ist. `--kindmodus` schaltet
das echte Verhalten zum Testen ein. Für gesperrte Konten ändert sich nichts,
deshalb ist kein Release nötig. Geprüft auf diesem PC: UI ohne Argumente →
Entwicklermodus, kein Watchdog; Watchdog direkt gestartet → beendet sich mit
Log-Eintrag. 140 Tests grün.

### Lernplan, Karteikasten, Pflege-Oberfläche (1.4.0, 2026-11-02)

Anlass: Mit wachsendem Wortschatz waren Themen (CSV) und Zeiträume
(config.json) getrennt zu pflegen und unflexibel. Die frühere Entscheidung
„kein grafischer Admin-Editor“ (siehe unten) ist damit überholt; umgesetzt
wurde die dort genannte Variante (statische Seite auf GitHub Pages, speichert
per Commit).

- **Neues Projekt `EnglishSteamTrainer.Content` (net10.0):** alles
  Plattformunabhängige aus Core (Parser, TrainerConfig, AnswerChecker, XpRules,
  QuizItem/-Order) plus neu: `LearningPlan`, `ReviewSchedule`, `QuizPlanner`,
  `ContentSet`/`ContentCheck`/`ContentFiles`, `TopicProgress`, CSV-Writer.
  Namespace bleibt `EnglishSteamTrainer.Core`. Core (net10.0-windows)
  referenziert es. Grund: Blazor im Browser kann kein `-windows`-Projekt laden.
- **`content/plan.json`** ersetzt `focusTopics` (das wird weiter gelesen und
  hinten angehängt). Einträge pro Sprache und/oder Konto, mehrere Themen,
  Titel. `holdBackPlannedTopics`: Themen, die erst später geplant sind, fallen
  vorher aus der Abfrage. Der Eintrag Party-Essen (es, 02.–08.11.) ist aus
  config.json nach plan.json gewandert – v1.3.0-PCs haben damit bis zum Update
  kein Thema der Woche.
- **Karteikasten** aus dem Journal berechnet (Fächer 1–5, 1/3/7/14/30 Tage).
  Richtig zählt nur, wenn fällig; nach einem Fehler erst wieder Fach 1. Die
  Reihenfolge der Runde baut `QuizPlanner` (vorher in `MainWindow`).
- **Stabile IDs:** Spalte `Id` in vocabulary.csv, Journal-Schlüssel ist
  `card.Key` (= Id, sonst Word). Die Oberfläche setzt Id beim Umbenennen eines
  gespeicherten Worts. Grammatik/Verben haben weiter ihren natürlichen
  Schlüssel (Subjekt/Verb bzw. Infinitiv).
- **Neue Spalten** `verbs.csv` → `Topics`; Grammatik-`Topic` zählt als Thema.
  Die CSV-Dateien wurden einmal mit dem neuen Writer geschrieben (nur
  Kopfzeilen und zwei überflüssige `;;`). Test
  `Writing_the_content_back_changes_nothing` sichert, dass Speichern aus der
  Oberfläche nichts ungewollt umformatiert. Writer schreibt LF wie im Repo.
- **Bericht:** Tabelle „Lernplan“ je Thema (Aufgaben/geübt/sicher), der
  Watchdog lädt dafür Plan und Inhalte.
- **Pflege-Oberfläche** `src/EnglishSteamTrainer.Admin` (Blazor WASM),
  `.github/workflows/admin.yml` → GitHub Pages. Liest/schreibt über die
  Git-Daten-API (ein Commit für alle Dateien, `force: false` → kein
  Überschreiben fremder Pushes). Token im localStorage. Demo-Modus liest eine
  beim Build kopierte `wwwroot/demo-content/` (gitignored).

Verifiziert: Build der Solution, 139 Tests grün. Oberfläche mit Edge
(playwright-core) im Demo-Modus durchgeklickt, Dev-Server und getrimmter
Release-Build unter `/karteikarten/`: Filtern, Bearbeiten/Umbenennen (Id),
Thema für Auswahl, Liste einfügen, Thema umbenennen, Thema auf Woche ziehen,
Eintrag verschieben, Vorschau, Speichern im Demo gesperrt, Verwerfen – keine
Konsolenfehler. GitHub-API: Lese-Endpunkte und CORS-Preflight (PATCH mit
Authorization) gegen das echte Repo geprüft.

**Nicht verifiziert:** Speichern auf GitHub (braucht ein Token, kein
Test-Commit gemacht) und die Berichte-Seite mit Token. Erster echter
Speicher-Vorgang: danach den Commit auf GitHub ansehen.

Erledigt am 2026-11-02: gepusht, Pages eingeschaltet, Release v1.4.0
veröffentlicht, Seite live (Klicktest im Demo-Modus gegen die echte Seite grün).

Offen:
1. Fine-grained Token für die Oberfläche anlegen (Contents RW, nur dieses
   Repo), einloggen, eine kleine Änderung speichern – erster echter Test des
   Speicherns.
2. Lern-App 1.4.0 auf echtem Bildschirm ansehen (Kopfzeile mit Titel/mehreren
   Themen, 🎯 bei Grammatik/Verben, Fußzeile „… erst später laut Lernplan“).

### Thema der Woche (für 1.3.0, 2026-10-30, noch nicht veröffentlicht)

- Neue Spalte `Topics` in `vocabulary.csv`, `focusTopics` + `focusSharePercent`
  in `config.json` (README „Thema der Woche“). Die Auswahl macht
  `QuizOrder.MixFocus`, die App zeigt das Thema in der Kopfzeile.
- Neu: 30 spanische und 26 englische Wörter zum Thema „Party-Essen“. Die vorhandenen
  Wörter haben Themen bekommen. Eingetragen ist Spanisch, Party-Essen,
  02.–08.11.2026.
- Build + 100 Tests grün. **UI nicht gestartet** (zweizeiliger Untertitel und
  🎯-Kopfzeile auf echtem Bildschirm prüfen; `--test --language es` zeigt das
  Thema erst ab dem 02.11.).

### Fortschrittsbericht für Eltern (in v1.2.0, Einrichtung offen)

Ziel: Vom Entwickler-/Admin-PC sehen, ob und wie viel gelernt wurde. Gewählt
wurde (2026-10-25, bewusst gegen ein eigenes privates Repo): Upload in den
Branch `berichte` des **öffentlichen** Programm-Repos. Berichte sind damit
öffentlich lesbar, und das Token auf den Kind-PCs hat Schreibrecht auf das
ganze Programm-Repo (inkl. Tags/Releases). Ein eigenes privates Repo wäre nur
`-ReportRepository besitzer/name` (Branch dort ebenfalls `berichte` anlegen). Inhalt: nur die Tagesgrafik.

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
1. ~~Branch `berichte` anlegen~~ – erledigt am 2026-10-25 (verwaister Branch,
   nur README, im öffentlichen Programm-Repo).
2. Fine-grained Token erstellen. Das geht nur von Hand auf GitHub: nur Repo
   `karteikarten`, Recht *Contents: Read and write*.
3. ~~Committen, Version 1.2.0, Tag pushen~~ – erledigt am 2026-10-25.
4. Auf den Kind-PCs (DESKTOP-NO7VIKD/tiago, Groot/leand) als Admin
   `Install.ps1 -ReportToken ...` ausführen. Das geht erst, wenn **v1.2.1**
   installiert ist (erst diese Version schreibt in den Branch `berichte`).
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
- Entscheidung (2026-10-25, überholt am 2026-11-02 durch die
  Pflege-Oberfläche): kein grafischer Admin-Editor. Falls doch: statische
  Seite über GitHub Pages, die per Commit speichert, keine Desktop-App.

### Testmodus (in v1.2.1, 2026-10-25)

- `--test` (+ `--language`, `--only`), siehe README. Gesperrte Konten bekommen
  ihn nicht. Selbst wenn: Freigeschaltet wird nur über die signierte Tagesdatei,
  und die schreibt der Testmodus nie, der SYSTEM-Watchdog sperrt weiter.
- Kurz geprüft mit `--only verbs` auf DESKTOP-NO7VIKD (Konto sebas): Start,
  Anzeige, Schließen, Wegwerf-Journal wird gelöscht. Die Verb-Felder habe ich
  danach kompakter gemacht, weil unten bei 1080p der Belohnungstext
  abgeschnitten war. Neues Layout nicht mehr angesehen.

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
