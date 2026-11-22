#!/usr/bin/env bash
#
# Installs the DataField42 website on a Debian or Ubuntu server, or brings an existing install up to
# date. Run it as root:
#
#     sudo bash setup-server.sh
#
# Every step checks what is already there, so running it again is safe: after a change to this
# script, after it stopped halfway, or simply to deploy the latest code. Settings and passwords
# written on the first run are kept.
#
# Answers can be given up front instead of at the prompts, for example:
#
#     sudo DOMAIN=bf1942.eu ADMIN_USER=admin ADMIN_PASSWORD='a-long-password' bash setup-server.sh
#
# DOMAIN          Domain the site is served on. Caddy gets an HTTPS certificate for it. Empty means
#                 plain HTTP on port 80, by IP address.
# DATA_DIR        Folder the game files are kept in (its mods/ subfolder holds the mods), and
#                 where the file transfer keeps its working files. Default: /srv/datafield42/data.
#                 Asked for on the first run only: after that, website.env decides.
# BRANCH          Branch to deploy. Default: main, or the one used last time.
# ADMIN_USER      First admin account, only asked for while there is none. Default: admin.
# ADMIN_PASSWORD  Its password.

set -Eeuo pipefail

REPO_URL="${REPO_URL:-https://github.com/Ahrkylien/BF1942-DataField42.git}"

INSTALL_DIR=/srv/datafield42
SRC_DIR=$INSTALL_DIR/src       # git clone of the whole repository
BUILD_DIR=$INSTALL_DIR/build   # output of the last build
APP_DIR=$INSTALL_DIR/app       # the live site; the service runs from here
DEFAULT_DATA_DIR=$INSTALL_DIR/data  # where the game files go unless another folder is chosen
UPDATE_DIR=$INSTALL_DIR/update # the admin panel's Update page: status, log and deployed version

CONFIG_DIR=/etc/datafield42
ENV_FILE=$CONFIG_DIR/website.env   # the site's settings, including the database password
SETUP_FILE=$CONFIG_DIR/setup.conf  # answers given to this script, offered again on the next run

SERVICE=datafield42
SERVICE_USER=datafield42
SERVICE_HOME=/var/lib/datafield42  # home of the service user; holds the NuGet cache
DOTNET_DIR=/usr/share/dotnet
DOTNET_CHANNEL=10.0
APP_PORT=5000
PROJECT="DataField42 Central Server/DataField42.Website/DataField42.Website.csproj"

# Written by the "Analyse maps" job into the live site, so an update must never delete them.
GENERATED_DIR=wwwroot/images/ingamemaps

step() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
info() { printf '    %s\n' "$*"; }
fail() { printf '\n\033[1;31mError: %s\033[0m\n' "$*" >&2; exit 1; }
trap 'fail "the command on line $LINENO failed. Fix the cause and run the script again; finished steps are skipped or repeated safely."' ERR

as_service_user() {
    runuser -u "$SERVICE_USER" -- env HOME="$SERVICE_HOME" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 "$@"
}

# Runs the application once with exactly the settings the service gets, for its setup commands.
# The fixed description keeps the arguments, such as a password, out of the system log.
run_app() {
    systemd-run --wait --pipe --quiet --collect --description="DataField42 setup command" \
        -p User="$SERVICE_USER" -p WorkingDirectory="$APP_DIR" -p EnvironmentFile="$ENV_FILE" \
        /usr/bin/dotnet "$APP_DIR/DataField42.Website.dll" "$@"
}

have_tty() { [[ -e /dev/tty ]] && { : </dev/tty; } 2>/dev/null; }

ask() { # ask <prompt> <default>
    local answer
    have_tty || { echo "$2"; return; }
    read -rp "    $1 [$2]: " answer </dev/tty
    echo "${answer:-$2}"
}

# A value from the connection string in the settings file, such as password or server.
connection_value() {
    awk -v key="$1" 'sub(/^ConnectionStrings__DataField42=/, "") {
        n = split($0, parts, ";")
        for (i = 1; i <= n; i++) {
            split(parts[i], kv, "=")
            if (tolower(kv[1]) == key) { print substr(parts[i], length(kv[1]) + 2); exit }
        }
    }' "$ENV_FILE"
}

# Adds a setting to the settings file unless it is there, so a server installed before the setting
# existed gets it without the file, which is otherwise never touched, being rewritten.
add_setting() { # add_setting <name> <value>
    grep -q "^$1=" "$ENV_FILE" || printf '%s=%s\n' "$1" "$2" >>"$ENV_FILE"
}

