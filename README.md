# English Steam Trainer

Lernprogramm (C# / WPF, .NET 10) für Windows: Erst wenn heute genug Aufgaben
richtig beantwortet sind (Standard: 15), werden Steam, Browser & Co. für das
Kind-Konto freigeschaltet. Lernsprache pro Konto: **Englisch** oder **Spanisch**.

## Überblick

| Was | Wo gepflegt | Wie kommt es auf die PCs |
|---|---|---|
| Programmversion | dieses Repo, Git-Tag `v1.2.3` | GitHub-Release → Aufgabe `EnglishSteamTrainer-Update` (beim Hochfahren + alle 6 h) |
| Vokabeln, Grammatik, unregelmäßige Verben, Themen | [Pflege-Oberfläche](https://sebastianhockmann.github.io/karteikarten/) oder `content/<sprache>/*.csv` im Repo | Watchdog lädt alle 30 Min. von GitHub in einen Cache |
| Lernplan (welches Thema wann, pro Kind) | Pflege-Oberfläche oder `content/plan.json` | wie Vokabeln |
| Gesperrte Programme, Anzahl Aufgaben | `content/config.json` im Repo | wie Vokabeln |
| Konto → Sprache (zentral, hat Vorrang) | `content/config.json` → `users` | wie Vokabeln |
| Welche Konten gesperrt sind, Sprache als Rückfall | `settings.json` auf dem PC | `scripts\Install.ps1` |
| Lernverlauf, Punkte | signiertes Lernjournal pro Konto auf dem PC | – |
| Fortschrittsbericht für Eltern (optional) | wird aus dem Lernjournal erzeugt | Watchdog lädt ihn alle 30 Min. in den Branch `berichte` dieses Repos (**öffentlich**) |

Alles unter `C:\ProgramData\EnglishSteamTrainer` ist **nur für Admins und
SYSTEM beschreibbar** – das Kind kann weder `settings.json` noch den Online-Cache
ändern. Ausnahme: `unlocks\` (Tagesfortschritt + Lernjournal, signiert) und `logs\`.

Fällt das Internet aus, gilt die zuletzt geladene Version (Cache), danach die mit
dem Programm ausgelieferte. Ungültige Online-Dateien (z. B. leere Sperrliste,
kaputte CSV) werden nicht übernommen – die letzte gute Version bleibt aktiv.

## Neuen PC / neues Kind einrichten

PowerShell **als Administrator** öffnen und ausführen – das Repo muss dafür
nicht heruntergeladen werden:

```powershell
irm https://raw.githubusercontent.com/sebastianhockmann/karteikarten/main/scripts/Install.ps1 | iex
```

Das Skript fragt Windows-Konto und Sprache ab, lädt die neueste Version von
GitHub (SHA256-geprüft), installiert sie und richtet Autostart, Watchdog und
Updater ein. Ohne Rückfragen:

```powershell
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/sebastianhockmann/karteikarten/main/scripts/Install.ps1))) -TargetUser lena -Language es
```

- **Weiteres Konto auf demselben PC:** einfach erneut ausführen (jedes Konto
  mit eigener Sprache).
- **Sprache ändern:** am einfachsten zentral in `content/config.json` (siehe
  „Sprache und Aufgabenzahl pro Kind“), alternativ `Install.ps1` erneut ausführen.
- **Entfernen:** `Install.ps1 -TargetUser lena -Uninstall` (mit `-RemoveFiles`
  zusätzlich Programmordner und Lernverlauf löschen).
- Bestehende Installationen der Vorgängerversion (`blocked-users.txt`,
  `Deploy.ps1`-Aufgaben, alte XP in `progress.json`) werden automatisch
  übernommen.

Das Kind muss ein **Standardkonto** (kein Admin) haben – sonst kann es den
Watchdog beenden.

## Inhalte pflegen

Alles in `content/` ist zentral für alle PCs. Am bequemsten über die
**Pflege-Oberfläche** (nächster Abschnitt). Alternativ direkt auf GitHub
bearbeiten (Stift-Symbol) oder auf dem Entwickler-PC:

```powershell
# content\... bearbeiten, dann prüfen (testet u. a. alle CSV-Dateien und config.json):
dotnet test tests/EnglishSteamTrainer.Core.Tests
git add content
git commit -m "Neue Vokabeln"
git push origin main
```

Ein neues Release ist dafür **nicht** nötig.

## Pflege-Oberfläche (Vokabeln, Themen, Lernplan im Browser)

**https://sebastianhockmann.github.io/karteikarten/** – läuft komplett im
Browser (auch auf Tablet/Handy), ohne Server. Gespeichert wird direkt als
Commit auf `main`, die PCs holen sich die Änderung wie gewohnt.

- **Vokabeln:** suchen, nach Thema filtern, bearbeiten, mehrere Wörter auf
  einmal einem Thema zuordnen, Wortlisten einfügen (`perro;Hund`, auch aus
  Excel kopiert). Wird ein Wort umbenannt, bleibt sein Lernverlauf erhalten
  (Spalte `Id`, siehe Format).
- **Grammatik**, **Unregelmäßige Verben:** bearbeiten, Themen vergeben.
- **Themen:** Übersicht mit Anzahl je Art und Lernplan-Zeiträumen,
  umbenennen (auch zusammenführen) und entfernen.
- **Lernplan:** Wochenkalender pro Kind; Themen auf eine Woche ziehen,
  Einträge verschieben, Zeitraum/Kinder/Titel bearbeiten.
- **Vorschau:** zeigt für ein Kind und einen Tag, welche Aufgaben in welcher
  Reihenfolge kämen (gleiche Logik wie die Lern-App).
- **Berichte:** die Fortschrittsberichte aus dem Branch `berichte`.

Vor dem Speichern prüft die Seite alles mit denselben Regeln wie CI (Spalten,
Pflichtfelder, Duplikate, `;`/`|` in Werten, Themen im Lernplan ohne
Aufgaben). Bei Fehlern ist Speichern gesperrt, Hinweise (z. B. Überschneidung
im Plan) stehen auf der Übersicht. Hat inzwischen jemand anderes auf `main`
gepusht, wird nichts überschrieben – dann neu laden und erneut eintragen.

**Anmelden:** Fine-grained Personal Access Token
([anlegen](https://github.com/settings/personal-access-tokens/new)), *Only
select repositories* → `karteikarten`, *Contents: Read and write*. Das Token
bleibt nur im Browser dieses Geräts (localStorage); „Abmelden“ löscht es. Es
ist ein eigenes Token, nicht das der Kind-PCs. **Ohne Anmeldung ansehen
(Demo)** zeigt den Stand der letzten Veröffentlichung, Speichern ist dort gesperrt.

**Einmalig einrichten:** Repo → Settings → Pages → *Source: GitHub Actions*.
Danach veröffentlicht die Action **Pflege-Oberfläche**
(`.github/workflows/admin.yml`) die Seite bei jeder Änderung an
`src/EnglishSteamTrainer.Admin`, `src/EnglishSteamTrainer.Content` oder `content/`.
Lokal starten: `dotnet run --project src/EnglishSteamTrainer.Admin`.

## Wann holen sich die PCs was?

| Was | Wer prüft | Wann | Wirksam |
|---|---|---|---|
| `content/` (Vokabeln, Grammatik, Lernplan, Sperrliste, Sprachzuordnung) | Watchdog (SYSTEM) | beim Start und alle 30 Minuten | Sperrliste nach ≤ 15 s; Vokabeln und Sprache beim nächsten Start der Lern-App (= nächste Anmeldung) |
| Neue Programmversion (Release) | Aufgabe `EnglishSteamTrainer-Update` | 3 Min. nach dem Hochfahren, danach alle 6 Stunden | sofort; offene Lern-App wird neu gestartet |

GitHub liefert geänderte Dateien bis zu ~5 Minuten verzögert aus (Cache).

Sofort abgleichen statt warten (PowerShell als Admin auf dem Kind-PC):

```powershell
Start-ScheduledTask EnglishSteamTrainer-Update      # neue Version jetzt suchen
Stop-ScheduledTask  EnglishSteamTrainer-Watchdog    # Watchdog neu starten =
Start-ScheduledTask EnglishSteamTrainer-Watchdog    #   Inhalte jetzt laden
```

Kontrolle: `C:\ProgramData\EnglishSteamTrainer\logs\watchdog-<PC-Name>$.log`
(der SYSTEM-Watchdog heißt nach dem Computerkonto, z. B. `watchdog-DESKTOP-NO7VIKD$.log`)
(„Online-Inhalte aktualisiert …“, „Gesperrte Konten: tiago (Englisch, zentral)“)
und `logs\update.log`. Die Fußzeile der App zeigt Version, Sprache samt Herkunft
und den Stand der Inhalte.

## Sprache und Aufgabenzahl pro Kind

In `content/config.json`:

```json
"requiredCorrectAnswers": 15,
"users": [
  { "computer": "DESKTOP-NO7VIKD", "account": "tiago", "language": "en", "requiredCorrectAnswers": 15 },
  { "computer": "LAPTOP-LEANDRO",  "account": "leand", "language": "es", "requiredCorrectAnswers": 30 },
  { "computer": "*",               "account": "max",   "language": "es" }
]
```

- `computer` = PC-Name (Befehl `hostname`; `Install.ps1` gibt die passende
  Zeile am Ende aus). `"*"` oder weglassen = auf jedem PC.
- `account` = Windows-Kontoname (nicht der Anzeigename, siehe `Get-LocalUser`).
- `language`: `en` (Englisch) oder `es` (Spanisch). Fehlt sie, gilt die bei
  `Install.ps1` gewählte Sprache.
- `requiredCorrectAnswers` (1–200): Fehlt sie, gilt der allgemeine Wert oben.
  Wirkt sofort (Watchdog prüft alle 15 s; die App beim nächsten Start).
- Je Einstellung gewinnt ein Eintrag für genau diesen PC vor `"*"`.
- Ein Tippfehler (z. B. `"spanish"` oder `0` Antworten) lässt die ganze Datei
  durchfallen – CI wird rot, die PCs behalten den alten Stand.
- Die Liste legt **nicht** fest, wer gesperrt ist. Gesperrt (und mit Autostart
  versehen) wird ein Konto erst durch `Install.ps1` auf dem jeweiligen PC – ein
  Eintrag hier allein sperrt niemanden, das Entfernen hebt keine Sperre auf.

## Vokabeln und Grammatik – Format

`content/<sprache>/vocabulary.csv` – Trennzeichen `;`, Alternativen mit `|`:

```
Word;German;Hint;GermanAlternatives;WordAlternatives;Topics;Id
perro;Hund;Bellt und wedelt mit dem Schwanz;;;Tiere
coche;Auto;Hat vier Räder;Wagen;carro;Zuhause & Schule
queso;Käse;Wird aus Milch gemacht;;;Essen|Party-Essen
bunny;Hase;Hat lange Ohren;;;Tiere;rabbit
```

`Topics` (optional, mehrere mit `|`) ordnet ein Wort Themen zu, siehe
„Lernplan“. `Id` bleibt normalerweise leer. Nur wenn ein Wort umbenannt wird,
steht dort der alte Text – unter dem führt das Lernjournal das Wort weiter
(Karteikasten, Wiederholungen). Die Pflege-Oberfläche setzt das automatisch.

`content/<sprache>/grammar.csv` – genau **drei** falsche Antworten. `Topic`
wird bei der Frage angezeigt und ist zugleich das Thema im Lernplan:

```
Subject;Verb;Correct;Distractors;Topic
yo;hablar;hablo;hablas|habla|hablamos;Presente
```

`content/en/verbs.csv` – unregelmäßige Verben (nur Englisch). Mehrere richtige
Schreibweisen mit `|`, die erste wird angezeigt:

```
Infinitive;SimplePast;PastParticiple;German;Topics
be;was|were;been;sein
get;got;got|gotten;bekommen;Unit 3
```

`Topics` ist optional (mehrere mit `|`), wie bei den Vokabeln.

Abgefragt wird: eine der drei Formen und die deutsche Bedeutung sind
vorgegeben, die anderen beiden Formen tippt das Kind ein. Nur wenn beide
stimmen, zählt die Aufgabe. Neue Verben = neue Zeile, kein Release nötig.
Wie viele Verben pro Runde: `verbFormQuestionsPerSession` in `config.json`
(Standard 5, `0` = aus).

Groß-/Kleinschreibung, Akzente (`adios` = `adiós`), Umlaute (`gruen` = `grün`)
und Artikel (`der Hund`, `el perro`) spielen bei der Prüfung keine Rolle. Ein
Tippfehler wird erst ab 5 Buchstaben verziehen (gibt dann weniger Punkte).

Nach jedem Push prüft die GitHub-Action **CI** alle Dateien in `content/`
(Spaltenzahl, Duplikate, Pflichtfelder). Roter Haken = Datei prüfen.

## Lernplan

`content/plan.json` legt fest, welche Themen wann dran sind – für alle
Kinder, eine Sprache oder einzelne Kinder (am einfachsten in der
Pflege-Oberfläche unter „Lernplan“):

```json
{
  "holdBackPlannedTopics": true,
  "schedule": [
    { "from": "2026-11-02", "to": "2026-11-08", "language": "es", "topics": [ "Party-Essen" ] },
    { "from": "2026-11-09", "to": "2026-11-15", "accounts": [ "tiago" ], "title": "Unit 3", "topics": [ "Schule", "Present Progressive" ] }
  ]
}
```

- `from`/`to`: erster und letzter Tag (einschließlich), Format `JJJJ-MM-TT`.
- `topics`: ein oder mehrere Themen. Zählen tun Vokabeln (Spalte `Topics`),
  Grammatik (Spalte `Topic`) und Verben (Spalte `Topics`); Groß-/Kleinschreibung
  egal. CI und Oberfläche prüfen, dass es zu jedem Thema Aufgaben gibt.
- `language` (`en`/`es`) und `accounts` (Windows-Kontonamen) sind optional.
  Fehlen beide, gilt der Eintrag für alle Kinder.
- `title` (optional) wird in der App statt der Themenliste angezeigt.
- Gelten an einem Tag mehrere Einträge, gewinnt der für ein Kind vor dem für
  eine Sprache vor dem für alle, sonst der erste in der Datei.
- Im Zeitraum eines Eintrags kommen `focusSharePercent` (config.json, 1–100,
  Standard 70) Prozent der neuen und fälligen Aufgaben aus seinen Themen. Die
  App zeigt oben „🎯 Thema der Woche: …“ und markiert die Aufgaben dazu.
- Grammatik und Verben, die zum Thema gehören, sind dabei **nicht** auf
  `grammarQuestionsPerSession`/`verbFormQuestionsPerSession` begrenzt – eine
  „Woche der unregelmäßigen Verben“ besteht also wirklich zu ~70 % aus Verben.
  Die Grenzen gelten nur für Grammatik und Verben außerhalb des Themas.
  (Ab 1.4.1; 1.4.0 begrenzt auch Themen-Verben.)
- `holdBackPlannedTopics` (Standard `true`): Themen, die erst **später** im
  Plan stehen, werden vorher nicht abgefragt. So kann man den Wortschatz für
  das ganze Schuljahr schon eintragen. Themen, die gar nicht im Plan stehen,
  und Aufgaben ohne Thema sind immer dabei. Hat eine Aufgabe mehrere Themen,
  reicht ein begonnenes oder ungeplantes, damit sie dabei ist.
- `focusTopics` in `config.json` (Version 1.3) gilt weiterhin, neue Einträge
  gehören in `plan.json`.
- Ältere Programmversionen (vor 1.4.0) lesen `plan.json` nicht und üben alles gemischt.

## Karteikasten (Wiederholung)

Welche Aufgabe wann wiederkommt, berechnet die App aus dem Lernjournal
(Leitner-Prinzip, je Vokabel, Grammatik-Frage und Verb):

| Fach | wieder fällig nach |
|---|---|
| 1 | 1 Tag |
| 2 | 3 Tagen |
| 3 | 7 Tagen |
| 4 | 14 Tagen |
| 5 | 30 Tagen |

- Richtig, als die Karte fällig (oder neu) war → ein Fach weiter. Tippfehler →
  bleibt im Fach. Falsch → Fach 1, kommt bei der nächsten Runde zuerst.
- Eine Karte, die nicht fällig war, als Lückenfüller richtig zu beantworten,
  ändert nichts – so springt keine Karte an einem Tag mehrere Fächer weiter.
- Reihenfolge einer Runde: zuletzt falsch beantwortete Aufgaben → fällige
  Wiederholungen und neue Aufgaben (davon der Anteil aus dem Lernplan-Thema
  zuerst, Grammatik und Verben dazwischen) → übrige Vokabeln als Lückenfüller,
  die am frühesten fälligen zuerst.
- Ab Fach 3 gilt eine Aufgabe als „sicher“ – der Fortschrittsbericht zeigt
  das je Lernplan-Thema.
- Es braucht keine Umstellung: Die Fächer ergeben sich aus dem vorhandenen
  Lernjournal.

## Entwicklermodus (Entwickler / Eltern)

Gilt **automatisch** für jedes Konto, das nicht gesperrt ist – also auf
Entwickler-PCs ohne Installation und für Eltern-Konten auf Kind-PCs. Die App
zeigt dann „🛠 ENTWICKLERMODUS“ in der Kopfzeile.

```powershell
dotnet run --project src/EnglishSteamTrainer.UI                       # aus dem Repo (oder F5)
dotnet run --project src/EnglishSteamTrainer.UI -- --only verbs --language es
& "C:\ProgramData\EnglishSteamTrainer\EnglishSteamTrainer.UI.exe"     # Eltern-Konto auf einem Kind-PC
```

- Es wird **kein Watchdog** gestartet – Chrome, Edge, Steam usw. des eigenen
  Kontos bleiben unberührt. Wird `EnglishSteamTrainer.Watchdog.exe` direkt
  gestartet, beendet er sich mit einem Hinweis im Log.
- Das Fenster lässt sich jederzeit schließen. Antworten landen in einem
  Wegwerf-Journal (wird beim Beenden gelöscht), Punkte, Verlauf und
  Tagesfortschritt bleiben unverändert, nichts wird freigeschaltet.
- **„Lernen als“** (Auswahl oben rechts): lernt wie eines der Kinder aus
  `config.json` (`users`) – mit dessen Sprache, Lernplan/Thema der Woche und
  Anzahl Aufgaben. Jeder Wechsel startet mit einem frischen Wegwerf-Journal.
  Gleich beim Start: `--als tiago`.
- `--language es`: andere Sprache. `--only vocab|grammar|verbs`: nur eine Art
  von Aufgaben. `--test` ist weiterhin erlaubt, aber nicht mehr nötig.
- **`--kindmodus`**: echtes Verhalten für das eigene Konto, z. B. um die Sperre
  zu testen. Der Ersatz-Watchdog beendet dann sofort Steam, Chrome, Edge und
  Discord dieses Kontos, bis das Tagesziel erreicht ist, und der Fortschritt
  zählt. Beenden: Tagesziel lösen oder `Stop-Process -Name EnglishSteamTrainer.Watchdog`.
- Gesperrte Konten (in `settings.json`) bekommen immer die normale App,
  `--test` zeigt dort nur einen Hinweis. Ist `settings.json` unlesbar, gilt das
  Konto als gesperrt.

## Gesperrte Programme pflegen

`content/config.json`:

```json
{
  "requiredCorrectAnswers": 15,
  "grammarQuestionsPerSession": 5,
  "verbFormQuestionsPerSession": 5,
  "blockedApps": [
    { "name": "Steam", "executables": [ "steam.exe", "steamwebhelper.exe" ] },
    { "name": "Firefox", "executables": [ "firefox.exe" ] }
  ]
}
```

Umbenannte Kopien (z. B. `steam.exe` → `spiel.exe`) werden über die
Versionsinfo der EXE trotzdem erkannt. Für neue Programme ohne eigenes Icon
(`src/EnglishSteamTrainer.UI/Icons/<name>.png`) zeigt die App den Namen an.

## Punkte – nachvollziehbar statt willkürlich

Jede Antwort wird mit Zeit, Aufgabe, eingegebener Antwort, Ergebnis und den
dafür vergebenen XP im **Lernjournal** gespeichert
(`C:\ProgramData\EnglishSteamTrainer\unlocks\<SID>.journal.txt`). XP, Level,
Serien und der Tagesfortschritt werden ausschließlich daraus berechnet.

| Regel | XP |
|---|---|
| Richtige Antwort | +10 |
| Richtig mit kleinem Tippfehler | +5 |
| Jede 5. richtige Antwort in Folge | +5 Bonus |
| Falsch | 0, Serie beginnt neu |
| Überspringen / Tipp | 0, kein Abzug |
| Level | alle 100 XP |

In der App zeigt **„📊 Verlauf & Punkte“** jede einzelne Antwort mit laufender
Summe, eine Tagesübersicht (Trefferquote, Tagesziel erreicht?) und die
schwierigsten Wörter. Wörter, deren letzter Versuch falsch war, kommen beim
nächsten Mal zuerst dran.

Jede Journalzeile ist mit der vorherigen verkettet signiert (HMAC). Wird eine
Zeile von Hand geändert oder gelöscht, zählt ab dort nichts mehr, die App zeigt
einen Hinweis, und die veränderte Datei wird als `*.manipuliert-*.txt`
aufgehoben. (Der Schlüssel steht im Programm – das schützt gegen Bearbeiten im
Editor, nicht gegen jemanden, der den Code liest.)

## Fortschrittsbericht ansehen (vom Entwickler-/Admin-PC)

Auf Wunsch lädt der Watchdog pro Kind-Konto einen Bericht in den Branch
`berichte` dieses Repos:
`https://github.com/sebastianhockmann/karteikarten/blob/berichte/<PC-Name>/<konto>.md`.
Das Repo ist **öffentlich**, die Berichte (PC-Name, Kontoname, Lerntage) kann
also jeder lesen. `main`, CI und Releases bleiben davon unberührt.
GitHub zeigt darin eine Grafik der richtigen Antworten pro Tag (letzte 30 Tage,
Linie = Tagesziel) und eine Tabelle mit richtig/falsch/Tagesziel je Tag. Tage
ohne Antworten stehen als „nicht gelernt“ drin. Darunter steht je
Lernplan-Thema (letzte 3 Monate und nächste 4 Wochen), wie viele Aufgaben schon
geübt und wie viele „sicher“ sind (Karteikasten-Fach 3+). Hochgeladen wird nur, wenn sich
etwas geändert hat (neue Antworten oder ein neuer Tag), also höchstens alle 30
Minuten. Der Verlauf des Repos zeigt daher auch, wann der PC zuletzt online war.

