from pathlib import Path
from PIL import Image, ImageOps
import hashlib
p=Path('outputs/grimhand-v094-design/revision-02/art')
names=['idle','attack','defend','defeat']
before={n:hashlib.sha256((p/f'{n}.png').read_bytes()).hexdigest() for n in names}
source=p/'source/hit_revised.png'
im=Image.open(source).convert('RGBA').resize((112,112),Image.Resampling.NEAREST)
im.putalpha(im.getchannel('A').point(lambda a:255 if a>=128 else 0))
canvas=Image.new('RGBA',(128,128));canvas.paste(im,(8,8))
left=canvas.resize((1024,1024),Image.Resampling.NEAREST)
right=ImageOps.mirror(left)
right.save(p/'hit.png');right.save(p/'hit_revised.png')
assert Image.open(p/'hit.png').tobytes()==ImageOps.mirror(left).tobytes()
assert before=={n:hashlib.sha256((p/f'{n}.png').read_bytes()).hexdigest() for n in names}
print('Hit mirrored horizontally from fixed source; alpha and dimensions preserved; other four unchanged.')
