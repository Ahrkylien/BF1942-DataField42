# DataField42 Central Server

The bf1942.eu website: the map and mod collection, the live server list and round history, and the
admin pages the collection is maintained from.

## How it fits together

| Project | Contains |
| --- | --- |
| `DataField42 Central Server/DataField42.Website` | ASP.NET Core MVC: pages, data access, admin jobs, file transfer |
| `BF1942.FileSystem` | Refractor Flat Archives and the path-addressable file systems over them |
| `BF1942.Core` | The `.con` script engine, lexicons, the server query |

The database is the central server's existing MariaDB schema, shared with the Python central server
and the servers tracker. It is mapped explicitly in `Data/DataField42DbContext.cs` and there are no
migrations for it: nothing here may rename a column those tools also write. The two tables this
application owns, `AdminUser` and `AdminJobRun`, are created on startup if they are missing.

The database and its tables use `utf8mb4_unicode_ci`, set explicitly so that sort order does not depend on the server version.

## Configuration

Settings come from several places, and a later one overrides an earlier one. In Development all of
them apply; in Production only the first, the environment-specific file and the last two do.

| # | Source | Holds | In repo | Environments |
| --- | --- | --- | --- | --- |
| 1 | `appsettings.json` | Every setting, with the values the application documents itself by | yes | all |
| 2 | `appsettings.{Environment}.json` | What differs for that environment. Only `appsettings.Development.json` exists: log levels, and the database on localhost as `root` with no password that `setup-local` falls back on when you press Enter | yes | matching one |
| 3 | User secrets | The connection string, written by `init-db` | no | Development |
| 4 | `appsettings.Local.json` | What differs on your machine and is not secret, such as where the game files are | no | Development |
| 5 | Environment variables | Everything the production server needs | no | all |
| 6 | Command line | A one-off override | no | all |

User secrets are for things that would do harm if published, which is a database password and
nothing else here. A filesystem path is not a secret, so it goes in a file named for what it is.

`appsettings.json` deliberately leaves everything about one deployment empty: the paths, the
filesystems to report on and the per-server player corrections name hosts and directories that have
no business in a public repository. The production server supplies them as environment variables.
A section is addressed with `__`, and a list or dictionary entry by its index or key:

```bash
ConnectionStrings__DataField42="server=localhost;database=DataField42;user=DataField42;password=..."
Website__FileDataBaseDirectory=/srv/datafield42/data
Website__FileTransferDirectory=/srv/datafield42/data/fileTransfer
Website__MonitoredFilesystems__0=/dev/sda1
Website__PlayerCountCorrections__203.0.113.10=1
```

A required setting that is missing or empty stops the application at startup, with a message naming
it, rather than serving a site that only looks like it works.

The database version, which decides what SQL is generated, is not a setting: the application asks
the server once at startup and logs what it found. The database therefore has to be reachable for
the site to start, and an unreachable one stops it with a message naming the host and port.

### Settings

Everything below is under `Website`, except the connection string.

| Setting | Required | Default | Meaning |
| --- | --- | --- | --- |
| `ConnectionStrings:DataField42` | yes | none | How to reach the database |
| `FileDataBaseDirectory` | yes | none | Root of the game files. Mods live in its `mods` subdirectory, which is what the scan and analysis jobs read |
| `FileTransferDirectory` | yes | none | Where the file transfer keeps its file list, rules and uploads |
| `DefaultMod` | no | `BF1942` | The mod the maps page opens on |
| `MasterServerUrl` | no | `http://master.bf1942.org/json?full` | Where the live server list is read from |
| `ServerListCacheDuration` | no | `00:00:20` | How long a fetched server list is reused |
| `ServerQueryTimeout` | no | `00:00:03` | How long to wait for a game server to answer |
| `PlayerCountCorrections` | no | empty | Players to subtract per server IP, for a server with a permanently connected bot or spectator |
| `FeaturedServerId` | no | `413` in `appsettings.json` | The tracked server the extra page graphs an average week for |
| `MonitoredFilesystems` | no | empty | Filesystems to report free space for, as `df` names them, on the extra page |
| `PlayerActivitiesFile` | no | none | The server manager's map activity file, read by `/MyServer` |
| `ScrapedPagesFile` | no | none | The scraper's output, read by `/Scraping` |
| `FolderInfosFile` | no | none | The folder comparison file, read by `/ToDoFiles` |
| `Update:SourceDirectory`, `BuildDirectory`, `TargetDirectory`, `StateDirectory` | no | none | The clone, the build folder, the live folder and the status folder the Update page uses. Set by `setup-server.sh`; see *Updating from the admin panel* |

