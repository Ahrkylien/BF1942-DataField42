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
import google_crc32c
from datetime import datetime

class Version:
    def __init__(self, version_string):
        self.version_numbers = [int(num) for num in version_string.split('.')]
        self.major = self.version_numbers[0]
        self.minor = self.version_numbers[0]
        self.patch = self.version_numbers[0]

    def __gt__(self, other):
        return self.version_numbers > other.version_numbers
    
    def __str__(self):
        return ".".join(str(item) for item in self.version_numbers)

def log(level, message):
    print(f"{datetime.now().isoformat()} [{level}] {message}")

def logError(message):
    log("Error", message)
    
def logWarning(message):
    log("Warning", message)
    
def logInfo(message):
    log("Info", message)
    
def logDebug(message):
    log("Debug", message)

class SyncRuleManager:
    def __init__(self, rule_file_path):
        self.rule_file_path = rule_file_path
        self.ignore_file_sync_rules = []
        self.parse_rule_file()

    def parse_rule_file(self):
        lines = []
        try:
            with open(self.rule_file_path, 'r') as file:
                lines = file.readlines()
        except IOError:
            pass

        for line in lines:
            if line.strip().startswith("//"):  # comment
                continue

            line_parts = line.split(' ')

            if line_parts[0] == "ignore" and len(line_parts) == 5:
                try:
                    file_rule = FileRule(line_parts[1], line_parts[2], line_parts[3], line_parts[4])
                    self.ignore_file_sync_rules.append(file_rule)
                except Exception as ex:
                    logWarning(f"Can't parse line: {line} in: {self.rule_file_path}, Exception: {ex}")

    def get_ignore_file_sync_scenario(self, file_info):
        for file_rule in self.ignore_file_sync_rules:
            if file_rule.matches(file_info):
                return file_rule.ignore_sync_scenario
        return IgnoreSyncScenarios.never

class FileRule:
    def __init__(self, ignore_sync_scenario, file_type, mod, file_name):
        self.ignore_sync_scenario = IgnoreSyncScenarios[ignore_sync_scenario.lower()]
        self.file_type = Bf1942FileTypes[file_type.lower()] # will this work??
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

    def matches(self, file_info):
        return (self.mod == "*" or self.mod == file_info.mod.lower()) and \
               self.file_type == file_info.file_type and \
               (self.file_name == "*" or self.file_name == file_info.file_name_without_patch_number.lower())

class FileInfo:
    def __init__(self, file_name, file_type, mod):
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

AllowableChars = "0-9a-zA-Z_-";

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

class ChecksumRepository:
    def __init__(self, filename):
        self.filename = filename
        self.records = self.load_records()
        self.lock = threading.Lock()  # Lock for thread safety

    def load_records(self):
        try:
            with open(self.filename, 'r') as file:
                return json.load(file)
        except: pass
        return []

    def save_records(self):
        with open(self.filename, 'w') as file:
            json.dump(self.records, file)

    def add_record(self, checksum, size, lastTimeModified):
        record = {
            'checksum': str(checksum),
            'size': int(size),
            'lastTimeModified': int(lastTimeModified)
        }
        with self.lock:
            logInfo(f"Adding Checksum to ChecksumRepository: {checksum}")
            self.records.append(record)
            self.save_records()

    def find_checksum(self, size, lastTimeModified):
        for record in self.records:
            if record['size'] == int(size) and record['lastTimeModified'] == int(lastTimeModified):
                return record['checksum']
        return None

