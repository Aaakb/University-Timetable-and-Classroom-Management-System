@echo off
setlocal
set "updatedDesktop=%~dp0out\subject-update\bin\Presentation_Layer\release\Presentation_Layer.exe"
if not exist "%updatedDesktop%" (
    echo The updated desktop build was not found.
    echo Build the solution with: dotnet build "University Timetable Management System.slnx" --artifacts-path out/subject-update -c Release
    pause
    exit /b 1
)
start "" /D "%~dp0" "%updatedDesktop%"
