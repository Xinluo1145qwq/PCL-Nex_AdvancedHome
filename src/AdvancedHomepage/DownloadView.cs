using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「下载自定义文件」卡片：填一个地址，用启动器自己的多线程下载引擎存到本地。
/// 引擎走 <c>PCL.Network.FileDownloader</c>，和下载页是同一套。
/// </summary>
internal sealed class DownloadView : Grid
{
    private const double RefWidth = 340;
    private const double HeightRatio = 0.78;

    private readonly StackPanel _root = new() { Orientation = Orientation.Vertical };
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new();

    private readonly TextBox _url;
    private readonly TextBox _folder;
    private readonly TextBox _name;
    private readonly Border _start;
    private readonly TextBlock _startText;
    private readonly Border _open;
    private readonly TextBlock _status;

    private readonly Border _barHost;
    private readonly Border _bar;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private PCL.Network.DownloadFile? _tracked;
    private CancellationTokenSource? _cancel;
    private string _lastTarget = "";
    private bool _running;

    private double _width = RefWidth;
    private double _height = RefWidth * HeightRatio;
    private double _scale = 1;

    public DownloadView()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Top;

        _title.Text = "下载";
        _title.FontWeight = FontWeights.SemiBold;
        _title.Foreground = Palette.TextPrimary;

        _subtitle.Text = "用启动器的多线程引擎下载任意文件";
        _subtitle.Margin = new Thickness(0, 3, 0, 0);
        _subtitle.Foreground = Palette.TextTertiary;

        _root.Children.Add(_title);
        _root.Children.Add(_subtitle);

        _url = Input("https://example.com/file.zip");
        _url.TextChanged += (_, _) => AutoFillName();

        _folder = Input(Downloads.DefaultFolder);
        _name = Input("");

        _startText = new TextBlock
        {
            Text = "开始下载",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Palette.Solid("#FFFFFF"),
            VerticalAlignment = VerticalAlignment.Center
        };