class DataField42Communication:
    def __init__(self, socket):
        self.socket = socket
    
    def receiveBytes(self, length, timeout=None, log=True):
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
            logDebug(f"<< {total_data}")
        return total_data
    
    def receiveFile(self, length, timeout=None):
        total_data = self.receiveBytes(length, timeout, log=False)
        logDebug(f"<< ~file~")
        return total_data
    
    def receiveDataLength(self, timeout=None):
        return int.from_bytes(self.receiveBytes(4, timeout), 'little')
        
    def receiveString(self, timeout=None):
        length = self.receiveDataLength(timeout)
        return self.receiveBytes(length, timeout).decode('utf-8')
        
    def receiveInt(self, timeout=None):
        return int(self.receiveString(timeout))
        
    def receiveSpaceSeperatedString(self, timeout=None):
        return self.receiveString(timeout).split()
        
    def awaitAcknowledgement(self, timeout=None):
        if self.receiveString(timeout) != "ok":
            raise Exception("Acknowledge not received")
        
    def send(self, message, awaitAcknowledgement = True, prependWithLength = True):
        if type(message) != bytes:
            message = str(message).encode('utf-8')
        
        if prependWithLength:
            message = len(message).to_bytes(4, byteorder = 'little') + message
            logDebug(f">> {message}")
        else:
            logDebug(f">> ~file~")
        
        send = self.socket.sendall(message)
        if awaitAcknowledgement:
            self.awaitAcknowledgement()
        return send

def updateAndRestartScript(newScriptBytes):
    with open(sys.argv[0], 'wb') as file:
        file.write(newScriptBytes)
    
    if not restartSystemdService("DataField42Server"):
        pythonPath = sys.executable
        args = [f"\"{arg}\"" for arg in [pythonPath] + sys.argv]
        os.execl(pythonPath, *args)

def restartSystemdService(serviceName):
    try:
        subprocess.run(["sudo", "systemctl", "restart", serviceName], check=True)
        return True
    except Exception as e:
        logInfo(f"Failed to restart SystemD service '{serviceName}'. Reason: {e}")
        return False

def smartPathJoin(baseDir, relPath, isDir = False): #append baseDir with relPath
    currentDir = "." if baseDir == "" else baseDir
    relPathParts = relPath.replace('\\', '/').split('/')
    for i, path_part in enumerate(relPathParts):
        entryToJoin = None
        for entry in os.scandir(currentDir):
            if entry.name.lower() == path_part.lower():
                if entry.is_dir() and (i < len(relPathParts)-1 or isDir): 
                    entryToJoin = entry.name
                    break
                elif entry.is_file() and i == len(relPathParts)-1 and not isDir: 
                    entryToJoin = entry.name
                    break
        if entryToJoin == None: return(None)
        currentDir = os.path.join(currentDir, entryToJoin)
    return(currentDir)

def getChecksum(path):
    checksumFromRepository = checksumRepository.find_checksum(os.path.getsize(path), os.path.getmtime(path))
    if checksumFromRepository != None:
        return checksumFromRepository
    checksum = "00000000"
    with open(path, "rb") as file:
        checksum = google_crc32c.value(file.read())
        checksum = f"{(checksum & 0xFFFFFFFF):08X}"
        checksumRepository.add_record(checksum, os.path.getsize(path), os.path.getmtime(path))
    return checksum

class MyTCPHandler(socketserver.BaseRequestHandler):
    def handle(self):
        logInfo(f"#### New Connection: {self.client_address[0]} ####")
        socket = self.request
        communication = DataField42Communication(socket)
        
        arguments = communication.receiveSpaceSeperatedString()
        header = arguments.pop(0)
        
        logInfo(f"{header} : {arguments}")
        
        if header == "handshake" and len(arguments) == 1:
            handshake(communication, *arguments)
        elif header == "download" and len(arguments) >= 5: # download map mod IP port keyhash <optional key value pairs>
            arguments = arguments[:5] + [arguments[5:]]
            downloadFiles(communication, *arguments)
        else:
            communication.send("unknown identifier")
        logInfo("####  Connection Closed  ####")

def handshake(communication, version):
    communication.send(DataField42ServerVersion, awaitAcknowledgement = False);

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

def getRelavantModNames(initConPath):
    modNames = []
    with open(initConPath, 'r') as file:
        for line in file:
            if line.startswith('game.addModPath'):
                _, modPath = line.split(' ', 1)
                modNames.append(modPath.split("/")[1].strip())
    return modNames