Durations are written `hh:mm:ss`. A relative `FileDataBaseDirectory` or `FileTransferDirectory`
resolves against the project folder, not against the directory the process was started from. The
three file settings at the end are used as given.

## Running it

```bash
dotnet run --project "DataField42 Central Server/DataField42.Website"
```

Static assets are served under `/static`, so every asset URL is the same as it was.

## Running it locally

Start any MySQL or MariaDB server, then run this once:

```bash
.\setup-local.ps1
```

It asks for two things, and Enter accepts the default for either:

- **A connection string.** Enter means localhost as `root` with no password, which is what XAMPP and
  a default MySQL install provide. The database itself does not have to exist yet. A string you give
  is stored in user secrets.
- **The folder the game files are kept in.** Enter keeps the one already configured or, on a first
  run, uses a `game-files` folder beside the project. Whichever it ends up being is created, with
  its `mods` subfolder, and always stored in `appsettings.Local.json` along with the file transfer
  folder inside it. Nothing else supplies these two settings, so the site does not start until the
  script has run. Other settings already in that file are kept.

It then creates the database and its schema, and an `admin` account with the password
`admin-password`. Set DataField42.Website as the startup project and press F5; it is the project in
the "DataField42 Central Server" folder. Both of the answers can also be given as parameters,
`-ConnectionString` and `-FileDataBaseDirectory`, to run it without prompts.

Re-running it is safe. If your execution policy refuses unsigned scripts -- `AllSigned` does, and
says "is not digitally signed" -- run `setup-local.cmd` instead. It starts the same script for that
one process with the policy bypassed, takes the same parameters, and changes nothing about the
machine. Or run the two commands the script wraps:

```bash
dotnet run --project "DataField42 Central Server/DataField42.Website" -- init-db "server=127.0.0.1;database=DataField42;user=root;password="
```

```bash
dotnet run --project "DataField42 Central Server/DataField42.Website" -- create-admin admin admin-password
```

`init-db` given a connection string also saves it, so that is the only place one has to be typed;
given none, it uses the configured one. `create-admin` adds an account or changes an existing one's
password. Neither runs on startup, which is deliberate: see *Why the site does not create its own
schema* below.

Two things worth knowing:

- User secrets and `appsettings.Local.json` are read in Development only, which is what Visual
  Studio's launch profiles set. Running with no launch profile means Production, where everything
  has to come from the environment.
- The map and mod pages are empty until there are files to read. Copy some mod folders into the
  `mods` subfolder of the game files folder, then run the "Scan mods", "Scan maps and archives" and
  "Analyse maps" jobs from `/Admin`, the same path the production data was built by. The server list
  comes from the master server, so it is live even locally; round history is written by the tracker,
  so a local instance has none.

## Installing on a server

`setup-server.sh` installs the site on a Debian or Ubuntu server: the .NET SDK, MariaDB with the
database and its user, Caddy as the web server with an HTTPS certificate, the code, a build, and a
systemd service that keeps the site running. Log in to the server with an account that can use
`sudo`, and from any directory, such as your home directory, run:

```bash
curl -fsSL "https://raw.githubusercontent.com/Ahrkylien/BF1942-DataField42/main/DataField42%20Central%20Server/setup-server.sh" -o setup-server.sh
sudo bash setup-server.sh
```

It needs root because it installs packages and writes to `/etc` and `/srv`. The downloaded copy can
be deleted afterwards. On a minimal install without `curl` or `sudo`, run `apt install curl sudo`
as root first, or run the script as root directly.

It asks for three things:

- **The domain**, such as `bf1942.eu`. Caddy gets a certificate for it, which only works once the
  domain's DNS points at the server and ports 80 and 443 are open. `-` serves plain HTTP by IP.
- **The folder for the game files**, `/srv/datafield42/data` by default. Mods go in its `mods`
  subfolder, and the file transfer keeps its working files in `fileTransfer` beside it. Pick a
  folder on a disk with room for the collection. It is asked on the first run; after that the
  setting in `website.env` decides.
- **The first admin account**, only while there is none yet.

