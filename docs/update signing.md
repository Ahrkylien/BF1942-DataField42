# Signing the updates

## The risk

The client and the DataField42 server script update themselves from the central server over plain TCP (port 28901), and nothing proves that what they receive came from the central server:

- A client asks for `update` or `updateFile` and runs the `updater.exe` or `DataField42.exe` it is sent.
- A game server asks for `updateServer` and runs the script it is sent.

Anyone who can intercept or redirect a connection, for example on a shared network or through a poisoned DNS answer, can make clients and game servers run their own program.

The checksums in the download protocol do not help here: they only check that the files of a map arrived whole, and they come from the same connection as the files.
