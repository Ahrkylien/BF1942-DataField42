import socket
import socketserver
import os
import sys
import re
import subprocess
import threading
import select
import time
import json
from enum import Enum
from datetime import datetime
import google_crc32c


class Version:
    def __init__(self, version_string: str):
        self.version_numbers = [int(num) for num in version_string.split('.')]
        self.major, self.minor, self.patch = self.version_numbers[:-1]

    def __gt__(self, other):
        return self.version_numbers > other.version_numbers

    def __str__(self):
        return ".".join(map(str, self.version_numbers))


def log(level, message):
    print(f"{datetime.now().isoformat()} [{level}] {message}")


def log_error(message):
    log("Error", message)


def log_warning(message):
    log("Warning", message)


def log_info(message):
    log("Info", message)


def log_debug(message):
    log("Debug", message)


AllowableChars = "0-9a-zA-Z_-"


class Bf1942FileTypes(Enum):
    nonetype = 0
    movie = 1
    music = 2
    modmiscfile = 3
    archive = 4
    level = 5


class IgnoreSyncScenarios(Enum):
    always = 0
    never = 1


class SyncRuleManager:
    def __init__(self, rule_file_path):
        self.rule_file_path = rule_file_path
        self.ignore_file_sync_rules = []
        self.parse_rule_file()

    def parse_rule_file(self):
        try:
            with open(self.rule_file_path, 'r') as file:
                for line in file:
                    self.parse_rule_line(line)
        except IOError:
            pass

    def parse_rule_line(self, line):
        if line.strip().startswith("//"):  # comment
            return

        line_parts = line.split(' ')
        if line_parts[0] == "ignore" and len(line_parts) == 5:
            try:
                file_rule = FileRule(line_parts[1], line_parts[2], line_parts[3], line_parts[4])
                self.ignore_file_sync_rules.append(file_rule)
            except Exception as ex:
                log_warning(f"Can't parse line: {line}, Exception: {ex}")

    def get_ignore_file_sync_scenario(self, file_info) -> IgnoreSyncScenarios:
        for file_rule in self.ignore_file_sync_rules:
            if file_rule.matches(file_info):
                return file_rule.ignore_sync_scenario
        return IgnoreSyncScenarios.never


class FileInfo:
    def __init__(self, file_name: str, file_type: Bf1942FileTypes, mod: str):
        self.file_name = file_name
        self.file_type = file_type
        self.mod = mod

    @property
    def file_name_without_patch_number(self):
        if self.file_type == Bf1942FileTypes.level or self.file_type == Bf1942FileTypes.archive:
            file_name_without_extension = os.path.splitext(self.file_name)[0]
            file_extension = os.path.splitext(self.file_name)[1]
            match = re.match(f"^([{AllowableChars}]+)(_{{1}})([0-9]{{1,3}})$", file_name_without_extension)
            return f"{match.group(1)}{file_extension}" if match else self.file_name
        else:
            return self.file_name


class FileRule:
    def __init__(self, ignore_sync_scenario: str, file_type: str, mod: str, file_name: str):
        self.ignore_sync_scenario = IgnoreSyncScenarios[ignore_sync_scenario.lower()]
        self.file_type = Bf1942FileTypes[file_type.lower()]
        self.mod = mod.lower()
        self.file_name = file_name.lower()

        if (self.file_type == Bf1942FileTypes.level or self.file_type == Bf1942FileTypes.archive) and not self.file_name.endswith(".rfa"):
            self.file_name += ".rfa"
        elif (self.file_type == Bf1942FileTypes.movie or self.file_type == Bf1942FileTypes.music) and not self.file_name.endswith(".bik"):
            self.file_name += ".bik"
        elif self.file_type == Bf1942FileTypes.modmiscfile:
            if self.file_name in ["contentcrc32", "init"]:
                self.file_name += ".con"
            elif self.file_name == "mod":
                self.file_name += ".dll"
            elif self.file_name == "lexiconall":
                self.file_name += ".dat"
            elif self.file_name == "serverinfo":
                self.file_name += ".dds"

    def matches(self, file_info: FileInfo):
        return (self.mod == "*" or self.mod == file_info.mod.lower()) and \
            self.file_type == file_info.file_type and \
            (self.file_name == "*" or self.file_name == file_info.file_name_without_patch_number.lower())


