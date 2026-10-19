"""User-authorized deterministic pixel/alpha/canvas normalization; no AI redraw."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, shutil, hashlib

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/grimhand-v094-design'
ART=OUT/'art'
RAW=ART/'source'
PIX=ART/'pixel'
PNG=ART/'unity-png'
PREVIEW=ART/'preview'
for folder in [RAW,PIX,PNG,PREVIEW]: folder.mkdir(parents=True,exist_ok=True)
sources=json.loads((Path(__file__).parent/'art-sources.json').read_text(encoding='utf-8'))
records={r['name']:Path(r['path']) for r in sources}
records['idle']=ART/'berserker_idle_v3.png'
icon=Path('C:/Users/Kelthuzad/.codex/generated_images/01a02f0a-ff52-7273-b893-166c6208381b/exec-02dcf287-3767-46f0-b6b9-6afe09728db6.png')
CANVAS=(224,192)
ANCHOR=(112,168)
# Head height, face center X and foot ground Y, measured on source images.
# Defeat uses the same physical head scale; never shrink the whole body to fake kneeling.
landmarks={
 'idle':(300,620,1235), 'attack_windup':(232,870,990),
 'attack_contact':(240,860,940), 'attack_followthrough':(230,920,940),
 'defense':(230,925,990), 'hit':(235,755,970), 'death':(235,865,935)
}
manifest=[]
frames={}
labels={'idle':'待机','attack_windup':'攻击蓄力','attack_contact':'下劈命中','attack_followthrough':'攻击收势','defense':'架挡防御','hit':'受击','death':'战败跪倒'}
def hard_alpha(image):
 im=image.convert('RGBA')
 a=im.getchannel('A').point(lambda x:255 if x>=128 else 0)
 im.putalpha(a)
 # Remove hidden RGB as well, so diagnostic viewers cannot display fake halos.
 result=Image.new('RGBA',im.size,(0,0,0,0));result.paste(im,(0,0),a)
 return result
for name,src in records.items():
 shutil.copy2(src,RAW/f'{name}.png')
 if name not in landmarks: continue # generated profile/idle variant failed pose/continuity review
 head,x,foot=landmarks[name]
 image=hard_alpha(Image.open(src))
 scale=32/head
 target=image.resize((round(image.width*scale),round(image.height*scale)),Image.Resampling.NEAREST)
 xoff=round(ANCHOR[0]-x*scale);yoff=round(ANCHOR[1]-foot*scale)
 frame=Image.new('RGBA',CANVAS,(0,0,0,0));frame.alpha_composite(target,(xoff,yoff))
 # Exact body dimensions are preserved across frames by measured head scale, not whole-image fit.
 frame.save(PIX/f'berserker_{name}.png')
 frame.resize((CANVAS[0]*8,CANVAS[1]*8),Image.Resampling.NEAREST).save(PNG/f'berserker_{name}.png')
 frames[name]=frame
 manifest.append({'kind':labels[name],'file':f'art/unity-png/berserker_{name}.png','status':'已统一像素网格和画布；待Unity验收','review':'同角色头部约32逻辑像素；同一脚底基点；非完整平滑逐帧动画'})

# Make a genuine localized breathing keyframe; do not translate or scale the whole sprite.
idle=frames['idle']
breath=idle.copy()
roi=(94,96,130,123)
patch=idle.crop(roi)
breath.paste((0,0,0,0),roi)
breath.alpha_composite(patch.resize((36,28),Image.Resampling.NEAREST),(94,95))
for name,im in [('idle_00',idle),('idle_01',breath)]:
 im.save(PIX/f'berserker_{name}.png')
 im.resize((CANVAS[0]*8,CANVAS[1]*8),Image.Resampling.NEAREST).save(PNG/f'berserker_{name}.png')

# Card profile is a deterministic crop from the exact accepted idle, not a different face.
profile=idle.crop((78,36,149,127))
profile.save(PIX/'berserker_card_profile.png')
profile.resize((568,728),Image.Resampling.NEAREST).save(PNG/'berserker_card_profile.png')
manifest.append({'kind':'CardProfile','file':'art/unity-png/berserker_card_profile.png','status':'从同一Idle裁出','review':'无重新生成、无换脸；需核对卡面椭圆裁切'})

rage=hard_alpha(Image.open(icon));shutil.copy2(icon,RAW/'rage_icon.png')
rage=rage.crop(rage.getbbox()).resize((128,128),Image.Resampling.NEAREST)
rage.save(PNG/'berserker_rage_icon.png')
manifest.append({'kind':'狂怒图标','file':'art/unity-png/berserker_rage_icon.png','status':'128×128候选','review':'状态0—4数字由UI绘制，不烘焙进图标'})

def palette(im):
 rgb=im.convert('RGB').quantize(colors=255,method=Image.Quantize.FASTOCTREE)
 alpha=im.getchannel('A')
 p=Image.new('P',im.size);p.putpalette([0,0,0]+rgb.getpalette()[:765])
 p.putdata([0 if a==0 else c+1 for c,a in zip(rgb.get_flattened_data(),alpha.get_flattened_data())])
 p.info['transparency']=0
 return p
def gif(name,ims,durations):
 converted=[palette(im.resize((im.width*3,im.height*3),Image.Resampling.NEAREST)) for im in ims]
 converted[0].save(PREVIEW/name,save_all=True,append_images=converted[1:],duration=durations,loop=0,transparency=0,disposal=2,optimize=False)
gif('berserker_attack.gif',[idle,frames['attack_windup'],frames['attack_contact'],frames['attack_followthrough'],idle],[500,180,120,220,500])
gif('berserker_idle.gif',[idle,breath],[900,900])
manifest.append({'kind':'待机双关键帧','file':'art/preview/berserker_idle.gif','status':'局部胸廓呼吸原型，非全身微移','review':'只有2张不同图；GIF仅预览。游戏的0.13秒帧率需配置重复停留'})
manifest.append({'kind':'攻击三关键帧','file':'art/preview/berserker_attack.gif','status':'动作节奏预览；未接入运行时','review':'蓄力、下劈、收势；必须将伤害事件绑定命中帧'})

contact=Image.new('RGBA',(CANVAS[0]*4,CANVAS[1]*2),(27,30,36,255))
draw=ImageDraw.Draw(contact)
ordered=['idle','attack_windup','attack_contact','attack_followthrough','defense','hit','death']
for i,name in enumerate(ordered):
 x=(i%4)*CANVAS[0];y=(i//4)*CANVAS[1]
 contact.alpha_composite(frames[name],(x,y));draw.text((x+4,y+4),name,fill='white')
contact.resize((CANVAS[0]*8,CANVAS[1]*4),Image.Resampling.NEAREST).save(PREVIEW/'pose-contact-sheet.png')
metrics=[]
for path in sorted(PNG.glob('*.png')):
 image=Image.open(path);alpha=image.getchannel('A');bbox=alpha.getbbox()
 metrics.append({'file':str(path.relative_to(OUT)).replace('\\','/'),'width':image.width,'height':image.height,'alphaExtrema':alpha.getextrema(),'bbox':bbox,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
assert all(m['alphaExtrema']==(0,255) for m in metrics)
assert all(Image.open(PNG/f'berserker_{n}.png').size==(1792,1536) for n in landmarks)
assert all(m['bbox'][0]>0 and m['bbox'][1]>0 and m['bbox'][2]<m['width'] and m['bbox'][3]<m['height'] for m in metrics if 'card_profile' not in m['file'] and 'rage_icon' not in m['file'])
(OUT/'art-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'art-metrics.json').write_text(json.dumps(metrics,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'unity-binding-proposal.json').write_text(json.dumps({'CharacterId':'char_berserker','PixelsPerUnit':100,'MipMap':False,'Filter':'Point候选，实际需和旧角色并排核对','CanvasLogical':CANVAS,'FrameScale':8,'FootBaselineLogicalY':168,'IdlePortrait':'berserker_idle.png','AttackPortrait':'berserker_attack_contact.png','DefensePortrait':'berserker_defense.png','HitPortrait':'berserker_hit.png','DeathPortrait':'berserker_death.png','CardProfilePortrait':'berserker_card_profile.png','IdleAnimationFrames':['berserker_idle_00.png']*7+['berserker_idle_01.png']*7+['berserker_idle_00.png']*2,'PreserveOriginalFacing':True,'RuntimeIntegration':'未实施；不得直接假设多关键帧已自动接入。宽画布需要核对显示大小与缩放。'},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'pngCount':len(metrics),'canvas':CANVAS,'alpha':'binary0/255','manifestItems':len(manifest)},ensure_ascii=False))
