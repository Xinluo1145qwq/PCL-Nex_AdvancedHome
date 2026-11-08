using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 「计算器」卡片：默认占 3 列 × 4 行。
/// 四则运算 + 括号 + 百分号 + 正负号，结果保留最多 6 位小数；
/// 底部一条可回填的历史记录（‹ › 翻看），双击结果复制到剪贴板。
/// 历史放在静态表里，切页、删卡再加回来都还在，关掉启动器才清空。
/// </summary>
internal sealed class CalculatorView : Grid
{
    private const double RefWidth = 210;
    private const int MaxHistory = 50;

    internal sealed record Entry(string Expr, string Result);

    /// <summary>同一个启动器会话内共享，重启（插件重载）即清空。</summary>
    private static readonly List<Entry> History = new();

    private readonly TextBlock _title = new();
    private readonly TextBlock _hint = new();
    private readonly TextBlock _expr = new();
    private readonly TextBlock _result = new();
    private readonly Viewbox _resultBox = new();
    private readonly Border _focusLine = new();

    private readonly TextBlock _historyText = new();
    private readonly Border _older;
    private readonly Border _newer;
    private readonly TextBlock _olderText;
    private readonly TextBlock _newerText;

    private readonly Grid _keys = new();
    private readonly List<CalcKey> _buttons = new();

    private readonly DispatcherTimer _hintTimer;
    private DateTime _hintUntil;

    private string _expression = "";
    private string _value = "0";
    private bool _invalid;
    private int _cursor = -1;
    private bool _justEvaluated;

    private double _width = RefWidth;
    private double _scale = 1;

    private sealed class CalcKey
    {
        public Border Box = null!;
        public TextBlock Label = null!;
        public string Token = "";
        public string Kind = "digit";
    }

    public CalculatorView()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Focusable = true;
        FocusVisualStyle = null;
        Background = Brushes.Transparent;

        _title.Text = "计算器";
        _title.FontWeight = FontWeights.SemiBold;
        _title.Foreground = Palette.TextPrimary;
        _title.VerticalAlignment = VerticalAlignment.Center;

        _hint.FontSize = 11;
        _hint.Foreground = Palette.AccentBlue;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.HorizontalAlignment = HorizontalAlignment.Right;

        var head = new Grid();
        head.Children.Add(_title);
        head.Children.Add(_hint);

        _expr.TextAlignment = TextAlignment.Right;
        _expr.Foreground = Palette.TextTertiary;
        _expr.TextTrimming = TextTrimming.CharacterEllipsis;
        _expr.Margin = new Thickness(0, 4, 0, 0);

