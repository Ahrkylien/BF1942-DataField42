#define AppId "DataField42"
#define AppVersion "2.0"
#define DotNetRuntimeIntallerName "DotNetRuntimeInstaller.exe"

[Setup]
AppId={#AppId}
AppName={#AppId}
UninstallDisplayName={#AppId}
AppVersion={#AppVersion}
WizardStyle=modern
DefaultDirName={code:GetBF1942Directory}
DefaultGroupName={#AppId}
UninstallDisplayIcon={app}\{#AppId}.exe
SolidCompression=yes
UninstallFilesDir={app}\{#AppId}
DirExistsWarning=no  
AppendDefaultDirName=no
OutputBaseFilename={#AppId} v{#AppVersion} Installer

[Files]
Source: ..\DataField42\bin\Publish\{#AppId}.exe; DestDir: {app}

[Icons]
Name: {commondesktop}\{#AppId}; Filename: {app}\{#AppId}.exe; WorkingDir: {app}

[Code]
var
  RequiresRestart: Boolean;
  DownloadPage: TDownloadWizardPage;
  RuntimeDownloaded: Boolean;

  
function GetBF1942Directory(def: string): string;
var
  sTemp : string;
begin
  Result := ''; // Default path
  
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\EA GAMES\Battlefield 1942', 'GAMEDIR', sTemp) and FileExists(sTemp + '\BF1942.exe') then
    Result := sTemp
  else if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Origin\Battlefield 1942', 'GAMEDIR', sTemp) and FileExists(sTemp + '\BF1942.exe') then
    Result := sTemp
  else if FileExists(ExpandConstant('{pf32}') + '\EA Games\Battlefield 1942\BF1942.exe') then
    Result := ExpandConstant('{pf32}') + '\EA Games\Battlefield 1942'
  else if FileExists(ExpandConstant('{pf64}') + '\EA Games\Battlefield 1942\BF1942.exe') then
    Result := ExpandConstant('{pf64}') + '\EA Games\Battlefield 1942'
  else if FileExists(ExpandConstant('{pf}') + '\EA Games\Battlefield 1942\BF1942.exe') then // very future proof
    Result := ExpandConstant('{pf}') + '\EA Games\Battlefield 1942';
end;


function DotNetRuntimeAlreadyExists(DotNetName: string): Boolean;
var
  Cmd, Args: string;
  FileName: string;
  Output: AnsiString;
  Command: string;
  ResultCode: Integer;
begin
  FileName := ExpandConstant('{tmp}\dotnet.txt');
  Cmd := ExpandConstant('{cmd}');
  Command := 'dotnet --list-runtimes';
  Args := '/C ' + Command + ' > "' + FileName + '" 2>&1';
  Result := False;
  if ExecAsOriginalUser(Cmd, Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    if LoadStringFromFile(FileName, Output) then
      Result := Pos(DotNetName, Output) > 0;
  Log('DotNetRuntimeAlreadyExists: ' + IntToStr(Integer(Result)));
end;


function DownloadDotNetRuntime(): Boolean;
begin
  Result := True;
  
  DownloadPage.Clear;
  // Use AddEx to specify a username and password
  if IsX64 then
    DownloadPage.Add('https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe', '{#DotNetRuntimeIntallerName}', '')
  else if IsX86 then
    DownloadPage.Add('https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x86.exe', '{#DotNetRuntimeIntallerName}', '')
  else if IsARM64 then
    DownloadPage.Add('https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-arm64.exe', '{#DotNetRuntimeIntallerName}', '')
  else
    SuppressibleMsgBox(AddPeriod('Unknown CPU architecture'), mbCriticalError, MB_OK, IDOK);
  
  DownloadPage.Show;
  try
    DownloadPage.Download; // This downloads the files to {tmp}
  except
    if DownloadPage.AbortedByUser then
      Log('Aborted by user.')
    else
      SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
    Result := False;
  finally
    DownloadPage.Hide;
  end;
end;


function InstallDotNetRuntime(): String;
var
  StatusText: string;
  ResultCode: Integer;
begin
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := 'Installing .Net Runtime';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  try
    if not Exec(ExpandConstant('{tmp}/{#DotNetRuntimeIntallerName}'), '/install /passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then begin
      Result := '.Net Runtime failed to launch: ' + SysErrorMessage(resultCode);
    end
    else begin
      // See https://learn.microsoft.com/en-us/dotnet/core/install/windows?tabs=net80#install-with-windows-installer
      // And https://learn.microsoft.com/en-us/windows/win32/msi/standard-installer-command-line-options
      // And https://learn.microsoft.com/en-us/dotnet/framework/deployment/deployment-guide-for-developers#return-codes
      case ResultCode of
        0: ; // Installation completed successfully.
        1602: Result := 'The user canceled installation of the .NET runtime. The .NET runtime is required.';
        1603: Result := 'A fatal error occurred during installation of the .NET runtime.';
        1641: RequiresRestart := True;
        3010: RequiresRestart := True;
        5100: Result := 'The user''s computer does not meet system requirements.';
      else
        Result := 'A fatal error occurred during installation. (' + IntToStr(ResultCode) + ')';
      end;
    end;
  finally
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;
end;


function GetIsUpgrade: Boolean;
var
  setupReg: string;
begin
  setupReg := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1';
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, setupReg) or RegKeyExists(HKEY_CURRENT_USER, setupReg);
end;


function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if RuntimeDownloaded then
    Result := InstallDotNetRuntime();
end;


function InitializeSetup(): Boolean;
begin
  Result := True;
  if GetIsUpgrade() then
    begin
      MsgBox('{#AppId} is already installed.', mbInformation, MB_OK);
      Result := False;
    end;
end;


function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := True;
end;


procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;


function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpReady) and not DotNetRuntimeAlreadyExists('Microsoft.WindowsDesktop.App 6.0.') then begin
    RuntimeDownloaded := DownloadDotNetRuntime();
    if not RuntimeDownloaded then
      Result := False; // TODO: give warning instead of return false
  end;
end;

function NeedRestart(): Boolean;
begin
  Result := RequiresRestart;
end;