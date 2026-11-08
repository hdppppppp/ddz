#!/usr/bin/env python3
"""生成 Android 启动图标（DDZ 斗地主）。

纯 Pillow 程序化绘制：深绿毛毡底 + 圆形徽章 + 三张扇形排布的扑克牌（♠A ♥K ♣Q）。
在 CI 内运行（requirements: pillow），产物写入指定输出目录的 ic_launcher.png。

用法: python gen_icon.py <output.png> [size]
"""
import math
import sys

from PIL import Image, ImageDraw, ImageFont

# 毛毡绿底与徽章金
FELT = (22, 101, 52, 255)
FELT_DARK = (14, 74, 38, 255)
GOLD = (240, 200, 80, 255)
GOLD_DARK = (190, 150, 40, 255)
WHITE = (250, 248, 242, 255)
RED = (200, 40, 40, 255)
BLACK = (30, 30, 34, 255)


def rounded_card(draw, cx, cy, w, h, angle, face, suit_char, suit_color):
    """画一张带旋转的圆角卡牌（简单多边形近似旋转）。"""
    # 卡角半径
    r = w // 6
    pts_base = [(-w / 2, -h / 2 + r), (-w / 2, h / 2 - r), (-w / 2 + r, h / 2),
                (w / 2 - r, h / 2), (w / 2, h / 2 - r), (w / 2, -h / 2 + r),
                (w / 2 - r, -h / 2), (-w / 2 + r, -h / 2)]
    cos_a, sin_a = math.cos(angle), math.sin(angle)
    pts = [(cx + x * cos_a - y * sin_a, cy + x * sin_a + y * cos_a) for x, y in pts_base]
    draw.polygon(pts, fill=WHITE, outline=(200, 196, 186, 255))

    # 中央字（不做旋转文字，Pillow 旋转文字需单独图层；小尺寸下直接画正字可接受）
    cx1, cy1 = draw.textbbox((0, 0), face, font=suit_char)
    tw, th = cx1, cy1


def gen(size: int, out_path: str):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # 圆形底（毛毡绿 + 深色描边）
    d.ellipse([0, 0, size - 1, size - 1], fill=FELT, outline=FELT_DARK, width=max(2, size // 48))

    # 内圈金色描边
    m = size // 16
    d.ellipse([m, m, size - m - 1, size - m - 1], outline=GOLD_DARK, width=max(2, size // 64))

    # 三张扇形卡牌（左倾 A♠ 黑、中间 K♥ 红抬高、右倾 Q♣ 黑）
    cw, ch = int(size * 0.24), int(size * 0.34)
    cx, cy = size / 2, size * 0.56

    def card_poly(angle_deg, color_char, suit_sym):
        angle = math.radians(angle_deg)
        # 旋转矩形四角
        pts = []
        for dx, dy in [(-cw / 2, -ch / 2), (cw / 2, -ch / 2), (cw / 2, ch / 2), (-cw / 2, ch / 2)]:
            x = cx + dx * math.cos(angle) - dy * math.sin(angle)
            y = cy + dx * math.sin(angle) + dy * math.cos(angle)
            pts.append((x, y))
        d.polygon(pts, fill=WHITE, outline=(205, 200, 190, 255), width=max(2, size // 200))
        # 卡面字符
        fsize = int(size * 0.085)
        try:
            font = ImageFont.truetype("arial.ttf", fsize)
        except OSError:
            font = ImageFont.load_default()
        # 字符放在卡中心
        text = f"{color_char}\n{suit_sym}"
        tb = d.multiline_textbbox((0, 0), text, font=font, align="center", spacing=0)
        tw, th = tb[2] - tb[0], tb[3] - tb[1]
        d.multiline_text((cx - tw / 2, cy - th / 2), text, font=font,
                         fill=suit_sym in ("♥",) and RED or BLACK, align="center", spacing=0)

    card_poly(-18, "A", "♠")
    card_poly(18, "Q", "♣")
    card_poly(0, "K", "♥")  # 中间最后画，压在上面

    # 顶部金色"斗"字
    fsize = int(size * 0.19)
    try:
        font_big = ImageFont.truetype("msyh.ttc", fsize)
    except OSError:
        try:
            font_big = ImageFont.truetype("DejaVuSans.ttf", fsize)
        except OSError:
            font_big = ImageFont.load_default()
    text = "斗"
    tb = d.textbbox((0, 0), text, font=font_big)
    tw, th = tb[2] - tb[0], tb[3] - tb[1]
    tx, ty = size / 2 - tw / 2, size * 0.12
    # 金色带阴影
    d.text((tx + size * 0.008, ty + size * 0.008), text, font=font_big, fill=(60, 40, 10, 255))
    d.text((tx, ty), text, font=font_big, fill=GOLD)

    img.save(out_path)
    print(f"icon written: {out_path} ({size}x{size})")


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "ic_launcher.png"
    s = int(sys.argv[2]) if len(sys.argv) > 2 else 512
    gen(s, out)
