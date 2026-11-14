"""Android mipmap icon generator: overwrite the engine template ic_launcher.png.

Cocos 3.8.6 gradle project has ic_launcher in BOTH build/android/proj/res and
native/engine/android/res; writing both triggers "Duplicate resources" in
mergeReleaseResources. So we only overwrite the template layer and remove the
project-layer copies:

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


def _clean_dupes(workspace):
    """Remove stale project-layer icons that duplicate the native template."""
    proj_res = os.path.join(workspace, "build", "android", "proj", "res")
    if os.path.isdir(proj_res):
        for density in DENSITIES:
            p = os.path.join(proj_res, density, "ic_launcher.png")
            if os.path.exists(p):
                os.remove(p)
                print("removed duplicate: %s" % p)


def main(target_dir):
    target_dir = os.path.abspath(target_dir)
    here = os.path.dirname(os.path.abspath(__file__))
    gen = os.path.join(here, "gen_icon.py")

    # legacy call: build/android/proj -> locate workspace root and template layer
    if os.path.basename(target_dir) == "proj":
        workspace = os.path.dirname(os.path.dirname(os.path.dirname(target_dir)))
        _clean_dupes(workspace)
        target_dir = os.path.join(workspace, "native", "engine", "android")

    res_dir = os.path.join(target_dir, "res")
    if not os.path.isdir(res_dir):
        print("::error::res dir not found: %s" % res_dir)
        sys.exit(1)

    for density, size in DENSITIES.items():
        target = os.path.join(res_dir, density)
        os.makedirs(target, exist_ok=True)
        out = os.path.join(target, "ic_launcher.png")
        subprocess.run([sys.executable, gen, out, str(size)], check=True)

    print("icons applied to all densities")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "build/android/proj")
