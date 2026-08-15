# -*- coding: utf-8 -*-
import pathlib, re, codecs

keys = (
    "battle_will", "final_bulwark", "plague_spread", "revive_blessing",
    "heavy_armor", "DamagePerRespond", "sun_god", "tactical", "rage",
    "soul_devour", "psionic_body", "blood_lineage", "blood_share",
    "immortal", "queen_prevention", "demon_curse", "soul_reinforce",
    "battle_roar", "respond_count", "hp_loss", "god_wrath", "holy_infusion",
    "charge", "gather", "lament", "despair", "sigh", "corrupted_form",
    "blood_carnival", "dark_tear", "malicious", "preempt", "shield_bash",
    "retreat", "respond_stance", "armor_boost", "sand_arrow", "sand_shield",
    "rot_touch", "holy_infuse", "decay_form", "blood_hand", "blood_bite",
    "dark_fog", "life_siphon", "blood_ritual", "scale", "snake_tail",
    "poison_scale", "venom_spit", "shed_skin", "snake_sense", "poison_sac",
    "python_crush", "snake_god", "energy_store", "psionic_cannon",
    "soul_consume", "spirit_burst", "spirit_gather", "summon_card_spirit",
    "super_cannon", "blood_heir", "split_blood", "heavy", "final_blood",
    "guardian", "defensive_stance", "decree", "undead_curse", "vamp_aura",
    "curse_chain", "soul_song", "zero_cost"
)

base = pathlib.Path(r"Assets/_Project/Data/Cards")
interesting = []
for p in sorted(base.glob("Card_*.asset")):
    t = p.read_text(encoding="utf-8")
    name_m = re.search(r'DisplayName:\s*"([^"]+)"', t)
    if not name_m:
        continue
    dn = codecs.decode(name_m.group(1), "unicode_escape")
    # dump all upgrade-related cards from excel missing list + specials
    interesting.append((dn, p.name, t))

# print specific names
want = {
    "防御架势","誓死守护","战意觉醒","重甲强化","战术大师的终结技","怒火焚身","最终壁垒",
    "法老权令","瘟疫蔓延","亡灵诅咒","复活祝福","神圣灌注","太阳神之怒","腐朽化身",
    "恶魔诅咒","掠血之手","黑暗之雾","吸血光环","黑暗撕裂","诅咒之链","鲜血狂欢",
    "血族传承","分血仪式","最终鲜血仪式","女王的预防","不朽蛇蜕","蛇神的回应",
    "蓄能","灵魂吞噬","灵能体","灵魂强化","灵魂挽歌","召唤卡牌之灵","叹息之墙",
    "战斗咆哮","重整旗鼓","生命之泉"
}
for dn, pname, t in interesting:
    if dn not in want:
        continue
    types = re.findall(r"^\s+Type: (\d+)", t, re.M)
    vals = re.findall(r"^\s+Value: (-?\d+)", t, re.M)
    sids = re.findall(r"^\s+StatusId: (.*)$", t, re.M)
    stacks = re.findall(r"^\s+Stacks: (-?\d+)", t, re.M)
    hploss = re.findall(r"HpLossStep(?:Percent|Value): (-?\d+)", t)
    print(f"{dn}|{pname}|Type={types}|Val={vals}|Status={sids}|Stacks={stacks}|HpLoss={hploss}")
