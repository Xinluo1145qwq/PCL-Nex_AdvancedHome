using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 设置窗口自己用的固定浅色皮肤：#E5E8EF 底、#D7DAE1 侧栏、近白卡片。
/// 颜色写死是刻意的——这个窗口不跟启动器切深色，否则深色主题下
/// OnSurface 变成浅色字，落在浅底上会看不见。强调色仍取用户选的配色方案。
/// </summary>
internal static class UiSkin
{
    public static readonly Brush Canvas = Paint("#E5E8EF");
    public static readonly Brush Nav = Paint("#D7DAE1");
    public static readonly Brush Card = Paint("#FBFCFE");
    public static readonly Brush CardAlt = Paint("#EEF1F6");
    public static readonly Brush Line = Paint("#E1E5EC");
    public static readonly Brush Field = Paint("#FFFFFF");
    public static readonly Brush FieldLine = Paint("#C9CFDA");
    public static readonly Brush Ink = Paint("#1B1D22");
    public static readonly Brush Dim = Paint("#5B616D");

    /// <summary>永远浅色的 M3 角色表（PCL 深色主题下也能用）。</summary>
    public static M3.Scheme L => M3.Light;

    public static SolidColorBrush Paint(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>主色在浅底上按要求压深一档，做小号强调文字时对比度更稳。</summary>
    public static Brush Accent => L.Primary;

    /// <summary>悬停状态层：在给定底色上叠一层 onSurface。</summary>
    public static Brush Hover(Brush background) => M3.Layer(M3.Of(background), M3.Of(Ink), M3.HoverAlpha);

    /// <summary>卡片外观：近白底 + 1px 浅描边 + 16dp 圆角。</summary>
    public static Border Panel(FrameworkElement content, Thickness padding)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(M3.CornerL),
            Background = Card,
            BorderThickness = new Thickness(1),
            BorderBrush = Line,
            Padding = padding,
            Child = content
        };
    }

    /// <summary>细滚动条：轨道透明、3dp 圆角滑块，悬停加深。设置窗口和组件库共用。</summary>
    public static Style? ThinScrollBar()
    {
        string thumb = Hex(M3.Alpha(Ink, 0.26));
        string hover = Hex(M3.Alpha(Ink, 0.46));

        string xaml = $@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>
  <Setter Property='Width' Value='10'/>
  <Setter Property='Background' Value='Transparent'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ScrollBar'>
        <Grid Background='Transparent'>
          <Track x:Name='PART_Track' IsDirectionReversed='True'>
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Border x:Name='Bar' CornerRadius='3' Margin='2,2' Background='{thumb}'/>
                    <ControlTemplate.Triggers>
                      <Trigger Property='IsMouseOver' Value='True'>
                        <Setter TargetName='Bar' Property='Background' Value='{hover}'/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
            <Track.DecreaseRepeatButton>
              <RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/>
            </Track.IncreaseRepeatButton>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

        return (Style)XamlReader.Parse(xaml);
    }

    private static string Hex(Brush brush)
    {
        var color = M3.Of(brush);
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
