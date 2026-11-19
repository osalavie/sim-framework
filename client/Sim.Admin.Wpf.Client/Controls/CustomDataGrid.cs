using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Sim.Admin.Wpf.Client.Controls.Inputs;

namespace Sim.Admin.Wpf.Client.Controls;

public sealed class CustomDataGrid : DataGrid
{
    public static readonly DependencyProperty EnableSelectionProperty = DependencyProperty.Register(
        nameof(EnableSelection), typeof(bool), typeof(CustomDataGrid),
        new PropertyMetadata(false, (d, _) => ((CustomDataGrid)d).UpdateSelectionColumn()));

    /// <summary>未显式设置宽度的文本/复选列使用的默认列宽。</summary>
    public static readonly DependencyProperty DefaultColumnWidthProperty = DependencyProperty.Register(
        nameof(DefaultColumnWidth), typeof(double), typeof(CustomDataGrid),
        new PropertyMetadata(120d, (d, _) => ((CustomDataGrid)d).ApplyColumnMetrics()));

    /// <summary>列宽下限；列级 MinWidth 更大时以列级为准。</summary>
    public static readonly DependencyProperty MinimumColumnWidthProperty = DependencyProperty.Register(
        nameof(MinimumColumnWidth), typeof(double), typeof(CustomDataGrid),
        new PropertyMetadata(60d, (d, _) => ((CustomDataGrid)d).ApplyColumnMetrics()));

    /// <summary>固定在右侧的操作列；设置后从主列集合移出，由同源副表渲染。</summary>
    public static readonly DependencyProperty FrozenRightColumnProperty = DependencyProperty.Register(
        nameof(FrozenRightColumn), typeof(DataGridColumn), typeof(CustomDataGrid),
        new PropertyMetadata(null, (d, e) =>
            ((CustomDataGrid)d).UpdateFrozenColumn(e.OldValue as DataGridColumn, e.NewValue as DataGridColumn)));

    /// <summary>行键字段名，复选状态按该字段记录（对应 Web 表格的 row-key）。</summary>
    public static readonly DependencyProperty RowKeyPathProperty = DependencyProperty.Register(
        nameof(RowKeyPath), typeof(string), typeof(CustomDataGrid),
        new PropertyMetadata("Id", (d, _) => ((CustomDataGrid)d).ResetSelection()));

