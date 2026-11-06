param([Parameter(Mandatory)][string]$PackageRoot)
$ErrorActionPreference = 'Stop'
$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$helper = Join-Path $PackageRoot 'UniversityTimetable.DatabaseSetup.exe'
if (-not (Test-Path -LiteralPath $helper)) { throw 'The published database setup helper is missing.' }
$suffix = [Guid]::NewGuid().ToString('N')
$instance = 'UTInstallerTest_' + $suffix
$database = 'UTInstallerTest_' + $suffix
$testRoot = Join-Path $env:TEMP $instance
$localDb = Join-Path $env:ProgramFiles 'Microsoft SQL Server/170/Tools/Binn/SqlLocalDB.exe'
$settings = Join-Path $testRoot 'database-settings.json'
$checks = [Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $prepare = @('prepare', '--backup', (Join-Path $PackageRoot 'Database/InitialData.bak'),
        '--data-dir', $testRoot, '--settings', $settings, '--instance', $instance, '--database', $database)
    & $helper @prepare
    if ($LASTEXITCODE -ne 0) { throw 'Restoring the packaged database failed.' }
    $checks.Add('Database restored into an isolated LocalDB instance.')
    $config = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json
    # Windows PowerShell's legacy provider uses the compact spelling of this keyword.
    $legacyConnection = $config.ConnectionStrings.UniversityTimetable -replace 'Trust Server Certificate', 'TrustServerCertificate'
    $connection = [System.Data.SqlClient.SqlConnection]::new($legacyConnection)
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = 'CREATE TABLE dbo.InstallerPreservationProbe (Value int NOT NULL); INSERT dbo.InstallerPreservationProbe VALUES (731);'
        [void]$command.ExecuteNonQuery()
    } finally { $connection.Dispose() }
    & $helper @prepare
    if ($LASTEXITCODE -ne 0) { throw 'An idempotent second preparation failed.' }
    $connection = [System.Data.SqlClient.SqlConnection]::new($legacyConnection)
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT Value FROM dbo.InstallerPreservationProbe;'
        if ([int]$command.ExecuteScalar() -ne 731) { throw 'Existing database data was replaced.' }
    } finally { $connection.Dispose() }
    $checks.Add('A second installation preserves existing database changes.')
    & $helper compare --settings $settings --manifest (Join-Path $PackageRoot 'Database/Snapshot.json')
    if ($LASTEXITCODE -ne 0) { throw 'Restored table counts differ from the packaged snapshot.' }
    $checks.Add('Every source user table has the expected row count after restoration.')
    $app = Join-Path $PackageRoot 'Presentation_Layer.exe'
    foreach ($argument in @('--update-db', '--check-cp-sat')) {
        $start = [Diagnostics.ProcessStartInfo]::new($app)
        $start.ArgumentList.Add($argument)
        $start.WorkingDirectory = $PackageRoot
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.Environment['UNIVERSITY_TIMETABLE_CONNECTION_STRING'] = $config.ConnectionStrings.UniversityTimetable
        $process = [Diagnostics.Process]::Start($start)
        if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'The published application check timed out.' }
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        if ($process.ExitCode -ne 0) { throw "Published application check failed ($argument): $stderr" }
        if ($argument -eq '--check-cp-sat' -and $stdout -notlike '*CP-SAT runtime OK: Optimal*') { throw 'The CP-SAT check did not solve the verification model.' }
        $process.Dispose()
    }
    & $helper compare --settings $settings --manifest (Join-Path $PackageRoot 'Database/Snapshot.json')
    if ($LASTEXITCODE -ne 0) { throw 'Application startup unexpectedly changed the packaged table counts.' }
    $checks.Add('The actual published application initializes the restored copy and runs CP-SAT.')
    $checks | ForEach-Object { 'PASS ' + $_ }
    $checks | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PackageRoot '../verification.json') -Encoding utf8
} finally {
    if ($database -notmatch '^UTInstallerTest_[0-9a-f]{32}$' -or $instance -ne $database) { throw 'Unsafe test cleanup target.' }
    try {
        $master = [System.Data.SqlClient.SqlConnection]::new("Server=(localdb)\$instance;Database=master;Integrated Security=True;Encrypt=False;Connect Timeout=5")
        $master.Open()
        try {
            $command = $master.CreateCommand()
            $command.CommandText = "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END"
            [void]$command.ExecuteNonQuery()
        } finally { $master.Dispose() }
    } finally {
        & $localDb stop $instance -k 2>$null | Out-Null
        & $localDb delete $instance 2>$null | Out-Null
    }
}
