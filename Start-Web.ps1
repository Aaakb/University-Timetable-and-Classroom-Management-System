param([switch]$Https)
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    $launchProfile = if ($Https) { 'https' } else { 'local' }
    Write-Host 'Starting University Timetable Management System web application. Stop with Ctrl+C.'
    dotnet run --project (Join-Path $PSScriptRoot 'UniversityTimetable.API\UniversityTimetable.API.csproj') --launch-profile $launchProfile
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with code $LASTEXITCODE" }
}
finally { Pop-Location }
