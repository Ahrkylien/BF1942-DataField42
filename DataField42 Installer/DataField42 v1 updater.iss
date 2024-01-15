#define AppId "DataField42"
#define AppExePath "..\DataField42\bin\Publish\" + AppId + ".exe"
#define AppVersion GetFileVersion(AppExePath)
#define DotNetRuntimeIntallerName "DotNetRuntimeInstaller.exe"

[Setup]
AppId={#AppId}
AppName={#AppId}
UninstallDisplayName={#AppId}
AppVersion={#AppVersion}
WizardStyle=modern
ShowLanguageDialog=auto
DisableDirPage=yes
DefaultDirName={src}
DirExistsWarning=no
AppendDefaultDirName=no
DefaultGroupName={code:GetBF1942Group}
DisableProgramGroupPage=yes
DisableReadyPage=yes
SolidCompression=yes
Compression=lzma2/ultra
SetupIconFile=../DataField42/logo.ico
UninstallDisplayIcon={app}\{#AppId}.exe
UninstallFilesDir={app}\{#AppId}
OutputDir=bin
OutputBaseFilename={#AppId} v1 to v2.0.0.0 Updater

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "sp"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"

[CustomMessages]
en.Run=Run %1
sp.Run=Ejecutar %1
fr.Run=Lancer %1
it.Run=Esegui %1
de.Run=%1 starten
ru.Run=Запустить %1
ja.Run=%1を実行

[Files]
Source: {#AppExePath}; DestDir: {app}

[Icons]
Name: {commondesktop}\{#AppId}; Filename: {app}\{#AppId}.exe; WorkingDir: {app}
Name: {group}\{#AppId}; Filename: {app}\{#AppId}.exe; WorkingDir: {app}

[Run]
Filename: "{app}\{#AppId}.exe"; Parameters: "install"; 
Filename: "{app}\{#AppId}.exe"; Description: {cm:Run,{#AppId}}; Flags: nowait postinstall 

[Code]
var
  RequiresRestart: Boolean;
  DownloadPage: TDownloadWizardPage;
  RuntimeDownloaded: Boolean;


function GetBF1942Group(def: String): String;
begin
  Result := 'EA Games\Battlefield 1942';
  
  if DirExists(ExpandConstant('{userprograms}') + '\EA Games\Battlefield 1942') then
    Result := 'EA Games\Battlefield 1942'
  else if DirExists(ExpandConstant('{userprograms}') + '\EA Games\Battlefield 1942 HD') then
    Result := 'EA Games\Battlefield 1942 HD'
  else if DirExists(ExpandConstant('{userprograms}') + '\EA Games\Battlefield 1942 WWII Anthology HD') then
    Result := 'EA Games\Battlefield 1942 WWII Anthology HD'
  else if DirExists(ExpandConstant('{userprograms}') + '\Battlefield 1942') then
    Result := 'Battlefield 1942'
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


function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := True;
end;

(* Event Functions: *)

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;


function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if not DotNetRuntimeAlreadyExists('Microsoft.WindowsDesktop.App 6.0.') then begin
    RuntimeDownloaded := DownloadDotNetRuntime();
    if RuntimeDownloaded then
      Result := InstallDotNetRuntime()
    else
      Result := 'Failed Downloading the .NET runtime';
  end;
end;


function NeedRestart(): Boolean;
begin
  Result := RequiresRestart;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  strContent: String;
  intErrorCode: Integer;
  strSelf_Delete_BAT: String;
begin
  if CurStep=ssDone then
  begin
    strContent := ':try_delete' + #13 + #10 +
          'del "' + ExpandConstant('{srcexe}') + '"' + #13 + #10 +
          'del "' + ExpandConstant('{src}') + '\\DataField42_updater.exe' + '"' + #13 + #10 +
          'del "' + ExpandConstant('{src}') + '\\DataField42 installer.exe' + '"' + #13 + #10 +
          'if exist "' + ExpandConstant('{srcexe}') + '" goto try_delete' + #13 + #10 +
          'del %0';

    strSelf_Delete_BAT := ExtractFilePath(ExpandConstant('{tmp}')) + 'SelfDelete.bat';
    SaveStringToFile(strSelf_Delete_BAT, strContent, False);
    Exec(strSelf_Delete_BAT, '', '', SW_HIDE, ewNoWait, intErrorCode);
  end;
end;