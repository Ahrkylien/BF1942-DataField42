using CommunityToolkit.Mvvm.Input;
using DataField42.Settings;
using DF.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace DataField42;

public class FileRuleCollectionSetting : ISetting
{
    private readonly Func<List<FileRule>> _getter;
    private readonly Action<List<FileRule>> _setter;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }
    public string Description { get; set; } = string.Empty;

    public ObservableCollection<FileRuleViewModel> Items { get; }

    /// <summary>Draft entry for the add-new-rule form.</summary>
    public FileRuleViewModel NewRule { get; } = new FileRuleViewModel();

    public ICommand RemoveItemCommand { get; }
    public ICommand CommitNewRuleCommand { get; }

    public FileRuleCollectionSetting(string name, Func<List<FileRule>> getter, Action<List<FileRule>> setter)
    {
        Name = name;
        _getter = getter;
        _setter = setter;

        Items = new ObservableCollection<FileRuleViewModel>(getter().Select(FileRuleViewModel.FromFileRule));
        Items.CollectionChanged += OnCollectionChanged;

        foreach (var item in Items)
            item.PropertyChanged += OnItemChanged;

        RemoveItemCommand = new RelayCommand<FileRuleViewModel>(RemoveItem);
        CommitNewRuleCommand = new RelayCommand(() => TryCommitNewRule());
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (FileRuleViewModel item in e.NewItems)
                item.PropertyChanged += OnItemChanged;

        if (e.OldItems != null)
            foreach (FileRuleViewModel item in e.OldItems)
                item.PropertyChanged -= OnItemChanged;

        Sync();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        Sync();
    }

    private void Sync()
    {
        _setter(Items.Select(vm => vm.ToFileRule()).ToList());
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
    }

    public bool TryCommitNewRule()
    {
        if (!NewRule.IsValid)
            return false;
        Items.Add(FileRuleViewModel.FromFileRule(NewRule.ToFileRule()));
        NewRule.Mod = "*";
        NewRule.FileName = "*";
        return true;
    }

    public void RemoveItem(FileRuleViewModel item) => Items.Remove(item);
}