    public static readonly DependencyProperty IsRowCheckedProperty = DependencyProperty.RegisterAttached(
        "IsRowChecked", typeof(bool), typeof(CustomDataGrid),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((CustomDataGrid?)FindAncestor<CustomDataGrid>(d as DependencyObject))?.OnRowCheckedChanged(d, e)));

    private DataGridTemplateColumn? _selectionColumn;
    private CustomCheckBox? _selectAll;
    private readonly HashSet<object> _checkedKeys = [];
    private int _syncDepth;
    private bool _widthsInitialized;
    private bool _widthsUserAdjusted;
    private bool _applyingWidths;
    private bool _handlingResize;
    private readonly Dictionary<DataGridColumn, double> _lastWidths = [];
    private readonly Dictionary<DataGridColumn, DependencyPropertyDescriptor> _widthDescriptors = [];
    private readonly Dictionary<DataGridColumn, EventHandler> _widthHandlers = [];
    private DependencyPropertyDescriptor? _itemsSourceDescriptor;
    private EventHandler? _itemsSourceHandler;
    private ScrollViewer? _scrollViewer;
    private ContentControl? _frozenHost;
    private DataGrid? _frozenGrid;
    private bool _syncingSelection;

    public CustomDataGrid()
    {
        AutoGenerateColumns = false;
        IsReadOnly = true;
        CanUserAddRows = false;
        CanUserDeleteRows = false;
        HeadersVisibility = DataGridHeadersVisibility.Column;
        GridLinesVisibility = DataGridGridLinesVisibility.None;
        SelectionMode = DataGridSelectionMode.Single;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Auto);

        Columns.CollectionChanged += OnColumnsChanged;
        Loaded += (_, _) =>
            Dispatcher.BeginInvoke(InitializeColumnWidths, System.Windows.Threading.DispatcherPriority.Loaded);

        _itemsSourceDescriptor = DependencyPropertyDescriptor.FromProperty(
            ItemsSourceProperty, typeof(CustomDataGrid));
        _itemsSourceHandler = (_, _) => OnItemsSourceChanged();
        _itemsSourceDescriptor.AddValueChanged(this, _itemsSourceHandler);

        LayoutUpdated += (_, _) => UpdateFrozenHost();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _scrollViewer = GetTemplateChild("DG_ScrollViewer") as ScrollViewer;
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnMainScrollChanged;
            _scrollViewer.ScrollChanged += OnMainScrollChanged;
        }
        _scrollViewer?.ApplyTemplate();
        _frozenHost = _scrollViewer?.Template?.FindName("PART_FrozenColumnsHost", _scrollViewer) as ContentControl;
        if (_frozenHost is not null) _frozenHost.Content = _frozenGrid;
        UpdateFrozenHost();
        SyncFrozenScroll();
    }

    /// <summary>勾选行变化（含全选）；光标选中不触发。</summary>
    public event EventHandler? CheckedChanged;

    public bool EnableSelection
    {
        get => (bool)GetValue(EnableSelectionProperty);
        set => SetValue(EnableSelectionProperty, value);
    }

    public double DefaultColumnWidth
    {
        get => (double)GetValue(DefaultColumnWidthProperty);
        set => SetValue(DefaultColumnWidthProperty, value);
    }

    public double MinimumColumnWidth
    {
        get => (double)GetValue(MinimumColumnWidthProperty);
        set => SetValue(MinimumColumnWidthProperty, value);
    }

    public DataGridColumn? FrozenRightColumn
    {
        get => (DataGridColumn?)GetValue(FrozenRightColumnProperty);
        set => SetValue(FrozenRightColumnProperty, value);
    }

    public string RowKeyPath
    {
        get => (string)GetValue(RowKeyPathProperty);
        set => SetValue(RowKeyPathProperty, value);
    }

    public IReadOnlyList<object> CheckedRowKeys => _checkedKeys.ToList();

    /// <summary>当前勾选的数据行。</summary>
    public IReadOnlyList<object> CheckedItems
    {
        get
        {
            var items = new List<object>();
            if (ItemsSource is null) return items;
            foreach (var item in ItemsSource)
                if (IsChecked(item)) items.Add(item);
            return items;
        }
    }

    public static bool GetIsRowChecked(DependencyObject element) =>
        (bool)element.GetValue(IsRowCheckedProperty);

    public static void SetIsRowChecked(DependencyObject element, bool value) =>
        element.SetValue(IsRowCheckedProperty, value);

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (_syncingSelection || _frozenGrid is null) return;
        _syncingSelection = true;
        try { _frozenGrid.SelectedItem = SelectedItem; }
        finally { _syncingSelection = false; }
    }

    private void OnItemsSourceChanged()
    {
        ResetSelection();
        if (_frozenGrid is null) return;
        _frozenGrid.ItemsSource = ItemsSource;
        SyncFrozenScroll();
    }

    private void UpdateFrozenColumn(DataGridColumn? oldColumn, DataGridColumn? newColumn)
    {
        if (oldColumn is not null && Columns.Contains(oldColumn)) Columns.Remove(oldColumn);
        _frozenGrid?.Columns.Clear();
        if (newColumn is null)
        {
            UpdateFrozenHost();
            return;
        }
        if (Columns.Contains(newColumn)) Columns.Remove(newColumn);
        EnsureFrozenGrid();
        _frozenGrid!.Columns.Add(newColumn);
        _frozenGrid.ItemsSource = ItemsSource;
        UpdateFrozenHost();
        SyncFrozenScroll();
    }

    private void EnsureFrozenGrid()
    {
        if (_frozenGrid is not null) return;
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserResizeColumns = false,
            CanUserSortColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            AlternationCount = 2,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 480,
            SnapsToDevicePixels = true,
            RowStyle = TryFindResource("CustomDataGridRowStyle") as Style,
            CellStyle = TryFindResource("CustomDataGridFrozenCellStyle") as Style,
            ColumnHeaderStyle = TryFindResource("CustomDataGridColumnHeaderStyle") as Style
        };
        grid.SetResourceReference(BackgroundProperty, "PaperBrush");
        grid.SetResourceReference(RowBackgroundProperty, "PaperBrush");
        grid.SelectionChanged += FrozenSelectionChanged;
        grid.PreviewMouseWheel += FrozenMouseWheel;
        grid.Loaded += (_, _) => SyncFrozenScroll();
        _frozenGrid = grid;
        if (_frozenHost is not null) _frozenHost.Content = grid;
    }

    private void FrozenSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || _frozenGrid is null) return;
        _syncingSelection = true;
        try { SelectedItem = _frozenGrid.SelectedItem; }
        finally { _syncingSelection = false; }
    }

    private void FrozenMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scrollViewer is null) return;
        _scrollViewer.ScrollToVerticalOffset(Math.Max(0, _scrollViewer.VerticalOffset - e.Delta));
        e.Handled = true;
    }

    private void OnMainScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateFrozenHost();
        SyncFrozenScroll();
    }

    private void SyncFrozenScroll()
    {
        if (_frozenGrid is null) return;
        FindDescendant<ScrollViewer>(_frozenGrid)?
            .ScrollToVerticalOffset(_scrollViewer?.VerticalOffset ?? 0);
    }

    private void UpdateFrozenHost()
    {
        if (_frozenHost is null) return;
        var hasFrozen = _frozenGrid is { Columns.Count: > 0 };
        _frozenHost.Visibility = hasFrozen ? Visibility.Visible : Visibility.Collapsed;
        if (!_widthsInitialized)
        {
            var ready = (!hasFrozen || _frozenHost.ActualWidth > 0) &&
                        (FindDescendant<ScrollViewer>(this)?.ViewportWidth ?? 0) > 0;
            if (ready)
                Dispatcher.BeginInvoke(InitializeColumnWidths, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        else
        {
            RebalanceWidths();
        }
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        // 复选列的双击只切换勾选，不向外触发（页面据此忽略行编辑）。
        if (_selectionColumn is not null && IsSelectionSource(e.OriginalSource))
        {
            e.Handled = true;
            return;
        }
        base.OnMouseDoubleClick(e);
    }

    private bool IsSelectionSource(object? originalSource)
    {
        var node = originalSource as DependencyObject;
        while (node is not null and not DataGridCell) node = VisualTreeHelper.GetParent(node);
        return node is DataGridCell cell && ReferenceEquals(cell.Column, _selectionColumn);
    }

    protected override void OnLoadingRow(DataGridRowEventArgs e)
    {
        base.OnLoadingRow(e);
        if (_selectionColumn is null) return;
        _syncDepth++;
        try { SetIsRowChecked(e.Row, IsChecked(e.Row.Item)); }
        finally { _syncDepth--; }
    }

    private void OnRowCheckedChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (_syncDepth > 0 || _selectionColumn is null || element is not DataGridRow row) return;
        var key = KeyOf(row.Item);
        if (key is null) return;
        var changed = (bool)e.NewValue ? _checkedKeys.Add(key) : _checkedKeys.Remove(key);
        if (!changed) return;
        RefreshSelectAll();
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionColumn()
    {
        if (EnableSelection && _selectionColumn is null)
        {
            _selectAll = new CustomCheckBox
            {
                IsThreeState = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _selectAll.Click += SelectAllClick;
            _selectionColumn = new DataGridTemplateColumn
            {
                Header = _selectAll,
                CellTemplate = TryFindResource("SelectableRowCheckTemplate") as DataTemplate,
                HeaderStyle = TryFindResource("CustomDataGridCenteredHeaderStyle") as Style,
                CellStyle = TryFindResource("CustomDataGridSelectionCellStyle") as Style,
                Width = new DataGridLength(44),
                CanUserSort = false,
                CanUserResize = false
            };
            Columns.Insert(0, _selectionColumn);
        }
        else if (!EnableSelection && _selectionColumn is not null)
        {
            _selectAll?.Click -= SelectAllClick;
            Columns.Remove(_selectionColumn);
            _selectionColumn = null;
            _selectAll = null;
        }
        UpdateFrozenSelectionCount();
        ResetSelection();
    }

    /// <summary>复选列固定在左侧，横向滚动时保持可见。</summary>
    private void UpdateFrozenSelectionCount()
    {
        var count = _selectionColumn is not null && Columns.Count > 1 ? 1 : 0;
        if (FrozenColumnCount != count) FrozenColumnCount = count;
    }

    /// <summary>数据源或行键变化后清空勾选；复选状态属于表格组件，不属于页面。</summary>
    private void ResetSelection()
    {
        if (_selectionColumn is null) return;
        _checkedKeys.Clear();
        _syncDepth++;
        try
        {
            foreach (var row in RealizedRows()) SetIsRowChecked(row, false);
        }
        finally { _syncDepth--; }
        RefreshSelectAll();
    }

    private void SelectAllClick(object sender, RoutedEventArgs e)
    {
        if (_selectAll is null) return;
        var select = _selectAll.IsChecked != null;
        _syncDepth++;
        try
        {
            _checkedKeys.Clear();
            if (select && ItemsSource is { } source)
            {
                foreach (var item in source)
                    if (KeyOf(item) is { } key) _checkedKeys.Add(key);
            }
            foreach (var row in RealizedRows()) SetIsRowChecked(row, select && IsChecked(row.Item));
        }
        finally { _syncDepth--; }
        RefreshSelectAll();
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshSelectAll()
    {
        if (_selectAll is null) return;
        var total = 0;
        var selected = 0;
        if (ItemsSource is { } source)
        {
            foreach (var item in source)
            {
                total++;
                if (IsChecked(item)) selected++;
            }
        }
        _selectAll.IsChecked = total == 0 || selected == 0 ? false : selected == total ? true : (bool?)null;
    }

    private bool IsChecked(object item)
    {
        var key = KeyOf(item);
        return key is not null && _checkedKeys.Contains(key);
    }

    private object? KeyOf(object? item)
    {
        if (item is null) return null;
        var path = RowKeyPath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        var property = TypeDescriptor.GetProperties(item)[path];
        return property?.GetValue(item);
    }

    private IEnumerable<DataGridRow> RealizedRows()
    {
        if (ItemsSource is null) yield break;
        foreach (var item in ItemsSource)
            if (ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                yield return row;
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyColumnMetrics();
        UpdateFrozenSelectionCount();
    }

    /// <summary>
    /// 初次加载时把列宽固定为绝对宽度：星号/未设宽度的列取默认值或列级 MinWidth，
    /// 剩余空间平均分给数据列；总宽超出视口时由横向滚动条承载，拖拽某列不再压缩相邻列。
    /// </summary>
    private void InitializeColumnWidths()
    {
        if (_widthsInitialized || Columns.Count == 0) return;

        // 冻结列会占据模板独立列；等它测量出宽度后再按剩余视口均分，避免出现填充空白列。
        if (_frozenGrid is { Columns.Count: > 0 } && (_frozenHost is null || _frozenHost.ActualWidth <= 0)) return;

        var viewport = FindDescendant<ScrollViewer>(this)?.ViewportWidth ?? ActualWidth;
        if (viewport <= 0) return;
        _widthsInitialized = true;

        var candidates = Columns
            .Where(column => !ReferenceEquals(column, _selectionColumn) && !IsAutoTemplate(column))
            .ToList();

        _applyingWidths = true;
        try
        {
            foreach (var column in candidates)
            {
                if (column.Width.IsAbsolute) continue;
                column.Width = new DataGridLength(Math.Max(DefaultColumnWidth, column.MinWidth));
            }

            var occupied = OccupiedWidth();
            // 留 1px 余量，避免小数取整后重复出现横向滚动条。
            var spare = viewport - occupied - 1;
            if (spare > 0 && candidates.Count > 0)
            {
                var extra = spare / candidates.Count;
                foreach (var column in candidates)
                    column.Width = new DataGridLength(column.Width.Value + extra);
            }
            _widthsUserAdjusted = false;
        }
        finally { _applyingWidths = false; }
    }

    /// <summary>初次分配后冻结列宽度变化（如操作按钮加载）时补齐剩余空间，避免出现空白填充列。</summary>
    private void RebalanceWidths()
    {
        if (!_widthsInitialized || _widthsUserAdjusted) return;
        var viewport = FindDescendant<ScrollViewer>(this)?.ViewportWidth ?? 0;
        if (viewport <= 0) return;
        var candidates = Columns
            .Where(column => !ReferenceEquals(column, _selectionColumn) && column.Width.IsAbsolute)
            .ToList();
        if (candidates.Count == 0) return;

        var spare = viewport - OccupiedWidth() - 1;
        _applyingWidths = true;
        try
        {
            if (spare > 0.5)
            {
                var extra = spare / candidates.Count;
                foreach (var column in candidates)
                    column.Width = new DataGridLength(column.Width.Value + extra);
            }
            else if (spare < -0.5)
            {
                // 冻结列变宽后按最小列宽收窄数据列，使初次加载恰好铺满、不产生无意义滚动。
                var remaining = -spare;
                var adjustable = candidates.Where(column => column.Width.Value > column.MinWidth + 0.5).ToList();
                while (remaining > 0.5 && adjustable.Count > 0)
                {
                    var share = remaining / adjustable.Count;
                    for (var index = adjustable.Count - 1; index >= 0; index--)
                    {
                        var column = adjustable[index];
                        var take = Math.Min(share, column.Width.Value - column.MinWidth);
                        if (take <= 0)
                        {
                            adjustable.RemoveAt(index);
                            continue;
                        }
                        column.Width = new DataGridLength(column.Width.Value - take);
                        remaining -= take;
                    }
                }
            }
        }
        finally { _applyingWidths = false; }
    }

    private double OccupiedWidth()
    {
        var occupied = 0d;
        foreach (var column in Columns)
        {
            if (column.Width.IsAbsolute) occupied += column.Width.Value;
            else if (IsAutoTemplate(column)) occupied += column.ActualWidth;
        }
        return occupied;
    }

    private static bool IsAutoTemplate(DataGridColumn column) =>
        column is DataGridTemplateColumn && column.Width.IsAuto;

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private void ApplyColumnMetrics()
    {
        if (Columns is null) return;
        foreach (var column in Columns)
        {
            WatchColumnWidth(column);
            if (ReferenceEquals(column, _selectionColumn)) continue;

            // 列级 MinWidth（页面显式设置）优先；未设置时使用表格的全局下限。
            if (column.MinWidth < MinimumColumnWidth) column.MinWidth = MinimumColumnWidth;

            // 仅对未指定宽度的数据列应用默认列宽，操作列等模板列保持自适应。
            if (column is DataGridTextColumn or DataGridCheckBoxColumn && column.Width.IsAuto)
                column.Width = new DataGridLength(DefaultColumnWidth);
        }
    }

    /// <summary>记录用户手动拖拽列宽，之后不再自动补齐剩余空间。</summary>
    private void WatchColumnWidth(DataGridColumn column)
    {
        if (_widthHandlers.ContainsKey(column)) return;
        var descriptor = DependencyPropertyDescriptor.FromProperty(
            DataGridColumn.WidthProperty, typeof(DataGridColumn));
        _lastWidths[column] = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
        EventHandler handler = (_, _) => OnColumnWidthChanged(column);
        descriptor.AddValueChanged(column, handler);
        _widthDescriptors[column] = descriptor;
        _widthHandlers[column] = handler;
    }

    /// <summary>
    /// 用户拖拽列宽后的补偿：无横向滚动条时缩小列，由右侧相邻列补宽；末列不允许缩出空白。
    /// 扩大列宽不压缩邻列，总宽超出时由横向滚动条承载。
    /// </summary>
    private void OnColumnWidthChanged(DataGridColumn column)
    {
        var width = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
        var previous = _lastWidths.TryGetValue(column, out var value) ? value : width;
        _lastWidths[column] = width;

        if (_applyingWidths || _handlingResize) return;
        // 任何用户拖拽都停止自动收窄/补齐，避免扩大列宽后又被自动缩回。
        _widthsUserAdjusted = true;
        if (width >= previous - 0.01) return; // 扩大：内容变宽，由横向滚动承载
        if (!_widthsInitialized) return;

        var viewport = FindDescendant<ScrollViewer>(this)?.ViewportWidth ?? 0;
        if (viewport <= 0) return;
        var deficit = viewport - OccupiedWidth();
        if (deficit <= 0.5) return;

        var columns = Columns.Where(item => !ReferenceEquals(item, _selectionColumn)).ToList();
        var index = columns.IndexOf(column);
        if (index < 0) return;

        // 末列缩小会直接产生空白，补回自身；其余列由右侧相邻列补宽。
        var target = index == columns.Count - 1 ? column : columns[index + 1];
        var targetWidth = target.Width.IsAbsolute ? target.Width.Value + deficit : target.ActualWidth + deficit;
        _handlingResize = true;
        _applyingWidths = true;
        try
        {
            target.Width = new DataGridLength(targetWidth);
            _lastWidths[target] = targetWidth;
            _lastWidths[column] = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
        }
        finally
        {
            _applyingWidths = false;
            _handlingResize = false;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
