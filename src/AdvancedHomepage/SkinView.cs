using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 皮肤预览。相机、比例与动作都对齐 NameMC 用的 skinview3d：
/// fov 50、相机距离约 43.8、人物居中（脚底 -16 / 头顶 +16）。
/// </summary>
internal sealed class SkinView : Grid
{
    // ── skinview3d 的默认参数 ──
    private const double CameraFov = 50;          // 垂直视场角，和 skinview3d 一致
    private const double CameraDistance = 50;     // 比 skinview3d 的 43.8 稍远，卡片里留出呼吸感
    private const double CameraTargetY = 1.6;     // 视线略高于腰，人物整体落在画面偏下
    private const double ModelShift = -16;        // 模型整体下移，让身体中心落在原点

    // ── 动作幅度 ──
    // 注意：WPF 的 AxisAngleRotation3D.Angle 单位是「度」，不是弧度。
    // 之前把 0.8 弧度直接写进去，实际只转了 0.8 度（≈0.014 rad），
    // 腿几乎没动，画面里只剩「整个人上下压」——这就是「走路像蹲」的真正原因。
    private const double WalkLegAngle = 28;       // 腿前后摆的幅度（度，≈0.49 rad，对齐 skinview3d 的 0.5 rad）
    private const double WalkArmAngle = 23;       // 手臂前后摆的幅度（度），比腿略小
    private const double ArmGapAngle = 3.6;       // 手臂外张的基准角（度，= π × 0.02）
    private const double WalkSpeed = 7.5;         // 步频（rad/s），一个来回约 0.84 秒

    private const double BounceAmplitude = 1.3;   // 弹跳动作：上下起伏的幅度（格），保持之前那版的手感
    private const double BounceSway = 2.2;        // 弹跳动作：左右轻晃的角度（度）

    private const double HeadPivotY = 24;
    private const double ArmPivotY = 24;
    private const double LegPivotY = 12;

    private readonly Viewport3D _viewport = new();
    private readonly PerspectiveCamera _camera;
    private readonly TextBlock _caption;
    private readonly ModelVisual3D _root = new();
    private readonly Model3DGroup _modelGroup = new();