Both can be given up front to run it without prompts:
`sudo DOMAIN=bf1942.eu DATA_DIR=/mnt/games ADMIN_USER=admin ADMIN_PASSWORD='...' bash setup-server.sh`.
`BRANCH=...` deploys a branch other than `main`.

Re-running it is safe, whether the script changed or it stopped halfway: every step checks what is
already there, and the settings file and passwords from the first run are kept. Each run also pulls
and deploys the latest code, so it is a manual update as well. The build goes to its own folder
first, so a failed build leaves the running site alone. To re-run, use the same two commands, or
`sudo bash "/srv/datafield42/src/DataField42 Central Server/setup-server.sh"`.

Afterwards the server looks like this:

```text
/srv/datafield42/                  everything owned by the datafield42 user
  src/                             git clone of the whole repository
  build/                           output of the last build
  update/                          the Update page's status, log and deployed version
  app/                             the live site; the service runs DataField42.Website.dll here
<game files folder>/               Website:FileDataBaseDirectory, chosen during the install
  mods/                            the game files: copy mod folders here
  fileTransfer/                    Website:FileTransferDirectory
/etc/datafield42/
  website.env                      the site's settings, as environment variables (see Configuration)
  setup.conf                       the answers given to the script, offered again next run
/etc/systemd/system/datafield42.service
/etc/caddy/Caddyfile               rewritten by the script on every run
/usr/share/dotnet/                 the .NET SDK, linked as /usr/bin/dotnet
/var/lib/datafield42/              home of the datafield42 user, with its NuGet cache
```

`website.env` is where this server's settings live: the connection string with a generated database
password, the two directories, and commented-out lines for the optional settings. The script creates
it once and never changes it. After editing it, run `sudo systemctl restart datafield42`. The site's
log is in `journalctl -u datafield42 -f`.

Files copied into the game files folder's `mods` subfolder have to be writable by the site, since
the file transfer moves files there: copy them as the `datafield42` user, or run `sudo chown -R datafield42: <game files folder>`
afterwards. Then run the scan and analyse jobs from `/Admin`. To move an existing database here,
import its dump into the `DataField42` database; the script only creates tables that are missing.

## Updating from the admin panel

On a server installed with `setup-server.sh`, `/Admin/Update` deploys the latest code from GitHub.
It shows the branch and commit the site is running, a branch list with `main` selected, and two
buttons:

- **Check for updates** fetches the branch from GitHub and lists the commits the running site lacks.
- **Update** fetches the branch into the clone, builds it in Release mode into a folder of its own,
  and, only if the build succeeds, copies the result over the live folder and restarts the site. A
  failed build never touches the running site: the page shows the build's output and the old version
  keeps running. The page follows the update through the restart and shows how it ended.

Everything happens inside the site, as the user it runs as, with no extra privileges. The copy writes
each file beside the old one and renames it into place, so a file the running site has open is never
rewritten, and removes files the new build no longer has. `wwwroot/images/ingamemaps`, where the
"Analyse maps" job writes, is never deleted from. The restart is the site stopping itself once the
copy is done; the systemd service's `Restart=always` starts it again from the new files. Only one
update runs at a time, and the repository is public, so no credentials are involved.

The page needs four settings, which `setup-server.sh` writes to `website.env`, also into a file from
an earlier install: `Website__Update__SourceDirectory` (the clone), `BuildDirectory`,
`TargetDirectory` (the live folder) and `StateDirectory`, where the update keeps its `status`,
`update.log` and a `deployed` record of the branch and commit. Without them, as on a development
machine, the page says updating is not available.

A schema change that a new version needs is applied by the site itself for the tables it owns. For
the shared tables, run `init-db` as the install script does.

## Why the site does not create its own schema

These tables are written by the Python central server and the servers tracker as well as by this
application, so this application does not own them. It maps onto them and creates nothing. Only
`AdminUser` and `AdminJobRun`, which it does own, are created automatically.

Creating a shared schema at startup would need DDL privileges at run time when the site's database
user should only need DML, would race between instances starting together, and would turn a
mistyped connection string into an empty site that looks like it is working rather than an error
that says what is wrong.

So schema changes are applied deliberately, by hand or through `init-db`, and the site fails fast
when what it expects is not there.

## Admin access

There are no addresses or accounts in the code. Admin pages redirect to `/Login`, which is linked
from nowhere, and sign-in is against the `AdminUser` table with a hashed password. Create the first
account on the server:

