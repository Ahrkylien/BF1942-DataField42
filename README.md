# BF1942 DataField42
A Battlefield 1942 tool for automatic map/mod downloads.\
## Features:
- Download maps and mods seamlessly from the central database or the server you're joining, eliminating the "MAP NOT FOUND" pop-up message.
- Utilize an extra Desktop application with its own game server lobby for a convenient server joining experience while synchronizing files with the selected server.
- Enable fast and easy switching between servers that may have different versions of a mod.
  - Sync game files with DataField42-compatible servers to match their specific versions, ensuring a smooth transition between servers with varying mod and map versions.
- Store data in a cache for reuse, preventing the need for redownloading and ensuring nothing is removed.
- Maintain functionality even when the central database (bf1942.eu) is down, allowing seamless joining of servers that support DataField42.
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

The only dependency that you need to manually download is google_crc32c:\
pip3 install google-crc32c\
https://pypi.org/project/google-crc32c/\