        _start = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 7, 16, 7),
            Background = Palette.AccentBlue,
            Cursor = Cursors.Hand,
            Child = _startText
        };
        _start.MouseEnter += (_, _) => _start.Opacity = 0.85;
        _start.MouseLeave += (_, _) => _start.Opacity = 1;
        _start.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_running) Cancel();
            else Begin();
        };

        _open = SmallButton("打开文件夹", OpenFolder);

        _barHost = new Border
        {
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = Palette.Solid(Palette.IsDark ? "#1AFFFFFF" : "#12000000"),
            Visibility = Visibility.Collapsed
        };
        _bar = new Border
        {
            CornerRadius = new CornerRadius(1.5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Palette.AccentBlue,
            Width = 0
        };
        _barHost.Child = new Grid { Children = { _bar } };

        _status = new TextBlock
        {
            Text = "填好地址就能开始下载。",
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Palette.TextTertiary
        };

        BuildChildren();
        Children.Add(_root);

        _timer.Tick += (_, _) => Tick();
        Build(RefWidth, RefWidth * HeightRatio);
        Unloaded += (_, _) => _timer.Stop();
    }

    /// <summary>把控件重新挂一遍（改字号后行高要跟着变，直接重建最省事）。</summary>
    private void BuildChildren()
    {
        _root.Children.Add(Label("下载地址"));
        _root.Children.Add(_url);

        _root.Children.Add(Label("保存到"));
        var folderRow = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = new Border
        {
            Child = _folder,
            Margin = new Thickness(0)
        };
        Grid.SetColumn(box, 0);
        folderRow.Children.Add(box);

        var pick = SmallButton("选择", PickFolder);
        pick.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(pick, 1);
        folderRow.Children.Add(pick);
        _root.Children.Add(folderRow);

        _root.Children.Add(Label("文件名（留空则按地址自动取）"));
        _root.Children.Add(_name);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 0)
        };
        buttons.Children.Add(_start);
        _open.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(_open);
        _root.Children.Add(buttons);

        _barHost.Margin = new Thickness(0, 12, 0, 0);
        _root.Children.Add(_barHost);

        _status.Margin = new Thickness(0, 8, 0, 0);
        _root.Children.Add(_status);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width;

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * HeightRatio;
        h = Math.Max(h, w * HeightRatio * 0.6);

        if (Math.Abs(w - _width) > 0.4 || Math.Abs(h - _height) > 0.4) Build(w, h);

        var size = new Size(w, double.IsInfinity(availableSize.Height) ? h : Math.Min(h, availableSize.Height));
        base.MeasureOverride(size);
        return size;
    }

    private void Build(double width, double height)
    {
        if (width <= 0 || height <= 0) return;

        _width = width;
        _height = height;

        double s = Math.Clamp(width / RefWidth, 0.72, 2.2);
        _scale = s;

        var margin = new Thickness(16 * s, 16 * s, 16 * s, 16 * s);
        _root.Margin = margin;

        _title.FontSize = UiStyles.CardTitleSize(width);
        _subtitle.FontSize = 11 * s;
        _status.FontSize = 11 * s;

        foreach (var box in new[] { _url, _folder, _name })
        {
            box.FontSize = 12 * s;
            box.Height = 34 * s;
            box.Padding = new Thickness(9 * s, 0, 9 * s, 0);
        }

        _start.Padding = new Thickness(16 * s, 7 * s, 16 * s, 7 * s);
        _startText.FontSize = 12 * s;
        _open.Padding = new Thickness(12 * s, 7 * s, 12 * s, 7 * s);

        foreach (var label in _root.Children.OfType<TextBlock>())
        {
            if (ReferenceEquals(label, _title) || ReferenceEquals(label, _subtitle) || ReferenceEquals(label, _status))
                continue;
            label.FontSize = 11 * s;
            label.Margin = new Thickness(0, 9 * s, 0, 0);
        }
    }

    /// <summary>画布显示/换主题时会调，这里只刷新一次状态文字。</summary>
    public void Reload() => Tick();

    // ─────────────────────────── 下载 ───────────────────────────

    private void Begin()
    {
        var url = Downloads.NormalizeUrl(_url.Text);
        if (string.IsNullOrWhiteSpace(url))
        {
            SetStatus("先填一个下载地址。", true);
            _url.Focus();
            return;
        }

        var folder = _folder.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder)) folder = Downloads.DefaultFolder;

        string target;
        try
        {
            Directory.CreateDirectory(folder);
            var name = _name.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = Downloads.FileNameFrom(url);
            target = Path.Combine(folder, name);

            if (File.Exists(target)) File.Delete(target);
        }
        catch (Exception ex)
        {
            SetStatus("保存位置不可用：" + ex.Message, true);
            return;
        }

        _lastTarget = target;
        _url.Text = url;
        _folder.Text = folder;

        var tracked = new PCL.Network.DownloadFile(
            new[] { url },
            target,
            new PCL.ModBase.FileChecker(),
            true,
            null);

        _tracked = tracked;
        _cancel = new CancellationTokenSource();
        _running = true;

        _startText.Text = "取消下载";
        _start.Background = Palette.Solid(Palette.IsDark ? "#4A4A52" : "#7A8088");
        _barHost.Visibility = Visibility.Visible;
        _bar.Width = 0;
        SetStatus("开始下载…");
        _timer.Start();

        Log.Info($"开始下载：{url} -> {target}");

        var token = _cancel.Token;
        Task.Run(async () =>
        {
            try
            {
                await PCL.Network.FileDownloader.DownloadAsync(
                    new[] { url },
                    target,
                    true,
                    null,
                    token,
                    true,
                    tracked).ConfigureAwait(false);

                Finish(target, null);
            }
            catch (OperationCanceledException)
            {
                Finish(target, "已取消");
            }
            catch (Exception ex)
            {
                Finish(target, ex.GetBaseException().Message);
            }
        });
    }

    private void Cancel()
    {
        try
        {
            _cancel?.Cancel();
            SetStatus("正在取消…");
        }
        catch (Exception ex)
        {
            Log.Warn("取消下载失败：" + ex.Message);
        }
    }

    private void Finish(string target, string? error)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _running = false;
            _timer.Stop();
            _startText.Text = "开始下载";
            _start.Background = Palette.AccentBlue;

            if (error == null)
            {
                _barHost.Visibility = Visibility.Visible;
                _bar.Width = Math.Max(0, _barHost.ActualWidth);
                SetStatus("下载完成：" + Path.GetFileName(target));
                Log.Info($"下载完成：{target}");
            }
            else
            {
                _barHost.Visibility = Visibility.Collapsed;
                SetStatus(error == "已取消" ? "已取消下载。" : "下载失败：" + error, true);
                Log.Warn($"下载失败（{target}）：{error}");
            }
        }));
    }

    private void Tick()
    {
        var tracked = _tracked;
        if (tracked == null || !_running) return;

        double progress = Math.Clamp(tracked.Progress, 0, 1);
        _bar.Width = Math.Max(0, _barHost.ActualWidth * progress);

        var speedText = tracked.Speed > 0 ? " · " + Downloads.HumanSize(tracked.Speed) + "/s" : "";

        SetStatus(tracked.IsUnknownSize
            ? $"已下载 {Downloads.HumanSize(tracked.DownloadedBytes)}{speedText}"
            : $"{progress * 100:0.#}% · {Downloads.HumanSize(tracked.DownloadedBytes)} / "
              + $"{Downloads.HumanSize(tracked.TotalSize)}{speedText}");
    }

    private void OpenFolder()
    {
        var folder = _folder.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder)) folder = Downloads.DefaultFolder;

        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus("打不开文件夹：" + ex.Message, true);
        }
    }

    private void PickFolder()
    {
        try
        {
            var picked = PCL.Core.UI.SystemDialogs.SelectFolder("选择下载保存位置", _folder.Text.Trim());
            if (!string.IsNullOrWhiteSpace(picked)) _folder.Text = picked;
        }
        catch (Exception ex)
        {
            Log.Warn("选择文件夹失败：" + ex.Message);
        }
    }

    /// <summary>地址填完顺手补个文件名，省得再输一遍。</summary>
    private void AutoFillName()
    {
        if (!string.IsNullOrWhiteSpace(_name.Text)) return;

        var url = Downloads.NormalizeUrl(_url.Text);
        if (string.IsNullOrWhiteSpace(url)) return;

        var guess = Downloads.FileNameFrom(url);
        if (guess.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) return;   // 猜不准就不填
        _name.Text = guess;
    }

    private void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.Foreground = error ? Palette.Solid("#E4695F") : Palette.TextTertiary;
        ToolTipService.SetToolTip(_status, UiStyles.RoundedToolTip(text));
    }

    // ─────────────────────────── 装饰 ───────────────────────────

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Margin = new Thickness(0, 9, 0, 0),
        Foreground = Palette.TextTertiary
    };

    private TextBox Input(string text) => new()
    {
        Height = 34,
        Margin = new Thickness(0, 5, 0, 0),
        FontSize = 12,
        Padding = new Thickness(9, 0, 9, 0),
        VerticalContentAlignment = VerticalAlignment.Center,
        Background = Palette.Solid(Palette.IsDark ? "#1AFFFFFF" : "#0A000000"),
        Foreground = Palette.TextPrimary,
        BorderThickness = new Thickness(1),
        BorderBrush = Palette.Solid(Palette.IsDark ? "#26FFFFFF" : "#14000000"),
        CaretBrush = Palette.TextPrimary,
        Text = text
    };

    private Border SmallButton(string text, Action onClick)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = Palette.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center
        };

        var button = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 7, 12, 7),
            Background = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#0B000000"),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette.Solid(Palette.IsDark ? "#22FFFFFF" : "#12000000"),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = label
        };

        button.MouseEnter += (_, _) => button.Background = Palette.Solid(Palette.IsDark ? "#22FFFFFF" : "#16000000");
        button.MouseLeave += (_, _) => button.Background = Palette.Solid(Palette.IsDark ? "#14FFFFFF" : "#0B000000");
        button.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return button;
    }
}
