$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixture = Join-Path $env:TEMP ('UTSourceExport_' + [Guid]::NewGuid().ToString('N'))
$files = @{
    'tools/Export-Source.ps1' = Get-Content -LiteralPath (Join-Path $root 'tools/Export-Source.ps1') -Raw
    'Models/Model.cs' = 'public sealed class Model {}'
    'Models/Models.csproj' = '<Project Sdk="Microsoft.NET.Sdk" />'
    'Business_Logic_Layer/Backups/BackupService.cs' = 'public sealed class BackupService {}'
    'Templates/Excel/example.xlsx' = 'required template'
    'graphify-out/graph.json' = '{"nodes":[],"edges":[]}'
    'graphify-out/graph.html' = '<html>map</html>'
    'graphify-out/GRAPH_REPORT.md' = 'map report'
    '.artifacts/copy/Old.cs' = 'generated copy'
    '.artifacts/copy/library.dll' = 'generated binary'
    'Models/bin/library.dll' = 'build output'
    'Models/obj/project.assets.json' = 'build cache'
    'graphify-out/cache/semantic/item.json' = 'regenerable cache'
    'Backups/private.bak' = 'database backup'
    'output/Setup.exe' = 'installer'
    'tmp/Old.cs' = 'temporary source copy'
}
foreach ($entry in $files.GetEnumerator()) {
    $path = Join-Path $fixture $entry.Key
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    [IO.File]::WriteAllText($path, $entry.Value)
}
$archive = Join-Path $fixture 'source.zip'
& (Join-Path $fixture 'tools/Export-Source.ps1') -Destination $archive | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $names = @($zip.Entries.FullName)
    foreach ($required in @('Models/Model.cs','Models/Models.csproj','Business_Logic_Layer/Backups/BackupService.cs','Templates/Excel/example.xlsx','graphify-out/graph.json','graphify-out/graph.html','graphify-out/GRAPH_REPORT.md')) {
        if ($required -notin $names) { throw "Required source/map/template is missing: $required" }
    }
    foreach ($excluded in @('.artifacts/*','*/bin/*','*/obj/*','graphify-out/cache/*','Backups/*','output/*','tmp/*')) {
        if ($names -like $excluded) { throw "Generated/private content entered the source archive: $excluded" }
    }
    'PASS source export keeps source, templates and Graphify while excluding build copies, caches and database backups.'
} finally { $zip.Dispose() }
