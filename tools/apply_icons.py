"""Android mipmap 图标生成清单：把 gen_icon.py 的产物按密度写入 gradle 工程。

在 CI 中于 stage=make 之后执行：
  python tools/apply_icons.py build/android/proj
"""
import os
import subprocess
import sys

DENSITIES = {
    "mipmap-mdpi": 48,
    "mipmap-hdpi": 72,
    "mipmap-xhdpi": 96,
    "mipmap-xxhdpi": 144,
    "mipmap-xxxhdpi": 192,
}


def main(proj_dir: str):
    here = os.path.dirname(os.path.abspath(__file__))
    gen = os.path.join(here, "gen_icon.py")

    res_dir = os.path.join(proj_dir, "res")
    if not os.path.isdir(res_dir):
        print(f"::error::res 目录不存在: {res_dir}")
        sys.exit(1)

    for density, size in DENSITIES.items():
        target_dir = os.path.join(res_dir, density)
        os.makedirs(target_dir, exist_ok=True)
        out = os.path.join(target_dir, "ic_launcher.png")
        subprocess.run([sys.executable, gen, out, str(size)], check=True)

    print("icons applied to all densities")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "build/android/proj")