        _result.TextAlignment = TextAlignment.Right;
        _result.FontWeight = FontWeights.SemiBold;
        _result.Foreground = Palette.TextPrimary;
        _result.Background = Brushes.Transparent;
        _result.Cursor = Cursors.Hand;
        _result.ToolTip = UiStyles.RoundedToolTip("双击复制结果");
        _result.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                CopyResult();
                e.Handled = true;
            }
        };

        _resultBox.Stretch = Stretch.Uniform;
        _resultBox.StretchDirection = StretchDirection.DownOnly;
        _resultBox.HorizontalAlignment = HorizontalAlignment.Right;
        _resultBox.VerticalAlignment = VerticalAlignment.Center;
        _resultBox.Child = _result;

        _focusLine.HorizontalAlignment = HorizontalAlignment.Stretch;
        _focusLine.Background = Palette.AccentBlue;
        _focusLine.CornerRadius = new CornerRadius(1);
        _focusLine.Visibility = Visibility.Collapsed;

        var display = new Grid();
        display.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        display.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        display.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        display.Children.Add(_expr);
        display.Children.Add(_resultBox);
        Grid.SetRow(_resultBox, 1);
        display.Children.Add(_focusLine);
        Grid.SetRow(_focusLine, 2);

        // ── 历史记录条：‹  [ 1+2 = 3 ]  › ──
        _olderText = new TextBlock { Text = "‹", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _newerText = new TextBlock { Text = "›", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        _older = ArrowBox(_olderText, () => Browse(-1));
        _newer = ArrowBox(_newerText, () => Browse(1));

        _historyText.TextAlignment = TextAlignment.Center;
        _historyText.TextTrimming = TextTrimming.CharacterEllipsis;
        _historyText.VerticalAlignment = VerticalAlignment.Center;
        _historyText.Cursor = Cursors.Hand;
        _historyText.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            UseHistory();
        };

        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Children.Add(_older);
        bar.Children.Add(_historyText);
        bar.Children.Add(_newer);
        Grid.SetColumn(_historyText, 1);
        Grid.SetColumn(_newer, 2);

        var barHost = new Border { Child = bar, CornerRadius = new CornerRadius(M3.CornerS) };
        barHost.MouseLeftButtonDown += (_, e) => e.Handled = false;

        // ── 键盘区：5 列 × 5 行，塞进 3×3 的卡片里 ──
        for (int c = 0; c < 5; c++)
            _keys.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 5; r++)
            _keys.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddKey("C", "C", "func", 0, 0);
        AddKey("(", "(", "func", 0, 1);
        AddKey(")", ")", "func", 0, 2);
        AddKey("%", "%", "func", 0, 3);
        AddKey("⌫", "⌫", "func", 0, 4);

        AddKey("7", "7", "digit", 1, 0);
        AddKey("8", "8", "digit", 1, 1);
        AddKey("9", "9", "digit", 1, 2);
        AddKey("÷", "/", "op", 1, 3);
        AddKey("×", "*", "op", 1, 4);

        AddKey("4", "4", "digit", 2, 0);
        AddKey("5", "5", "digit", 2, 1);
        AddKey("6", "6", "digit", 2, 2);
        AddKey("−", "-", "op", 2, 3);
        AddKey("+", "+", "op", 2, 4);

        AddKey("1", "1", "digit", 3, 0);
        AddKey("2", "2", "digit", 3, 1);
        AddKey("3", "3", "digit", 3, 2);
        AddKey("±", "±", "func", 3, 3);
        AddKey("=", "=", "eq", 3, 4);

        AddKey("0", "0", "digit", 4, 0, 3);
        AddKey(".", ".", "digit", 4, 3, 2);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });              // 标题
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });  // 输入 + 结果
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });              // 历史条
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });   // 键盘

        Children.Add(head);
        Children.Add(display);
        Grid.SetRow(display, 1);
        Children.Add(barHost);
        Grid.SetRow(barHost, 2);

        var keysHost = new Grid();
        keysHost.Children.Add(_keys);
        Children.Add(keysHost);
        Grid.SetRow(keysHost, 3);

        // 点卡片任意位置拿到键盘焦点；焦点在卡片里时显示一条高亮线
        PreviewMouseDown += (_, _) => Focus();
        IsKeyboardFocusWithinChanged += (_, _) =>
        {
            _focusLine.Visibility = IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Collapsed;
            if (DateTime.UtcNow < _hintUntil) return;

            if (IsKeyboardFocusWithin) _hint.Text = "键盘可用";
            else if (_hint.Text == "键盘可用") _hint.Text = "";
        };

        // 启动器自己会拦一层键盘（主页上打字会跳到搜索），
        // 所以直接在输入预处理阶段取键：只要焦点在卡片里就由我们处理。
        // 订阅必须放在 Loaded：切到别的页面时卡片会被卸载、回来时重新加载，
        // 构造函数不会再跑一遍，只在构造里订阅就会永久丢键（键盘失效的根因）。
        Loaded += (_, _) =>
        {
            InputManager.Current.PreProcessInput -= OnPreProcessInput;
            InputManager.Current.PreProcessInput += OnPreProcessInput;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            InputManager.Current.PreProcessInput -= OnPreProcessInput;
            _hintTimer.Stop();
        };

        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        _hintTimer.Tick += (_, _) =>
        {
            // 被新提示续过时间就继续等，别把「已复制」提前盖掉
            if (DateTime.UtcNow < _hintUntil) return;

            _hintTimer.Stop();
            _hint.Text = IsKeyboardFocusWithin ? "键盘可用" : "";
        };

        RefreshTheme();
        Refresh();
    }

    // ─────────────────────────── 外观 ───────────────────────────

    private Border ArrowBox(TextBlock label, Action onClick)
    {
        var box = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerS),
            Cursor = Cursors.Hand,
            Child = label,
            Background = Brushes.Transparent
        };

        box.MouseEnter += (_, _) => box.Background = Palette.Solid(Palette.IsDark ? "#1AFFFFFF" : "#0F000000");
        box.MouseLeave += (_, _) => box.Background = Brushes.Transparent;
        box.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Focus();
            onClick();
        };

        return box;
    }

    private void AddKey(string label, string token, string kind, int row, int col, int span = 1)
    {
        var text = new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = kind is "op" or "eq" or "func" ? FontWeights.Bold : FontWeights.Medium
        };

        // M3 按键：圆角方块 + 状态层，按下时轻微缩小（Expressive 空间弹簧）
        var box = new Border
        {
            CornerRadius = new CornerRadius(M3.CornerS),
            Cursor = Cursors.Hand,
            Child = text,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };

        box.MouseEnter += (_, _) => ApplyKeyState(box, kind, M3.HoverAlpha);
        box.MouseLeave += (_, _) => ApplyKeyState(box, kind, 0);
        box.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Focus();
            AnimateKey(box, 0.94);
            Press(token);
        };
        box.MouseLeftButtonUp += (_, _) => AnimateKey(box, 1);

        Grid.SetRow(box, row);
        Grid.SetColumn(box, col);
        Grid.SetColumnSpan(box, span);
        _keys.Children.Add(box);

        _buttons.Add(new CalcKey { Box = box, Label = text, Token = token, Kind = kind });
    }

    /// <summary>按键的角色底：数字 / 运算符 / 功能键 / 等号各用一种容器色。</summary>
    private static Brush KeyBackground(string kind) => kind switch
    {
        "op" => M3.C.SecondaryContainer,
        "eq" => M3.C.Primary,
        "func" => M3.C.TertiaryContainer,
        _ => M3.C.SurfaceHighest
    };

    private static Brush KeyForeground(string kind) => kind switch
    {
        "op" => M3.C.OnSecondaryContainer,
        "eq" => M3.C.OnPrimary,
        "func" => M3.C.OnTertiaryContainer,
        _ => M3.C.OnSurface
    };

    /// <summary>M3 状态层：把 on surface 按比例叠到按键底色上（每次都从角色底重新算，不会越叠越深）。</summary>
    private static void ApplyKeyState(Border box, string kind, double alpha)
    {
        box.Background = alpha <= 0
            ? KeyBackground(kind)
            : M3.Layer(Palette.Of(KeyBackground(kind)), Palette.Of(M3.C.OnSurface), alpha);
    }

    private static void AnimateKey(Border box, double target)
    {
        if (box.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            box.RenderTransform = scale;
        }

        var anim = new System.Windows.Media.Animation.DoubleAnimation(target, M3.FastSpatial)
        {
            EasingFunction = M3.Spatial
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    /// <summary>主题变了重新取色（PCL 切深浅色时调用）。</summary>
    public void RefreshTheme()
    {
        _title.Foreground = Palette.TextPrimary;
        _hint.Foreground = Palette.AccentBlue;
        _expr.Foreground = Palette.TextTertiary;
        _result.Foreground = Palette.TextPrimary;
        _focusLine.Background = Palette.AccentBlue;

        _historyText.Foreground = Palette.TextSecondary;
        _olderText.Foreground = Palette.TextTertiary;
        _newerText.Foreground = Palette.TextTertiary;

        // M3 角色分配：数字 = surface container highest；运算符 = secondary container；
        // 功能键（C / 括号 / % / 退格）= tertiary container；等号 = primary。
        foreach (var key in _buttons)
        {
            key.Box.Background = KeyBackground(key.Kind);
            key.Label.Foreground = KeyForeground(key.Kind);
        }

        RefreshHistoryBar();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = _width;

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = w * 1.6;

        if (Math.Abs(w - _width) > 0.4) Build(w);

        var size = new Size(w, double.IsInfinity(availableSize.Height) ? h : Math.Min(h, availableSize.Height));
        base.MeasureOverride(size);
        return size;
    }

    private void Build(double width)
    {
        if (width <= 0) return;

        _width = width;
        double s = Math.Clamp(width / RefWidth, 0.68, 2.2);
        _scale = s;

        Margin = new Thickness(11 * s, 9 * s, 11 * s, 9 * s);

        _title.FontSize = UiStyles.CardTitleSize(width);
        _hint.FontSize = 11 * s;

        _expr.FontSize = 11 * s;
        _expr.Margin = new Thickness(0, 2 * s, 0, 0);
        _result.FontSize = 24 * s;
        _resultBox.MaxHeight = 32 * s;

        _focusLine.Height = Math.Max(1, 1.3 * s);
        _focusLine.Margin = new Thickness(0, 1.5 * s, 0, 0);

        _older.Width = 17 * s;
        _newer.Width = 17 * s;
        _olderText.FontSize = 14 * s;
        _newerText.FontSize = 14 * s;
        _historyText.FontSize = 11 * s;

        // 历史条
        var barHost = (Border)Children[2];
        barHost.Padding = new Thickness(1 * s, 2 * s, 1 * s, 2 * s);
        barHost.Margin = new Thickness(0, 4 * s, 0, 4 * s);
        barHost.Background = M3.C.SurfaceContainer;

        // 键盘区
        var keysHost = (Grid)Children[3];
        keysHost.Margin = new Thickness(0, 2 * s, 0, 0);

        foreach (var key in _buttons)
        {
            key.Box.Margin = new Thickness(2 * s);
            key.Box.CornerRadius = new CornerRadius(M3.CornerS * s);
            key.Label.FontSize = (key.Kind == "eq" ? 15 : 13.5) * s;
        }

        Refresh();
    }

    // ─────────────────────────── 显示 ───────────────────────────

    private void Refresh()
    {
        Recalculate();

        _expr.Text = Trim(_expression, _width / Math.Max(6, 11.5 * _scale * 0.62));
        _result.Text = _invalid ? "—" : _value;
        _result.Opacity = _invalid ? 0.45 : 1;

        RefreshHistoryBar();
    }

    private static string Trim(string text, double maxChars)
    {
        int limit = (int)Math.Floor(maxChars);
        if (limit < 4) limit = 4;
        if (text.Length <= limit) return text;
        return "…" + text.Substring(text.Length - (limit - 1));
    }

    private void RefreshHistoryBar()
    {
        if (History.Count == 0)
        {
            _historyText.Text = "暂无历史记录";
            _historyText.Foreground = Palette.TextTertiary;
            _older.Opacity = 0.4;
            _newer.Opacity = 0.4;
            _historyText.ToolTip = null;
            return;
        }

        if (_cursor < 0 || _cursor >= History.Count) _cursor = History.Count - 1;
        var entry = History[_cursor];

        _historyText.Text = $"{entry.Expr} = {entry.Result}";
        _historyText.Foreground = Palette.TextSecondary;
        _historyText.ToolTip = UiStyles.RoundedToolTip($"{entry.Expr} = {entry.Result}\n点击把结果填回输入框");
        _older.Opacity = 1;
        _newer.Opacity = 1;
    }

    // ─────────────────────────── 输入 ───────────────────────────

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (!IsKeyboardFocusWithin) return;

        switch (e.StagingItem.Input)
        {
            // 不看 RoutedEvent：预处理阶段有时拿到的是 KeyDown 而不是 PreviewKeyDown，
            // 之前只匹配 PreviewKeyDown，回车就漏给了启动器的「启动游戏」，一按就开游戏。
            case KeyEventArgs key when key.IsDown:
                if (HandleKey(key.Key))
                {
                    e.Cancel();
                    key.Handled = true;
                }
                break;

            case TextCompositionEventArgs text:
                // 卡片有焦点时把可打印字符都收走，免得启动器弹出全局搜索
                HandleText(text.Text);
                e.Cancel();
                text.Handled = true;
                break;
        }
    }

    private bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                Press("=");
                return true;
            case Key.Back:
                Press("⌫");
                return true;
            case Key.Escape:
            case Key.Delete:
                Press("C");
                return true;
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                CopyResult();
                return true;
            default:
                return false;
        }
    }

    private void HandleText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        bool changed = false;

        foreach (char raw in text)
        {
            string? token = raw switch
            {
                >= '0' and <= '9' => raw.ToString(),
                '.' or ',' => ".",
                '+' => "+",
                '-' => "-",
                '*' or 'x' or 'X' => "*",
                '/' => "/",
                '%' => "%",
                '(' => "(",
                ')' => ")",
                '=' => "=",
                _ => null
            };

            if (token == null) continue;
            Press(token);
            changed = true;
        }

        if (changed) Refresh();
    }

    private void Press(string token)
    {
        switch (token)
        {
            case "C":
                _expression = "";
                _justEvaluated = false;
                break;
            case "⌫":
                if (_expression.Length > 0) _expression = _expression.Substring(0, _expression.Length - 1);
                _justEvaluated = false;
                break;
            case "=":
                Commit();
                break;
            case "±":
                ToggleSign();
                _justEvaluated = false;
                break;
            default:
                Append(token);
                break;
        }

        Refresh();
    }

    private void Append(string token)
    {
        bool startsNew = _justEvaluated && (char.IsDigit(token[0]) || token is "." or "(");
        if (startsNew) _expression = "";
        _justEvaluated = false;

        char last = _expression.Length > 0 ? _expression[^1] : '\0';

        switch (token)
        {
            case "(":
                if (char.IsDigit(last) || last is ')' or '%') _expression += "*";
                _expression += "(";
                break;

            case ")":
            {
                int open = 0, close = 0;
                foreach (char c in _expression)
                {
                    if (c == '(') open++;
                    else if (c == ')') close++;
                }

                if (open > close && !(last is '(' or '+' or '-' or '*' or '/' or '\0'))
                    _expression += ")";
                break;
            }

            case "%":
                if (char.IsDigit(last) || last is ')' or '%' or '.') _expression += "%";
                break;

            case ".":
                // 当前数字里已经有点了就不再补
                if (char.IsDigit(last) || last is ')' or '%')
                {
                    int start = _expression.Length;
                    while (start > 0 && (char.IsDigit(_expression[start - 1]) || _expression[start - 1] is '.' or '%'))
                        start--;

                    if (!_expression.Substring(start).Contains('.')) _expression += ".";
                }
                else
                {
                    _expression += "0.";
                }
                break;

            case "+":
            case "-":
            case "*":
            case "/":
                if (_expression.Length == 0)
                {
                    if (token == "-") _expression = "-";
                    break;
                }

                if (last is '+' or '-' or '*' or '/')
                    _expression = _expression.Substring(0, _expression.Length - 1) + token;
                else if (last != '(')
                    _expression += token;
                break;

            default: // 数字
                if (last is ')' or '%') _expression += "*";
                _expression += token;
                break;
        }
    }

    private void ToggleSign()
    {
        if (_expression.Length == 0)
        {
            _expression = "-";
            return;
        }

        char last = _expression[^1];

        // 刚输完运算符 / 左括号：直接补一个负号
        if (last is '+' or '-' or '*' or '/' or '(')
        {
            _expression += "-";
            return;
        }

        // 找当前数字的起点
        int start = _expression.Length;
        while (start > 0)
        {
            char c = _expression[start - 1];
            if (char.IsDigit(c) || c is '.' or '%') start--;
            else break;
        }

        if (start == 0)
        {
            _expression = "-" + _expression;
            return;
        }

        // 前面已经有一个一元负号就去掉，否则补上
        if (_expression[start - 1] == '-'
            && (start - 1 == 0 || _expression[start - 2] is '(' or '+' or '-' or '*' or '/'))
        {
            _expression = _expression.Remove(start - 1, 1);
            return;
        }

        _expression = _expression.Insert(start, "-");
    }

    private void Commit()
    {
        if (_expression.Length == 0 || _justEvaluated) return;

        if (!Calc.TryEvaluate(_expression, out double value, out _))
        {
            Recalculate();
            return;
        }

        string result = Calc.Format(value);

        // 只按了一个数就回车，不往历史里塞「1 = 1」这种没信息量的记录
        bool hasOperator = _expression.IndexOfAny(new[] { '+', '-', '*', '/', '(', ')', '%' }) >= 0;
        if (hasOperator)
        {
            History.Add(new Entry(_expression, result));
            if (History.Count > MaxHistory) History.RemoveAt(0);
            _cursor = History.Count - 1;
            ShowHint("已记入历史");
        }

        _expression = result;
        _value = result;
        _invalid = false;
        _justEvaluated = true;
    }

    private void Browse(int delta)
    {
        if (History.Count == 0) return;

        if (_cursor < 0 || _cursor >= History.Count) _cursor = History.Count - 1;
        _cursor = Math.Clamp(_cursor + delta, 0, History.Count - 1);

        Focus();
        RefreshHistoryBar();
    }

    private void UseHistory()
    {
        if (History.Count == 0) return;

        if (_cursor < 0 || _cursor >= History.Count) _cursor = History.Count - 1;

        _expression = History[_cursor].Result;
        Focus();
        Refresh();

        ShowHint("已填回结果");
    }

    private void Recalculate()
    {
        if (_expression.Length == 0)
        {
            _value = "0";
            _invalid = false;
            return;
        }

        if (Calc.TryEvaluate(_expression, out double value, out _))
        {
            _value = Calc.Format(value);
            _invalid = false;
        }
        else
        {
            _invalid = true;
        }
    }

    private void ShowHint(string text)
    {
        _hint.Text = text;
        _hintUntil = DateTime.UtcNow.AddMilliseconds(1100);
        _hintTimer.Stop();
        _hintTimer.Start();
    }

    private void CopyResult()
    {
        string text = _invalid ? "" : _value;
        if (text.Length == 0) return;

        try
        {
            Clipboard.SetText(text);
            ShowHint("已复制");
        }
        catch (Exception ex)
        {
            Log.Warn("复制计算结果失败：" + ex.Message);
        }
    }
}

