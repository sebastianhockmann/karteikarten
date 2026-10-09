<#
.SYNOPSIS
    Installiert die neueste Version von English Steam Trainer aus den GitHub-
    Releases. Liegt im Programmordner und laeuft als SYSTEM ueber die Aufgabe
    "EnglishSteamTrainer-Update" (beim Hochfahren und alle 6 Stunden).

    Ablauf: neueste Version abfragen -> ZIP laden -> SHA256 pruefen -> alles
    anhalten -> Sicherung anlegen -> neue Dateien kopieren -> Watchdog und
    (falls vorher offen) Lern-App wieder starten. Geht beim Kopieren etwas
    schief, wird die Sicherung zurueckgespielt.

    Lernverlauf (unlocks), Logs, Online-Cache und settings.json werden nie
    angefasst. Log: logs\update.log

.EXAMPLE
    .\Update.ps1
    Manuell als Administrator: sofort auf neue Version pruefen.

.EXAMPLE
    .\Update.ps1 -Force
    Neueste Version auch dann installieren, wenn sie nicht neuer ist.
#>
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$InstallDir = $PSScriptRoot
$logDir = Join-Path $InstallDir "logs"
$logFile = Join-Path $logDir "update.log"
# Arbeitsordner bewusst im Programmordner: nur SYSTEM/Admins duerfen dort schreiben.
# (In C:WindowsTemp koennten normale Benutzer Dateien ins Paket schmuggeln.)
$updateDir = Join-Path $InstallDir "update"
$backupDir = Join-Path $updateDir "backup"
$packageName = "EnglishSteamTrainer-win-x64.zip"
$watchdogTaskName = "EnglishSteamTrainer-Watchdog"
$dataItems = @("unlocks", "logs", "cache", "settings.json", "update")
$headers = @{ "User-Agent" = "EnglishSteamTrainer-Updater" }

function Write-Log([string]$message) {
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null

    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 512KB) {
        Move-Item $logFile "$logFile.old" -Force
    }

    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'): $message"
    Add-Content -Path $logFile -Value $line -Encoding UTF8
    Write-Host $line
}

function ConvertTo-Version([string]$text) {
    $clean = ($text -replace '^v', '' -split '[-+]')[0]
    $version = $null
    if ([version]::TryParse($clean, [ref]$version)) { return $version }
    return [version]"0.0.0"
}

function Get-LatestRelease([string]$repository) {
    # Direkt nach dem Hochfahren ist das Netz oft noch nicht da.
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            return Invoke-RestMethod "https://api.github.com/repos/$repository/releases/latest" -Headers $headers -TimeoutSec 30
        }
        catch {
            if ($attempt -eq 5) { throw }
            Start-Sleep -Seconds 60
        }
    }
}

function Copy-ProgramFiles([string]$from, [string]$to) {
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    Get-ChildItem $from -Force |
        Where-Object { $dataItems -notcontains $_.Name } |
        Copy-Item -Destination $to -Recurse -Force
}

$workDir = $null

try {
    $repository = "sebastianhockmann/karteikarten"
    $settingsFile = Join-Path $InstallDir "settings.json"
    if (Test-Path $settingsFile) {
        $settings = Get-Content $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($settings.repository) { $repository = $settings.repository }
    }

    $versionFile = Join-Path $InstallDir "version.txt"
    $installedText = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { "0.0.0" }
    $installed = ConvertTo-Version $installedText

    $release = Get-LatestRelease $repository
    $latest = ConvertTo-Version $release.tag_name

    if ($latest -le $installed -and -not $Force) {
        exit 0
    }

    Write-Log "Neue Version $($release.tag_name) gefunden (installiert: $installedText)."

    $zipAsset = $release.assets | Where-Object { $_.name -eq $packageName } | Select-Object -First 1
    $hashAsset = $release.assets | Where-Object { $_.name -eq "$packageName.sha256" } | Select-Object -First 1
    if (-not $zipAsset -or -not $hashAsset) {
        throw "Release $($release.tag_name) enthaelt kein $packageName (+ .sha256)."
    }

    $workDir = Join-Path $updateDir "download"
    if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $workDir | Out-Null
    $zipPath = Join-Path $workDir $packageName

    Invoke-WebRequest $zipAsset.browser_download_url -OutFile $zipPath -Headers $headers -UseBasicParsing -TimeoutSec 600
    $expected = (Invoke-WebRequest $hashAsset.browser_download_url -Headers $headers -UseBasicParsing).Content
    if ($expected -is [byte[]]) { $expected = [Text.Encoding]::ASCII.GetString($expected) }
    $actual = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected.Trim().ToLowerInvariant()) {
        throw "Pruefsumme stimmt nicht - Download wird verworfen."
    }

    $packageDir = Join-Path $workDir "package"
    Expand-Archive -Path $zipPath -DestinationPath $packageDir
    foreach ($requiredFile in @("EnglishSteamTrainer.UI.exe", "EnglishSteamTrainer.Watchdog.exe", "Update.ps1", "version.txt")) {
        if (-not (Test-Path (Join-Path $packageDir $requiredFile))) {
            throw "Paket unvollstaendig, es fehlt: $requiredFile"
        }
    }

    # Wer hatte die Lern-App offen? Die wird danach in seiner Sitzung neu gestartet
    # (der Fortschritt ist pro Antwort gespeichert, es geht nichts verloren).
    $uiUsers = @(Get-Process -Name "EnglishSteamTrainer.UI" -IncludeUserName -ErrorAction SilentlyContinue |
        ForEach-Object { ($_.UserName -split '\\')[-1] } |
        Sort-Object -Unique)

    Stop-ScheduledTask -TaskName $watchdogTaskName -ErrorAction SilentlyContinue
    Get-Process -Name "EnglishSteamTrainer.Watchdog", "EnglishSteamTrainer.UI" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    if (Test-Path $backupDir) { Remove-Item $backupDir -Recurse -Force }
    Copy-ProgramFiles $InstallDir $backupDir

    try {
        Copy-ProgramFiles $packageDir $InstallDir
        Write-Log "Version $($release.tag_name) installiert."
    }
    catch {
        Write-Log "Fehler beim Kopieren ($($_.Exception.Message)) - stelle vorherige Version wieder her."
        Copy-ProgramFiles $backupDir $InstallDir
        throw
    }
    finally {
        Start-ScheduledTask -TaskName $watchdogTaskName -ErrorAction SilentlyContinue

        foreach ($user in $uiUsers) {
            Start-ScheduledTask -TaskName "EnglishSteamTrainer-App-$user" -ErrorAction SilentlyContinue
        }
    }
}
catch {
    Write-Log "Update fehlgeschlagen: $($_.Exception.Message)"
    exit 1
}
finally {
    if ($workDir -and (Test-Path $workDir)) {
        Remove-Item $workDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
