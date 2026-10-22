param([string]$SourceDirectory='University Timetable and Classroom Management System/bin/Debug/net8.0-windows')
$ErrorActionPreference='Stop'
$sourcePath = (Resolve-Path -LiteralPath $SourceDirectory).Path
$workspacePath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$resultPath = Join-Path $workspacePath 'output/native-runtime-verification'
[void](New-Item -ItemType Directory -Force -Path $resultPath)
$caseName = 'long-path-' + [Guid]::NewGuid().ToString('N').Substring(0,8) + '-'
$caseName += 'x' * [Math]::Max(1,230-$resultPath.Length-1-$caseName.Length)
$longPath = Join-Path $resultPath $caseName
[void](New-Item -ItemType Directory -Force -Path $longPath)
foreach($file in Get-ChildItem -LiteralPath $sourcePath -File) {
    if ($file.Extension -in @('.dll','.exe') -or $file.Name.EndsWith('.deps.json') -or $file.Name.EndsWith('.runtimeconfig.json')) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $longPath $file.Name)
    }
}
$nativePath=Join-Path $longPath 'runtimes/win-x64/native'
[void](New-Item -ItemType Directory -Force -Path $nativePath)
foreach($file in Get-ChildItem -LiteralPath (Join-Path $sourcePath 'runtimes/win-x64/native') -File) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $nativePath $file.Name)
}
$dllPath=Join-Path $nativePath 'google-ortools-native.dll'
if ($dllPath.Length -le 260) { throw 'Regression requires native library path longer than MAX_PATH.' }
$checks=[System.Collections.Generic.List[object]]::new()
foreach($directory in @($sourcePath,$longPath)) {
    $start=[System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName=Join-Path $directory 'Presentation_Layer.exe'
    [void]$start.ArgumentList.Add('--check-cp-sat')
    $start.WorkingDirectory=$directory
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    # Deliberately invalid: the diagnostic must not initialize or access SQL.
    $start.Environment['UNIVERSITY_TIMETABLE_CONNECTION_STRING']='Server=127.0.0.1,1;Database=DoNotUse;Integrated Security=True;Connect Timeout=1;Encrypt=False'
    $process=[System.Diagnostics.Process]::Start($start)
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw 'Native runtime smoke test timed out.' }
    $stdout=$process.StandardOutput.ReadToEnd(); $stderr=$process.StandardError.ReadToEnd()
    if ($process.ExitCode -ne 0 -or !$stdout.Contains('CP-SAT runtime OK: Optimal')) { throw "Native runtime failed: $stdout $stderr" }
    $checks.Add([pscustomobject]@{Directory=$directory;NativePathLength=(Join-Path $directory 'runtimes/win-x64/native/google-ortools-native.dll').Length;ExitCode=$process.ExitCode;Output=$stdout.Trim();Passed=$true})
    "PASS apphost smoke test: $directory"
    $process.Dispose()
}
# Run a real solve twice in separate apphost processes, including the long path.
$checks | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resultPath 'results.json') -Encoding utf8
"PASS: two actual application launches; long native path length $($dllPath.Length); no SQL initialization."