# --------------------------------------------------------------------------------------------------
step "Checking the system"

[[ $EUID -eq 0 ]] || fail "run this as root: sudo bash $0"
command -v apt-get >/dev/null || fail "this script supports Debian and Ubuntu (apt) only."
command -v systemctl >/dev/null || fail "this script needs systemd."

# Answers given on the command line win over the ones saved by the last run.
domain_given=${DOMAIN+yes}; domain_arg=${DOMAIN-}
branch_given=${BRANCH+yes}; branch_arg=${BRANCH-}
data_dir_given=${DATA_DIR+yes}; data_dir_arg=${DATA_DIR-}
# shellcheck source=/dev/null
[[ -f $SETUP_FILE ]] && source "$SETUP_FILE"
[[ -n $domain_given ]] && DOMAIN=$domain_arg
[[ -n $branch_given ]] && BRANCH=$branch_arg
[[ -n $data_dir_given ]] && DATA_DIR=$data_dir_arg
info "$(. /etc/os-release && echo "$PRETTY_NAME")"

# --------------------------------------------------------------------------------------------------
step "Settings"

if [[ -z $domain_given ]]; then
    DOMAIN=$(ask "Domain to serve the site on (- for none: plain HTTP by IP)" "${DOMAIN:--}")
fi
[[ $DOMAIN == - ]] && DOMAIN=
BRANCH=${BRANCH:-main}

# The settings file is the source of truth for where the game files are, once it exists.
if [[ -f $ENV_FILE ]] && grep -q '^Website__FileDataBaseDirectory=' "$ENV_FILE"; then
    DATA_DIR=$(sed -n 's/^Website__FileDataBaseDirectory=//p' "$ENV_FILE" | head -n1)
elif [[ -z $data_dir_given ]]; then
    DATA_DIR=$(ask "Folder for the game files" "${DATA_DIR:-$DEFAULT_DATA_DIR}")