def getNameParts(path):
    returner = {"name": "", "patchNumber": None, "extension": ""}
    filename, file_extension = os.path.splitext(path)
    returner["extension"] = file_extension
    filename = os.path.basename(filename)
    lastUnderscorePos = filename.rfind("_")
    if lastUnderscorePos != -1:
        patchNumber = filename[lastUnderscorePos+1:]
        if patchNumber.isnumeric(): #patchNumber can be any length (tested for lenth: 1,2 and 3)
            returner["patchNumber"] = int(patchNumber)
            returner["name"] = filename[0:lastUnderscorePos]
        else:
            returner["name"] = filename
    else:
        returner["name"] = filename
    return(returner)

def getFilesToSync(mapName, modName):
    global dataField42Server
    files = []
    gameDirectory = dataField42Server.gameDirectory
    modsDirectory = smartPathJoin(gameDirectory, "mods", True)
    if modsDirectory != None:
        modNames = [item.lower() for item in os.listdir(modsDirectory) if os.path.isdir(os.path.join(modsDirectory, item))]
        if modName.lower() in modNames:
            allRelevantModNames = getRelavantModNames(smartPathJoin(gameDirectory, f"mods/{modName}/init.con"))
            for relevantModName in allRelevantModNames:
                modFolder = smartPathJoin(gameDirectory, f"mods/{relevantModName}", True)
                if modFolder == None:
                    logWarning(f"Cant find mod: {relevantModName}")
                    return [] # mod should exist
                # mod map RFAs:
                levelsFolder = smartPathJoin(gameDirectory, f"mods/{relevantModName}/Archives/bf1942/levels", True)
                if levelsFolder != None:
                    for filename in os.listdir(levelsFolder):
                        fileInfo = getNameParts(filename)
                        if fileInfo["name"].lower() == mapName.lower() and fileInfo["extension"].lower() == ".rfa":
                            files.append([relevantModName, "Archives/bf1942/levels/"+filename, os.path.join(levelsFolder, filename), Bf1942FileTypes.level])
                # mod base files:
                for filePathRelative in ARCHIVES:
                    path = smartPathJoin(modFolder, filePathRelative)
                    if path != None:
                        files.append([relevantModName, filePathRelative, path, Bf1942FileTypes.archive])
                for filePathRelative in MOD_MISC_FILES:
                    path = smartPathJoin(modFolder, filePathRelative)
                    if path != None:
                        files.append([relevantModName, filePathRelative, path, Bf1942FileTypes.modmiscfile])
                # mod movies:
                moviesFolder = smartPathJoin(modFolder, "movies", True)
                if moviesFolder != None:
                    files += [[relevantModName, os.path.relpath(os.path.join(dp, f), modFolder), os.path.join(dp, f), Bf1942FileTypes.movie] for dp, dn, filenames in os.walk(moviesFolder) for f in filenames if os.path.splitext(f)[1].lower() == '.bik']
                # mod music:
                musicFolder = smartPathJoin(modFolder, "music", True)
                if musicFolder != None:
                    files += [[relevantModName, os.path.relpath(os.path.join(dp, f), modFolder), os.path.join(dp, f), Bf1942FileTypes.music] for dp, dn, filenames in os.walk(musicFolder) for f in filenames if os.path.splitext(f)[1].lower() == '.bik']
        logWarning(f"Cant find mod: {modName}")
    else: logError("Can't find mods folder")
    
    filesAfterRulesApplied = []
    for file in files:
        fileInfo = FileInfo(os.path.basename(file[1]), file[3], file[0])
        if syncRuleManager.get_ignore_file_sync_scenario(fileInfo) == IgnoreSyncScenarios.never:
            filesAfterRulesApplied.append(file)
    
    return filesAfterRulesApplied

