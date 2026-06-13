#define AppId "DataField42"
#define AppExePath "..\DataField42\bin\Publish\" + AppId + ".exe"
#define AppVersion GetFileVersion(AppExePath)

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


function GetIsUpgrade: Boolean;
var
  SetupReg: string;
begin
  SetupReg := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1';
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, SetupReg) or RegKeyExists(HKEY_CURRENT_USER, SetupReg);
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