    private readonly RotateTransform3D _spin = new(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0));
    private readonly RotateTransform3D _tilt = new(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0));

    /// <summary>迈步时上下起伏（正面视角下，只有这个是明显能看见的）。</summary>
    private readonly TranslateTransform3D _bob = new();

    /// <summary>迈步时的左右轻晃。</summary>
    private readonly RotateTransform3D _sway = new(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0));

    private readonly List<(RotateTransform3D X, RotateTransform3D Z, double Sign)> _arms = new();
    private readonly List<(RotateTransform3D Transform, double Sign)> _legs = new();
    private RotateTransform3D? _neckYaw;
    private RotateTransform3D? _neckPitch;

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastTick;

    private bool _headFollow = true;
    private bool _walking = true;
    private bool _bouncing = true;
    private bool _dragRotate = true;
    private bool _editing;
    private bool _active = true;
    private bool _slim;                 // 细手臂（Alex 那种 3px 手臂）
    private bool _dragging;
    private Point _dragOrigin;
    private double _yawAtDragStart;
    private double _pitchAtDragStart;

    // NameMC 的预览稍微侧着站，方便一眼看出是 3D
    private double _yaw = -12;
    private double _pitch;
    private double _phase;
    private double _followYaw;
    private double _followPitch;

    public SkinView()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;

        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);

        _viewport.Margin = new Thickness(0);
        Children.Add(_viewport);

        _caption = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(14, 11, 14, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Foreground = Palette.TextPrimary
        };
        Children.Add(_caption);

        // WPF 的 FieldOfView 是「水平」视场角，名字和 three.js 的垂直 fov 不是一回事。
        // 先按 16:9 给个初值，等尺寸出来再按宽高比换算，保证垂直 50° 和 skinview3d 一致。
        _camera = new PerspectiveCamera
        {
            FieldOfView = CameraFov,
            Position = new Point3D(0, CameraTargetY, CameraDistance),
            LookDirection = new Vector3D(0, 0, -CameraDistance),
            UpDirection = new Vector3D(0, 1, 0),
            NearPlaneDistance = 0.5,
            FarPlaneDistance = 300
        };
        _viewport.Camera = _camera;

        var visuals = new ModelVisual3D();
        var lights = new Model3DGroup();
        // 和 NameMC 的观感对齐：正面接近全亮，侧面只是略微压暗，不做出强烈明暗对比
        lights.Children.Add(new AmbientLight(Color.FromRgb(0x8A, 0x8A, 0x92)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(0xB4, 0xB4, 0xB8), new Vector3D(-0.34, -0.58, -1)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(0x2E, 0x2E, 0x36), new Vector3D(0.72, 0.2, 0.9)));
        visuals.Content = lights;
        _viewport.Children.Add(visuals);

        AddShadow();

        var spinGroup = new Transform3DGroup();
        spinGroup.Children.Add(_spin);
        spinGroup.Children.Add(_tilt);
        _root.Transform = spinGroup;
        _root.Content = _modelGroup;
        _viewport.Children.Add(_root);

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Animate();

        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Start();
            else Stop();
        };

        // 拖动转身：水平绕人物转圈，垂直改俯仰，和 OrbitControls 的手感一致
        MouseLeftButtonDown += (_, e) =>
        {
            if (_editing || !_dragRotate) return;
            _dragging = true;
            _dragOrigin = e.GetPosition(this);
            _yawAtDragStart = _yaw;
            _pitchAtDragStart = _pitch;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        };

        MouseMove += (_, e) =>
        {
            if (_editing) return;
            var point = e.GetPosition(this);

            if (_dragging)
            {
                double h = Math.Max(60, ActualHeight);
                _yaw = _yawAtDragStart + (point.X - _dragOrigin.X) * 360 / h;
                _pitch = Math.Clamp(_pitchAtDragStart + (point.Y - _dragOrigin.Y) * 180 / h, -80, 80);
                ApplyOrientation();
                return;
            }

            if (!_headFollow || ActualWidth <= 1 || ActualHeight <= 1) return;

            double nx = Math.Clamp((point.X / ActualWidth - 0.5) * 2, -1, 1);
            double ny = Math.Clamp((point.Y / ActualHeight - 0.5) * 2, -1, 1);

            // 鼠标往上 → 抬头，往下 → 低头。绕 X 轴正角度会把脸压向胸口，
            // 所以这一项跟 ny 同号（以前写成 -ny，方向是反的）。
            _followYaw = nx * 32;
            _followPitch = ny * 15;
            ApplyHead();
        };

        MouseLeftButtonUp += (_, _) => EndDrag();
        MouseLeave += (_, _) =>
        {
            if (_dragging) EndDrag();

            _followYaw = 0;
            _followPitch = 0;
            ApplyHead();
        };

        SizeChanged += (_, _) =>
        {
            UpdateCaptionSize();
            UpdateCamera(ActualWidth, ActualHeight);
        };
    }

    // ─────────────────────────── 外观 ───────────────────────────

    /// <summary>Viewport3D 自身没有内容尺寸，不给它撑开的话卡片会被算成 0 高。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        double w = availableSize.Width;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0.5) w = 240;

        double h = availableSize.Height;
        if (double.IsNaN(h) || double.IsInfinity(h) || h <= 0.5) h = 320;

        var size = new Size(w, h);
        _viewport.Measure(size);
        UpdateCaptionSize(w);
        UpdateCamera(w, h);
        return size;
    }

    /// <summary>把水平视场角换算成垂直 50°，卡片再窄也不会把人物缩小。</summary>
    private void UpdateCamera(double width, double height)
    {
        if (width <= 1 || height <= 1) return;

        double aspect = width / height;
        double half = Math.Tan(CameraFov * Math.PI / 360);
        double hfov = 2 * Math.Atan(half * aspect) * 180 / Math.PI;
        _camera.FieldOfView = Math.Clamp(hfov, 18, 115);
    }

    private void UpdateCaptionSize(double width = 0)
    {
        double w = width > 1 ? width : ActualWidth;
        if (w <= 1) w = 240;

        _caption.FontSize = UiStyles.CardTitleSize(w);
        _caption.MaxWidth = Math.Max(40, w - 28);
    }

    /// <summary>卡片左上角的标题（皮肤名），字号和其他组件标题一致。</summary>
    public void SetCaption(string text)
    {
        _caption.Text = text ?? "";
        UpdateCaptionSize();
    }

    public void SetSkin(BitmapSource skin) => SetSkin(skin, null);

    /// <summary>重做模型；<paramref name="model"/> 为 slim 时手臂按 3px 画。</summary>
    public void SetSkin(BitmapSource skin, string? model)
    {
        try
        {
            _slim = string.Equals(model, "slim", StringComparison.OrdinalIgnoreCase);
            Build(skin ?? SkinLibrary.PaintSteve());
        }
        catch (Exception ex)
        {
            Log.Error("构建皮肤模型失败", ex);
        }
    }

    /// <summary>
    /// 动作开关。走路和弹跳是两套独立动作：
    /// 走路 = 手臂 / 腿前后摆；弹跳 = 整个人上下弹、四肢保持不动。
    /// </summary>
    public void Configure(bool headFollow, bool walk, bool bounce, bool dragRotate)
    {
        _headFollow = headFollow;
        _walking = walk;
        _bouncing = bounce;
        _dragRotate = dragRotate;

        if (!walk) ResetLimbs();
        if (!walk && !bounce) ResetPose();
        if (!headFollow)
        {
            _followYaw = 0;
            _followPitch = 0;
            ApplyHead();
        }

        if (walk || bounce) Start();
        else Stop();
    }

    /// <summary>编辑模式下不再处理拖动，让卡片本身能被拖动。</summary>
    public void SetEditing(bool editing)
    {
        _editing = editing;
        if (editing) EndDrag();
        Cursor = editing ? Cursors.Arrow : Cursors.Hand;
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (active) Start();
        else Stop();
    }

    /// <summary>
    /// 只要配置里开着走动就把计时器跑起来，不再看「当前是否可见」。
    /// 之前是在这里用 IsVisible 挡了一道，卡片被主页切走再切回来时计时器就再也不会启动，
    /// 表现就是「走动动画不动了」。
    /// </summary>
    private void Start()
    {
        if (!_walking && !_bouncing) return;

        _lastTick = _clock.Elapsed;
        if (!_timer.IsEnabled) _timer.Start();
    }

    private void Stop() => _timer.Stop();

    // ─────────────────────────── 动画 ───────────────────────────

    private void Animate()
    {
        var now = _clock.Elapsed;
        double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0, 0.08);
        _lastTick = now;

        if (!_walking && !_bouncing) return;

        // 不可见 / 没激活时只走时钟，不摆姿势，省得白算
        if (!_active || !IsVisible) return;

        _phase += dt * WalkSpeed;
        double t = _phase;
        double swing = Math.Sin(t);

        if (_walking)
        {
            // 腿：左右反相，绕髋部前后摆
            foreach (var (transform, sign) in _legs)
                SetAngle(transform, swing * WalkLegAngle * sign);

            // 手臂：与同侧腿反相（右手向前时右腿在后），幅度略小，并保持固定外张
            foreach (var (x, z, sign) in _arms)
            {
                SetAngle(x, -swing * WalkArmAngle * sign);
                SetAngle(z, sign > 0 ? -ArmGapAngle : ArmGapAngle);
            }

            // 腿绕髋部转时脚底抬高 12×(1-cosθ)，而鞋底前角又往下压 2×sinθ，
            // 两者相减才是「脚踩地」需要下压的高度；幅度不到 1 格，看不出来。
            double rad = Math.Abs(swing) * WalkLegAngle * Math.PI / 180;
            _bob.OffsetY = -Math.Max(0, 12 * (1 - Math.Cos(rad)) - 2.2 * Math.Sin(rad));
            SetAngle(_sway, 0);
        }
        else
        {
            // 只开弹跳：四肢保持自然下垂
            ResetLimbs();
            _bob.OffsetY = 0;
            SetAngle(_sway, 0);
        }

        if (_bouncing)
        {
            _bob.OffsetY += (Math.Abs(Math.Cos(t)) - 0.5) * BounceAmplitude;
            SetAngle(_sway, Math.Sin(t) * BounceSway);
        }

        ApplyHead();
    }

    /// <summary>AxisAngleRotation3D.Angle 是角度制；统一从这里写，避免再混单位。</summary>
    private static void SetAngle(RotateTransform3D transform, double degrees)
    {
        if (transform.Rotation is AxisAngleRotation3D axis) axis.Angle = degrees;
    }

    /// <summary>只把手脚摆回自然下垂，不动整体的起伏。</summary>
    private void ResetLimbs()
    {
        foreach (var (transform, _) in _legs)
            SetAngle(transform, 0);

        foreach (var (x, z, sign) in _arms)
        {
            SetAngle(x, 0);
            SetAngle(z, sign > 0 ? -ArmGapAngle : ArmGapAngle);
        }

        ApplyHead();
    }

    private void ResetPose()
    {
        ResetLimbs();

        _bob.OffsetY = 0;
        SetAngle(_sway, 0);
    }

    private void ApplyOrientation()
    {
        ((AxisAngleRotation3D)_spin.Rotation).Angle = _yaw;
        ((AxisAngleRotation3D)_tilt.Rotation).Angle = _pitch;
    }

    private void ApplyHead()
    {
        // 头部只跟鼠标，不参与走路动画——MC 的模型走路时头是不动的，
        // 之前叠的那点晃动只会让人觉得「脑袋在乱转」。
        if (_neckYaw != null)
            SetAngle(_neckYaw, _headFollow ? _followYaw : 0);

        if (_neckPitch != null)
            SetAngle(_neckPitch, _headFollow ? _followPitch : 0);
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        Cursor = _editing ? Cursors.Arrow : Cursors.Hand;
    }

    // ─────────────────────────── 建模 ───────────────────────────

    private sealed class Part
    {
        public string Name = "";
        public double W, H, D;
        public double BaseX, BaseY;      // 内层贴图在皮肤图上的左上角（64×64 坐标）
        public double OverlayX, OverlayY = -1;   // 外层（帽子 / 外套 / 袖子 / 裤子），-1 表示没有
        public Point3D Center;
        public Point3D Joint;
        public bool Limb;
    }

    /// <summary>脚底的一圈软阴影，让模型站在地上而不是浮着。</summary>
    private void AddShadow()
    {
        double y = ModelShift - 0.02;
        const double rx = 11.5, rz = 7.2;

        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(-rx, y, rz), new(rx, y, rz), new(rx, y, -rz), new(-rx, y, -rz)
            },
            TextureCoordinates = new PointCollection
            {
                new(0, 0), new(1, 0), new(1, 1), new(0, 1)
            },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };

        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x3C, 0, 0, 0), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, 0, 0, 0), 0.52));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));

        var material = new DiffuseMaterial(brush);
        var shadow = new GeometryModel3D(mesh, material) { BackMaterial = material };

        var visual = new ModelVisual3D { Content = shadow };
        _viewport.Children.Insert(1, visual);
    }

    private void Build(BitmapSource skin)
    {
        _modelGroup.Children.Clear();
        _arms.Clear();
        _legs.Clear();
        _neckYaw = null;
        _neckPitch = null;

        bool legacy = skin.PixelHeight < 64;

        if (skin.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap(skin, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            skin = converted;
        }

        // 标准人形比例：头 8×8×8（24~32）、躯干 8×12×4（12~24）、
        // 手臂 4×12×4（12~24，中心 X ±6）、腿 4×12×4（0~12，中心 X ±2）。
        // 细手臂（Alex）只有 3px 宽，中心收到 ±5.5。
        double armW = _slim ? 3 : 4;
        double armX = _slim ? 5.52 : 6.02;

        var parts = new List<Part>
        {
            new() { Name = "head", W = 8, H = 8, D = 8, BaseX = 0, BaseY = 0,
                    OverlayX = 32, OverlayY = 0,
                    Center = new Point3D(0, 28, 0), Joint = new Point3D(0, HeadPivotY, 0) },
            new() { Name = "body", W = 8, H = 12, D = 4, BaseX = 16, BaseY = 16,
                    OverlayX = legacy ? -1 : 16, OverlayY = legacy ? -1 : 32,
                    Center = new Point3D(0, 18, 0) },
            new() { Name = "armR", W = armW, H = 12, D = 4, BaseX = 40, BaseY = 16,
                    OverlayX = legacy ? -1 : 40, OverlayY = legacy ? -1 : 32,
                    Center = new Point3D(-armX, 18, 0), Joint = new Point3D(-armX, ArmPivotY, 0), Limb = true },
            new() { Name = "armL", W = armW, H = 12, D = 4,
                    BaseX = legacy ? 40 : 32, BaseY = legacy ? 16 : 48,
                    OverlayX = legacy ? -1 : 48, OverlayY = legacy ? -1 : 48,
                    Center = new Point3D(armX, 18, 0), Joint = new Point3D(armX, ArmPivotY, 0), Limb = true },
            new() { Name = "legR", W = 4, H = 12, D = 4, BaseX = 0, BaseY = 16,
                    OverlayX = legacy ? -1 : 0, OverlayY = legacy ? -1 : 32,
                    Center = new Point3D(-2.02, 6, 0), Joint = new Point3D(-2.02, LegPivotY, 0), Limb = true },
            new() { Name = "legL", W = 4, H = 12, D = 4,
                    BaseX = legacy ? 0 : 16, BaseY = legacy ? 16 : 48,
                    OverlayX = legacy ? -1 : 0, OverlayY = legacy ? -1 : 48,
                    Center = new Point3D(2.02, 6, 0), Joint = new Point3D(2.02, LegPivotY, 0), Limb = true }
        };

        // 整体下移 16：脚底 -16、头顶 +16，摄像机看向原点时人物正好居中
        var bodyGroup = new Model3DGroup
        {
            Transform = new TranslateTransform3D(0, ModelShift, 0)
        };

        foreach (var part in parts) AddPart(bodyGroup, skin, part);

        // 迈步时整个人上下起伏 + 左右轻晃：正面看过去也能看出在走
        var bobGroup = new Transform3DGroup();
        bobGroup.Children.Add(_sway);
        bobGroup.Children.Add(_bob);
        _modelGroup.Transform = bobGroup;

        _modelGroup.Children.Add(bodyGroup);

        ApplyOrientation();
        ResetPose();
    }

    /// <summary>加一个方块：六个面各自烘焙一张贴图，内外两层在这里就合成好。</summary>
    private void AddPart(Model3DGroup group, BitmapSource skin, Part part)
    {
        double w = part.W;
        double h = part.H;
        double d = part.D;

        double x0 = part.Center.X - w / 2, x1 = part.Center.X + w / 2;
        double y0 = part.Center.Y - h / 2, y1 = part.Center.Y + h / 2;
        double z0 = part.Center.Z - d / 2, z1 = part.Center.Z + d / 2;

        double bx = part.BaseX, by = part.BaseY;
        double ox = part.OverlayX, oy = part.OverlayY;

        // 皮肤展开图里每个面的位置；顺序：前、后、右、左、上、下
        var faces = new (Point3D A, Point3D B, Point3D C, Point3D E, Rect Uv)[]
        {
            (new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), new Point3D(x0, y1, z1),
                new Rect(bx + d, by + d, w, h)),
            (new Point3D(x1, y0, z0), new Point3D(x0, y0, z0), new Point3D(x0, y1, z0), new Point3D(x1, y1, z0),
                new Rect(bx + d + w + d, by + d, w, h)),
            (new Point3D(x0, y0, z0), new Point3D(x0, y0, z1), new Point3D(x0, y1, z1), new Point3D(x0, y1, z0),
                new Rect(bx, by + d, d, h)),
            (new Point3D(x1, y0, z1), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), new Point3D(x1, y1, z1),
                new Rect(bx + d + w, by + d, d, h)),
            (new Point3D(x0, y1, z1), new Point3D(x1, y1, z1), new Point3D(x1, y1, z0), new Point3D(x0, y1, z0),
                new Rect(bx + d, by, w, d)),
            (new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y0, z1), new Point3D(x0, y0, z1),
                new Rect(bx + d + w, by, w, d))
        };

        Transform3D? shared = null;
        RotateTransform3D? limbRotate = null;
        RotateTransform3D? armGap = null;

        if (part.Limb)
        {
            limbRotate = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0), part.Joint);

            if (part.Name is "armR" or "armL")
            {
                armGap = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0), part.Joint);
                var limbGroup = new Transform3DGroup();
                limbGroup.Children.Add(armGap);
                limbGroup.Children.Add(limbRotate);
                shared = limbGroup;
            }
            else
            {
                shared = limbRotate;
            }
        }
        else if (part.Name is "head")
        {
            if (_neckYaw == null)
            {
                _neckYaw = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0), part.Joint);
                _neckPitch = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0), part.Joint);
            }

            var headGroup = new Transform3DGroup();
            headGroup.Children.Add(_neckPitch);
            headGroup.Children.Add(_neckYaw);
            shared = headGroup;
        }

        foreach (var face in faces)
        {
            var uv = face.Uv;
            var over = ox >= 0
                ? new Rect(ox + (uv.X - bx), oy + (uv.Y - by), uv.Width, uv.Height)
                : (Rect?)null;

            var material = new DiffuseMaterial(FaceBrush(skin, uv, over));
            var mesh = BuildQuad(face.A, face.B, face.C, face.E);
            var geometry = new GeometryModel3D(mesh, material) { BackMaterial = material };
            if (shared != null) geometry.Transform = shared;
            group.Children.Add(geometry);
        }

        if (limbRotate == null) return;

        bool right = part.Name is "armR" or "legR";
        bool arm = part.Name is "armR" or "armL";
        double sign = right ? 1 : -1;

        if (arm && armGap != null)
        {
            if (!_arms.Exists(item => ReferenceEquals(item.X, limbRotate)))
                _arms.Add((limbRotate, armGap, sign));
        }
        else if (!arm)
        {
            if (!_legs.Exists(item => ReferenceEquals(item.Transform, limbRotate)))
                _legs.Add((limbRotate, sign));
        }
    }

    /// <summary>
    /// 把一个面烘焙成独立贴图：内层打底、外层叠上去，再按最近邻放大。
    /// 两点必须这么写：
    /// ① 每个面单独一张图、整幅铺满那个面，3D 采样永远落在 mipmap 第 0 层；
    /// ② 放大是自己按像素块复制的，GDI 的 DrawImage 缩放是双线性，
    ///    哪怕设了 NearestNeighbor 也会把 8×8 的脸糊成一团。
    /// </summary>
    private static Brush FaceBrush(BitmapSource skin, Rect baseRect, Rect? overlayRect)
    {
        const int Scale = 24;   // 8×8 → 192×192，卡片放大也够锐

        bool legacy = skin.PixelHeight < 64;
        double sx = skin.PixelWidth / 64.0;
        double sy = legacy ? 1 : skin.PixelHeight / 64.0;

        int w = Math.Max(1, (int)Math.Round(baseRect.Width * sx));
        int h = Math.Max(1, (int)Math.Round(baseRect.Height * sy));

        var pixels = ReadBlock(skin, baseRect, sx, sy, w, h);

        if (overlayRect is { } o)
            Blend(pixels, ReadBlock(skin, o, sx, sy, w, h));

        var bitmap = Upscale(pixels, w, h, Scale);

        var brush = new ImageBrush(bitmap)
        {
            Stretch = Stretch.Fill,
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = new Rect(0, 0, 1, 1),
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewport = new Rect(0, 0, 1, 1),
            TileMode = TileMode.None
        };

        brush.Freeze();
        return brush;
    }

    /// <summary>按皮肤像素坐标取一块 BGRA 像素。</summary>
    private static byte[] ReadBlock(BitmapSource skin, Rect rect, double sx, double sy, int w, int h)
    {
        int x = Math.Clamp((int)Math.Round(rect.X * sx), 0, Math.Max(0, skin.PixelWidth - w));
        int y = Math.Clamp((int)Math.Round(rect.Y * sy), 0, Math.Max(0, skin.PixelHeight - h));

        var buffer = new byte[w * h * 4];
        skin.CopyPixels(new Int32Rect(x, y, w, h), buffer, w * 4, 0);
        return buffer;
    }

    /// <summary>外层按自身的 alpha 叠到内层上（straight alpha）。</summary>
    private static void Blend(byte[] dst, byte[] src)
    {
        for (int i = 0; i < dst.Length; i += 4)
        {
            int a = src[i + 3];
            if (a == 0) continue;

            if (a == 255)
            {
                dst[i] = src[i];
                dst[i + 1] = src[i + 1];
                dst[i + 2] = src[i + 2];
                dst[i + 3] = 255;
                continue;
            }

            double k = a / 255.0;
            dst[i] = (byte)Math.Round(src[i] * k + dst[i] * (1 - k));
            dst[i + 1] = (byte)Math.Round(src[i + 1] * k + dst[i + 1] * (1 - k));
            dst[i + 2] = (byte)Math.Round(src[i + 2] * k + dst[i + 2] * (1 - k));
            dst[i + 3] = (byte)Math.Max(dst[i + 3], a);
        }
    }

    /// <summary>最近邻放大：每个像素复制成 Scale×Scale 的方块，边缘不会糊。</summary>
    private static BitmapSource Upscale(byte[] pixels, int w, int h, int scale)
    {
        int W = w * scale, H = h * scale;
        var big = new byte[W * H * 4];

        for (int y = 0; y < h; y++)
        {
            int rowStart = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                int si = rowStart + x * 4;
                for (int dy = 0; dy < scale; dy++)
                {
                    int di = ((y * scale + dy) * W + x * scale) * 4;
                    for (int dx = 0; dx < scale; dx++)
                    {
                        big[di] = pixels[si];
                        big[di + 1] = pixels[si + 1];
                        big[di + 2] = pixels[si + 2];
                        big[di + 3] = pixels[si + 3];
                        di += 4;
                    }
                }
            }
        }

        var bitmap = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, big, W * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static MeshGeometry3D BuildQuad(Point3D a, Point3D b, Point3D c, Point3D e)
    {
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection { a, b, c, e },
            TextureCoordinates = new PointCollection
            {
                new Point(0, 1),
                new Point(1, 1),
                new Point(1, 0),
                new Point(0, 0)
            },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };

        mesh.Freeze();
        return mesh;
    }
}