def downloadFiles(communication, mapName, modName, IP, port, keyhash, keyValuePair = []):
    files = getFilesToSync(mapName, modName)
        
    # add file sizes and checksums:
    totalSize = 0
    for file in files:
        size = os.path.getsize(file[2])
        totalSize += size
        file.append(size)
        file.append(getChecksum(file[2]))
        
    filesToSend = []
    
    fileInfoStrings = []
    
    for file in files:
        fileInfoStrings.append(f"{file[0]} \"{file[1]}\" {file[5]} {file[4]} {int(os.path.getmtime(file[2]))}") # mod filePath crc32 size lastModified
    
    communication.send('\n'.join(fileInfoStrings), awaitAcknowledgement = False)
    fileInfoResponseStrings = communication.receiveSpaceSeperatedString()
    
    if len(fileInfoResponseStrings) != len(fileInfoStrings):
        communication.send(f"no 0 0")
        raise Exception(f"file info length response incorrect: {len(fileInfoResponseStrings)} != {len(fileInfoStrings)}")
    
    for i, fileInfoResponseString in enumerate(fileInfoResponseStrings):
        if fileInfoResponseString == "yes":
            filesToSend.append(files[i])
    
    totalSize = sum(file[4] for file in filesToSend)
    communication.send(f"yes {len(filesToSend)} {totalSize}")
    
    for file in filesToSend:
        with open(file[2], "rb") as f:
            fileBytes = f.read()
        communication.send(f"{file[0]} \"{file[1]}\" {file[5]} {file[4]} {int(os.path.getmtime(file[2]))}") # mod filePath crc32 size lastModified
        communication.send(fileBytes, prependWithLength = False)
    
    communication.awaitAcknowledgement()

class ConnectionToDataField42Master:
    def __init__(self):
        self.socket = None
        self.communication = None
        
    def connect(self):
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.socket.connect(('bf1942.eu', 28901))
        self.communication = DataField42Communication(self.socket)
    
    def sendHeartbeat(self):
        self.connect()
        self.communication.send(f"heartbeatServer {DataField42ServerVersion}", awaitAcknowledgement = False)
        version = self.communication.receiveString()
        return version
    
    def update(self):
        self.connect()
        self.communication.send(f"updateServer {DataField42ServerVersion}", awaitAcknowledgement = False)
        fileSize = self.communication.receiveInt()
        newScript = self.communication.receiveFile(fileSize)
        updateAndRestartScript(newScript)
    
class DataField42Server:
    def __init__(self, gameDirectory=""):
        self.gameDirectory = gameDirectory
    
    def start(self):
        logInfo("Starting DataField42 server")
        self.startHeartbeatAndUpdateMonitor()
        self.startFileServer()
    
    def startFileServer(self):
        s = socketserver.ThreadingTCPServer(('0.0.0.0', 28901), MyTCPHandler, False) # Do not automatically bind
        s.allow_reuse_address = True # Prevent 'cannot bind to address' errors on restart
        s.server_bind() # Manually bind, to support allow_reuse_address
        s.server_activate() # (see above comment)
        s.serve_forever()
    
    def startHeartbeatAndUpdateMonitor(self):
        threading.Thread(target=self.heartbeatAndUpdateMonitorThread).start()
    
    def heartbeatAndUpdateMonitorThread(self):
        connectionToDataField42Master = ConnectionToDataField42Master()
        while True:
            try:
                masterDataField42ServerVersion = connectionToDataField42Master.sendHeartbeat()
                if Version(masterDataField42ServerVersion) > DataField42ServerVersion:
                    connectionToDataField42Master.update()
            except Exception as e:
                logError(f"Can't send heartbeat to bf1942.eu: {e}")
            time.sleep(60) 



DataField42ServerVersion = Version("2.0.0.0")
checksumRepository = ChecksumRepository("ChecksumRepository.json")
syncRuleManager = SyncRuleManager("Synchronization rules.txt")



dataField42Server = DataField42Server("")
dataField42Server.start()