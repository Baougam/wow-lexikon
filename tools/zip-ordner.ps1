# Packt einen Ordner als ZIP mit Schrägstrichen „/“ in den Pfaden (so verlangen es CurseForge und andere Plattformen).
#   . .\tools\zip-ordner.ps1; Zip-Ordner -Quelle addon\WowLexikon -Ziel out.zip -Wurzel WowLexikon [-Ersetze @{ 'Data/Learned.lua' = '…' }]
function Zip-Ordner([string]$Quelle, [string]$Ziel, [string]$Wurzel, [hashtable]$Ersetze = @{}) {
  Add-Type -AssemblyName System.IO.Compression
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  if (Test-Path $Ziel) { Remove-Item $Ziel }
  $quelle = (Resolve-Path $Quelle).Path.TrimEnd('\')
  $fs = [IO.File]::Open($Ziel, [IO.FileMode]::Create)
  $zip = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
  try {
    foreach ($f in Get-ChildItem $quelle -Recurse -File) {
      $rel = $f.FullName.Substring($quelle.Length + 1).Replace('\', '/')
      $entry = $zip.CreateEntry("$Wurzel/$rel", [IO.Compression.CompressionLevel]::Optimal)
      $s = $entry.Open()
      try {
        if ($Ersetze.ContainsKey($rel)) { $b = (New-Object Text.UTF8Encoding($false)).GetBytes($Ersetze[$rel]); $s.Write($b, 0, $b.Length) }
        else { $b = [IO.File]::ReadAllBytes($f.FullName); $s.Write($b, 0, $b.Length) }
      } finally { $s.Dispose() }
    }
  } finally { $zip.Dispose(); $fs.Dispose() }
}