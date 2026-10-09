<#
.SYNOPSIS
    Installiert English Steam Trainer auf diesem PC (oder aktualisiert ihn) und
    richtet ihn fuer ein Kind-Konto mit einer Lernsprache ein.

    - Laedt die neueste Version von GitHub (Releases) - oder baut sie mit
      -FromSource aus diesem Repo.
    - Installiert nach C:\ProgramData\EnglishSteamTrainer (fuer alle lesbar,
      nur fuer Admins beschreibbar - das Kind kann nichts umkonfigurieren).
    - Traegt Konto + Sprache in settings.json ein. Mehrere Konten pro PC sind
      moeglich (einfach mehrfach ausfuehren), jedes mit eigener Sprache.
    - Richtet drei Aufgaben ein:
        EnglishSteamTrainer-App-<Konto>  startet die Lern-App bei der Anmeldung
        EnglishSteamTrainer-Watchdog     sperrt Programme, laedt Online-Inhalte (SYSTEM)
        EnglishSteamTrainer-Update       installiert neue Versionen von GitHub (SYSTEM)

    Neuer PC, ohne das Repo herunterzuladen (PowerShell als Administrator):

        irm https://raw.githubusercontent.com/sebastianhockmann/karteikarten/main/scripts/Install.ps1 | iex

    oder mit Parametern ohne Rueckfragen:

        & ([scriptblock]::Create((irm https://raw.githubusercontent.com/sebastianhockmann/karteikarten/main/scripts/Install.ps1))) -TargetUser tiago -Language en

.EXAMPLE
    .\Install.ps1
    Fragt Konto und Sprache ab, installiert die neueste Version von GitHub.

.EXAMPLE
    .\Install.ps1 -TargetUser lena -Language es
    Richtet das Konto "lena" mit Spanisch ein.

.EXAMPLE
    .\Install.ps1 -TargetUser tiago -Language en -FromSource
    Baut die App aus dem lokalen Repo statt sie von GitHub zu laden (Entwicklung).

.EXAMPLE
    .\Install.ps1 -TargetUser tiago -Uninstall
    Hebt die Sperre fuer "tiago" auf und entfernt seinen Autostart. Ist danach
    kein Konto mehr eingetragen, werden auch Watchdog und Updater entfernt;
    mit -RemoveFiles zusaetzlich der Programmordner samt Lernverlauf.
#>
[CmdletBinding()]
param(
    [string]$TargetUser,
    [string]$Language,
    [switch]$FromSource,
    [string]$Repository = "sebastianhockmann/karteikarten",
    [switch]$Uninstall,
    [switch]$RemoveFiles
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$InstallDir = Join-Path $env:ProgramData "EnglishSteamTrainer"
$settingsFile = Join-Path $InstallDir "settings.json"
$legacyUsersFile = Join-Path $InstallDir "blocked-users.txt"
$watchdogTaskName = "EnglishSteamTrainer-Watchdog"
$updateTaskName = "EnglishSteamTrainer-Update"
$packageName = "EnglishSteamTrainer-win-x64.zip"
$languageNames = [ordered]@{ "en" = "Englisch"; "es" = "Spanisch" }

function Get-AppTaskName([string]$user) {
    return "EnglishSteamTrainer-App-$user"
}

function Assert-Admin {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())

    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Bitte als Administrator ausfuehren (Rechtsklick auf PowerShell > 'Als Administrator ausfuehren')."
    }
}

function Read-Settings {
    $settings = [ordered]@{
        repository     = $Repository
        contentBaseUrl = "https://raw.githubusercontent.com/$Repository/main/content/"
        users          = New-Object System.Collections.Generic.List[object]
    }

    if (Test-Path $settingsFile) {
        $existing = Get-Content $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json

        if ($existing.repository) { $settings.repository = $existing.repository }
        if ($existing.contentBaseUrl) { $settings.contentBaseUrl = $existing.contentBaseUrl }

        foreach ($user in @($existing.users)) {
            if ($user -and $user.account) {
                $settings.users.Add([ordered]@{ account = [string]$user.account; language = [string]$user.language })
            }
        }
    }
    elseif (Test-Path $legacyUsersFile) {
        # Vorgaenger-Version: ein Kontoname pro Zeile, immer Englisch.
        foreach ($line in Get-Content $legacyUsersFile) {
            $name = $line.Trim()
            if ($name -and -not $name.StartsWith("#")) {
                $settings.users.Add([ordered]@{ account = $name; language = "en" })
            }
        }
    }

    return $settings
}

function Write-Settings($settings) {
    $json = ConvertTo-Json -InputObject $settings -Depth 5
    [IO.File]::WriteAllText($settingsFile, $json, (New-Object Text.UTF8Encoding($false)))
    Remove-Item $legacyUsersFile -ErrorAction SilentlyContinue
}

function Find-SettingsUser($settings, [string]$user) {
    foreach ($entry in $settings.users) {
        if ($entry.account -eq $user) { return $entry }
    }
    return $null
}

function Select-TargetUser {
    $accounts = @(Get-LocalUser | Where-Object { $_.Enabled } | Sort-Object Name)

    if ($accounts.Count -eq 0) {
        throw "Keine aktivierten lokalen Windows-Konten auf diesem PC gefunden."
    }

    Write-Host ""
    Write-Host "Fuer welches Windows-Konto soll gelernt werden?"
    for ($i = 0; $i -lt $accounts.Count; $i++) {
        Write-Host "  [$($i + 1)] $($accounts[$i].Name)"
    }
    Write-Host ""

    $selection = Read-Host "Nummer eingeben"
    $index = 0

    if (-not [int]::TryParse($selection, [ref]$index) -or $index -lt 1 -or $index -gt $accounts.Count) {
        throw "Ungueltige Auswahl: '$selection'"
    }

    return $accounts[$index - 1].Name
}

function Select-Language([string]$current) {
    $codes = @($languageNames.Keys)

    Write-Host ""
    Write-Host "Welche Sprache soll '$TargetUser' lernen?"
    for ($i = 0; $i -lt $codes.Count; $i++) {
        $marker = if ($codes[$i] -eq $current) { "  (bisher)" } else { "" }
        Write-Host "  [$($i + 1)] $($languageNames[$codes[$i]])$marker"
    }
    Write-Host ""

    $selection = Read-Host "Nummer eingeben"

    if (-not $selection -and $current) {
        return $current
    }

    $index = 0
    if (-not [int]::TryParse($selection, [ref]$index) -or $index -lt 1 -or $index -gt $codes.Count) {
        throw "Ungueltige Auswahl: '$selection'"
    }

    return $codes[$index - 1]
}

function Get-ReleasePackage {
    $headers = @{ "User-Agent" = "EnglishSteamTrainer-Installer" }
    $release = Invoke-RestMethod "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    $zipAsset = $release.assets | Where-Object { $_.name -eq $packageName } | Select-Object -First 1
    $hashAsset = $release.assets | Where-Object { $_.name -eq "$packageName.sha256" } | Select-Object -First 1

    if (-not $zipAsset -or -not $hashAsset) {
        throw "Release $($release.tag_name) enthaelt kein $packageName (+ .sha256)."
    }

    $workDir = Join-Path $env:TEMP ("EnglishSteamTrainer-" + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $workDir | Out-Null
    $zipPath = Join-Path $workDir $packageName

    Write-Host "Lade Version $($release.tag_name) von GitHub ..."
    Invoke-WebRequest $zipAsset.browser_download_url -OutFile $zipPath -Headers $headers -UseBasicParsing
    $expected = (Invoke-WebRequest $hashAsset.browser_download_url -Headers $headers -UseBasicParsing).Content
    if ($expected -is [byte[]]) { $expected = [Text.Encoding]::ASCII.GetString($expected) }
    $expected = $expected.Trim().ToLowerInvariant()
    $actual = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()

    if ($actual -ne $expected) {
        throw "Pruefsumme stimmt nicht - Download beschaedigt oder manipuliert. Abbruch."
    }

    $packageDir = Join-Path $workDir "package"
    Expand-Archive -Path $zipPath -DestinationPath $packageDir
    return $packageDir
}

function Get-SourcePackage {
    if (-not $PSScriptRoot) {
        throw "-FromSource geht nur, wenn das Skript aus dem Repo heraus gestartet wird."
    }

    $packageDir = Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\package"
    & (Join-Path $PSScriptRoot "Build-Package.ps1") -OutputDir $packageDir | Out-Host
    return $packageDir
}

function Stop-TrainerProcesses {
    if (Get-ScheduledTask -TaskName $watchdogTaskName -ErrorAction SilentlyContinue) {
        Stop-ScheduledTask -TaskName $watchdogTaskName -ErrorAction SilentlyContinue
    }

    Get-Process -Name "EnglishSteamTrainer.Watchdog", "EnglishSteamTrainer.UI" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
}

function Remove-LegacyTasks([string]$user) {
    # Aufgabennamen frueherer Versionen (Deploy.ps1 / Install-Autostart.ps1).
    foreach ($name in @("EnglishSteamTrainer-$user", "EnglishSteamTrainer")) {
        if (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue) {
            Unregister-ScheduledTask -TaskName $name -Confirm:$false
        }
    }
}

function Register-SystemTasks {
    $watchdogPath = Join-Path $InstallDir "EnglishSteamTrainer.Watchdog.exe"
    $systemPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

    # Startet beim Hochfahren, bei jeder Anmeldung und sicherheitshalber alle 5
    # Minuten (laeuft er schon, passiert nichts). Kein Zeitlimit.
    $watchdogTriggers = @(
        New-ScheduledTaskTrigger -AtStartup
        New-ScheduledTaskTrigger -AtLogOn
        New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 5)
    )
    $watchdogSettings = New-ScheduledTaskSettingsSet `
        -ExecutionTimeLimit ([TimeSpan]::Zero) `
        -RestartCount 999 `
        -RestartInterval (New-TimeSpan -Minutes 1) `
        -MultipleInstances IgnoreNew `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable

    Register-ScheduledTask -TaskName $watchdogTaskName -Force `
        -Action (New-ScheduledTaskAction -Execute $watchdogPath -WorkingDirectory $InstallDir) `
        -Trigger $watchdogTriggers `
        -Principal $systemPrincipal `
        -Settings $watchdogSettings | Out-Null

    # Updater: kurz nach dem Hochfahren und danach alle 6 Stunden.
    $startupTrigger = New-ScheduledTaskTrigger -AtStartup
    $startupTrigger.Delay = "PT3M"
    $updateTriggers = @(
        $startupTrigger
        New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(30) -RepetitionInterval (New-TimeSpan -Hours 6)
    )
    $updateSettings = New-ScheduledTaskSettingsSet `
        -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
        -MultipleInstances IgnoreNew `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable
    $updateAction = New-ScheduledTaskAction -Execute "powershell.exe" `
        -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$(Join-Path $InstallDir 'Update.ps1')`"" `
        -WorkingDirectory $InstallDir

    Register-ScheduledTask -TaskName $updateTaskName -Force `
        -Action $updateAction `
        -Trigger $updateTriggers `
        -Principal $systemPrincipal `
        -Settings $updateSettings | Out-Null
}

function Register-AppTask([string]$user) {
    $qualifiedUser = "$env:COMPUTERNAME\$user"
    $exePath = Join-Path $InstallDir "EnglishSteamTrainer.UI.exe"

    # Interactive = laeuft sichtbar in der Sitzung des Kontos, sobald es sich
    # anmeldet. Dafuer wird sein Passwort nicht benoetigt.
    Register-ScheduledTask -TaskName (Get-AppTaskName $user) -Force `
        -Action (New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $InstallDir) `
        -Trigger (New-ScheduledTaskTrigger -AtLogOn -User $qualifiedUser) `
        -Principal (New-ScheduledTaskPrincipal -UserId $qualifiedUser -LogonType Interactive -RunLevel Limited) `
        -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries) | Out-Null
}

function Set-InstallPermissions {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

    # Der Watchdog laeuft als SYSTEM aus diesem Ordner: normale Benutzer duerfen hier
    # nichts anlegen oder aendern (sonst koennten sie z. B. eine DLL unterschieben,
    # settings.json aendern oder den Online-Cache faelschen). Nur in "unlocks"
    # (Tagesfortschritt, Lernjournal) und "logs" duerfen sie eigene Dateien anlegen.
    # SIDs: S-1-5-18 = SYSTEM, S-1-5-32-544 = Administratoren, S-1-5-32-545 = Benutzer,
    # S-1-3-0 = Ersteller-Besitzer.
    icacls $InstallDir /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)RX" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Konnte die Berechtigungen fuer $InstallDir nicht setzen."
    }

    foreach ($dataDir in @("unlocks", "logs")) {
        $path = Join-Path $InstallDir $dataDir
        New-Item -ItemType Directory -Force -Path $path | Out-Null
        icacls $path /grant "*S-1-5-32-545:(CI)(WD,AD)" "*S-1-3-0:(OI)(CI)(IO)F" | Out-Null
    }
}

