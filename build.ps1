# Baut WowLexikon.exe nur mit Bordmitteln von Windows (.NET Framework 4.8 Compiler) – keine Installation nötig.
#   .\build.ps1                  → eigene Version mit eingebauten Spieldaten (Questtexte + Namen von Blizzard, nur für dich)
#   .\build.ps1 -OhneSpieldaten  → weitergebbare Version: erzeugt die Spieldaten auf jedem PC selbst
param([switch]$OhneSpieldaten, [string]$Ausgabe)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw   = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$wm   = "$env:WINDIR\System32\WinMetadata"
$out  = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
if (-not $Ausgabe) { $Ausgabe = Join-Path $out 'WowLexikon.exe' }

$refs = @(
  'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Xml.dll',
  "$fw\System.Xaml.dll", 'System.Net.Http.dll', 'System.Web.Extensions.dll', 'System.Security.dll',
  "$fw\System.IO.Compression.dll",
  "$fw\WPF\WindowsBase.dll", "$fw\WPF\PresentationCore.dll", "$fw\WPF\PresentationFramework.dll",
  "$fw\System.Runtime.dll", "$fw\System.Threading.Tasks.dll", "$fw\System.Runtime.WindowsRuntime.dll",
  "$wm\Windows.Foundation.winmd", "$wm\Windows.Media.winmd", "$wm\Windows.Graphics.winmd",
  "$wm\Windows.Storage.winmd", "$wm\Windows.Globalization.winmd"
) | ForEach-Object { "/r:$_" }

$src = Get-ChildItem "$root\src\*.cs" | ForEach-Object { $_.FullName }

# Addon als ZIP einbetten (nur das Haupt-Addon, ohne die lokal erzeugten Spieldaten)
. (Join-Path $root 'tools\zip-ordner.ps1')
$zip = Join-Path $out 'addon.zip'
Zip-Ordner -Quelle (Join-Path $root 'addon\WowLexikon') -Ziel $zip -Wurzel 'WowLexikon'

$res = @(
  "/resource:$root\data\glossar-wow.json,WowLexikon.glossar.json",
  "/resource:$root\data\questwoerter.txt,WowLexikon.questwoerter.txt",
  "/resource:$root\data\woerter-auto.txt,WowLexikon.woerter-auto.txt",
  "/resource:$zip,WowLexikon.addon.zip"
)
if (-not $OhneSpieldaten) {
  # Spieldaten (Questtexte + Namen je Sprache, erzeugt mit tools/daten-bauen.mjs) – nur für die eigene Version
  $res += Get-ChildItem "$root\data\*.txt.gz" | ForEach-Object { "/resource:$($_.FullName),WowLexikon.$($_.Name)" }
}

& "$fw\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /nowarn:1702 `
  "/out:$Ausgabe" "/win32manifest:$root\app.manifest" `
  @res @refs @src
if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen" }
Write-Host "Fertig: $Ausgabe$(if ($OhneSpieldaten) { ' (ohne Spieldaten)' })"