fi
DATA_DIR=${DATA_DIR:-$DEFAULT_DATA_DIR}
[[ $DATA_DIR == /* ]] || fail "the game files folder must be an absolute path, not '$DATA_DIR'."
DATA_DIR=${DATA_DIR%/}
info "Repository: $REPO_URL ($BRANCH)"
info "Domain:     ${DOMAIN:-none, HTTP on port 80}"
info "Game files: $DATA_DIR"

mkdir -p "$CONFIG_DIR"
cat >"$SETUP_FILE" <<EOF
# Answers given to setup-server.sh, offered again on the next run.
DOMAIN="$DOMAIN"
BRANCH="$BRANCH"
DATA_DIR="$DATA_DIR"
EOF

# --------------------------------------------------------------------------------------------------
step "Installing packages"

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq ca-certificates curl git gnupg rsync openssl mariadb-server libfontconfig1 >/dev/null

# .NET needs ICU; its package name carries the version, which differs per release.
icu=$(apt-cache pkgnames libicu | grep -E '^libicu[0-9]+$' | sort -V | tail -n1 || true)
[[ -n $icu ]] && apt-get install -y -qq "$icu" >/dev/null

# Caddy is in Debian 12+ and Ubuntu 23.04+; older releases get it from Caddy's own repository.
if ! apt-cache show caddy >/dev/null 2>&1; then
    info "Adding Caddy's package repository"
    curl -fsSL https://dl.cloudsmith.io/public/caddy/stable/gpg.key | gpg --dearmor --yes -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -fsSL https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt >/etc/apt/sources.list.d/caddy-stable.list
    apt-get update -qq
fi
apt-get install -y -qq caddy >/dev/null
info "git, rsync, MariaDB and Caddy are installed."

# --------------------------------------------------------------------------------------------------
step "Installing the .NET $DOTNET_CHANNEL SDK"

# Microsoft's install script works on any distribution and only downloads when a newer patch exists.
# The SDK, not just the runtime, because the site is built on the server.
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_DIR" >/dev/null
rm -f /tmp/dotnet-install.sh
ln -sf "$DOTNET_DIR/dotnet" /usr/bin/dotnet
info ".NET SDK $(dotnet --version)"

# --------------------------------------------------------------------------------------------------
step "Creating the service user and folders"

if ! id "$SERVICE_USER" >/dev/null 2>&1; then
    useradd --system --home-dir "$SERVICE_HOME" --create-home --shell /usr/sbin/nologin "$SERVICE_USER"
fi
mkdir -p "$SRC_DIR" "$BUILD_DIR" "$APP_DIR" "$UPDATE_DIR" "$DATA_DIR/mods" "$DATA_DIR/fileTransfer"
chown "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR" "$SRC_DIR" "$BUILD_DIR" "$APP_DIR" "$UPDATE_DIR" "$DATA_DIR" "$DATA_DIR/mods" "$DATA_DIR/fileTransfer"
info "$INSTALL_DIR, owned by $SERVICE_USER"

# --------------------------------------------------------------------------------------------------
step "Website settings"

# Created once and never overwritten: it is where the settings for this server are edited.
if [[ ! -f $ENV_FILE ]]; then
    db_password=$(openssl rand -hex 24)
    cat >"$ENV_FILE" <<EOF
# Settings for the DataField42 website, read by the $SERVICE service.
# After a change: sudo systemctl restart $SERVICE
# setup-server.sh creates this file once and never changes it. See the README for every setting.

ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:$APP_PORT
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

ConnectionStrings__DataField42=server=localhost;database=DataField42;user=datafield42;password=$db_password

Website__FileDataBaseDirectory=$DATA_DIR
Website__FileTransferDirectory=$DATA_DIR/fileTransfer

# Optional:
#Website__MonitoredFilesystems__0=/dev/sda1
#Website__PlayerCountCorrections__203.0.113.10=1
#Website__PlayerActivitiesFile=
#Website__ScrapedPagesFile=
#Website__FolderInfosFile=
EOF
    info "Created $ENV_FILE with a new database password."
else
    info "Keeping the existing $ENV_FILE."
fi

# The folders the admin panel's Update page works with. Added to an older file as well.
if ! grep -q '^Website__Update__' "$ENV_FILE"; then
    printf '\n# The admin panel'"'"'s Update page:\n' >>"$ENV_FILE"
fi
add_setting Website__Update__SourceDirectory "$SRC_DIR"
add_setting Website__Update__BuildDirectory "$BUILD_DIR"
add_setting Website__Update__TargetDirectory "$APP_DIR"
add_setting Website__Update__StateDirectory "$UPDATE_DIR"
chown "root:$SERVICE_USER" "$ENV_FILE"
chmod 640 "$ENV_FILE"

# --------------------------------------------------------------------------------------------------
step "Database"

db_server=$(connection_value server)
db_name=$(connection_value database)
db_user=$(connection_value user)
db_password=$(connection_value password)

if [[ $db_server == localhost || $db_server == 127.0.0.1 ]]; then
    systemctl enable --now mariadb >/dev/null 2>&1
    # The settings file is the source of truth: the account gets the password written there.
    mysql <<EOF
CREATE DATABASE IF NOT EXISTS \`$db_name\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$db_user'@'localhost' IDENTIFIED BY '$db_password';
CREATE USER IF NOT EXISTS '$db_user'@'127.0.0.1' IDENTIFIED BY '$db_password';
ALTER USER '$db_user'@'localhost' IDENTIFIED BY '$db_password';
ALTER USER '$db_user'@'127.0.0.1' IDENTIFIED BY '$db_password';
GRANT ALL PRIVILEGES ON \`$db_name\`.* TO '$db_user'@'localhost';
GRANT ALL PRIVILEGES ON \`$db_name\`.* TO '$db_user'@'127.0.0.1';
FLUSH PRIVILEGES;
EOF
    info "MariaDB database '$db_name' and user '$db_user' exist."
else
    info "The database is on $db_server, not here; skipping the local MariaDB setup."
fi

# --------------------------------------------------------------------------------------------------
step "Getting the code"

if [[ ! -d $SRC_DIR/.git ]]; then
    # A clone that stopped halfway leaves a folder without .git: start that one over.
    find "$SRC_DIR" -mindepth 1 -delete
    as_service_user git clone --quiet --branch "$BRANCH" "$REPO_URL" "$SRC_DIR"
else
    as_service_user git -C "$SRC_DIR" remote set-url origin "$REPO_URL"
    as_service_user git -C "$SRC_DIR" fetch --quiet --prune origin
    as_service_user git -C "$SRC_DIR" checkout --quiet --force -B "$BRANCH" "origin/$BRANCH"
fi
commit=$(as_service_user git -C "$SRC_DIR" rev-parse HEAD)
info "$BRANCH at $(as_service_user git -C "$SRC_DIR" log -1 --format='%h %s')"

# --------------------------------------------------------------------------------------------------
step "Building"

# Into its own folder first: if the build fails, the live site is not touched.
find "$BUILD_DIR" -mindepth 1 -delete
as_service_user dotnet publish "$SRC_DIR/$PROJECT" -c Release -o "$BUILD_DIR" \
    -p:SourceRevisionId="$commit" --nologo -v quiet
info "Build succeeded."

# --------------------------------------------------------------------------------------------------
step "Deploying"

systemctl stop "$SERVICE" 2>/dev/null || true

rsync -a --delete --exclude "/$GENERATED_DIR/" "$BUILD_DIR/" "$APP_DIR/"
# The map images in the repository are added, but the ones the site generated itself are kept.
if [[ -d $BUILD_DIR/$GENERATED_DIR ]]; then
    mkdir -p "$APP_DIR/$GENERATED_DIR"
    rsync -a "$BUILD_DIR/$GENERATED_DIR/" "$APP_DIR/$GENERATED_DIR/"
fi
chown -R "$SERVICE_USER:$SERVICE_USER" "$APP_DIR"

# What the admin panel's Update page shows as the running version.
printf 'branch=%s\ncommit=%s\n' "$BRANCH" "$commit" >"$UPDATE_DIR/deployed"
chown "$SERVICE_USER:$SERVICE_USER" "$UPDATE_DIR/deployed"

cat >/etc/systemd/system/$SERVICE.service <<EOF
[Unit]
Description=DataField42 website
After=network-online.target mariadb.service
Wants=network-online.target mariadb.service

[Service]
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$APP_DIR
EnvironmentFile=$ENV_FILE
ExecStart=/usr/bin/dotnet $APP_DIR/DataField42.Website.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=$SERVICE

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
info "Deployed to $APP_DIR."

# --------------------------------------------------------------------------------------------------
step "Preparing the database schema"

# Only creates what is missing, so it changes nothing in a database that is already set up.
run_app init-db

admin_count=0
if [[ $db_server == localhost || $db_server == 127.0.0.1 ]]; then
    admin_count=$(mysql -N -B -e "SELECT COUNT(*) FROM \`$db_name\`.AdminUser")
fi

if [[ $admin_count -eq 0 ]]; then
    ADMIN_USER=${ADMIN_USER:-$(ask "Admin account to create" admin)}
    generated=false
    if [[ -z ${ADMIN_PASSWORD:-} ]]; then
        if have_tty; then
            while :; do
                read -rsp "    Password for '$ADMIN_USER': " ADMIN_PASSWORD </dev/tty; echo
                [[ -n $ADMIN_PASSWORD ]] && break
                info "The password may not be empty."
            done
        else
            ADMIN_PASSWORD=$(openssl rand -base64 18)
            generated=true
        fi
    fi
    run_app create-admin "$ADMIN_USER" "$ADMIN_PASSWORD"
    $generated && info "Generated password for '$ADMIN_USER': $ADMIN_PASSWORD"
else
    info "An admin account exists already."
fi

# --------------------------------------------------------------------------------------------------
step "Starting the website"

systemctl enable "$SERVICE" >/dev/null 2>&1
systemctl restart "$SERVICE"

# Any HTTP answer counts: the site stops at startup on a bad setting or an unreachable database,
# while an error page can come from something outside it, such as the master server being down.
status=000
for _ in $(seq 30); do
    status=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$APP_PORT/" || true)
    [[ $status != 000 ]] && break
    sleep 1
done
if [[ $status == 000 ]]; then
    journalctl -u "$SERVICE" -n 30 --no-pager >&2
    fail "the site did not start. Its log is above; settings are in $ENV_FILE."
fi
info "The site answers on port $APP_PORT (HTTP $status)."

# --------------------------------------------------------------------------------------------------
step "Web server (Caddy)"

# This script owns the Caddyfile and writes it on every run.
cat >/etc/caddy/Caddyfile <<EOF
# Written by setup-server.sh; changes here are overwritten on its next run.
${DOMAIN:-:80} {
    encode zstd gzip
    reverse_proxy 127.0.0.1:$APP_PORT
}
EOF
systemctl enable caddy >/dev/null 2>&1
systemctl reload-or-restart caddy
info "Caddy forwards ${DOMAIN:-port 80} to the site."

# --------------------------------------------------------------------------------------------------
step "Done"

if [[ -n $DOMAIN ]]; then
    info "https://$DOMAIN  (the domain's DNS must point at this server, with ports 80 and 443 open)"
else
    info "http://<this server's IP>"
fi
info "Sign in at /Login. Settings: $ENV_FILE. Logs: journalctl -u $SERVICE -f"
info "Game files go in $DATA_DIR/mods (owned by $SERVICE_USER); then run the scan jobs from /Admin."
info "Later updates can be made from /Admin/Update."
