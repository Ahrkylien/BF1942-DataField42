<#
.SYNOPSIS
    Sets up a developer machine to run the website. Run it once.

.DESCRIPTION
    Asks how to reach your database and where the game files are kept, stores both where they
    belong, creates the database and its schema, and creates an admin account to sign in with.
    Safe to re-run.

.PARAMETER ConnectionString
    How to reach your database. Prompted for if not given, and the prompt may be left empty to use
    the default in appsettings.Development.json: localhost as 'root' with no password, which is
    what XAMPP and a default MySQL install provide.

.PARAMETER FileDataBaseDirectory
    Where the game files are kept; its 'mods' subdirectory is what the scan and analysis jobs read.
    Prompted for if not given, and the prompt may be left empty to keep the current value or, if
    there is none, to use a 'game-files' folder beside the project. Always stored in
    appsettings.Local.json, along with the file transfer directory inside it.

.PARAMETER AdminUser
    The admin account to create. Defaults to 'admin'.

.PARAMETER AdminPassword
    Its password. Defaults to 'admin-password'. Local only -- never use this on a real server.
#>

[CmdletBinding()]
param(
    [string] $ConnectionString,
    [string] $FileDataBaseDirectory,
    [string] $AdminUser = 'admin',
    [string] $AdminPassword = 'admin-password'
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 writes JSON with a double space after every colon and nested objects pushed
# out to the column of their parent key, which PowerShell 7 does not. Re-indented here so the file
# looks the same whichever version runs this script, and like every other settings file.
function ConvertTo-IndentedJson($InputObject) {
    $depth = 0
    $lines = foreach ($line in ($InputObject | ConvertTo-Json -Depth 10) -split "`r?`n") {
        $text = $line.Trim() -replace '^("(?:[^"\\]|\\.)*"):\s+', '$1: '
        if ($text -match '^[}\]]') { $depth-- }
        ('  ' * $depth) + $text
        if ($text -match '[{\[]$') { $depth++ }
    }
    ($lines -join "`n") + "`n"
}
$project = Join-Path $PSScriptRoot 'DataField42.Website\DataField42.Website.csproj'
$localSettingsPath = Join-Path (Split-Path $project -Parent) 'appsettings.Local.json'

# Read now, so the prompt can offer what is already configured. Other settings in the file are kept.
$localSettings = if (Test-Path $localSettingsPath) {
    Get-Content $localSettingsPath -Raw | ConvertFrom-Json
} else {
    [pscustomobject]@{}
}
$currentDirectory = $localSettings.Website.FileDataBaseDirectory

if (-not $ConnectionString) {
    Write-Host 'Any MySQL or MariaDB server will do. The database itself does not have to exist yet.'
    Write-Host 'Press Enter to use localhost as root with no password, or give a connection string' -ForegroundColor DarkGray
    Write-Host 'such as: server=127.0.0.1;port=3307;database=DataField42;user=bf1942;password=secret' -ForegroundColor DarkGray
    $ConnectionString = Read-Host 'Connection string'
}

if (-not $FileDataBaseDirectory) {
    Write-Host ''
    Write-Host 'The folder the game files are kept in. It needs a "mods" subfolder, which is created if missing.'
    if ($currentDirectory) {
        Write-Host "Press Enter to keep $currentDirectory" -ForegroundColor DarkGray
    } else {
        Write-Host 'Press Enter for the default, a "game-files" folder beside the project.' -ForegroundColor DarkGray
    }
    $FileDataBaseDirectory = Read-Host 'Game files folder'
}

# Nothing else supplies this setting, so it is always written: the answer given, the one already
# there, or a 'game-files' folder beside the project. Stored in a local, untracked file rather than
# user secrets, because a path is not a secret. The file transfer's working files live inside it.
if (-not $FileDataBaseDirectory) {
    $FileDataBaseDirectory = if ($currentDirectory) { $currentDirectory } else { Join-Path (Split-Path $project -Parent) 'game-files' }
}

$FileDataBaseDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($FileDataBaseDirectory)
New-Item -ItemType Directory -Force -Path (Join-Path $FileDataBaseDirectory 'mods') | Out-Null

if (-not $localSettings.Website) {
    $localSettings | Add-Member -NotePropertyName Website -NotePropertyValue ([pscustomobject]@{})
}
$localSettings.Website | Add-Member -NotePropertyName FileDataBaseDirectory -NotePropertyValue $FileDataBaseDirectory -Force
$localSettings.Website | Add-Member -NotePropertyName FileTransferDirectory -NotePropertyValue (Join-Path $FileDataBaseDirectory 'fileTransfer') -Force

[System.IO.File]::WriteAllText($localSettingsPath, (ConvertTo-IndentedJson $localSettings), (New-Object System.Text.UTF8Encoding $false))
Write-Host "Game files folder: $FileDataBaseDirectory (stored in appsettings.Local.json)."

# Build once, then call the application itself: setting up a database is something the website
# knows how to do, and this script only decides what to ask for and in which order.
$buildOutput = dotnet build $project --nologo --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    $buildOutput | Write-Host
    throw 'The build failed.'
}

$targetPath = (dotnet msbuild $project -getProperty:TargetPath -verbosity:quiet).Trim()
$website = [System.IO.Path]::ChangeExtension($targetPath, '.exe')
if (-not (Test-Path $website)) { throw "Could not find the built application at $website." }

# The setup commands read and write user secrets, which only apply in the Development environment.
$env:ASPNETCORE_ENVIRONMENT = 'Development'

# Run from the project folder, as Visual Studio does: the application resolves its own files --
# db/schema.sql among them -- relative to the directory it was started in.
Push-Location (Split-Path $project -Parent)
try {

    # Given a connection string, init-db also stores it; given none, it uses the configured
    # default and leaves the settings alone.
    if ($ConnectionString) { & $website init-db $ConnectionString } else { & $website init-db }
    if ($LASTEXITCODE -ne 0) { throw 'init-db failed.' }

    & $website create-admin $AdminUser $AdminPassword
    if ($LASTEXITCODE -ne 0) { throw 'create-admin failed.' }

}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "Done. Press F5, then sign in at /Login as '$AdminUser'." -ForegroundColor Green
Write-Host 'The map and mod pages stay empty until there are mods to read: copy some mod folders into the'
Write-Host '"mods" subfolder of the game files folder, then run the scan and analyse jobs from /Admin.'
