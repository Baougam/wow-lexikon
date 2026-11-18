# Neue Version erstellen – und auf Wunsch gleich auf GitHub hochladen.
#
#   Erstes Mal:   .\tools\veroeffentlichen.ps1 -Version 1.0.0 -GitHubRepo DEINNAME/wow-lexikon -Hochladen
#   Danach:       .\tools\veroeffentlichen.ps1 -Version 1.0.1 -Hochladen
#
# Was passiert:
#   1. Versionsnummer wird in Addon und Programm eingetragen, die Update-Adresse fest ins Programm eingebaut
#      (https://github.com/<repo>/releases/latest/download/manifest.json – zeigt immer auf die neueste Version).
#   2. Ordner „release“: WowLexikon.exe (ohne Blizzard-Texte), WowLexikon.zip (Addon), WowLexikon-<Version>.zip
#      (dasselbe für CurseForge), manifest.json.
#   3. Mit -Hochladen: neue Version („Release“) auf GitHub anlegen und die Dateien anhängen. Alle Begleit-Programme
#      finden sie dann automatisch (beim Start, alle 6 Stunden oder über „Nach Updates suchen“).
#
# Zugang für -Hochladen (einmalig): auf github.com → Settings → Developer settings → Personal access tokens →
# Fine-grained token, nur für dieses Repository, Berechtigung „Contents: Read and write“. Den Schlüssel in die Datei
# tools\.github-token legen (wird nicht weitergegeben) oder als Umgebungsvariable GITHUB_TOKEN setzen.
param(
  [Parameter(Mandatory = $true)][string]$Version,
  [string]$GitHubRepo,
  [switch]$Hochladen,
  [string]$Hinweis = "",
  [string]$BasisUrl
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$rel = Join-Path $root 'release'
$utf8 = New-Object Text.UTF8Encoding($false)

# Repository merken
$cfgFile = Join-Path $root 'tools\release-config.json'
$cfg = if (Test-Path $cfgFile) { Get-Content $cfgFile -Raw | ConvertFrom-Json } else { [pscustomobject]@{ repo = '' } }
if ($GitHubRepo) { $cfg.repo = $GitHubRepo; [IO.File]::WriteAllText($cfgFile, ($cfg | ConvertTo-Json), $utf8) }
if (-not $BasisUrl) {
  if (-not $cfg.repo) { throw "Bitte einmal -GitHubRepo DEINNAME/wow-lexikon angeben (oder -BasisUrl)." }
  $BasisUrl = "https://github.com/$($cfg.repo)/releases/latest/download"
}
$base = $BasisUrl.TrimEnd('/')

if (Test-Path $rel) { Remove-Item $rel -Recurse -Force }
New-Item -ItemType Directory -Force $rel | Out-Null

# 1. Versionsnummer und Update-Adresse eintragen
$toc = Join-Path $root 'addon\WowLexikon\WowLexikon.toc'
[IO.File]::WriteAllText($toc, ([IO.File]::ReadAllText($toc) -replace '## Version: .*', "## Version: $Version"), $utf8)
$core = Join-Path $root 'addon\WowLexikon\Core.lua'
[IO.File]::WriteAllText($core, ([IO.File]::ReadAllText($core) -replace 'WL\.version = "[^"]*"', "WL.version = `"$Version`""), $utf8)
$app = Join-Path $root 'src\App.cs'
$appText = [IO.File]::ReadAllText($app)
$appText = $appText -replace 'public const string Version = "[^"]*"', "public const string Version = `"$Version`""
$appText = $appText -replace 'public const string DefaultUpdateUrl = "[^"]*"', "public const string DefaultUpdateUrl = `"$base/manifest.json`""
[IO.File]::WriteAllText($app, $appText, $utf8)

# 2. Programm ohne Spieldaten bauen, Addon packen, manifest schreiben
& (Join-Path $root 'build.ps1') -OhneSpieldaten -Ausgabe (Join-Path $rel 'WowLexikon.exe')
. (Join-Path $root 'tools\zip-ordner.ps1')
$zip = Join-Path $rel 'WowLexikon.zip'
$leer = "-- Wird vom Begleit-Programm überschrieben.`nWowLexikon_Learned = WowLexikon_Learned or { words = {}, quests = {}, build = `"`", generated = `"`" }`n"
Zip-Ordner -Quelle (Join-Path $root 'addon\WowLexikon') -Ziel $zip -Wurzel 'WowLexikon' -Ersetze @{ 'Data/Learned.lua' = $leer }
Copy-Item $zip (Join-Path $rel "WowLexikon-$Version.zip")
$manifest = [ordered]@{
  addonVersion = $Version; addonZip = "$base/WowLexikon.zip"
  appVersion = $Version;   appExe = "$base/WowLexikon.exe"
  hinweis = $Hinweis;      datum = (Get-Date -Format 'yyyy-MM-dd')
}
[IO.File]::WriteAllText((Join-Path $rel 'manifest.json'), ($manifest | ConvertTo-Json), $utf8)

# Die eigene Version (mit eingebauten Spieldaten) ebenfalls neu bauen – mit derselben Update-Adresse
& (Join-Path $root 'build.ps1')

Write-Host ""
Write-Host "Version $Version erstellt: $rel"
Write-Host "Update-Adresse (fest eingebaut): $base/manifest.json"

# 3. Hochladen
if ($Hochladen) {
  if (-not $cfg.repo) { throw "Für -Hochladen bitte -GitHubRepo DEINNAME/wow-lexikon angeben." }
  $tokenFile = Join-Path $root 'tools\.github-token'
  $token = if ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } elseif (Test-Path $tokenFile) { (Get-Content $tokenFile -Raw).Trim() } else { $null }
  if (-not $token) { throw "Kein GitHub-Zugang: Schlüssel in tools\.github-token ablegen (siehe Kopf dieser Datei)." }
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  $h = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'wowlexikon-release' }
  $notes = "WoW Lexikon $Version"
  $log = Join-Path $root 'curseforge\CHANGELOG.md'
  if (Test-Path $log) {
    $m = [regex]::Match([IO.File]::ReadAllText($log), "(?ms)^## $([regex]::Escape($Version))\s*\r?\n(.*?)(?=^## |\z)")
    if ($m.Success) { $notes = $m.Groups[1].Value.Trim() }
  }
  if ($Hinweis) { $notes = "$Hinweis`n`n$notes" }
  $body = @{ tag_name = "v$Version"; name = "WoW Lexikon $Version"; body = $notes; make_latest = 'true' } | ConvertTo-Json
  Write-Host "Lege Version v$Version auf GitHub an …"
  $release = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$($cfg.repo)/releases" -Headers $h `
    -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8'
  foreach ($f in Get-ChildItem $rel -File) {
    Write-Host "  lade hoch: $($f.Name)"
    Invoke-RestMethod -Method Post -Uri "https://uploads.github.com/repos/$($cfg.repo)/releases/$($release.id)/assets?name=$([uri]::EscapeDataString($f.Name))" `
      -Headers $h -InFile $f.FullName -ContentType 'application/octet-stream' | Out-Null
  }
  Write-Host ""
  Write-Host "Fertig: $($release.html_url)"
  Write-Host "Download-Link zum Weitergeben: https://github.com/$($cfg.repo)/releases/latest/download/WowLexikon.exe"
  Write-Host "Alle Begleit-Programme holen sich Version $Version automatisch. Für CurseForge: release\WowLexikon-$Version.zip hochladen."
} else {
  Write-Host "Zum Hochladen denselben Befehl mit -Hochladen ausführen."
}
