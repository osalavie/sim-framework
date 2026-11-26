using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sim.Admin.Wpf.Client.Controls.Container;

/// <summary>Naive UI 的 n-pagination：每页条数选择 + 页码导航，页数较多时以省略号收敛。</summary>
public sealed class CustomPagination : Control
{
    private static readonly IReadOnlyList<int> DefaultPageSizes = [20, 50, 100, 200, 500];

    public static readonly DependencyProperty PageIndexProperty = DependencyProperty.Register(
        nameof(PageIndex), typeof(int), typeof(CustomPagination),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPaginationChanged, CoercePageIndex));

    public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(
        nameof(PageCount), typeof(int), typeof(CustomPagination),
        new PropertyMetadata(1, OnPaginationChanged));

    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(
        nameof(PageSize), typeof(int), typeof(CustomPagination),
        new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPageSizeChanged, CoercePageSize));

    /// <summary>下拉可选的每页条数；也可在输入框内输入区间内的自定义值。</summary>
    public static readonly DependencyProperty PageSizesProperty = DependencyProperty.Register(
        nameof(PageSizes), typeof(IReadOnlyList<int>), typeof(CustomPagination),
        new PropertyMetadata(DefaultPageSizes, OnPageSizesChanged));

    public static readonly DependencyProperty MinimumPageSizeProperty = DependencyProperty.Register(
        nameof(MinimumPageSize), typeof(int), typeof(CustomPagination),
        new PropertyMetadata(20, OnPageSizeBoundsChanged));

    public static readonly DependencyProperty MaximumPageSizeProperty = DependencyProperty.Register(
        nameof(MaximumPageSize), typeof(int), typeof(CustomPagination),
        new PropertyMetadata(500, OnPageSizeBoundsChanged));

    private StackPanel? _pages;
    private Button? _previous;
    private Button? _next;
    private TextBlock? _info;
    private ComboBox? _pageSizePicker;
    private bool _syncingPageSize;

    /// <summary>用户切换页码后触发，此时 <see cref="PageIndex"/> 已更新。</summary>
    public event EventHandler? PageChanged;

    /// <summary>用户切换每页条数后触发，此时 <see cref="PageSize"/> 已更新；页码重置由使用方负责。</summary>
    public event EventHandler? PageSizeChanged;

    public int PageIndex
    {
        get => (int)GetValue(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    public int PageCount
    {
        get => (int)GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    /// <summary>每页条数，默认 20，限制在 <see cref="MinimumPageSize"/> 与 <see cref="MaximumPageSize"/> 之间。</summary>
    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>下拉可选的每页条数，默认 20/50/100/200/500。</summary>
    public IReadOnlyList<int> PageSizes
    {
        get => (IReadOnlyList<int>)GetValue(PageSizesProperty);
        set => SetValue(PageSizesProperty, value);
    }

    /// <summary>允许的最小每页条数，默认 20。</summary>
    public int MinimumPageSize
    {
        get => (int)GetValue(MinimumPageSizeProperty);
        set => SetValue(MinimumPageSizeProperty, value);
    }

    /// <summary>允许的最大每页条数，默认 500。</summary>
    public int MaximumPageSize
    {
        get => (int)GetValue(MaximumPageSizeProperty);
        set => SetValue(MaximumPageSizeProperty, value);
    }

    public override void OnApplyTemplate()
    {
        DetachTemplateParts();
        base.OnApplyTemplate();

        _pages = GetTemplateChild("PART_Pages") as StackPanel;
        _previous = GetTemplateChild("PART_Prev") as Button;
        _next = GetTemplateChild("PART_Next") as Button;
        _info = GetTemplateChild("PART_Info") as TextBlock;
        _pageSizePicker = GetTemplateChild("PART_PageSize") as ComboBox;

        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
            _previous.Click += OnPreviousClick;
        }

        if (_next is not null)
        {
            _next.Click -= OnNextClick;
            _next.Click += OnNextClick;
        }

        if (_pageSizePicker is not null)
        {
            _pageSizePicker.ItemsSource = PageSizes;
            _pageSizePicker.SelectionChanged += OnPageSizeSelectionChanged;
            _pageSizePicker.LostKeyboardFocus += OnPageSizeLostKeyboardFocus;
            _pageSizePicker.PreviewKeyDown += OnPageSizeKeyDown;
        }

        SyncPageSizePicker();
        Rebuild();
    }

    private static object CoercePageIndex(DependencyObject d, object baseValue)
    {
        var pagination = (CustomPagination)d;
        var value = (int)baseValue;
        var count = Math.Max(1, pagination.PageCount);
        return Math.Clamp(value, 1, count);
    }

    private static object CoercePageSize(DependencyObject d, object baseValue) =>
        ((CustomPagination)d).ClampPageSize((int)baseValue);

    private static void OnPaginationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pagination = (CustomPagination)d;
        if (e.Property == PageCountProperty)
        {
            pagination.CoerceValue(PageIndexProperty);
        }

        pagination.Rebuild();
    }

    private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomPagination)d).SyncPageSizePicker();

    private static void OnPageSizesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomPagination)d).SyncPageSizePicker();

    private static void OnPageSizeBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomPagination)d).CoerceValue(PageSizeProperty);

    private void DetachTemplateParts()
    {
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }

        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }

        if (_pageSizePicker is not null)
        {
            _pageSizePicker.SelectionChanged -= OnPageSizeSelectionChanged;
            _pageSizePicker.LostKeyboardFocus -= OnPageSizeLostKeyboardFocus;
            _pageSizePicker.PreviewKeyDown -= OnPageSizeKeyDown;
        }

        _pages = null;
        _previous = null;
        _next = null;
        _info = null;
        _pageSizePicker = null;
    }

    private int ClampPageSize(int value)
    {
        var minimum = Math.Max(1, MinimumPageSize);
        var maximum = Math.Max(minimum, MaximumPageSize);
        return Math.Clamp(value, minimum, maximum);
    }

    private void ApplyPageSize(int value)
    {
        var size = ClampPageSize(value);
        if (size != PageSize)
        {
            SetCurrentValue(PageSizeProperty, size);
            PageSizeChanged?.Invoke(this, EventArgs.Empty);
        }

        SyncPageSizePicker();
    }

    private void SyncPageSizePicker()
    {
        if (_pageSizePicker is null)
        {
            return;
        }

        _syncingPageSize = true;
        try
        {
            var sizes = PageSizes;
            if (!ReferenceEquals(_pageSizePicker.ItemsSource, sizes))
            {
                _pageSizePicker.ItemsSource = sizes;
            }

            var text = PageSize.ToString(CultureInfo.InvariantCulture);
            if (sizes.Contains(PageSize))
            {
                _pageSizePicker.SelectedItem = PageSize;
                // 选中项未变化时 ComboBox 不会同步编辑框文本；显式回写以便非法输入回退。
                _pageSizePicker.Text = text;
            }
            else
            {
                _pageSizePicker.SelectedIndex = -1;
                _pageSizePicker.Text = text;
            }
        }
        finally
        {
            _syncingPageSize = false;
        }
    }

    private void OnPageSizeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPageSize || _pageSizePicker?.SelectedItem is not int size)
        {
            return;
        }

        ApplyPageSize(size);
    }

    private void OnPageSizeLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        CommitPageSize();

    private void OnPageSizeKeyDown(object sender, KeyEventArgs e)
    {
        // 下拉展开时 Enter 交给 ComboBox 选中高亮项；收起状态下提交手输值。
        if (e.Key != Key.Enter || _pageSizePicker is null || _pageSizePicker.IsDropDownOpen)
        {
            return;
        }

        CommitPageSize();
        e.Handled = true;
    }

    private void CommitPageSize()
    {
        if (_pageSizePicker is null)
        {
            return;
        }

        var text = _pageSizePicker.Text?.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            SyncPageSizePicker();
            return;
        }

        ApplyPageSize(value);
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e) => MoveTo(PageIndex - 1);

    private void OnNextClick(object sender, RoutedEventArgs e) => MoveTo(PageIndex + 1);

    private void MoveTo(int page)
    {
        var target = Math.Clamp(page, 1, Math.Max(1, PageCount));
        if (target == PageIndex)
        {
            return;
        }

        SetCurrentValue(PageIndexProperty, target);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild()
    {
        if (_pages is null)
        {
            return;
        }

        if (_previous is not null)
        {
            _previous.IsEnabled = PageIndex > 1;
        }

        if (_next is not null)
        {
            _next.IsEnabled = PageIndex < PageCount;
        }

        if (_info is not null)
        {
            _info.Text = $"第 {PageIndex} / {Math.Max(1, PageCount)} 页";
        }

        _pages.Children.Clear();
        foreach (var number in BuildPageNumbers())
        {
            if (number == 0)
            {
                _pages.Children.Add(new TextBlock
                {
                    Text = "⋯",
                    Margin = new Thickness(4, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                continue;
            }

            var button = new Button
            {
                Content = number.ToString(),
                Tag = number,
                Style = (Style)FindResource(number == PageIndex ? "CustomPageButtonCurrentStyle" : "CustomPageButtonStyle")
            };
            button.Click += OnPageClick;
            _pages.Children.Add(button);
        }
    }

    private IEnumerable<int> BuildPageNumbers()
    {
        const int window = 2;
        var count = Math.Max(1, PageCount);
        var pages = new SortedSet<int> { 1, count };

        for (var page = PageIndex - window; page <= PageIndex + window; page++)
        {
            if (page >= 1 && page <= count)
            {
                pages.Add(page);
            }
        }

        var result = new List<int>();
        var previous = 0;
        foreach (var page in pages)
        {
            if (previous != 0 && page - previous > 1)
            {
                result.Add(0);
            }

            result.Add(page);
            previous = page;
        }

        return result;
    }

    private void OnPageClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int page })
        {
            MoveTo(page);
        }
    }
}
