#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef LocalDbMsi
  #error LocalDbMsi is required
#endif
#ifndef VcRuntimeExe
  #error VcRuntimeExe is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif
#ifndef ProjectDir
  #error ProjectDir is required
#endif

[Setup]
AppId={{85A51CD7-EEC0-466F-B219-1535205D164B}
AppName=نظام إدارة الجداول الجامعية
AppVersion=2026.10.31
AppPublisher=University Timetable Management System
DefaultDirName={localappdata}\Programs\UniversityTimetable
DefaultGroupName=University Timetable
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir={#ReleaseDir}
OutputBaseFilename=UniversityTimetable-Setup-WithData-20261031
SetupIconFile={#ProjectDir}\University Timetable and Classroom Management System\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Presentation_Layer.exe
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
InfoBeforeFile={#ProjectDir}\installer\INSTALLATION-NOTES-AR.txt

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "database-settings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\database-settings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#LocalDbMsi}"; DestName: "SqlLocalDB.msi"; Flags: dontcopy
Source: "{#VcRuntimeExe}"; DestName: "vc_redist.x64.exe"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\University Timetable"; Filename: "{app}\Presentation_Layer.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\University Timetable"; Filename: "{app}\Presentation_Layer.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Presentation_Layer.exe"; Description: "تشغيل نظام إدارة الجداول الجامعية"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
  LocalDbExe, Parameters: String;
  VcInstalled, VcMinor: Cardinal;
begin
  Result := '';
  if not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', VcInstalled)
    or (VcInstalled <> 1)
    or not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Minor', VcMinor)
    or (VcMinor < 44) then
  begin
    WizardForm.StatusLabel.Caption := 'تثبيت ملفات تشغيل Microsoft Visual C++ المطلوبة لمحرك الجدولة...';
    ExtractTemporaryFile('vc_redist.x64.exe');
    if not ShellExec('runas', ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart', '',
      SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'تعذر بدء تثبيت ملفات تشغيل Visual C++. وافق على طلب صلاحيات Windows ثم أعد المحاولة.'
    else if (ExitCode <> 0) and (ExitCode <> 3010) and (ExitCode <> 1638) then
      Result := 'تعذر تثبيت ملفات تشغيل Visual C++. رمز الخطأ: ' + IntToStr(ExitCode)
    else if ExitCode = 3010 then
      NeedsRestart := True;
    if Result <> '' then Exit;
  end;
  LocalDbExe := ExpandConstant('{pf64}\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe');
  if not FileExists(LocalDbExe) then
  begin
    WizardForm.StatusLabel.Caption := 'تثبيت محرك قاعدة البيانات Microsoft SQL Server 2025 LocalDB...';
    ExtractTemporaryFile('SqlLocalDB.msi');
    Parameters := '/i "' + ExpandConstant('{tmp}\SqlLocalDB.msi') + '" /qn /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES';
    if not ShellExec('runas', ExpandConstant('{sys}\msiexec.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'تعذر بدء تثبيت محرك قاعدة البيانات. وافق على طلب صلاحيات Windows ثم أعد المحاولة.'
    else if (ExitCode <> 0) and (ExitCode <> 3010) then
      Result := 'تعذر تثبيت Microsoft SQL Server LocalDB. رمز الخطأ: ' + IntToStr(ExitCode)
    else if ExitCode = 3010 then
      NeedsRestart := True;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExitCode: Integer;
  Parameters, LogFile: String;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := 'تجهيز نسخة مستقلة من قاعدة البيانات الحالية...';
    LogFile := ExpandConstant('{localappdata}\UniversityTimetable\setup-database.log');
    Parameters := 'prepare --backup "' + ExpandConstant('{app}\Database\InitialData.bak') +
      '" --data-dir "' + ExpandConstant('{localappdata}\UniversityTimetable\Data') +
      '" --settings "' + ExpandConstant('{app}\database-settings.json') +
      '" --application "' + ExpandConstant('{app}\Presentation_Layer.exe') +
      '" --log "' + LogFile + '"';
    if not Exec(ExpandConstant('{app}\UniversityTimetable.DatabaseSetup.exe'), Parameters,
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      RaiseException('تعذر تشغيل أداة تجهيز قاعدة البيانات.');
    if ExitCode <> 0 then
      RaiseException('لم يكتمل تجهيز قاعدة البيانات. لم تُستبدل أي قاعدة موجودة. تفاصيل الخطأ: ' + LogFile);
  end;
end;
