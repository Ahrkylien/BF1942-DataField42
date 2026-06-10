using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace DF.Settings
{
    public class StringCollectionSetting : ISetting
    {
        private readonly Action<List<string>> _setter;
        private string _newItemText = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; }
        public string Description { get; set; } = string.Empty;

        public ObservableCollection<string> Items { get; }

        public string NewItemText
        {
            get => _newItemText;
            set { _newItemText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewItemText))); }
        }

        public ICommand AddCommand { get; }
        public ICommand RemoveCommand { get; }

        public StringCollectionSetting(string name, Func<IEnumerable<string>> getter, Action<List<string>> setter)
        {
            Name = name;
            _setter = setter;
            Items = new ObservableCollection<string>(getter());
            Items.CollectionChanged += OnCollectionChanged;
            AddCommand = new RelayCommand(() =>
            {
                var text = NewItemText.Trim();
                if (!string.IsNullOrEmpty(text) && !Items.Contains(text))
                    Items.Add(text);
                NewItemText = string.Empty;
            });
            RemoveCommand = new RelayCommand<string>(item => Items.Remove(item));
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _setter(Items.ToList());
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
        }
    }
}
