using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Sim.Admin.Wpf.Client.Controls.Container;

/// <summary>n-menu 的菜单节点：含子项时渲染为可展开分组，叶节点点击后导航。</summary>
public sealed class CustomMenuNode : INotifyPropertyChanged
{
    public CustomMenuNode(string key, string label, string glyph = "", params CustomMenuNode[] children)
    {
        Key = key;
        Label = label;
        Glyph = glyph;
        Children = children;
    }

    public string Key { get; }

    public string Label { get; }

    public string Glyph { get; }

    public IReadOnlyList<CustomMenuNode> Children { get; }

    public bool HasChildren => Children.Count > 0;

    private bool _isExpanded;

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

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class CustomMenuSelectedEventArgs(CustomMenuNode node) : EventArgs
{
    public CustomMenuNode Node { get; } = node;
}

/// <summary>n-menu 子项容器：展开时按内容高度做 0.3s 高度 + 淡入动画，收起时反向收缩。</summary>
public sealed class CustomMenuChildren : ItemsControl
{
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(300);

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(CustomMenuChildren),
        new PropertyMetadata(false, OnIsExpandedChanged));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public CustomMenuChildren()
    {
        ClipToBounds = true;
        MaxHeight = 0;
        Opacity = 0;
        Loaded += (_, _) => ApplyState(animate: false);
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomMenuChildren)d).ApplyState(animate: ((CustomMenuChildren)d).IsLoaded);

    private void ApplyState(bool animate)
    {
        if (!animate)
        {
            BeginAnimation(MaxHeightProperty, null);
            BeginAnimation(OpacityProperty, null);
            MaxHeight = IsExpanded ? double.PositiveInfinity : 0;
            Opacity = IsExpanded ? 1 : 0;
            return;
        }

        if (IsExpanded) Expand();
        else Collapse();
    }

    private void Expand()
    {
        var target = MeasureContentHeight();
        BeginAnimation(MaxHeightProperty, null);
        MaxHeight = 0;

        var expand = new DoubleAnimation(target, AnimationDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        expand.Completed += (_, _) =>
        {
            BeginAnimation(MaxHeightProperty, null);
            MaxHeight = double.PositiveInfinity;
        };
        BeginAnimation(MaxHeightProperty, expand);
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, AnimationDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void Collapse()
    {
        var from = ActualHeight;
        if (from <= 0)
        {
            MaxHeight = 0;
            Opacity = 0;
            return;
        }

        var collapse = new DoubleAnimation(from, 0, AnimationDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        collapse.Completed += (_, _) =>
        {
            BeginAnimation(MaxHeightProperty, null);
            MaxHeight = 0;
        };
        BeginAnimation(MaxHeightProperty, collapse);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, AnimationDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>临时放开高度限制测量内容真实高度，供 MaxHeight 动画使用。</summary>
    private double MeasureContentHeight()
    {
        var maxHeight = MaxHeight;
        MaxHeight = double.PositiveInfinity;
        InvalidateMeasure();
        var width = ActualWidth > 0
            ? ActualWidth
            : (Parent as FrameworkElement)?.ActualWidth ?? double.PositiveInfinity;
        Measure(new Size(width, double.PositiveInfinity));
        var height = DesiredSize.Height;
        MaxHeight = maxHeight;
        return height;
    }
}

/// <summary>Naive UI 的 n-menu 手风琴复刻：顶层可展开分组 + 子项，同一层级只展开一个分组。</summary>
public sealed class CustomMenu : ItemsControl
{
    private readonly List<CustomMenuNode> _roots = [];

    public event EventHandler<CustomMenuSelectedEventArgs>? SelectionChanged;

    /// <summary>选中叶节点（程序化导航）；分组节点仅展开。</summary>
    public void Select(string key)
    {
        var node = Find(_roots, key);
        if (node is null) return;
        if (node.HasChildren)
        {
            node.IsExpanded = true;
            return;
        }

        foreach (var root in _roots)
            root.IsExpanded = root.Children.Contains(node);
        if (node.IsSelected) SelectionChanged?.Invoke(this, new CustomMenuSelectedEventArgs(node));
        else node.IsSelected = true;
    }

    /// <summary>清除当前选中项（用于导航到个人信息等菜单外的页面）。</summary>
    public void ClearSelection()
    {
        foreach (var root in _roots)
        {
            root.IsSelected = false;
            foreach (var child in root.Children) child.IsSelected = false;
        }
    }

    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);
        foreach (var node in _roots) Attach(node, false);
        _roots.Clear();
        if (newValue is IEnumerable<CustomMenuNode> nodes)
        {
            foreach (var node in nodes)
            {
                _roots.Add(node);
                Attach(node, true);
            }
        }
    }

    private void Attach(CustomMenuNode node, bool subscribe)
    {
        node.PropertyChanged -= NodeChanged;
        if (subscribe) node.PropertyChanged += NodeChanged;
        foreach (var child in node.Children) Attach(child, subscribe);
    }

    private void NodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CustomMenuNode node) return;
        if (e.PropertyName == nameof(CustomMenuNode.IsExpanded) && node.IsExpanded)
        {
            foreach (var root in _roots)
                if (!ReferenceEquals(root, node)) root.IsExpanded = false;
        }
        else if (e.PropertyName == nameof(CustomMenuNode.IsSelected) && node.IsSelected)
        {
            foreach (var root in _roots)
                if (!root.Children.Contains(node)) root.IsExpanded = false;
            SelectionChanged?.Invoke(this, new CustomMenuSelectedEventArgs(node));
        }
    }

    private static CustomMenuNode? Find(IEnumerable<CustomMenuNode> nodes, string key)
    {
        foreach (var node in nodes)
        {
            if (node.Key == key) return node;
            var child = Find(node.Children, key);
            if (child is not null) return child;
        }
        return null;
    }
}
