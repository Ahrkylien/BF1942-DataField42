using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Interfaces;
using System.Windows.Input;

namespace DataField42.ViewModels;
public partial class InfoViewModel : ObservableObject, IPageViewModel
{
    private readonly string _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
    public string Introduction =>
@$"Welcome to DataField42!
Version: {_version}
Author: Arklyiën

About:
DataField42 retrieves the maps and mods you desire to play either from the server you're joining or, if the server doesn't support DataField42, from the central database.
If a server is compatible with DataField42, it syncs your game files to match its specific version. This allows seamless switching between servers featuring various mod and map versions. DataField42 stores data in a cache for reuse, ensuring nothing is removed or needs to be downloaded again.
In the scenario that the central database (bf1942.eu) is down DataField42 should still work when joining servers that support DataField42.

Limitations:
Joining a server in-game with the wrong version of the mod or map can cause your game to crash or display an error message. To prevent this, it's advisable to connect to the server through DataField42, provided the server supports it. This precaution is necessary because DataField42 is not automatically used in this scenario.

Here is some explanation on how to use the sync rule file:
--
--";
}

