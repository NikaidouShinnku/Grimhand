"""Side-by-side review only; originals are never modified."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/grimhand-v094-design/revision-02'
out=Image.new('RGB',(960,440),(39,41,46))
draw=ImageDraw.Draw(out)
try: font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
except OSError: font=ImageFont.load_default()
items=[('原战士受击',ROOT/'Assets/The Grimhands Asset/characters/warrior/warrior_hit_1024.png'),('原恶魔受击',ROOT/'Assets/The Grimhands Asset/characters/devil/devil_hit_1024.png'),('重画受击候选，未定稿',OUT/'art/hit.png')]
for index,(label,path) in enumerate(items):
 im=Image.open(path).convert('RGBA')
 mask=im.getchannel('A').point(lambda v:255 if v>=128 else 0)
 im.putalpha(mask);im=im.crop(mask.getbbox())
 scale=min(286/im.width,340/im.height)
 im=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.NEAREST)
 x=index*320+(320-im.width)//2
 out.paste(im,(x,390-im.height),im)
 draw.text((index*320+12,12),label,font=font,fill=(240,240,240))
out.save(OUT/'hit-style-comparison.png')
print('Saved hit-style-comparison.png')