class ChecksumRepository:
    def __init__(self, filename: str):
        self.filename = filename
        self.records = self.load_records()
        self.lock = threading.Lock()  # Lock for thread safety

    def load_records(self):
        try:
            with open(self.filename, 'r') as file:
                return json.load(file)
        except:
            pass
        return []

    def save_records(self):
        with open(self.filename, 'w') as file:
            json.dump(self.records, file)

    def add_record(self, checksum, size, last_time_modified):
        record = {
            'checksum': str(checksum),
            'size': int(size),
            'lastTimeModified': int(last_time_modified)
        }
        with self.lock:
            log_info(f"Adding Checksum to ChecksumRepository: {checksum}")
            self.records.append(record)
            self.save_records()

    def find_checksum(self, size, last_time_modified) -> str | None:
        for record in self.records:
            if record['size'] == int(size) and record['lastTimeModified'] == int(last_time_modified):
                return record['checksum']
        return None


class DataField42Communication:
    def __init__(self, socket: socket.socket, name: str):
        self.socket = socket
        self.name = name

    def receive_bytes(self, length: int, timeout: int | None = None, log: bool = True) -> bytes:
        total_data = b""
        start_time = time.time()
        while len(total_data) < length:
            if timeout is not None:
                ready, _, _ = select.select([self.socket], [], [], timeout)
                if not ready:
                    raise TimeoutError(f"Timeout occurred while waiting to receive {length} bytes")
            else:
                ready, _, _ = select.select([self.socket], [], [])
            data = self.socket.recv(length - len(total_data))
            if not data:
                raise Exception("Socket closed or no more data to receive")
            total_data += data
            # Update timeout based on elapsed time
            elapsed_time = time.time() - start_time
            if timeout is not None:
                timeout -= elapsed_time
            start_time = time.time()
        if log:
            log_debug(f"<< {total_data}")
        return total_data

    def receive_file(self, length: int, timeout: int | None = None) -> bytes:
        total_data = self.receive_bytes(length, timeout, log=False)
        log_debug(f"<< ~file~")
        return total_data

    def receive_data_length(self, timeout: int | None = None) -> int:
        return int.from_bytes(self.receive_bytes(4, timeout), 'little')

    def receive_string(self, timeout: int | None = None) -> str:
        length = self.receive_data_length(timeout)
        return self.receive_bytes(length, timeout).decode('utf-8')

    def receive_int(self, timeout: int | None = None) -> int:
        return int(self.receive_string(timeout))

    def receive_space_separated_string(self, timeout: int | None = None) -> list[str]:
        return self.receive_string(timeout).split()

    def await_acknowledgement(self, timeout: int | None = None):
        if self.receive_string(timeout) != "ok":
            raise Exception("Acknowledge not received")

    def send(self, message: any, await_acknowledgement=True, prepend_with_length=True):
        if not isinstance(message, bytes):
            message = str(message).encode('utf-8')
        if prepend_with_length:
            message = len(message).to_bytes(4, byteorder='little') + message
            log_debug(f">> {message}")
        self.socket.sendall(message)
        if await_acknowledgement:
            self.await_acknowledgement()

    def send_file(self, path: str, chunk_size=8192) -> None:
        log_debug(f">> ~file~ {path}")
        with open(path, "rb") as file:
            while True:
                file_bytes = file.read(chunk_size)
                if not file_bytes:
                    break
                self.send(file_bytes, await_acknowledgement=False, prepend_with_length=False)
        self.await_acknowledgement()

    def send_acknowledgement(self):
        self.send("ok", await_acknowledgement=False)


