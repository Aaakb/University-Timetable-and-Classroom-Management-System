@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
echo Starting University Timetable Management System at http://localhost:5180
echo Keep this window open. Press Ctrl+C to stop the server.
dotnet run --project "%~dp0UniversityTimetable.API\UniversityTimetable.API.csproj" --launch-profile local
if errorlevel 1 pause
popd
endlocal