/// <summary>
/// 小型递归下降解析器：+ - × ÷、括号、后缀百分号、正负号，支持隐式乘法（2(3+4)）。
/// 只做四则运算，不引第三方库。
/// </summary>
internal static class Calc
{
    public static bool TryEvaluate(string source, out double value, out string error)
    {
        value = 0;
        error = "";

        if (string.IsNullOrWhiteSpace(source)) return false;

        try
        {
            var parser = new Parser(source);
            double v = parser.ParseExpression();
            parser.SkipSpace();
            if (!parser.End) throw new FormatException();

            if (double.IsNaN(v) || double.IsInfinity(v)) throw new DivideByZeroException();

            value = v;
            return true;
        }
        catch (DivideByZeroException)
        {
            error = "不能除以 0";
            return false;
        }
        catch
        {
            error = "算式有误";
            return false;
        }
    }

    /// <summary>最多 6 位小数，去掉末尾的 0；太大或太小改用科学计数法。</summary>
    public static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "错误";

        if (Math.Abs(value) < 5e-7) value = 0;

        double rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);

        if (rounded != 0 && (Math.Abs(rounded) >= 1e12 || Math.Abs(rounded) < 1e-6))
            return rounded.ToString("0.######E+0", CultureInfo.InvariantCulture);

        return rounded.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;

        public Parser(string text) => _text = text;

        public bool End => _index >= _text.Length;

        public void SkipSpace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
        }

        private char Peek
        {
            get
            {
                SkipSpace();
                return _index < _text.Length ? _text[_index] : '\0';
            }
        }

        public double ParseExpression()
        {
            double value = ParseTerm();

            while (true)
            {
                char c = Peek;
                if (c == '+')
                {
                    _index++;
                    value += ParseTerm();
                }
                else if (c == '-')
                {
                    _index++;
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseTerm()
        {
            double value = ParseUnary();

            while (true)
            {
                char c = Peek;

                if (c == '*')
                {
                    _index++;
                    value *= ParseUnary();
                }
                else if (c == '/')
                {
                    _index++;
                    double divisor = ParseUnary();
                    if (Math.Abs(divisor) < 1e-12) throw new DivideByZeroException();
                    value /= divisor;
                }
                else if (c == '(' || char.IsDigit(c) || c == '.')
                {
                    // 隐式乘法：2(3+4) / 3.5.2 这种写法也能算
                    value *= ParseUnary();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseUnary()
        {
            char c = Peek;

            if (c == '+')
            {
                _index++;
                return ParseUnary();
            }

            if (c == '-')
            {
                _index++;
                return -ParseUnary();
            }

            double value = ParsePrimary();

            while (Peek == '%')
            {
                _index++;
                value /= 100.0;
            }

            return value;
        }

        private double ParsePrimary()
        {
            char c = Peek;

            if (c == '(')
            {
                _index++;
                double inner = ParseExpression();
                if (Peek != ')') throw new FormatException();
                _index++;
                return inner;
            }

            if (!char.IsDigit(c) && c != '.') throw new FormatException();

            int start = _index;
            bool dot = false;

            while (_index < _text.Length)
            {
                char ch = _text[_index];
                if (char.IsDigit(ch))
                {
                    _index++;
                }
                else if (ch == '.' && !dot)
                {
                    dot = true;
                    _index++;
                }
                else
                {
                    break;
                }
            }

            string slice = _text.Substring(start, _index - start);
            if (slice == ".") throw new FormatException();

            return double.Parse(slice, CultureInfo.InvariantCulture);
        }
    }
}