def update_and_restart_script(new_script_bytes: bytes):
    with open(sys.argv[0], 'wb') as file:
        file.write(new_script_bytes)
    if not restart_systemd_service("DataField42Server"):
        python_path = sys.executable
        args = [f"\"{arg}\"" for arg in [python_path] + sys.argv]
        os.execl(python_path, *args)


def restart_systemd_service(service_name: str) -> bool:
    try:
        subprocess.run(["sudo", "systemctl", "restart", service_name], check=True)
        return True
    except Exception as e:
        log_info(f"Failed to restart SystemD service '{service_name}'. Reason: {e}")
        return False


def smart_path_join(base_dir: str, rel_path: str, is_dir=False) -> str | None:
    current_dir = "." if base_dir == "" else base_dir
    rel_path_parts = rel_path.replace('\\', '/').split('/')
    for i, path_part in enumerate(rel_path_parts):
        entry_to_join = None
        for entry in os.scandir(current_dir):
            if entry.name.lower() == path_part.lower():
                if entry.is_dir() and (i < len(rel_path_parts) - 1 or is_dir):
                    entry_to_join = entry.name
                    break
                elif entry.is_file() and i == len(rel_path_parts) - 1 and not is_dir:
                    entry_to_join = entry.name
                    break
        if entry_to_join is None:
            return None
        current_dir = os.path.join(current_dir, entry_to_join)
    return current_dir


def get_checksum(path: str) -> str:
    checksum_from_repository = checksum_repository.find_checksum(os.path.getsize(path), os.path.getmtime(path))
    if checksum_from_repository is not None:
        return checksum_from_repository
    checksum = "00000000"
    with open(path, "rb") as file:
        checksum = google_crc32c.value(file.read())
        checksum = f"{(checksum & 0xFFFFFFFF):08X}"
        checksum_repository.add_record(checksum, os.path.getsize(path), os.path.getmtime(path))
    return checksum


class DataField42TCPHandler(socketserver.BaseRequestHandler):
    def handle(self):
        log_info(f"#### New Connection: {self.client_address[0]} ####")
        request_socket = self.request
        communication = DataField42Communication(request_socket, self.client_address[0])

        arguments = communication.receive_space_separated_string()
        header = arguments.pop(0)

        log_info(f"{header} : {arguments}")

        if header == "handshake" and len(arguments) == 1:
            handshake(communication, *arguments)
        elif header == "download" and len(arguments) >= 5:
            arguments = arguments[:5] + [arguments[5:]]
            download_files(communication, *arguments)
        else:
            communication.send("unknown identifier")
        log_info("#### Connection Closed ####")


def handshake(communication: DataField42Communication, version: str):
    communication.send(dataField42_server_version, await_acknowledgement=False)


ARCHIVES = [
    "Archives/ai.rfa",
    "Archives/aiMeshes.rfa",
    "Archives/animations.rfa",
    "Archives/Font.rfa",
    "Archives/menu.rfa",
    "Archives/menu_001.rfa",
    "Archives/Objects.rfa",
    "Archives/shaders.rfa",
    "Archives/sound.rfa",
    "Archives/sound_001.rfa",
    "Archives/standardMesh.rfa",
    "Archives/StandardMesh_001.rfa",
    "Archives/texture.rfa",
    "Archives/texture_001.rfa",
    "Archives/treeMesh.rfa",
    "Archives/bf1942/game.rfa",
]
MOD_MISC_FILES = [
    "contentCrc32.con",
    "init.con",
    "mod.dll",
    "lexiconAll.dat",
    "serverInfo.dds",
]


def get_relevant_mod_names(init_con_path: str) -> list[str]:
    mod_names = []
    with open(init_con_path, 'r') as file:
        for line in file:
            if line.lower().startswith('game.addmodpath'):
                _, mod_path = line.split(' ', 1)
                mod_names.append(mod_path.split("/")[1].strip())
    return mod_names


