<#
.SYNOPSIS
    Baut das Installationspaket (eigenstaendig lauffaehig, kein .NET auf dem
    Ziel-PC noetig): UI, Watchdog, mitgelieferte Inhalte, Install.ps1 und
    Update.ps1 in einem Ordner - optional als ZIP mit SHA256-Pruefsumme.

    Wird von der GitHub-Action (.github\workflows\release.yml) fuer jedes
    Release benutzt und von Install.ps1 -FromSource fuer lokale Builds.

.EXAMPLE
    .\Build-Package.ps1 -Version 1.2.0 -Zip
#>
param(
    # Standard: <Version> aus Directory.Build.props.
    [string]$Version,
    [string]$OutputDir,
    [switch]$Zip
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $repoRoot "artifacts"

if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $repoRoot "Directory.Build.props"))).Project.PropertyGroup.Version
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $artifacts "package"
}

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

foreach ($project in @("EnglishSteamTrainer.UI", "EnglishSteamTrainer.Watchdog")) {
    Write-Host "Baue $project $Version ..."
    dotnet publish (Join-Path $repoRoot "src\$project\$project.csproj") `
        -c Release -r win-x64 --self-contained true `
        -p:Version=$Version -p:DebugType=none `
        -o $OutputDir
    if ($LASTEXITCODE -ne 0) {
        throw "Build von $project fehlgeschlagen."
    }
}

Copy-Item (Join-Path $PSScriptRoot "Install.ps1") $OutputDir
Copy-Item (Join-Path $PSScriptRoot "Update.ps1") $OutputDir
Set-Content -Path (Join-Path $OutputDir "version.txt") -Value $Version -Encoding ASCII

foreach ($requiredFile in @("EnglishSteamTrainer.UI.exe", "EnglishSteamTrainer.Watchdog.exe", "content\config.json")) {
    if (-not (Test-Path (Join-Path $OutputDir $requiredFile))) {
        throw "Paket unvollstaendig, es fehlt: $requiredFile"
    }
}

Write-Host "Paket: $OutputDir"

if ($Zip) {
    $zipPath = Join-Path $artifacts "EnglishSteamTrainer-win-x64.zip"
    Remove-Item $zipPath, "$zipPath.sha256" -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path "$zipPath.sha256" -Value $hash -Encoding ASCII -NoNewline
    Write-Host "ZIP:  $zipPath"
    Write-Host "SHA256: $hash"
}