```bash
dotnet DataField42.Website.dll create-admin <username> <password>
```

The same command changes an existing account's password, which also signs out its open sessions.

## Servers tracker

The round history and the player counts behind `/Servers`, the round pages and the graphs are
recorded by a background service in the site, `ServerTrackerService`. It always runs, so only one
instance may point at a database: two would both write the same history.

Every minute it reads the master server's list, which carries for each server its latest status, when
its current round started, and the status its last finished round ended with. From that it records:

- **Servers**, found by address and game port, added when first seen.
- **Rounds**, found by server and start time, added when first seen, with the map, mod, mode, starting
  tickets and start delay. A round is marked as having had players once anyone is on it.
- **Player numbers** per round, and a total over all servers. A number that has not changed since the
  last two samples moves the latest sample forward instead of adding a row, so a quiet server costs two
  rows, the first and the latest, and the two still say when the number held.
- **The end of a round**, once, when the master server reports it finished: end time, tickets, the
  winner (the team with more tickets, or a draw), the scoreboard, the most players, and the average
  number of players weighted by how long each count held.

Times are stored in the local time of the machine, as the history always was, so keep the server's time
zone the same when moving the tracker. The one minute and the five-second retry, when the master server
cannot be read, are fixed in the code; a failure on one server skips that server for that pass and nothing else.

## Admin jobs

Each job runs in the background and reports its progress. Starting and stopping a job, and watching
it run, happen in place on the page: the button sends its request with `fetch`, the card redraws
from the job's new state, and the page polls once a second only while something is running. When a
job ends, the parts of the page that show its result, such as the file list written by stage 1, are
fetched again and swapped in. Nothing reloads. The forms still work as ordinary posts without script
(`wwwroot/js/admin-jobs.js` is what takes them over). One run of a job at a time; a running job can
be stopped, and stops after its current item rather than mid-write.

Every run is kept in the `AdminJobRun` table, so what a job did survives a restart or a reboot. A
running job is saved every five seconds, so a crash loses seconds and not the run. When the server
starts, a run still recorded as running is marked **Interrupted**: whatever was running it is gone,
and the log says so. Each card shows the last run after a restart, and "Earlier runs" lists the
every run of that job with its whole log in an expandable block, also available as text at
`/Admin/Runs/{id}/Log`. Nothing is ever removed: no run and no line of log is dropped. Saving never fails a job: if the database
cannot be written, the job carries on and the failure is logged.

"Scan maps and archives" and "Analyse maps" can be limited to a single mod.

The reference pages — `/AllCMD`, `/ObjectTemplate`, `/GeometryTemplate` and `/GC_Errors` — are
fixed HTML documents in `Resources/reference/`, read at run time and rendered inside the site
layout. They are deliberately not views: together they are about a megabyte of markup, and
compiling that as Razor adds minutes to every build that touches any view.

The hand-maintained files live in `Resources/`: `authors.yaml` and `tags.yaml` say who made what,
`modInfo.yaml` gives display names for mods whose own `init.con` has none, `integrityExceptions.yaml`
lists the findings on `/ErrorList` that are deliberate, and `clanTags.json` splits clan tags off
player names.

## File transfer

The collection is sorted on a workstation and served from here, and the two are brought back in line
in five stages. Only genuinely new files are ever transferred: a map that was renamed or moved is
renamed or moved in place.

1. **Here.** List every file this server holds, with a CRC-32C checksum, and download that list.
2. **Here.** Upload the rules and the archive the workstation produced, review the preview, and apply.
3. **Workstation.** List its own files.
4. **Workstation.** Compare the two lists into a set of rules.
5. **Workstation.** Pack the files this server is missing.

The preview reports what the rules would delete, move, copy, add and re-date, and warns when the
rules do not match what is on disk -- a file the rules add that is not in the archive, a source that
is not where the rules expect it, or a destination that is already occupied. Those warnings mean the
rules were built against a different state of the collection; re-run stage 1 and rebuild them.

The file list is JSON and the rules are a text file; the workstation's stage 3 to 5 tooling reads
and writes the same formats.

## Pages that need a login

`/Scraping`, `/ToDoFiles` and `/MyServer` are working lists of maps still to be collected and sorted.
They read files produced by other tools and describe work in progress, so they require an admin
session. Every other page is public.
