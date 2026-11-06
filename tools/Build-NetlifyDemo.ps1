$ErrorActionPreference='Stop'
$workspacePath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$outputPath=Join-Path $workspacePath ('output/netlify-mock-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
$sitePath=Join-Path $outputPath 'site'
[void](New-Item -ItemType Directory -Path $sitePath -Force)
Copy-Item -Path (Join-Path $workspacePath 'UniversityTimetable.API/wwwroot/*') -Destination $sitePath -Recurse
foreach($name in @('api.js','mock-data.js','demo-ui.js')){Copy-Item -LiteralPath (Join-Path $workspacePath ('demo/netlify/'+$name)) -Destination (Join-Path $sitePath ('js/'+$name)) -Force}
$applicationPath=Join-Path $sitePath 'js/app.js'
$application=[IO.File]::ReadAllText($applicationPath).Replace('حدّث البيانات لعرض آخر تغييرات تطبيق سطح المكتب.','هذه بيانات افتراضية للعرض وليست سجلات الجامعة.').Replace('سيُحذف هذا السجل من قاعدة البيانات المشتركة. يمنع النظام حذف السجلات المرتبطة عندما تتطلب قواعده ذلك.','سيُحذف هذا السجل من بيانات العرض في هذا المتصفح. تبقى البيانات على أجهزة الزوار الآخرين دون تغيير.')
[IO.File]::WriteAllText($applicationPath,$application,[Text.UTF8Encoding]::new($false))
foreach($page in Get-ChildItem -LiteralPath $sitePath -Filter '*.html' -File){
    $html=[IO.File]::ReadAllText($page.FullName).Replace('</head>','<link rel="stylesheet" href="/css/demo.css"><script type="module" src="/js/demo-ui.js"></script></head>')
    [IO.File]::WriteAllText($page.FullName,$html,[Text.UTF8Encoding]::new($false))
}
Copy-Item -LiteralPath (Join-Path $workspacePath 'demo/netlify/demo.css') -Destination (Join-Path $sitePath 'css/demo.css')
$workerPath=Join-Path $sitePath 'service-worker.js'
$worker=[IO.File]::ReadAllText($workerPath) -replace "const CACHE = '[^']+';","const CACHE = 'university-timetable-shell-demo-v1';"
$worker=$worker.Replace("const ASSETS = [","const ASSETS = ['/js/mock-data.js', '/js/demo-ui.js', '/css/demo.css', ")
[IO.File]::WriteAllText($workerPath,$worker,[Text.UTF8Encoding]::new($false))
"/*`n  Cache-Control: public, max-age=0, must-revalidate`n" | Set-Content -LiteralPath (Join-Path $sitePath '_headers') -Encoding ascii
Compress-Archive -Path (Join-Path $sitePath '*') -DestinationPath (Join-Path $outputPath 'Netlify-Mock-Demo.zip')
Copy-Item -LiteralPath (Join-Path $workspacePath 'docs/NETLIFY_MOCK_AR.md') -Destination (Join-Path $outputPath 'READ-ME.md')
[pscustomobject]@{SiteDirectory=$sitePath;UploadPackage=(Join-Path $outputPath 'Netlify-Mock-Demo.zip')}