Einmalig einrichten:

1. Der Branch `berichte` existiert bereits (ohne Programmcode, nur Berichte).
2. Settings → Developer settings → **Fine-grained personal access token**:
   Repository access *Only select repositories* → `karteikarten`,
   Permissions → *Contents: Read and write*, Ablaufdatum notieren.
3. Auf jedem Kind-PC (PowerShell als Administrator, Programm bereits installiert):
   ```powershell
   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/sebastianhockmann/karteikarten/main/scripts/Install.ps1))) -ReportToken github_pat_...
   ```
   Anderes Repo: zusätzlich `-ReportRepository besitzer/name`.

Das Token liegt in `C:\ProgramData\EnglishSteamTrainer\report-upload.json`, die
nur SYSTEM und Admins lesen dürfen (anders als `settings.json`). **Achtung:**
GitHub kann das Schreibrecht nicht auf einen Branch beschränken. Wer das Token
hat, kann auch `main` ändern, Tags setzen und Releases anlegen, die alle PCs
automatisch installieren. Bei Verdacht sofort auf GitHub widerrufen.
**Token abgelaufen:** Im Watchdog-Log steht dann
„Fortschrittsbericht nicht hochgeladen: HTTP 401 …“. Neues Token erstellen und
Schritt 3 wiederholen. **Ausschalten:** `report-upload.json` löschen.

