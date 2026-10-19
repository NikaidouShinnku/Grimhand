"""Install a reviewed sprite using deterministic nearest-neighbor canvas alignment."""
from pathlib import Path
import argparse
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/The Grimhands Asset/characters/berserker'
OUT = ROOT / 'outputs/grimhand-v094-design/attack-revision'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    args = parser.parse_args()
    source = Image.open(args.source).convert('RGBA')
    alpha = source.getchannel('A').point(lambda a: 255 if a >= 128 else 0)
    source.putalpha(alpha)
    bounds = alpha.getbbox()
    assert bounds and bounds[0] > 0 and bounds[1] > 0
    assert bounds[2] < source.width and bounds[3] < source.height, 'Reject cropped source'
    crop = source.crop(bounds)
    # Never independently stretch axes. Match the game's coarse square-pixel grid.
    scale = min(124 / crop.width, 118 / crop.height)
    logical = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.NEAREST)
    sprite = logical.resize((logical.width * 8, logical.height * 8), Image.Resampling.NEAREST)
    sprite = sprite.crop(sprite.getchannel('A').getbbox())
    idle = Image.open(ART / 'idle.png').convert('RGBA')
    baseline = idle.getchannel('A').getbbox()[3]
    canvas = Image.new('RGBA', (1024, 1024))
    canvas.alpha_composite(sprite, ((1024 - sprite.width) // 2, baseline - sprite.height))
    OUT.mkdir(parents=True, exist_ok=True)
    backup = OUT / 'attack-before.png'
    if not backup.exists():
        shutil.copy2(ART / 'attack.png', backup)
    shutil.copy2(args.source, OUT / 'attack-generated-source.png')
    canvas.save(OUT / 'attack.png')
    canvas.save(ART / 'attack.png')
    assert canvas.getchannel('A').getbbox()[3] == baseline
    assert canvas.getchannel('A').getextrema() == (0, 255)
    print('Installed', ART / 'attack.png', canvas.size, canvas.getchannel('A').getbbox())


if __name__ == '__main__':
    main()
