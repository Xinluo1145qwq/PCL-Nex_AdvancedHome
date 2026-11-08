using System;
using System.Windows;

namespace PCLN.AdvancedHomepage;

/// <summary>
/// 主页上的一个可摆放组件。每个组件从属于一种样式，位置按「样式 + 组件 id」分别保存，
/// 因此不同样式下的卡片互不影响。新增组件只要往 <see cref="HomeCanvas"/> 的注册表里追加一条。
/// </summary>
internal sealed class HomeWidget
{
    /// <summary>组件唯一标识，也是配置文件里的位置键。</summary>
    public string Id = "";

    /// <summary>「添加小组件」菜单里显示的名字。</summary>
    public string Title = "";

    /// <summary>「添加小组件」里的一句话说明。</summary>
    public string Description = "";

    /// <summary>添加面板里的分类：basic / favorite / pin / pcl。</summary>
    public string Category = "basic";

    /// <summary>添加面板卡片的图标（24×24 视图框的路径数据）。</summary>
    public string IconData = "";

    /// <summary>是否由用户即时创建（单项快捷入口），删除时整条移除。</summary>
    public bool Dynamic;

    /// <summary>删除这个组件时的额外清理（例如把快捷入口配置一并删掉）。</summary>
    public Action? OnRemoved;

    /// <summary>所属样式：fancy（花样）/ minimal（极简）。</summary>
    public string Style = "fancy";

    /// <summary>组件内容。</summary>
    public UIElement View = null!;

    /// <summary>默认占几列。</summary>
    public int ColSpan = 3;

    /// <summary>默认占几行。</summary>
    public int RowSpan = 4;

    /// <summary>默认列位置。</summary>
    public int DefaultCol;

    /// <summary>默认行位置。</summary>
    public int DefaultRow;

    /// <summary>
    /// 是否允许用户调整大小。日历这类版式写死的组件设为 false，
    /// 编辑模式下就不会出现右下角的缩放手柄。
    /// </summary>
    public bool Resizable = true;

    /// <summary>是否允许删除。</summary>
    public bool CanRemove = true;

    /// <summary>不加卡片底板，直接浮在壁纸上（问候组件就是这样）。</summary>
    public bool Transparent;

    /// <summary>
    /// 卡片底色的初始不透明度。问候组件默认 0（完全透出壁纸），
    /// 用户调过之后以配置里的值为准。
    /// </summary>
    public double DefaultOpacity = 1;

    /// <summary>「⋯」菜单里的自定义入口文案，为空表示没有。</summary>
    public string ConfigureLabel = "";

    /// <summary>点自定义入口时执行。</summary>
    public Action? Configure;

    /// <summary>
    /// 列表类组件支持的条数档位（例如最近存档的 2/4/6/8 项）。
    /// 为空表示这个组件没有「显示几项」这个选项。
    /// </summary>
    public int[] CountOptions = Array.Empty<int>();

    /// <summary>条数菜单的文案模板，<c>{0}</c> 会替换成档位。留空用「显示最近 N 项」。</summary>
    public string CountLabel = "";

    /// <summary>读取当前条数，用于在菜单里打勾。</summary>
    public Func<int>? GetCount;

    /// <summary>设置条数，设置完要自己刷新内容。</summary>
    public Action<int>? SetCount;

    /// <summary>重新读取内容（菜单改完条数后调用）。</summary>
    public Action? Reload;

    /// <summary>显示/隐藏时回调（隐藏时应停掉计时器等后台动作）。</summary>
    public Action<bool>? SetActive;

    /// <summary>编辑布局状态变化时回调，参数为是否处于编辑态。</summary>
    public Action<bool>? SetEditing;

    /// <summary>由隐藏变为可见时回调，用来按需重建内容。</summary>
    public Action? OnShown;

    /// <summary>主题变化时重新取色。</summary>
    public Action? ApplyTheme;

    public CardPlacement DefaultPlacement() => new()
    {
        Id = Id,
        Col = DefaultCol,
        Row = DefaultRow,
        ColSpan = ColSpan,
        RowSpan = RowSpan
    };
}
