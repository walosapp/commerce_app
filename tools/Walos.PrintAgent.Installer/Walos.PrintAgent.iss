#ifndef SourceDir
  #error SourceDir must point to the published Walos.PrintAgent files.
#endif
#ifndef OutputDir
  #error OutputDir must point to the installer artifact directory.
#endif
#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif

#define AppName "Walos Agent"
#define AppPublisher "Walos"
#define AppExeName "Walos.PrintAgent.exe"
#define AppId "{{4DA379B5-C948-4AC4-AD61-A5B27D0BB8B0}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Instalador de Walos Agent
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\Walos\PrintAgent
DefaultGroupName=Walos
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir={#OutputDir}
#ifdef SmokeTest
OutputBaseFilename=Walos-Agent-Smoke-Setup-{#AppVersion}
PrivilegesRequired=lowest
#else
OutputBaseFilename=Walos-Agent-Setup
PrivilegesRequired=admin
#endif
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
ChangesEnvironment=no
MinVersion=10.0.17763

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
#ifdef SmokeTest
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WalosPrintAgent"; ValueData: """{app}\{#AppExeName}"" --autostart"; Flags: uninsdeletevalue
#else
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WalosPrintAgent"; ValueData: """{app}\{#AppExeName}"" --autostart"; Flags: uninsdeletevalue
#endif

[Icons]
Name: "{group}\Walos Agent"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar Walos Agent"; Flags: nowait postinstall runasoriginaluser

[Code]
var
  DeleteLocalConfiguration: Boolean;

function HasCommandLineSwitch(const SwitchName: String): Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
  begin
    if CompareText(ParamStr(Index), SwitchName) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  DeleteLocalConfiguration := HasCommandLineSwitch('/CLEANCONFIG');

  if (not UninstallSilent) and (not DeleteLocalConfiguration) then
  begin
    DeleteLocalConfiguration := MsgBox(
      '¿También querés eliminar el pairing, la impresora configurada y el historial local de trabajos?' + #13#10 + #13#10 +
      'Elegí No para conservarlos durante una reinstalación o actualización.',
      mbConfirmation,
      MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteLocalConfiguration then
  begin
    DelTree(ExpandConstant('{localappdata}\Walos\PrintAgent'), True, True, True);
  end;
end;
