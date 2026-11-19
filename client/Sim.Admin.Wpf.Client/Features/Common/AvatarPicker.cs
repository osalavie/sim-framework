using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Common;

/// <summary>头像框：点击选图并裁剪上传，框内显示当前头像。</summary>
public sealed class AvatarPicker : Grid
{
    private ApiClient? _api;
    private Func<byte[], Task<string?>>? _upload;
    private bool _busy;

    private readonly Ellipse _background = new();
    private readonly Ellipse _frame = new() { StrokeThickness = 2 };
    private readonly TextBlock _hint = new()
    {
        Text = "点击上传",
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
    };
    private readonly ImageBrush _brush = new() { Stretch = Stretch.UniformToFill };

    public AvatarPicker()
    {
        Width = 88;
        Height = 88;
        Margin = new Thickness(0, 0, 0, 18);
        HorizontalAlignment = HorizontalAlignment.Center;
        Cursor = Cursors.Hand;

        _background.SetResourceReference(Shape.FillProperty, "PaperBrush");
        _frame.Fill = _brush;
        _frame.SetResourceReference(Shape.StrokeProperty, "EdgeBrush");
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        Children.Add(_background);
        Children.Add(_frame);
        Children.Add(_hint);

        MouseLeftButtonUp += SelectClick;
        MouseEnter += (_, _) => _frame.SetResourceReference(Shape.StrokeProperty, "PrimaryBrush");
        MouseLeave += (_, _) => _frame.SetResourceReference(Shape.StrokeProperty, "EdgeBrush");
    }

    public string? AvatarUrl { get; private set; }

    /// <summary>头像上传完成后触发，参数为新头像地址。</summary>
    public event EventHandler<string?>? AvatarChanged;

    public void Configure(ApiClient api, Func<byte[], Task<string?>> upload) => (_api, _upload) = (api, upload);

    public void SetAvatar(string? url)
    {
        AvatarUrl = string.IsNullOrWhiteSpace(url) ? null : url;
        _ = LoadPreviewAsync();
    }

    private async void SelectClick(object sender, MouseButtonEventArgs e)
    {
        if (_busy || _upload is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "选择头像",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var crop = new AvatarCropDialog(dialog.FileName) { Owner = Window.GetWindow(this) };
        if (crop.ShowDialog() != true || crop.CroppedPng is not { Length: > 0 } png) return;

        _busy = true;
        _hint.Text = "上传中…";
        _hint.Visibility = Visibility.Visible;
        try
        {
            var url = await _upload(png);
            AvatarUrl = string.IsNullOrWhiteSpace(url) ? AvatarUrl : url;
            ShowBytes(png);
            AvatarChanged?.Invoke(this, AvatarUrl);
        }
        catch (Exception error)
        {
            _hint.Text = "上传失败";
            _hint.Visibility = Visibility.Visible;
            ToolTip = error.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task LoadPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(AvatarUrl) || _api is null)
        {
            ShowPlaceholder();
            return;
        }
        try
        {
            var bytes = await _api.DownloadContentFileAsync(AvatarUrl);
            ShowBytes(bytes);
        }
        catch
        {
            ShowPlaceholder();
        }
    }

    private void ShowBytes(byte[] bytes)
    {
        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(bytes))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }
        bitmap.Freeze();
        _brush.ImageSource = bitmap;
        _frame.Fill = _brush;
        _hint.Visibility = Visibility.Collapsed;
        ToolTip = "点击更换头像";
    }

    private void ShowPlaceholder()
    {
        _brush.ImageSource = null;
        _frame.Fill = _brush;
        _hint.Text = "点击上传";
        _hint.Visibility = Visibility.Visible;
        ToolTip = "点击选择头像图片";
    }
}
