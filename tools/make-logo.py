"""生成仓库与插件市场用的 Logo（assets/logo.png）。运行：python tools/make-logo.py"""

from pathlib import Path

from PIL import Image, ImageDraw

SIZE = 512
SS = 4  # 超采样倍数
OUT = Path(__file__).resolve().parent.parent / "assets"

BLUE_TOP = (108, 156, 255)
BLUE_BOTTOM = (38, 62, 214)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def rounded(draw, box, radius, fill):
    draw.rounded_rectangle(box, radius=radius, fill=fill)


def main():
    w = SIZE * SS
    img = Image.new("RGBA", (w, w), (0, 0, 0, 0))

    # 背景：圆角方块 + 对角渐变
    gradient = Image.new("RGBA", (w, w))
    gd = ImageDraw.Draw(gradient)
    for y in range(w):
        for_x = y / (w - 1)
        # 斜向渐变：右上亮、左下深
        gd.line([(0, y), (w, y)], fill=lerp(BLUE_TOP, BLUE_BOTTOM, for_x) + (255,))

    mask = Image.new("L", (w, w), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, w - 1], radius=int(116 * SS), fill=255)
    img.paste(gradient, (0, 0), mask)

    draw = ImageDraw.Draw(img, "RGBA")

    # 顶部柔光
    from PIL import ImageFilter

    glow = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([-int(w * 0.35), -int(w * 0.55), int(w * 0.85), int(w * 0.35)],
                                 fill=(255, 255, 255, 40))
    glow = glow.filter(ImageFilter.GaussianBlur(radius=w * 0.06))
    img.alpha_composite(Image.composite(glow, Image.new("RGBA", (w, w), (0, 0, 0, 0)), mask))

    s = SS
    # 顶部工具条
    rounded(draw, (88 * s, 86 * s, 424 * s, 150 * s), 32 * s, (255, 255, 255, 96))
    # 左侧大卡片
    rounded(draw, (88 * s, 176 * s, 248 * s, 424 * s), 38 * s, (255, 255, 255, 238))
    # 右上卡片
    rounded(draw, (264 * s, 176 * s, 424 * s, 288 * s), 38 * s, (255, 255, 255, 238))
    # 右下高亮卡片
    rounded(draw, (264 * s, 304 * s, 424 * s, 424 * s), 38 * s, (140, 227, 255, 246))

    # 卡片内示意内容
    rounded(draw, (112 * s, 204 * s, 224 * s, 220 * s), 8 * s, (43, 74, 190, 210))
    rounded(draw, (112 * s, 236 * s, 196 * s, 250 * s), 7 * s, (43, 74, 190, 120))
    rounded(draw, (288 * s, 204 * s, 384 * s, 220 * s), 8 * s, (43, 74, 190, 210))
    rounded(draw, (288 * s, 236 * s, 352 * s, 250 * s), 7 * s, (43, 74, 190, 120))
    rounded(draw, (288 * s, 332 * s, 400 * s, 348 * s), 8 * s, (12, 76, 120, 210))
    rounded(draw, (288 * s, 364 * s, 356 * s, 378 * s), 7 * s, (12, 76, 120, 120))

    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    img.save(OUT / "logo.png")
    print("wrote", OUT / "logo.png")


if __name__ == "__main__":
    main()
