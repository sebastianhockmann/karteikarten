# English Steam Trainer

Lernprogramm (C# / WPF, .NET 10) für Windows: Erst wenn heute genug Aufgaben
richtig beantwortet sind (Standard: 15), werden Steam, Browser & Co. für das
Kind-Konto freigeschaltet. Lernsprache pro Konto: **Englisch** oder **Spanisch**.

## Überblick

| Was | Wo gepflegt | Wie kommt es auf die PCs |
|---|---|---|
| Programmversion | dieses Repo, Git-Tag `v1.2.3` | GitHub-Release → Aufgabe `EnglishSteamTrainer-Update` (beim Hochfahren + alle 6 h) |
| Vokabeln, Grammatik | `content/<sprache>/*.csv` im Repo | Watchdog lädt alle 30 Min. von GitHub in einen Cache |
| Gesperrte Programme, Anzahl Aufgaben | `content/config.json` im Repo | wie Vokabeln |
| Konto → Sprache (zentral, hat Vorrang) | `content/config.json` → `users` | wie Vokabeln |
| Welche Konten gesperrt sind, Sprache als Rückfall | `settings.json` auf dem PC | `scripts\Install.ps1` |
| Lernverlauf, Punkte | signiertes Lernjournal pro Konto auf dem PC | – |

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

## Inhalte pflegen (vom Entwickler-PC oder auf GitHub)

Alles in `content/` ist zentral für alle PCs. Entweder direkt auf GitHub
bearbeiten (Stift-Symbol) oder auf dem Entwickler-PC:

```powershell
# content\... bearbeiten, dann prüfen (testet u. a. alle CSV-Dateien und config.json):
dotnet test tests/EnglishSteamTrainer.Core.Tests
git add content
git commit -m "Neue Vokabeln"
git push origin main
```

Ein neues Release ist dafür **nicht** nötig.

## Wann holen sich die PCs was?

| Was | Wer prüft | Wann | Wirksam |
|---|---|---|---|
| `content/` (Vokabeln, Grammatik, Sperrliste, Sprachzuordnung) | Watchdog (SYSTEM) | beim Start und alle 30 Minuten | Sperrliste nach ≤ 15 s; Vokabeln und Sprache beim nächsten Start der Lern-App (= nächste Anmeldung) |
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
Word;German;Hint;GermanAlternatives;WordAlternatives
perro;Hund;Bellt und wedelt mit dem Schwanz;
coche;Auto;Hat vier Räder;Wagen;carro
```

`content/<sprache>/grammar.csv` – genau **drei** falsche Antworten:

```
Subject;Verb;Correct;Distractors;Topic
yo;hablar;hablo;hablas|habla|hablamos;Presente
```

Groß-/Kleinschreibung, Akzente (`adios` = `adiós`), Umlaute (`gruen` = `grün`)
und Artikel (`der Hund`, `el perro`) spielen bei der Prüfung keine Rolle. Ein
Tippfehler wird erst ab 5 Buchstaben verziehen (gibt dann weniger Punkte).

Nach jedem Push prüft die GitHub-Action **CI** alle Dateien in `content/`
(Spaltenzahl, Duplikate, Pflichtfelder). Roter Haken = Datei prüfen.

## Gesperrte Programme pflegen

`content/config.json`:

```json
{
  "requiredCorrectAnswers": 15,
  "grammarQuestionsPerSession": 5,
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
- `src/EnglishSteamTrainer.Core` – Logik: Inhalte laden/abgleichen,
  Antwortprüfung, Lernjournal, XP-Regeln, Freischaltung, Programmsperre.
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

UI starten: `EnglishSteamTrainer.UI` als Startprojekt (F5). Spanisch testen
mit dem Startargument `--language es` (gilt nur für Konten, die nicht in
`settings.json` stehen).

**Achtung:** Die UI startet einen Ersatz-Watchdog für das eigene Konto, wenn
die Watchdog-EXE daneben liegt – der beendet sofort Steam, Chrome, Edge und
Discord des eigenen Kontos. Vorher Browser-Arbeit speichern.

Lokalen Stand auf diesem PC installieren statt des GitHub-Release (als Admin):

```powershell
scripts\Install.ps1 -TargetUser tiago -Language en -FromSource
```

Lokale Builds tragen die Version aus `Directory.Build.props`; der Updater
ersetzt sie erst, wenn auf GitHub eine höhere Version erscheint.
