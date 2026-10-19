"""Deterministic canvas alignment only; no painting, interpolation or design changes."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/The Grimhands Asset/characters/berserker'
SOURCE = ROOT / 'outputs/grimhand-v094-design/revision-02/art'

def main():
    # The supplied GIF is authoritative. Match its first frame exactly in static views.
    with Image.open(ART / 'berserker_anim.gif') as gif:
        idle = gif.convert('RGBA').resize((1024, 1024), Image.Resampling.NEAREST)
        idle.save(ART / 'idle.png')
    target_bottom = idle.getchannel('A').getbbox()[3]
    old_idle = Image.open(SOURCE / 'idle.png').convert('RGBA')
    old_bounds = old_idle.getchannel('A').getbbox()
    new_bounds = idle.getchannel('A').getbbox()
    scale = (new_bounds[3] - new_bounds[1]) / (old_bounds[3] - old_bounds[1])
    for pose in ('attack', 'hit', 'defend', 'defeat'):
        image = Image.open(SOURCE / f'{pose}.png').convert('RGBA')
        # Keep one common character scale, not an independent fit-to-box per pose.
        size = round(1024 * scale)
        image = image.resize((size, size), Image.Resampling.NEAREST)
        bounds = image.getchannel('A').getbbox()
        canvas = Image.new('RGBA', (1024, 1024))
        x = (1024 - size) // 2
        # Feet stay on the same baseline through every pose; never crop opaque pixels.
        x = min(max(x, -bounds[0]), 1024 - bounds[2])
        y = target_bottom - bounds[3]
        assert bounds[1] + y >= 0, pose
        canvas.alpha_composite(image, (x, y))
        canvas.save(ART / f'{pose}.png')
        print(pose, canvas.size, canvas.getchannel('A').getbbox())

if __name__ == '__main__':
    main()
