param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK is required.' }
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Node.js is required for JavaScript syntax checks.' }

    if (-not $SkipBuild) {
        Write-Host 'Building the web API...'
        & dotnet build 'UniversityTimetable.API/UniversityTimetable.API.csproj' -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Web API build failed.' }
    }

    Write-Host 'Checking JavaScript syntax...'
    $scripts = Get-ChildItem 'UniversityTimetable.API/wwwroot' -Filter '*.js' -File -Recurse
    foreach ($script in $scripts) {
        & node --check $script.FullName
        if ($LASTEXITCODE -ne 0) { throw "JavaScript syntax check failed: $($script.FullName)" }
    }
    Write-Host "Checked $($scripts.Count) JavaScript files."

    Write-Host 'Running web parity checks...'
    & dotnet run --project 'tests/WebParity/WebParity.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Web parity checks failed.' }
    Write-Host 'Web checks passed.'
}
finally {
    Pop-Location
}
