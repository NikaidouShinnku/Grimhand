from pathlib import Path
from PIL import Image
import hashlib,json,shutil
p=Path('outputs/grimhand-v094-design/revision-02/art')
others=['idle','attack','defend','defeat']
before={n:hashlib.sha256((p/f'{n}.png').read_bytes()).hexdigest() for n in others}
src=Path('C:/Users/Kelthuzad/.codex/generated_images/01a02f0a-ff52-7273-b893-166c6208381b/exec-0b76aad3-9ee5-4f3f-be6e-24d13568937e.png')
shutil.copy2(src,p/'source/hit_revised.png')
im=Image.open(src).convert('RGBA')
im=im.resize((112,112),Image.Resampling.NEAREST)
im.putalpha(im.getchannel('A').point(lambda a:255 if a>=128 else 0))
canvas=Image.new('RGBA',(128,128));canvas.paste(im,(8,8))
canvas.resize((1024,1024),Image.Resampling.NEAREST).save(p/'hit_revised.png')
shutil.copy2(p/'hit.png',p/'source/hit_previous_normalized.png')
shutil.copy2(p/'hit_revised.png',p/'hit.png')
after={n:hashlib.sha256((p/f'{n}.png').read_bytes()).hexdigest() for n in others}
assert before==after
review={'other_poses_unchanged':before==after,'hit_source':str(src),'hit_pose':'Both boots planted, torso twists forward, hand at ribs, axe lowered behind.','eyes':'Small blue iris with dark upper lid; no blank blue rectangles or large teeth.','status':'Local revised candidate, not Unity accepted.'}
(p/'hit-revision-review.json').write_text(json.dumps(review,ensure_ascii=False,indent=2),encoding='utf8')
print('Hit saved locally; four approved poses unchanged.')
