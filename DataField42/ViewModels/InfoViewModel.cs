using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Interfaces;
using System.Windows.Input;

namespace DataField42.ViewModels;
public partial class InfoViewModel : ObservableObject, IPageViewModel
{
    private readonly string _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
    public string Introduction =>
@$"Welcome to DataField42
Version: {_version}
Author: Arklyiën

About:
DataField42 retrieves the maps and mods you desire to play either from the server you're joining or, if the server doesn't support DataField42, from the central database.
If a server is compatible with DataField42, it syncs your game files to match its specific version. This allows seamless switching between servers featuring various mod and map versions. DataField42 stores data in a cache for reuse, ensuring nothing is removed or needs to be downloaded again.
In the scenario that the central database (bf1942.eu) is down DataField42 should still work when joining servers that support DataField42.

Limitations:
Joining a server in-game with the wrong version of the mod or map can cause your game to crash or display an error message. To prevent this, it's advisable to connect to the server through DataField42, provided the server supports it. This precaution is essential because DataField42 isn't used when connecting to a server through the in-game browser if the game files for a particular version are already present.

Synchronization Rules for DataField42:
- DataField42 ensures that clients have the same files as the Data folder for seamless gameplay.
- All necessary files for the current map/mod are synchronized by DataField42.
- In the file ""/DataField42/Synchronization rules.txt"" you can add rules that define which files to ignore during synchronization.

Applying Rules:
- Define rules to exclude specific archives from synchronization.
- Rules applied to the base RFA also affect all its patches; individual rules for patches are not permitted (files such as: xxxxx_001.rfa).
- During synchronization, the file will adhere to the first rule in the rule file that matches its criteria, ignoring all subsequent rules for that particular file.
- Format: ignore <ignore_sync_scenario> <file_type>, <mod>, <file_name>

Special Considerations:
- The map.rfa within the played mod is synced if the player lacks that map, irrespective of rules.

ignore_sync_scenario values:
- Always: Files are never synced.
- DifferentVersion: Files are synced only if no other version exists in the game directory.
- Never: Files are always synced. This is used to exclude specific files from a group of ignored files by placing this rule above it.

file_type values:
- Movie
- Music
- ModMiscFile
- Archive
- Level

Example of Synchronization rules.txt:
// Never sync bf1942 archives:
ignore Always Archive bf1942 *
// Exclude mod.dll from synchronization:
ignore Always ModMiscFile * mod.dll
// Never sync the textures archive if you already have one:
ignore DifferentVersion Archive * texture";
}