function Install-Package([string]$packageDir) {
    foreach ($requiredFile in @("EnglishSteamTrainer.UI.exe", "EnglishSteamTrainer.Watchdog.exe", "Update.ps1", "version.txt")) {
        if (-not (Test-Path (Join-Path $packageDir $requiredFile))) {
            throw "Paket unvollstaendig, es fehlt: $requiredFile"
        }
    }

    Copy-Item -Path (Join-Path $packageDir "*") -Destination $InstallDir -Recurse -Force

    # Reste der alten Version (Inhalte liegen jetzt unter content\).
    foreach ($legacyFile in @("vocabulary.csv", "tense_questions.csv")) {
        Remove-Item (Join-Path $InstallDir $legacyFile) -ErrorAction SilentlyContinue
    }
}

Assert-Admin

if ([string]::IsNullOrWhiteSpace($TargetUser)) {
    $TargetUser = Select-TargetUser
}

if (-not (Get-LocalUser -Name $TargetUser -ErrorAction SilentlyContinue)) {
    $available = (Get-LocalUser | Select-Object -ExpandProperty Name) -join ", "
    throw "Windows-Konto '$TargetUser' wurde auf diesem PC nicht gefunden. Vorhandene Konten: $available"
}

$settings = Read-Settings
$existingUser = Find-SettingsUser $settings $TargetUser

