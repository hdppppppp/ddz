#!/usr/bin/env python3
"""生成 54 张扑克牌面 PNG 图集（DDZ 斗地主）。

纯 Pillow 程序化绘制：圆角白卡、左上角点数+花色、中央大花色符号、双王特殊底色。
在 CI 内运行（requirements: pillow），产物写入 assets/resources/cards/<id>.png。

用法: python gen_cards.py <output_dir> [width] [height]
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

W, H = 240, 336  # 2.5:3.5 比例
RADIUS = 18
WHITE = (252, 250, 244, 255)
BORDER = (205, 200, 188, 255)
RED = (198, 40, 40, 255)
BLACK = (34, 34, 38, 255)
JOKER_BG = (238, 228, 248, 255)
JOKER_RED = (150, 40, 160, 255)

RANK_NAMES = {0: '3', 1: '4', 2: '5', 3: '6', 4: '7', 5: '8', 6: '9', 7: '10',
              8: 'J', 9: 'Q', 10: 'K', 11: 'A', 12: '2'}
SUITS = ['♠', '♥', '♣', '♦']
SUIT_RED = {1, 3}  # ♥ ♦


def load_font(size, bold=False):
    for name in (['arialbd.ttf', 'arial.ttf'] if bold else ['arial.ttf']):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def load_cjk_font(size):
    for name in ('msyh.ttc', 'simhei.ttf', 'simsun.ttc'):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    try:
        return ImageFont.truetype('DejaVuSans.ttf', size)
    except OSError:
        return ImageFont.load_default()


def gen_card(card_id: int, out_dir: str):
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    is_joker = card_id >= 52
    bg = JOKER_BG if is_joker else WHITE
    color = JOKER_RED if is_joker else (RED if card_id < 52 and (card_id % 13) % 4 in SUIT_RED else BLACK)

    # 圆角卡底
    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=RADIUS, fill=bg, outline=BORDER, width=3)

    if is_joker:
        name = '小王' if card_id == 52 else '大王'
        font = load_cjk_font(64)
        d.text((W / 2, H / 2 - 20), name, font=font, fill=color, anchor='mm')
        sym = '🃏'
        fsym = load_cjk_font(48)
        d.text((W / 2, H / 2 + 60), 'JOKER', font=load_font(30, bold=True), fill=color, anchor='mm')
    else:
        suit = card_id // 13
        ordinal = card_id % 13
        rank_name = RANK_NAMES[ordinal]
        sym = SUITS[suit]

        # 左上角：点数 + 花色
        corner_font = load_font(56, bold=True)
        d.text((20, 14), rank_name, font=corner_font, fill=color)
        d.text((20, 78), sym, font=load_font(44), fill=color)

        # 右下角（旋转 180° 视觉：直接倒置绘制）
        corner2 = Image.new('RGBA', (W, 130), (0, 0, 0, 0))
        d2 = ImageDraw.Draw(corner2)
        d2.text((20, 10), rank_name, font=corner_font, fill=color)
        d2.text((20, 74), sym, font=load_font(44), fill=color)
        corner2 = corner2.rotate(180)
        img.alpha_composite(corner2, (W - W, H - 130))

        # 中央大花色
        big = load_font(150)
        d.text((W / 2, H / 2 + 10), sym, font=big, fill=color, anchor='mm')

    out = os.path.join(out_dir, f'{card_id}.png')
    img.save(out)


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else 'cards_out'
    os.makedirs(out_dir, exist_ok=True)
    for card_id in range(54):
        gen_card(card_id, out_dir)
    print(f'54 cards written to {out_dir}')


if __name__ == '__main__':
    main()
