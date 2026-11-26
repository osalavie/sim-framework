using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sim.Admin.Wpf.Client.Controls;

namespace Sim.Admin.Wpf.Client.Features.Common;

/// <summary>头像裁剪：正方形取景框，支持放大、缩小与拖动，输出裁剪后的 PNG。</summary>
public sealed class AvatarCropDialog : Window
{
    private const double ViewSize = 320;
    private const double MaxZoom = 4;
    private readonly BitmapImage _source;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private readonly Canvas _canvas;
    private readonly TextBlock _zoomText = new()
    {
        Margin = new Thickness(10, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center
    };
    private double _zoom = 1;
    private Point _dragOrigin;
    private double _originX;
    private double _originY;
    private bool _dragging;

    public AvatarCropDialog(string filePath)
    {
        Title = "裁剪头像";
        Width = 372;
        Height = 480;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");

        _source = new BitmapImage();
        _source.BeginInit();
        _source.UriSource = new Uri(filePath);
        _source.CacheOption = BitmapCacheOption.OnLoad;
        _source.EndInit();
        _source.Freeze();

        var image = new Image
        {
            Source = _source,
            Width = _source.PixelWidth,
            Height = _source.PixelHeight,
            Stretch = Stretch.Fill
        };
        var transforms = new TransformGroup();
        transforms.Children.Add(_scale);
        transforms.Children.Add(_translate);
        image.RenderTransform = transforms;

        _canvas = new Canvas { Width = ViewSize, Height = ViewSize, ClipToBounds = true };
        _canvas.Children.Add(image);
        var frame = new Border
        {
            Width = ViewSize,
            Height = ViewSize,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            IsHitTestVisible = false
        };
        frame.SetResourceReference(Border.BorderBrushProperty, "PrimaryBrush");
        var preview = new Grid { Width = ViewSize, Height = ViewSize, HorizontalAlignment = HorizontalAlignment.Center };
        preview.Children.Add(_canvas);
        preview.Children.Add(frame);

        _zoomText.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        _zoomText.Margin = new Thickness(2, 10, 0, 0);
        _zoomText.HorizontalAlignment = HorizontalAlignment.Left;
        _zoomText.VerticalAlignment = VerticalAlignment.Center;

        var save = new CustomButton { Content = "保存", Variant = CustomButtonVariant.Primary, MinWidth = 90 };
        save.Click += SaveClick;
        var cancel = new CustomButton { Content = "取消", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(preview);
        root.Children.Add(_zoomText);
        root.Children.Add(buttons);
        Content = root;

        _canvas.MouseLeftButtonDown += DragStart;
        _canvas.MouseMove += DragMove;
        _canvas.MouseLeftButtonUp += DragEnd;
        _canvas.MouseWheel += WheelZoom;

        var baseScale = BaseScale();
        ApplyScale(baseScale);
        _translate.X = (ViewSize - _source.PixelWidth * baseScale) / 2;
        _translate.Y = (ViewSize - _source.PixelHeight * baseScale) / 2;
        ClampTranslate();
        UpdateZoomText();
    }

    public byte[]? CroppedPng { get; private set; }

    private double BaseScale() => ViewSize / Math.Min(_source.PixelWidth, _source.PixelHeight);

    private void DragStart(object sender, MouseButtonEventArgs e)
    {
        _dragOrigin = e.GetPosition(_canvas);
        _originX = _translate.X;
        _originY = _translate.Y;
        _dragging = true;
        _canvas.CaptureMouse();
    }

    private void DragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var point = e.GetPosition(_canvas);
        _translate.X = _originX + point.X - _dragOrigin.X;
        _translate.Y = _originY + point.Y - _dragOrigin.Y;
        ClampTranslate();
    }

    private void DragEnd(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        _canvas.ReleaseMouseCapture();
    }

    private void WheelZoom(object sender, MouseWheelEventArgs e)
    {
        ZoomBy(e.Delta > 0 ? 1.1 : 1 / 1.1);
        e.Handled = true;
    }

    private void ZoomBy(double factor)
    {
        var current = _scale.ScaleX;
        var centerX = (-_translate.X + ViewSize / 2) / current;
        var centerY = (-_translate.Y + ViewSize / 2) / current;
        _zoom = Math.Clamp(_zoom * factor, 1, MaxZoom);
        var scale = BaseScale() * _zoom;
        ApplyScale(scale);
        _translate.X = ViewSize / 2 - centerX * scale;
        _translate.Y = ViewSize / 2 - centerY * scale;
        ClampTranslate();
        UpdateZoomText();
    }

    private void ApplyScale(double scale)
    {
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
    }

    private void ClampTranslate()
    {
        var width = _source.PixelWidth * _scale.ScaleX;
        var height = _source.PixelHeight * _scale.ScaleY;
        _translate.X = Math.Clamp(_translate.X, ViewSize - width, 0);
        _translate.Y = Math.Clamp(_translate.Y, ViewSize - height, 0);
    }

    private void UpdateZoomText() => _zoomText.Text = $"{_zoom:0.0}x";

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var scale = _scale.ScaleX;
        var sourceX = (int)Math.Round(-_translate.X / scale);
        var sourceY = (int)Math.Round(-_translate.Y / scale);
        var size = Math.Max(1, (int)Math.Round(ViewSize / scale));
        size = Math.Min(size, Math.Min(_source.PixelWidth, _source.PixelHeight));
        sourceX = Math.Clamp(sourceX, 0, Math.Max(0, _source.PixelWidth - size));
        sourceY = Math.Clamp(sourceY, 0, Math.Max(0, _source.PixelHeight - size));

        BitmapSource cropped = new CroppedBitmap(_source, new Int32Rect(sourceX, sourceY, size, size));
        // 头像输出限制在 512px 内，避免原图裁剪后上传过大。
        if (size > 512)
            cropped = new TransformedBitmap(cropped, new ScaleTransform(512d / size, 512d / size));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(cropped));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        CroppedPng = stream.ToArray();
        DialogResult = true;
    }
}