if ($Uninstall) {
    if ($existingUser) { [void]$settings.users.Remove($existingUser) }

    foreach ($name in @((Get-AppTaskName $TargetUser), "EnglishSteamTrainer-$TargetUser")) {
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
    }

    if ($settings.users.Count -gt 0) {
        Write-Settings $settings
        Write-Host "Sperre und Autostart fuer '$TargetUser' entfernt. Weiterhin eingerichtet: $(($settings.users | ForEach-Object { $_.account }) -join ', ')"
        return
    }

    Stop-TrainerProcesses
    foreach ($name in @($watchdogTaskName, $updateTaskName)) {
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
    }

    if ($RemoveFiles) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Host "Alles entfernt, inklusive $InstallDir (Lernverlauf geloescht)."
    }
    else {
        if (Test-Path $InstallDir) { Write-Settings $settings }
        Write-Host "Kein Konto mehr eingerichtet: Watchdog und Updater entfernt. Programmdateien und Lernverlauf bleiben unter $InstallDir (loeschen mit -RemoveFiles)."
    }
    return
}

if ([string]::IsNullOrWhiteSpace($Language)) {
    $Language = Select-Language $(if ($existingUser) { $existingUser.language } else { "" })
}

$Language = $Language.Trim().ToLowerInvariant()
if (-not $languageNames.Contains($Language)) {
    throw "Unbekannte Sprache '$Language'. Moeglich: $($languageNames.Keys -join ', ')"
}

