param([string]$PublicUrl)
$ErrorActionPreference = 'Stop'
$workspacePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$publicUri = $null
if ($PublicUrl) {
    $publicUri = [Uri]$PublicUrl
    if (!$publicUri.IsAbsoluteUri -or $publicUri.Scheme -ne 'https' -or $publicUri.UserInfo -or
        $publicUri.Query -or $publicUri.Fragment -or $publicUri.AbsolutePath -ne '/' -or $publicUri.IsLoopback) {
        throw 'PublicUrl must be the HTTPS origin of the ASP.NET host, without a path, query, or credentials.'
    }
}
$outputPath = Join-Path $workspacePath ('output/web-hosting-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
$serverPath = Join-Path $outputPath 'server'
[void](New-Item -ItemType Directory -Path $serverPath -Force)
dotnet publish (Join-Path $workspacePath 'UniversityTimetable.API/UniversityTimetable.API.csproj') --configuration Release --runtime win-x64 --self-contained false --output $serverPath -p:EnvironmentName=Production
if ($LASTEXITCODE -ne 0) { throw 'ASP.NET publish failed.' }

# Never ship a developer database connection or credentials inside the deployment ZIP.
@{ ConnectionStrings = @{ UniversityTimetable = '' } } | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $serverPath 'database-settings.json') -Encoding utf8
$allowedHosts = 'CONFIGURE-YOUR-HOST.invalid'
if ($publicUri) { $allowedHosts = $publicUri.DnsSafeHost }
@{ AllowedHosts = $allowedHosts } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $serverPath 'appsettings.Production.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $workspacePath 'docs/WEB_HOSTING_AR.md') -Destination (Join-Path $outputPath 'READ-ME-FIRST.md')
Compress-Archive -Path (Join-Path $serverPath '*') -DestinationPath (Join-Path $outputPath 'ASP.NET-Server.zip')

if ($publicUri) {
    # Redirect the browser to the complete application. Its API calls then use the
    # same origin and avoid Netlify's 26-second external proxy timeout.
    $redirectPath = Join-Path $outputPath 'netlify-redirect'
    [void](New-Item -ItemType Directory -Path $redirectPath)
    $origin = $publicUri.GetLeftPart([UriPartial]::Authority)
    "/*  $origin/:splat  302!" | Set-Content -LiteralPath (Join-Path $redirectPath '_redirects') -Encoding ascii
    '<!doctype html><html lang="ar" dir="rtl"><meta charset="utf-8"><title>نظام الجداول الجامعية</title><p>يجري تحويلك إلى خادم النظام.</p></html>' |
        Set-Content -LiteralPath (Join-Path $redirectPath 'index.html') -Encoding utf8
    Compress-Archive -Path (Join-Path $redirectPath '*') -DestinationPath (Join-Path $outputPath 'Netlify-Redirect.zip')
}
[pscustomobject]@{ OutputDirectory = $outputPath; ServerPackage = (Join-Path $outputPath 'ASP.NET-Server.zip'); RequiresSqlConnection = $true; RequiresPublicHost = !$publicUri }
