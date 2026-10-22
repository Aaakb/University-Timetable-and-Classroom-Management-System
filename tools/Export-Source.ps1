# Export the current source, including uncommitted files, without build output.
[CmdletBinding()]
param([string]$Destination)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Destination) {
    $Destination = Join-Path $projectRoot ('output/UniversityTimetable-Source-{0}.zip' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$Destination = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $Destination) { throw "Destination already exists: $Destination" }
[IO.Directory]::CreateDirectory((Split-Path -Parent $Destination)) | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$excludedFolders = @('.git', '.vs', 'bin', 'obj', 'node_modules', 'TestResults', '__pycache__')
$excludedRootFolders = @('out', 'output', 'tmp', '.tmp')
$pending = [Collections.Generic.Stack[string]]::new()
$pending.Push($projectRoot)
$fileCount = 0
$sourceBytes = 0L
$stream = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        while ($pending.Count -gt 0) {
            $directory = $pending.Pop()
            foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
                if ($item.PSIsContainer) {
                    if ($item.Name -in $excludedFolders) { continue }
                    if ($directory -eq $projectRoot -and $item.Name -in $excludedRootFolders) { continue }
                    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
                    $pending.Push($item.FullName)
                    continue
                }
                if ($item.FullName -eq $Destination) { continue }
                if ($item.Extension -in @('.zip', '.7z', '.rar', '.pdb', '.log', '.tmp', '.user', '.suo')) { continue }
                $entryName = $item.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $item.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
                $fileCount++
                $sourceBytes += $item.Length
            }
        }
    }
    finally { $archive.Dispose() }
}
finally { $stream.Dispose() }

[pscustomobject]@{
    Archive = $Destination
    Files = $fileCount
    SourceMB = [math]::Round($sourceBytes / 1MB, 2)
    ZipMB = [math]::Round((Get-Item -LiteralPath $Destination).Length / 1MB, 2)
}