$packageDir = if ($FromSource) { Get-SourcePackage } else { Get-ReleasePackage }
$newVersion = (Get-Content (Join-Path $packageDir "version.txt") -Raw).Trim()

Write-Host "Beende laufende Instanzen (falls vorhanden) ..."
Stop-TrainerProcesses

Write-Host "Installiere Version $newVersion nach $InstallDir ..."
Set-InstallPermissions
Install-Package $packageDir

if ($existingUser) {
    $existingUser.language = $Language
}
else {
    $settings.users.Add([ordered]@{ account = $TargetUser; language = $Language })
}
Write-Settings $settings

Remove-LegacyTasks $TargetUser
Register-SystemTasks
Register-AppTask $TargetUser

Start-ScheduledTask -TaskName $watchdogTaskName
Start-Sleep -Seconds 3
$watchdogRunning = (Get-ScheduledTask -TaskName $watchdogTaskName).State -eq "Running"

Write-Host ""
Write-Host "Fertig! Version $newVersion installiert unter $InstallDir"
Write-Host ""
Write-Host "Eingerichtete Konten auf diesem PC:"
foreach ($user in $settings.users) {
    Write-Host "  - $($user.account): $($languageNames[$user.language])"
}
Write-Host ""
if ($watchdogRunning) {
    Write-Host "Watchdog laeuft. Log: $(Join-Path $InstallDir 'logs\watchdog-*.log')"
}
else {
    Write-Warning "Watchdog laeuft NICHT - bitte Aufgabenplanung '$watchdogTaskName' und Logs pruefen."
}
Write-Host "Updates kommen automatisch (Aufgabe '$updateTaskName', Log: $(Join-Path $InstallDir 'logs\update.log'))."
Write-Host "Weiteres Konto: Skript erneut ausfuehren. Entfernen: Install.ps1 -TargetUser $TargetUser -Uninstall"