## Neue Version veröffentlichen

1. `<Version>` in `Directory.Build.props` hochzählen, committen, pushen.
2. Tag setzen und pushen:
   ```powershell
   git tag v1.1.0
   git push origin v1.1.0
   ```
3. Die GitHub-Action **Release** testet, baut ein eigenständiges Paket (kein
   .NET auf den PCs nötig) und legt das Release mit ZIP + SHA256 an.
4. Die PCs installieren es beim nächsten Hochfahren bzw. innerhalb von 6
   Stunden. Ist die Lern-App gerade offen, wird sie kurz beendet und neu
   gestartet (Fortschritt ist pro Antwort gespeichert). Schlägt das Kopieren
   fehl, wird die vorherige Version zurückgespielt.
   Log: `C:\ProgramData\EnglishSteamTrainer\logs\update.log`.
   Sofort aktualisieren (als Admin): `C:\ProgramData\EnglishSteamTrainer\Update.ps1`.

## Projekte

- `src/EnglishSteamTrainer.UI` – die Lern-App (WPF).
- `src/EnglishSteamTrainer.Content` – plattformunabhängig (auch im Browser):
  Inhalte lesen/schreiben/prüfen, Lernplan, Karteikasten, Zusammenstellung
  der Aufgaben (`QuizPlanner`), Antwortprüfung, XP-Regeln.