def get_name_parts(path: str) -> dict[str, str]:
    returner = {"name": "", "patchNumber": None, "extension": ""}
    filename, file_extension = os.path.splitext(path)
    returner["extension"] = file_extension
    filename = os.path.basename(filename)
    last_underscore_pos = filename.rfind("_")
    if last_underscore_pos != -1:
        patch_number = filename[last_underscore_pos + 1:]
        if patch_number.isnumeric():
            returner["patchNumber"] = int(patch_number)
            returner["name"] = filename[0:last_underscore_pos]
        else:
            returner["name"] = filename
    else:
        returner["name"] = filename
    return returner


class DataField42Server:
    def __init__(self, game_directory=""):
        self.game_directory = game_directory

    def start(self):
        log_info("Starting DataField42 server")
        self.start_heartbeat_and_update_monitor()
        self.start_file_server()

    def start_file_server(self):
        s = socketserver.ThreadingTCPServer(('0.0.0.0', 28901), DataField42TCPHandler, bind_and_activate=False)
        s.allow_reuse_address = True
        s.server_bind()
        s.server_activate()
        s.serve_forever()

    def start_heartbeat_and_update_monitor(self):
        threading.Thread(target=self.heartbeat_and_update_monitor_thread).start()

    def heartbeat_and_update_monitor_thread(self):
        connection_to_data_field42_master = ConnectionToDataField42Master()
        while True:
            try:
                master_data_field42_server_version = connection_to_data_field42_master.send_heartbeat()
                if Version(master_data_field42_server_version) > dataField42_server_version:
                    connection_to_data_field42_master.update()
            except Exception as e:
                log_error(f"Can't send heartbeat to bf1942.eu: {e}")
            time.sleep(60)


class ConnectionToDataField42Master:
    def __init__(self):
        self.socket = None
        self.communication = None

    def connect(self):
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.socket.connect(('bf1942.eu', 28901))
        self.communication = DataField42Communication(self.socket, 'bf1942.eu')

    def send_heartbeat(self):
        self.connect()
        self.communication.send(f"heartbeatServer {dataField42_server_version}", await_acknowledgement=False)
        version = self.communication.receive_string()
        return version

    def update(self):
        self.connect()
        self.communication.send(f"updateServer {dataField42_server_version}", await_acknowledgement=False)
        file_size = self.communication.receive_int()
        new_script = self.communication.receive_file(file_size)
        self.communication.send_acknowledgement()
        update_and_restart_script(new_script)


