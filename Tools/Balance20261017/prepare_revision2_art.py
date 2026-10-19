"""Previously authorized deterministic canvas/pixel/alpha preparation, no face painting."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,shutil
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/grimhand-v094-design/revision-02'
ART=OUT/'art';RAW=ART/'source';ICONS=OUT/'icons'
for folder in [RAW,ICONS]:folder.mkdir(parents=True,exist_ok=True)
sources=json.loads((Path(__file__).parent/'revision2-art-sources.json').read_text(encoding='utf-8'))
frames={};metrics=[]
def clean(path):
 im=Image.open(path).convert('RGBA');mask=im.getchannel('A').point(lambda v:255 if v>=128 else 0)
 canvas=Image.new('RGBA',im.size);canvas.paste(im,(0,0),mask);canvas.putalpha(mask);return canvas
for record in sources:
 name=record['name'];src=Path(record['path']);shutil.copy2(src,RAW/f'{name}.png');im=clean(src)
 if name=='rage':
  im=im.crop(im.getbbox());im.thumbnail((28,28),Image.Resampling.NEAREST)
  frame=Image.new('RGBA',(32,32));frame.alpha_composite(im,((32-im.width)//2,(32-im.height)//2))
  frame.save(ICONS/'rage_32.png');frame.resize((128,128),Image.Resampling.NEAREST).save(ICONS/'rage.png')
  continue
 # Identical square canvas transform for every source; do not fit individual poses to height.
 assert im.width==im.height, f'Non-square source needs manual review: {name}'
 frame=Image.new('RGBA',(128,128));frame.alpha_composite(im.resize((112,112),Image.Resampling.NEAREST),(8,8))
 frames[name]=frame;frame.resize((1024,1024),Image.Resampling.NEAREST).save(ART/f'{name}.png')
 box=frame.getbbox();assert 0<box[0]<box[2]<128 and 0<box[1]<box[3]<128
 metrics.append({'pose':name,'file':f'art/{name}.png','canvas':[1024,1024],'logicalGrid':[128,128],'bboxLogical':box,'alpha':'binary0/255','status':'候选；未Unity验收'})
idle=frames['idle'];box=idle.getbbox()
profile=idle.crop((max(0,box[0]-2),max(0,box[1]-2),min(128,box[2]-12),min(128,box[1]+65)))
profile.resize((profile.width*8,profile.height*8),Image.Resampling.NEAREST).save(ART/'card_profile.png')
try:font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
except OSError:font=ImageFont.load_default()
board=Image.new('RGB',(768,600),(39,41,46));draw=ImageDraw.Draw(board)
labels={'idle':'待机 idle','attack':'攻击 attack','hit':'受击 hit','defend':'防御 defend','defeat':'倒地 defeat'}
for i,name in enumerate(['idle','attack','hit','defend','defeat']):
 x=(i%3)*256;y=(i//3)*300;draw.text((x+8,y+8),labels[name],font=font,fill='white')
 im=frames[name].resize((256,256),Image.Resampling.NEAREST);board.paste(im,(x,y+34),im)
board.save(OUT/'五姿态对照.png')
icon=Image.open(ICONS/'rage_32.png');p=Image.new('RGB',(480,240),(39,41,46));d=ImageDraw.Draw(p)
for i,size in enumerate([16,24,32]):
 x=20+i*150;d.text((x,8),f'{size}px',font=font,fill='white')
 small=icon.resize((size,size),Image.Resampling.NEAREST);p.paste(small,(x,42),small)
 big=small.resize((size*4,size*4),Image.Resampling.NEAREST);p.paste(big,(x,90),big)
p.save(OUT/'狂怒图标尺寸复核.png')
(OUT/'art-review.json').write_text(json.dumps({'poses':metrics,'styleReview':'已检查小块眼睛、无白牙/痛苦漫画表情，五姿态与原角色对照；尚不等于Unity整体验收。','openIssues':['五图采用相同画布变换，动态脚底对齐尚未完成','细节轮廓和色阶仍需在游戏实际尺寸核对','三张新橙卡的专属状态图标目前只有语义方案，尚未绘制'],'icons':{'rage':'已提供32和128像素候选','duel':'方案','berserker_soul':'方案','burning_standard':'方案'}},ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'prompts.json').write_text(json.dumps({'mode':'内置图像工具；经此前授权使用Python做确定性像素/alpha/画布整理','assets':sources},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'poses':list(frames),'icon':'icons/rage.png','profile':'art/card_profile.png','review':'candidate-not-unity-validated'}))
