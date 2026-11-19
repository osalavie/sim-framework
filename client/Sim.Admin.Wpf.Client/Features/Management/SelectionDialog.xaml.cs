using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;

namespace Sim.Admin.Wpf.Client.Features.Management;

public partial class SelectionDialog : Window
{
    private readonly List<SelectionOption> _options;
    private readonly IReadOnlyList<SelectionOption> _roots;
    private readonly bool _treeMode;
    public List<string> SelectedIds { get; private set; } = [];

    public SelectionDialog(string title, List<SelectionOption> options, HashSet<string> selected,
        bool tree = false)
    {
        InitializeComponent();
        Title = title;
        _options = options;
        _treeMode = tree;
        foreach (var option in options) option.SetCheckedSilently(selected.Contains(option.Id));
        if (tree)
        {
            _roots = BuildTree(options, selected);
            foreach (var root in _roots) ExpandAll(root);
            OptionsList.Visibility = Visibility.Collapsed;
            OptionsTree.Visibility = Visibility.Visible;
            OptionsTree.ItemsSource = _roots;
        }
        else
        {
            _roots = [];
            OptionsList.ItemsSource = options;
        }
        Loaded += (_, _) => FilterBox.Focus();
    }

    private static List<SelectionOption> BuildTree(List<SelectionOption> options, HashSet<string> selected)
    {
        var byId = options.GroupBy(option => option.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var roots = new List<SelectionOption>();
        foreach (var option in options)
        {
            if (option.ParentId is { } parentId &&
                byId.TryGetValue(parentId, out var parent) &&
                !ReferenceEquals(parent, option))
            {
                option.Parent = parent;
                parent.Children.Add(option);
            }
            else
            {
                roots.Add(option);
            }
        }
        foreach (var root in roots) Normalize(root, selected);
        return roots;
    }

    private static void Normalize(SelectionOption node, HashSet<string> selected)
    {
        foreach (var child in node.Children) Normalize(child, selected);
        if (node.Children.Count == 0) return;
        if (!selected.Contains(node.Id))
        {
            node.SetCheckedSilently(false);
            return;
        }
        var selectedChildren = node.Children.Count(child => selected.Contains(child.Id));
        node.SetCheckedSilently(selectedChildren == node.Children.Count || selectedChildren == 0
            ? true
            : null);
    }

    private static void ExpandAll(SelectionOption node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children) ExpandAll(child);
    }

    private void FilterChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OptionClick(object sender, RoutedEventArgs e)
    {
        // WPF 三态点击循环是 true→null→false→true：全选节点会先变半选、叶子节点也会落到半选。
        // 统一为级联树语义：点击后仍是半选说明原先全选，改为取消整棵子树；其余情况一律全选。
        if (!_treeMode || sender is not CustomCheckBox { DataContext: SelectionOption option } box) return;
        option.IsChecked = box.IsChecked is not null;
    }

    private void ApplyFilter()
    {
        var keyword = FilterBox.Text.Trim();
        if (_treeMode)
        {
            foreach (var root in _roots) ApplyNodeFilter(root, keyword);
            return;
        }
        // 直接切换 ItemsSource，让列表项数变化立即重算滚动范围；
        // 仅改 IsVisible 时虚拟化面板不会马上刷新滚动条。
        OptionsList.ItemsSource = keyword.Length == 0
            ? _options
            : _options.Where(option => Matches(option, keyword)).ToList();
    }

    private static bool Matches(SelectionOption option, string keyword) =>
        option.Label.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        (option.SearchText?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool ApplyNodeFilter(SelectionOption node, string keyword)
    {
        var selfMatch = keyword.Length == 0 || Matches(node, keyword);
        var childMatch = false;
        foreach (var child in node.Children) childMatch |= ApplyNodeFilter(child, keyword);
        node.IsVisible = selfMatch || childMatch;
        if (keyword.Length > 0 && childMatch) node.IsExpanded = true;
        return node.IsVisible;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        SelectedIds = _options
            .Where(option => _treeMode ? option.IsChecked != false : option.IsChecked == true)
            .Select(option => option.Id)
            .ToList();
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}

public sealed class SelectionOption : INotifyPropertyChanged
{
    private bool? _isChecked;
    private bool _isVisible = true;
    private bool _isExpanded;
    private bool _syncing;

    public SelectionOption(string id, string label, string? parentId = null, string? searchText = null)
    {
        Id = id;
        Label = label;
        ParentId = parentId;
        SearchText = searchText;
    }

    public string Id { get; }
    public string Label { get; }
    public string? ParentId { get; }
    public string? SearchText { get; }
    public SelectionOption? Parent { get; internal set; }
    public ObservableCollection<SelectionOption> Children { get; } = [];

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
            if (_syncing) return;
            _syncing = true;
            try
            {
                if (value.HasValue) SetDescendants(value.Value);
                Parent?.RefreshFromChildren();
            }
            finally { _syncing = false; }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetCheckedSilently(bool? value)
    {
        if (_isChecked == value) return;
        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));
    }

    private void SetDescendants(bool value)
    {
        foreach (var child in Children)
        {
            child.SetCheckedSilently(value);
            child.SetDescendants(value);
        }
    }

    internal void RecalculateFromChildren()
    {
        if (Children.Count == 0) return;
        var checkedCount = Children.Count(child => child.IsChecked == true);
        var hasIndeterminate = Children.Any(child => child.IsChecked is null);
        var next = checkedCount == Children.Count
            ? true
            : checkedCount > 0 || hasIndeterminate ? (bool?)null : false;
        if (_isChecked == next) return;
        _isChecked = next;
        OnPropertyChanged(nameof(IsChecked));
    }

    private void RefreshFromChildren()
    {
        RecalculateFromChildren();
        Parent?.RefreshFromChildren();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
