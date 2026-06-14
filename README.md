[![Build status](https://github.com/Ahrkylien/BF1942-DataField42/actions/workflows/build.yml/badge.svg)](https://github.com/Ahrkylien/BF1942-DataField42/actions/workflows/build.yml)

# BF1942 DataField42
A Battlefield 1942 tool for automatic map/mod downloads.

## Features:
- Download maps and mods seamlessly from the central database or the server you're joining, eliminating the "MAP NOT FOUND" pop-up message.
- Utilize an extra Desktop application with its own game server lobby for a convenient server joining experience while synchronizing files with the selected server.
- Enable fast and easy switching between servers that may have different versions of a mod.
  - Sync game files with DataField42-compatible servers to match their specific versions, ensuring a smooth transition between servers with varying mod and map versions.
- Store data in a cache for reuse, preventing the need for redownloading and ensuring nothing is removed.
- Maintain functionality even when the central database (bf1942.eu) is down, allowing seamless joining of servers that support DataField42.

## How to install:
1. Download the latest installer (`DataField42 v... Installer.exe`) from the [Releases page](https://github.com/Ahrkylien/BF1942-DataField42/releases/latest).
2. Run the installer and point it to your Battlefield 1942 installation folder.
3. Launch DataField42 or start BF1942 as usual — DataField42 will handle the rest automatically.

## How it works:
DataField42 operates in two modes:

**Triggered by BF1942 ("MAP NOT FOUND" mode)**
During installation, DataField42 patches `bf1942.exe` so that when the game would normally show a "MAP NOT FOUND" error, it launches DataField42 instead. DataField42 then attempts to download the missing map or mod from the central database (bf1942.eu) or directly from the server you're joining, and once the download is complete the game continues.

> If DataField42 is uninstalled, the patch to `bf1942.exe` remains but is harmless — BF1942 simply reverts to showing the "MAP NOT FOUND" message as it normally would.

**Used as a launcher**
DataField42 includes its own server browser. Joining a server through it synchronizes all required files (maps, mods, and their correct versions) before launching BF1942, preventing version mismatches and in-game crashes.

## Limitations:
Joining a server in-game with the wrong version of the mod or map can cause your game to crash or display an error message. To prevent this, it's advisable to connect to the server through DataField42, provided the server supports it. This precaution is essential because DataField42 isn't used when connecting to a server through the in-game browser if the game files for a particular version are already present.

## DataField42 Server:
When browsing the releases you will also find a python script for hosting a DataField42 server. DataField42Server.py needs to sit in the same folder as the mods folder. Make sure to put client files in the mods folder and not server files.\
It should look like this:\
Some Folder/
- DataField42Server.py
- Mods/
  - Bf1942/
    - ...
  - OtherMod/
    - ...

For the clients to be able to reach the server you will need to open port 28901 (TCP) in your firewall/router.\
The minimal version of python is 3.10. The only dependency that you need to manually download is google_crc32c:\
pip3 install google-crc32c\
https://pypi.org/project/google-crc32c/