- `src/EnglishSteamTrainer.Core` – Windows-Teil: Inhalte laden/abgleichen,
  Lernjournal, Freischaltung, Programmsperre, Berichte.
- `src/EnglishSteamTrainer.Admin` – Pflege-Oberfläche (Blazor WebAssembly,
  GitHub Pages).
- `src/EnglishSteamTrainer.Watchdog` – läuft als SYSTEM, beendet gesperrte
  Programme der eingetragenen Konten (jede Sekunde) und lädt Online-Inhalte.
- `tests/EnglishSteamTrainer.Core.Tests` – Unit-Tests inkl. Prüfung von `content/`.
- `scripts/Install.ps1` – Installation/Einrichtung/Entfernen.
- `scripts/Update.ps1` – automatisches Update (wird mitinstalliert).
- `scripts/Build-Package.ps1` – baut das Installationspaket (CI und lokal).

## Entwicklung

```powershell
dotnet build EnglishSteamTrainer.sln
dotnet test tests/EnglishSteamTrainer.Core.Tests
```

UI starten: `EnglishSteamTrainer.UI` als Startprojekt (F5) – läuft im
Entwicklermodus (siehe oben), sperrt also nichts. Spanisch testen mit dem
Startargument `--language es`. Nur mit `--kindmodus` startet der Ersatz-Watchdog
und beendet Steam, Chrome, Edge und Discord des eigenen Kontos.

Lokalen Stand auf diesem PC installieren statt des GitHub-Release (als Admin):

```powershell
scripts\Install.ps1 -TargetUser tiago -Language en -FromSource
```

Lokale Builds tragen die Version aus `Directory.Build.props`; der Updater
ersetzt sie erst, wenn auf GitHub eine höhere Version erscheint.
