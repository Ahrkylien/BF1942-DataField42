using System.ComponentModel;

namespace DF.Settings
{
    public interface ISetting : INotifyPropertyChanged
    {
        string Name { get; }

        string Description { get; }
    }
}
