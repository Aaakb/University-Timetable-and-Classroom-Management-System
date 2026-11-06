[CmdletBinding()]
param(
    [string]$Destination,
    [string]$InnoCompiler,
    [string]$LocalDbInstaller,
    [string]$VcRuntimeInstaller,
    [string]$RuntimeVersion = '8.0.31',
    [switch]$Resume
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Destination) { $Destination = Join-Path $projectRoot ('output/DesktopInstaller-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Destination = [IO.Path]::GetFullPath($Destination)
if ((Test-Path -LiteralPath $Destination) -and -not $Resume) { throw 'The installer destination already exists. Choose a fresh directory or explicitly resume.' }
$dependencies = Join-Path $projectRoot 'output/installer-build/dependencies'
if (-not $InnoCompiler) { $InnoCompiler = Join-Path $dependencies 'inno/ISCC.exe' }
if (-not $LocalDbInstaller) { $LocalDbInstaller = Join-Path $dependencies 'en-US/SqlLocalDB.msi' }
if (-not $VcRuntimeInstaller) { $VcRuntimeInstaller = Join-Path $dependencies 'vc_redist.x64.exe' }
foreach ($path in @($InnoCompiler, $LocalDbInstaller, $VcRuntimeInstaller)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required build dependency is missing: $path" }
}
foreach ($prerequisite in @($LocalDbInstaller, $VcRuntimeInstaller)) {
    $signature = Get-AuthenticodeSignature -LiteralPath $prerequisite
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        throw 'Runtime prerequisites must have valid Microsoft signatures.'
    }
}
$payload = Join-Path $Destination 'package'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$desktopProject = Join-Path $projectRoot 'University Timetable and Classroom Management System/Presentation_Layer.csproj'
$helperProject = Join-Path $projectRoot 'tools/InstallerDatabase/InstallerDatabase.csproj'
foreach ($project in @($desktopProject, $helperProject)) {
    & dotnet publish $project -c Release -r win-x64 --self-contained true --ignore-failed-sources -m:1 -nr:false -p:UseSharedCompilation=false `
        "-p:RuntimeFrameworkVersion=$RuntimeVersion" -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $payload
    if ($LASTEXITCODE -ne 0) { throw "Publishing failed: $project" }
}
$databaseFolder = Join-Path $payload 'Database'
New-Item -ItemType Directory -Path $databaseFolder -Force | Out-Null
$helper = Join-Path $payload 'UniversityTimetable.DatabaseSetup.exe'
$backup = Join-Path $databaseFolder 'InitialData.bak'
if (-not ($Resume -and (Test-Path -LiteralPath $backup))) {
    & $helper backup --source-settings (Join-Path $projectRoot 'University Timetable and Classroom Management System/database-settings.json') --output $backup
    if ($LASTEXITCODE -ne 0) { throw 'Creating a verified backup of the current database failed.' }
}
$settings = @{ ConnectionStrings = @{ UniversityTimetable = 'Server=(localdb)\UniversityTimetable;Database=UniversityTimetable;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;' } }
$settings | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'database-settings.json') -Encoding utf8
& (Join-Path $projectRoot 'tests/DesktopInstaller/Verify-Package.ps1') -PackageRoot $payload
if ($LASTEXITCODE -ne 0) { throw 'The published package did not pass verification.' }
$sourceScript = Join-Path $projectRoot 'installer/UniversityTimetable.iss'
$compileScript = Join-Path $Destination 'InstallerSource.iss'
[IO.File]::WriteAllText($compileScript, [IO.File]::ReadAllText($sourceScript), [Text.UTF8Encoding]::new($true))
& $InnoCompiler /Q "/DPayloadDir=$payload" "/DLocalDbMsi=$LocalDbInstaller" "/DVcRuntimeExe=$VcRuntimeInstaller" "/DReleaseDir=$Destination" "/DProjectDir=$projectRoot" $compileScript
if ($LASTEXITCODE -ne 0) { throw 'The installer compiler failed.' }
$setup = Join-Path $Destination 'UniversityTimetable-Setup-WithData-20261031.exe'
$manifest = [ordered]@{
    CreatedAt = [DateTimeOffset]::Now.ToString('O')
    Setup = (Split-Path -Leaf $setup)
    Bytes = (Get-Item -LiteralPath $setup).Length
    Sha256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
    Platform = 'Windows x64'
    Runtime = '.NET ' + $RuntimeVersion + ' self-contained'
    DatabaseEngine = 'Microsoft SQL Server 2025 LocalDB (included)'
    DatabaseSnapshot = Get-Content -LiteralPath (Join-Path $databaseFolder 'Snapshot.json') -Raw | ConvertFrom-Json
    Verification = Get-Content -LiteralPath (Join-Path $Destination 'verification.json') -Raw | ConvertFrom-Json
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Destination 'release-manifest.json') -Encoding utf8
Write-Output ('INSTALLER READY: ' + $setup)
Write-Output ('SHA256: ' + $manifest.Sha256)
