$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$port = 5187
if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { Write-Host 'Port 5187 is already in use. Close the other application and try again.'; exit 1 }
Remove-Item Env:UNIVERSITY_TIMETABLE_CONNECTION_STRING -ErrorAction SilentlyContinue
Remove-Item Env:ConnectionStrings__UniversityTimetable -ErrorAction SilentlyContinue
$env:ASPNETCORE_URLS = "http://127.0.0.1:$port"
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$process = Start-Process -FilePath (Join-Path $PSScriptRoot 'UniversityTimetable.API.exe') -WorkingDirectory $PSScriptRoot -PassThru -WindowStyle Hidden
try {
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        if ($process.HasExited) { throw 'The web server could not start. Check database configuration.' }
        try { $response = Invoke-WebRequest -UseBasicParsing "http://127.0.0.1:$port/api/auth/csrf" -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready = $true; break } } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'The web server did not become ready.' }
    Start-Process "http://127.0.0.1:$port/"
    Write-Host 'The application is running. Keep this window open; close it to stop the web server.'
    $process.WaitForExit()
} finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue } }
