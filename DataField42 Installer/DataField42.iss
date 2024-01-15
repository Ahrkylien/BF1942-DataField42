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
DefaultDirName={code:GetBF1942Directory}
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
OutputBaseFilename={#AppId} v{#AppVersion} Installer

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "sp"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"

[Messages]
WizardSelectDir=Select the Battlefield 1942 installation folder
sp.WizardSelectDir=Selecciona la carpeta de instalación de Battlefield 1942
fr.WizardSelectDir=Sélectionnez le dossier d'installation de Battlefield 1942
it.WizardSelectDir=Seleziona la cartella di installazione di Battlefield 1942
de.WizardSelectDir=Wählen Sie den Installationsordner von Battlefield 1942 aus
ru.WizardSelectDir=Выберите папку установки Battlefield 1942
ja.WizardSelectDir=Battlefield 1942 のインストールフォルダを選択してください

SelectDirLabel3=Setup will install [name] into the following folder. This must be the Battlefield 1942 installation folder.
sp.SelectDirLabel3=La instalación colocará [name] en la siguiente carpeta. Esta debe ser la carpeta de instalación de Battlefield 1942.
fr.SelectDirLabel3=L'installation placera [name] dans le dossier suivant. Ceci doit être le dossier d'installation de Battlefield 1942.
it.SelectDirLabel3=L'installazione installerà [name] nella seguente cartella. Questa deve essere la cartella di installazione di Battlefield 1942.
de.SelectDirLabel3=Die Installation wird [name] in den folgenden Ordner installieren. Dies muss der Installationsordner von Battlefield 1942 sein.
ru.SelectDirLabel3=Установка разместит [name] в следующей папке. Это должна быть папка установки Battlefield 1942.
ja.SelectDirLabel3=Setupは[name]を次のフォルダにインストールします。これはBattlefield 1942のインストールフォルダである必要があります。

DiskSpaceMBLabel=The installation process will include setting up the .NET runtime, which is necessary for DataField42 to function properly.
sp.DiskSpaceMBLabel=El proceso de instalación incluirá la configuración del tiempo de ejecución de .NET, necesario para que DataField42 funcione correctamente.
fr.DiskSpaceMBLabel=Le processus d'installation inclura la configuration de l'environnement d'exécution .NET, nécessaire au bon fonctionnement de DataField42.
it.DiskSpaceMBLabel=Il processo di installazione includerà l'installazione dell'ambiente di runtime .NET, necessario per il corretto funzionamento di DataField42.
de.DiskSpaceMBLabel=Der Installationsprozess umfasst die Einrichtung der .NET-Runtime, die für die ordnungsgemäße Funktion von DataField42 erforderlich ist.
ru.DiskSpaceMBLabel=Процесс установки будет включать настройку среды выполнения .NET, необходимой для правильной работы DataField42.
ja.DiskSpaceMBLabel=インストールプロセスでは、DataField42の正常な動作に必要な.NETランタイムの設定が含まれます。

[CustomMessages]
en.NotValidBF1942Directory=Please select a valid BF1942 directory!
sp.NotValidBF1942Directory=¡Por favor, selecciona un directorio de BF1942 válido!
fr.NotValidBF1942Directory=Veuillez sélectionner un répertoire BF1942 valide !
it.NotValidBF1942Directory=Si prega di selezionare una directory BF1942 valida!
de.NotValidBF1942Directory=Bitte wählen Sie ein gültiges BF1942-Verzeichnis aus!
ru.NotValidBF1942Directory=Выберите действительный каталог BF1942, пожалуйста!
ja.NotValidBF1942Directory=有効なBF1942ディレクトリを選択してください！

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


function CheckBF1942Directory(DirectoryPath: String): Boolean;
begin
  Result := FileExists(DirectoryPath + '\BF1942.exe');
end;


function GetBF1942Directory(def: String): String;
var
  PathFromRegistry : string;
begin
  Result := ''; // Default path
  
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\EA GAMES\Battlefield 1942', 'GAMEDIR', PathFromRegistry) and CheckBF1942Directory(PathFromRegistry) then
    Result := PathFromRegistry
  else if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Origin\Battlefield 1942', 'GAMEDIR', PathFromRegistry) and CheckBF1942Directory(PathFromRegistry) then
    Result := PathFromRegistry
  else if CheckBF1942Directory(ExpandConstant('{pf32}') + '\EA Games\Battlefield 1942') then
    Result := ExpandConstant('{pf32}') + '\EA Games\Battlefield 1942'
  else if CheckBF1942Directory(ExpandConstant('{pf64}') + '\EA Games\Battlefield 1942') then
    Result := ExpandConstant('{pf64}') + '\EA Games\Battlefield 1942'
  else if CheckBF1942Directory(ExpandConstant('{pf}') + '\EA Games\Battlefield 1942') then // very future proof
    Result := ExpandConstant('{pf}') + '\EA Games\Battlefield 1942';
end;


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


function GetIsUpgrade: Boolean;
var
  SetupReg: string;
begin
  SetupReg := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1';
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, SetupReg) or RegKeyExists(HKEY_CURRENT_USER, SetupReg);
end;


function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := True;
end;

(* Event Functions: *)

function InitializeSetup(): Boolean;
begin
  Result := True;
  if GetIsUpgrade() then
    begin
      MsgBox('{#AppId} is already installed.', mbInformation, MB_OK);
      Result := False;
    end;
end;


procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True
  if (CurPageID = wpSelectDir) and (not CheckBF1942Directory(WizardDirValue)) then begin
    MsgBox(ExpandConstant('{cm:NotValidBF1942Directory}'), mbError, MB_OK)
    Result := False;
  end;
end;


procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectDir then
    WizardForm.NextButton.Caption := SetupMessage(msgButtonInstall);
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