def get_files_to_sync(map_name: str, mod_name: str) -> list[list[str]]:
    files = []
    game_directory = dataField42_server.game_directory
    mods_directory = smart_path_join(game_directory, "mods", True)

    if mods_directory is not None:
        mod_names = [item.lower() for item in os.listdir(mods_directory) if os.path.isdir(os.path.join(mods_directory, item))]
        if mod_name.lower() in mod_names:
            all_relevant_mod_names = get_relevant_mod_names(smart_path_join(game_directory, f"mods/{mod_name}/init.con"))
            for relevant_mod_name in all_relevant_mod_names:
                mod_folder = smart_path_join(game_directory, f"mods/{relevant_mod_name}", True)
                if mod_folder is None:
                    log_warning(f"Cant find mod: {relevant_mod_name}")
                    return []

                # mod map RFAs:
                levels_folder = smart_path_join(game_directory, f"mods/{relevant_mod_name}/Archives/bf1942/levels", True)
                if levels_folder is not None:
                    for filename in os.listdir(levels_folder):
                        file_info = get_name_parts(filename)
                        if file_info["name"].lower() == map_name.lower() and file_info["extension"].lower() == ".rfa":
                            files.append([relevant_mod_name, "Archives/bf1942/levels/" + filename, os.path.join(levels_folder, filename), Bf1942FileTypes.level])

                # mod base files:
                for file_path_relative in ARCHIVES:
                    filePath = smart_path_join(mod_folder, file_path_relative)
                    if filePath is not None:
                        files.append([relevant_mod_name, file_path_relative, filePath, Bf1942FileTypes.archive])
                for file_path_relative in MOD_MISC_FILES:
                    filePath = smart_path_join(mod_folder, file_path_relative)
                    if filePath is not None:
                        files.append([relevant_mod_name, file_path_relative, filePath, Bf1942FileTypes.modmiscfile])

                # mod movies:
                movies_folder = smart_path_join(mod_folder, "movies", True)
                if movies_folder is not None:
                    files += [[relevant_mod_name, os.path.relpath(os.path.join(dp, f), mod_folder), os.path.join(dp, f), Bf1942FileTypes.movie]
                              for dp, dn, filenames in os.walk(movies_folder) for f in filenames
                              if os.path.splitext(f)[1].lower() == '.bik']

                # mod music:
                music_folder = smart_path_join(mod_folder, "music", True)
                if music_folder is not None:
                    files += [[relevant_mod_name, os.path.relpath(os.path.join(dp, f), mod_folder), os.path.join(dp, f), Bf1942FileTypes.music]
                              for dp, dn, filenames in os.walk(music_folder) for f in filenames
                              if os.path.splitext(f)[1].lower() == '.bik']
                
                # always use normal slash in path send:
                for file in files:
                    file[1] = file[1].replace('\\', '/')
        else:
            log_warning(f"Cant find mod: {mod_name}")
    else:
        log_error("Can't find mods folder")

    files_after_rules_applied = [file for file in files
                        if sync_rule_manager.get_ignore_file_sync_scenario(FileInfo(os.path.basename(file[1]), file[3], file[0]))
                        == IgnoreSyncScenarios.never]

    return files_after_rules_applied


def download_files(communication: DataField42Communication, map_name: str, mod_name: str, ip: str, port: str, key_hash: str, key_value_pair: dict[str, str] | None = None):
    files = get_files_to_sync(map_name, mod_name)

    # add file sizes and checksums:
    total_size = 0
    for file in files:
        size = os.path.getsize(file[2])
        total_size += size
        file.append(str(size))
        file.append(get_checksum(file[2]))

    files_to_send = []

    file_info_strings = []

    for file in files:
        file_info_strings.append(f"{file[0]} \"{file[1]}\" {file[5]} {file[4]} {int(os.path.getmtime(file[2]))}")  # mod filePath checksum size lastModified

    communication.send('\n'.join(file_info_strings), await_acknowledgement=False)
    file_info_response_strings = communication.receive_space_separated_string()

    if len(file_info_response_strings) != len(file_info_strings):
        communication.send(f"no 0 0")
        raise Exception(f"file info length response incorrect: {len(file_info_response_strings)} != {len(file_info_strings)}")

    for i, file_info_response_string in enumerate(file_info_response_strings):
        if file_info_response_string == "yes":
            files_to_send.append(files[i])

    total_size = sum(file[4] for file in files_to_send)
    communication.send(f"yes {len(files_to_send)} {total_size}")

    for file in files_to_send:
        with open(file[2], "rb") as f:
            file_bytes = f.read()
        communication.send(f"{file[0]} \"{file[1]}\" {file[5]} {file[4]} {int(os.path.getmtime(file[2]))}")  # mod filePath checksum size lastModified
        communication.send(file_bytes, prepend_with_length=False)

    communication.await_acknowledgement()


dataField42_server_version = Version("2.0.1.0")
checksum_repository = ChecksumRepository("ChecksumRepository.json")
sync_rule_manager = SyncRuleManager("Synchronization rules.txt")

dataField42_server = DataField42Server("")
dataField42_server.start()
