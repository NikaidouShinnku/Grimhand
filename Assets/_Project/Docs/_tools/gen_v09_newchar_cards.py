# -*- coding: utf-8 -*-
"""Generate v0.9 毒蛇女王/巫妖女王 card .asset + .meta files.
Run from repo root.  Outputs to Assets/_Project/Data/Cards/.
"""
import os

OUT_DIR = r"c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand\Assets\_Project\Data\Cards"
SCRIPT_GUID = "d1e8e1f9605a6fb4fa4e4fa5586de5ee"  # CardDefinitionSO mono guid

# guid allocation (matches character asset deck refs)
GUIDS = [
 "2c20d40c5f2a481f8d4bcdfc3642eff7", # v_snake_bite
 "9aee953524fc4246b60316f3d43fd18b", # v_python_guard
 "a653f2ff099e4fe4aebeb4229dd40f24", # v_scale_harden
 "e17b6b8de564417f9c5c62aee343daae", # v_poison_touch
 "22ce8c3baf414d56a596fae17084c856", # v_queen_authority
 "d8e8de7da4074b2bb85a118e06e47904", # v_tail_strike
 "f21d59dafe334b4fab1afe7ebb8513f3", # v_poison_scale
 "36a865faa92a4755a7359abe72a10acd", # v_snake_king_blessing
 "da261a879b9c4dcda2ef1dfecb9fb3a2", # v_venom_spit
 "444aa827afe84de2bb05e5dd1e3e48e2", # v_detonate_venom
 "24fbdb6a619046448bd42928d44fa1c8", # v_digest_venom
 "c8b1f18002bc4297a8e700ad7a8bcae1", # v_shed_skin
 "b2bfed5088b04a6bbb1412bdd877dd67", # v_tongue_sense
 "7c207512786d43f88693f6809694c73f", # v_queen_prevention
 "223ed836f9614d829faae980b56fed04", # v_venom_sac_burst
 "ad356128b84f4dfcb3a9972d407db034", # v_venom_feast
 "832fcfe9b67e4c4ab4185bda16e542ab", # v_python_constrict
 "81d3a1ea91da4efe81cf4755f0728a55", # v_immortal_shed
 "23f88cd6938f443ba22c3aa64cda5d10", # v_poison_feedback
 "8e73daf020264edeac3848c141fd9dd0", # v_all_snakes_heart
 "56d3acd31c384f2292e5b0622ab3967d", # v_pray_ancient_god
 "41603381a7844e11a083e3d579be6265", # v_snake_god_response
 "ac1219cd58df4a84881579dbb8c87064", # l_ethereal_form
 "87966bc5c5b44f58b20d738740d01e53", # l_void_gaze
 "b2716ad59c6d4157b7ba8714e4f73536", # l_charge
 "467e729a2e8b472e974ce46cc245b4f0", # l_ghost_claw
 "b2314b0ec4444e6bb6467510222f796e", # l_gather_energy
 "e183b6f54ffe483fa8e2d90e20bd8647", # l_spirit_walk
 "cb9967efec4f46dab141f3f863089987", # l_psionic_cannon
 "a841add85ecf43828aaeed2ab074478b", # l_dread_whisper
 "88a580482ba34a3d93a92976ad5251a7", # l_soul_storm
 "30b3fe1a2d154c7db20edd1b533e7540", # l_two_realms_walker
 "535ee04919794de3a17168d923a4193d", # l_soul_devour
 "18335d0419fe4c699a678a44526e14d8", # l_psionic_body
 "312cef4f95994e74a7a3c5eb418f8365", # l_soul_reinforce
 "fba282208c1d4174a3de3a0ad43389fe", # l_realm_burst
 "3dc5f61563f44d3db4945b0c6d954d09", # l_psionic_focus
 "7429c756aea140b188c66100e24723ce", # l_realm_seal
 "023060ef0c0d4422b3d5dca974e348be", # l_soul_elegy
 "f88a4b0bb679414ea58981a7daf8c1a6", # l_summon_card_spirit
 "22a627f51caf40d784e062ca00c0d205", # l_summon_chaos_spirit
 "6708d36b3e964dd69a2cb6af101e4d68", # l_wall_of_sighs
 "913cd41408db47f493482de89d7c82bc", # l_despair_soul
 "9961bd01c5dc4e07ab86bb69189b3e22", # l_realm_descent
 "9fee3417884348dfbc5c22bd52464e82", # l_super_psionic_cannon
 "d60a7fb35175449c8c1a9431551bba86", # l_eternal_void
]

SNAKE = "char_snake_queen"
LICH = "char_lich_queen"

# CardType: Attack=0 Defense=1 Status=2
# CardRarity: Common=0 Rare=1 SuperRare=2 Epic=3 Legendary=4
# EffectActionType ints (see EffectEnums.cs)
T_DealDamage=0; T_GainBlock=1; T_Heal=2; T_ApplyStatus=3; T_RemoveStatus=4
T_DrawCards=7; T_GainBlockFromLastDamagePercent=9; T_DrawCardsIfEthereal=39
T_GainEnergy=24; T_DrawToHandLimit=25; T_GainBlockBonusIfSelfPoisoned=26
T_ApplyPoisonBySpeedCompare=27; T_RemovePoisonHealPerStack=28
T_TransferHalfPoisonToRandomEnemy=29; T_ApplyConstrict=30
T_SettlePoisonAndClear=31; T_ApplyDelayedDamage=32
T_EtherealCountBonusDamage=33; T_AddTokenCardToHand=34
T_ShuffleHandCosts=35; T_RandomSnakeGodEffect=36; T_SealNextEnemyCard=37
T_LockSelfCards=38; T_BuffAllOtherAllies=40; T_RevealEnemyIntent=41
T_ApplyStatusNextTurn=49
T_GainEnergyNextTurn=50
# EffectTarget ints
TG_DefaultEnemy=0; TG_Self=1; TG_FrontAlly=2; TG_BackAlly=3; TG_LastActionActor=4
TG_AllEnemies=12; TG_RandomEnemy=13; TG_RandomAlly=15
# ReactionConditionType
C_None=0; C_AttackOnSelf=1; C_DefenseOnTarget=2; C_StatusOnTarget=3
# TargetReach
R_FrontAndMiddle=1; R_Any=0

def A(**kw):
    """Build an action dict with defaults."""
    d = dict(Type=0, Target=0, Value=0, StatusId="", Stacks=1, Duration=-1,
             ScaleWithAttack=0, ScaleWithDefense=0, AttackScalePercent=100,
             DefenseScalePercent=100, Condition=0, Reach=1, SplashBehindTarget=0,
             SplashPowerPercent=100, BackRowPowerPercent=100, IgnoreDefPercent=0,
             BonusIfTargetHpBelowPercent=0, BonusIfTargetHpBelowFlat=0,
             BonusIfTargetHitThisTurnPercent=0, BonusIfTargetHasStatusId="",
             BonusIfTargetHasStatusFlat=0, LifestealPercent=0, HealMaxHpPercent=0,
             OnKillHealAmount=0, HitCount=1, AlternateAttackScalePercent=0,
             AlternateValue=0, UseAlternateIfTargetHasDebuff=0,
             AlternateAttackScaleIfActorUsedAttack=0, AlternateValueIfActorUsedAttack=0,
             DamageMultiplierPercentIfRespondArmed=100, SelfDamageFlat=0,
             RepeatPerEnemyAttackCardThisTurn=0, FallbackBlockDefenseScalePercent=100,
             FallbackBlockValue=0, SummonCharacterId="", GrantInvulnerableOnRespondArm=0,
             LifestealUnblockedOnly=0, HpLossStepPercent=0, HpLossStepValue=0,
             AlternateValueIfHealed=0, TokenCardId="", CostReduction=0)
    d.update(kw)
    return d

# field order for YAML emission
FIELDS = ["Type","Target","Value","StatusId","Stacks","Duration","ScaleWithAttack",
 "ScaleWithDefense","AttackScalePercent","DefenseScalePercent","Condition","Reach",
 "SplashBehindTarget","SplashPowerPercent","BackRowPowerPercent","IgnoreDefPercent",
 "BonusIfTargetHpBelowPercent","BonusIfTargetHpBelowFlat","BonusIfTargetHitThisTurnPercent",
 "BonusIfTargetHasStatusId","BonusIfTargetHasStatusFlat","LifestealPercent",
 "HealMaxHpPercent","OnKillHealAmount","HitCount","AlternateAttackScalePercent",
 "AlternateValue","UseAlternateIfTargetHasDebuff","AlternateAttackScaleIfActorUsedAttack",
 "AlternateValueIfActorUsedAttack","DamageMultiplierPercentIfRespondArmed",
 "SelfDamageFlat","RepeatPerEnemyAttackCardThisTurn","FallbackBlockDefenseScalePercent",
 "FallbackBlockValue","SummonCharacterId","GrantInvulnerableOnRespondArm",
 "LifestealUnblockedOnly","HpLossStepPercent","HpLossStepValue","AlternateValueIfHealed",
 "TokenCardId","CostReduction"]

# (id, name, owner, cost, cardType, rarity, keywords, [actions])
CARDS = [
 ("v_snake_bite","蛇牙撕咬",SNAKE,1,0,0,[],[A(Type=T_DealDamage,Target=TG_DefaultEnemy,Value=5,Reach=R_FrontAndMiddle),A(Type=T_ApplyStatus,Target=TG_DefaultEnemy,StatusId="poison",Stacks=2,Duration=3)]),
 ("v_python_guard","蟒蛇守护",SNAKE,1,1,0,[],[A(Type=T_GainBlock,Target=TG_Self,Value=10),A(Type=T_ApplyStatus,Target=TG_Self,StatusId="poison",Stacks=1,Duration=1)]),
 ("v_scale_harden","鳞片硬化",SNAKE,1,1,0,[],[A(Type=T_GainBlockBonusIfSelfPoisoned,Target=TG_Self,Value=8,Stacks=6)]),
 ("v_poison_touch","剧毒之触",SNAKE,1,0,0,[],[A(Type=T_ApplyPoisonBySpeedCompare,Target=TG_DefaultEnemy,Value=2,Stacks=5,Duration=2,Reach=R_FrontAndMiddle)]),
 ("v_queen_authority","女王威信",SNAKE,1,2,0,[],[A(Type=T_DrawCards,Target=TG_Self,Value=1)]),
 ("v_tail_strike","蛇尾突袭",SNAKE,1,0,0,["usable_in_constrict"],[A(Type=T_DealDamage,Target=TG_DefaultEnemy,Value=7,Reach=R_FrontAndMiddle)]),
 ("v_poison_scale","毒鳞",SNAKE,1,1,1,["respond_attack"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="damage_reduction",Stacks=50,Duration=1,Condition=C_AttackOnSelf),A(Type=T_ApplyStatus,Target=TG_LastActionActor,StatusId="poison",Stacks=3,Duration=-1,Condition=C_AttackOnSelf)]),
 ("v_snake_king_blessing","蛇王护佑",SNAKE,1,2,1,[],[A(Type=T_ApplyStatus,Target=TG_FrontAlly,StatusId="ethereal",Stacks=1,Duration=1)]),
 ("v_venom_spit","毒液喷吐",SNAKE,2,0,1,[],[A(Type=T_DealDamage,Target=TG_DefaultEnemy,Value=4,Reach=R_FrontAndMiddle),A(Type=T_ApplyStatus,Target=TG_DefaultEnemy,StatusId="poison",Stacks=4,Duration=3)]),
 ("v_detonate_venom","引爆毒囊",SNAKE,1,0,1,[],[A(Type=T_SettlePoisonAndClear,Target=TG_DefaultEnemy)]),
 ("v_digest_venom","消化剧毒",SNAKE,0,2,1,["quick_start"],[A(Type=T_GainEnergy,Target=TG_Self,Value=2),A(Type=T_ApplyStatus,Target=TG_Self,StatusId="poison",Stacks=8,Duration=2)]),
 ("v_shed_skin","蜕皮",SNAKE,1,2,1,[],[A(Type=T_RemovePoisonHealPerStack,Target=TG_Self,Value=3)]),
 ("v_tongue_sense","蛇信感知",SNAKE,1,2,1,["respond_status"],[A(Type=T_ApplyStatus,Target=TG_LastActionActor,StatusId="poison",Stacks=3,Duration=2,Condition=C_StatusOnTarget)]),
 ("v_queen_prevention","女王的预防",SNAKE,1,1,1,["respond_attack"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="damage_reduction",Stacks=60,Duration=1,Condition=C_AttackOnSelf)]),
 ("v_venom_sac_burst","毒囊破裂",SNAKE,2,2,2,["exhaust"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="venom_sac_burst",Stacks=1,Duration=-1)]),
 ("v_venom_feast","毒裂盛宴",SNAKE,2,0,2,[],[A(Type=T_SettlePoisonAndClear,Target=TG_AllEnemies)]),
 ("v_python_constrict","巨蟒绞杀",SNAKE,2,0,2,[],[A(Type=T_ApplyConstrict,Target=TG_DefaultEnemy,Value=35,Duration=2,Reach=R_FrontAndMiddle)]),
 ("v_immortal_shed","不朽蛇蜕",SNAKE,2,2,2,["exhaust"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="immortal_shed",Stacks=1,Duration=-1)]),
 ("v_poison_feedback","剧毒反哺",SNAKE,1,2,2,[],[A(Type=T_TransferHalfPoisonToRandomEnemy,Target=TG_Self)]),
 ("v_all_snakes_heart","万蛇噬心",SNAKE,3,0,3,[],[A(Type=T_ApplyConstrict,Target=TG_AllEnemies,Value=30,Duration=2)]),
 ("v_pray_ancient_god","祈求远古蛇神",SNAKE,3,2,3,["exhaust","quick_start"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="pray_ancient_snake_god",Stacks=1,Duration=-1),A(Type=T_ApplyStatus,Target=TG_Self,StatusId="vulnerable",Stacks=50,Duration=2),A(Type=T_LockSelfCards,Target=TG_Self,Value=2)]),
 ("v_snake_god_response","蛇神的回应",SNAKE,0,2,4,["exhaust","token"],[A(Type=T_RandomSnakeGodEffect,Target=TG_AllEnemies,Value=25,Stacks=10,AlternateValue=75)]),
 # ---- 巫妖女王 ----
 ("l_ethereal_form","虚化形态",LICH,1,2,0,[],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="ethereal",Stacks=1,Duration=1),A(Type=T_LockSelfCards,Target=TG_Self,Value=1)]),
 ("l_void_gaze","空洞凝视",LICH,1,2,0,[],[A(Type=T_DrawCardsIfEthereal,Target=TG_Self,Value=1,AlternateValue=2)]),
 ("l_charge","蓄能",LICH,1,2,0,[],[A(Type=T_ApplyStatusNextTurn,Target=TG_Self,StatusId="attack_up_pct",Stacks=20,Duration=3)]),
 ("l_ghost_claw","幽灵爪击",LICH,1,0,0,[],[A(Type=T_DealDamage,Target=TG_DefaultEnemy,Value=7,Reach=R_FrontAndMiddle)]),
 ("l_gather_energy","聚能",LICH,0,2,0,["quick_start","sacrifice"],[A(Type=T_DealDamage,Target=TG_Self,Value=10),A(Type=T_GainEnergy,Target=TG_Self,Value=2)]),
 ("l_spirit_walk","灵体漫步",LICH,2,1,1,[],[A(Type=T_DealDamage,Target=TG_Self,Value=8),A(Type=T_ApplyStatus,Target=TG_Self,StatusId="ethereal",Stacks=1,Duration=1)]),
 ("l_psionic_cannon","灵能炮",LICH,2,0,1,[],[A(Type=T_ApplyDelayedDamage,Target=TG_DefaultEnemy,Value=13)]),
 ("l_dread_whisper","恐惧低语",LICH,1,2,1,[],[A(Type=T_RevealEnemyIntent,Target=TG_Self)]),
 ("l_soul_storm","灵魂风暴",LICH,2,0,1,[],[A(Type=T_ApplyDelayedDamage,Target=TG_AllEnemies,Value=10)]),
 ("l_two_realms_walker","两界行者",LICH,2,2,2,["exhaust"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="ethereal_on_next_hit",Stacks=1,Duration=-1)]),
 ("l_soul_devour","灵魂吞噬",LICH,1,2,2,[],[A(Type=T_DealDamage,Target=TG_FrontAlly,Value=10,Reach=R_Any),A(Type=T_GainEnergyNextTurn,Target=TG_Self,Value=3)]),
 ("l_psionic_body","灵能体",LICH,2,2,2,["exhaust"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="psionic_body",Stacks=1,Duration=-1)]),
 ("l_soul_reinforce","灵魂强化",LICH,1,2,2,["sacrifice"],[A(Type=T_DealDamage,Target=TG_Self,Value=10),A(Type=T_BuffAllOtherAllies,Target=TG_Self,StatusId="attack_up_pct",Stacks=25,Duration=2)]),
 ("l_realm_burst","灵界爆发",LICH,2,0,2,[],[A(Type=T_DealDamage,Target=TG_DefaultEnemy,Value=15,Reach=R_Any),A(Type=T_RemoveStatus,Target=TG_Self,StatusId="ethereal",Stacks=1)]),
 ("l_psionic_focus","灵能聚集",LICH,1,0,2,[],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="attack_up_pct",Stacks=20,Duration=2),A(Type=T_ApplyDelayedDamage,Target=TG_DefaultEnemy,Value=12,Reach=R_FrontAndMiddle)]),
 ("l_realm_seal","灵界封印",LICH,4,2,3,["exhaust"],[A(Type=T_SealNextEnemyCard,Target=TG_Self)]),
 ("l_soul_elegy","灵魂挽歌",LICH,2,0,3,[],[A(Type=T_EtherealCountBonusDamage,Target=TG_AllEnemies,Value=8,Stacks=3)]),
 ("l_summon_card_spirit","召唤卡牌之灵",LICH,1,2,3,[],[A(Type=T_DrawCards,Target=TG_Self,Value=2)]),
 ("l_summon_chaos_spirit","召唤混乱之灵",LICH,1,2,3,["exhaust","quick_start"],[A(Type=T_DrawToHandLimit,Target=TG_Self),A(Type=T_ShuffleHandCosts,Target=TG_Self)]),
 ("l_wall_of_sighs","叹息之墙",LICH,2,1,3,["parry"],[A(Type=T_GainBlockFromLastDamagePercent,Target=TG_Self,Value=80,Condition=C_AttackOnSelf),A(Type=T_ApplyStatus,Target=TG_RandomAlly,StatusId="ethereal",Stacks=1,Duration=1,Condition=C_AttackOnSelf)]),
 ("l_despair_soul","绝望之魂",LICH,0,0,3,[],[A(Type=T_DealDamage,Target=TG_DefaultEnemy,Value=10,Reach=R_Any),A(Type=T_ApplyStatus,Target=TG_Self,StatusId="despair_soul_recall",Stacks=1,Duration=-1)]),
 ("l_realm_descent","灵界降临",LICH,8,2,4,["exhaust","quick_start"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="hand_cost_zero",Stacks=1,Duration=1)]),
 ("l_super_psionic_cannon","超级·无敌·灵能·巨炮",LICH,10,0,4,["inherit"],[A(Type=T_DealDamage,Target=TG_DefaultEnemy,Value=100,Reach=R_Any)]),
 ("l_eternal_void","永恒虚无",LICH,2,2,4,["exhaust"],[A(Type=T_ApplyStatus,Target=TG_Self,StatusId="ethereal",Stacks=1,Duration=-1),A(Type=T_ApplyStatus,Target=TG_Self,StatusId="eternal_void",Stacks=1,Duration=-1)]),
]

def esc(s):
    out=[]
    for c in s:
        o=ord(c)
        if o>127:
            out.append(f"\\u{o:04X}")
        else:
            out.append(c)
    return "".join(out)

def fmt_val(v):
    if isinstance(v,str):
        return f'"{v}"' if v else '""'
    if isinstance(v,bool):
        return "1" if v else "0"
    return str(v)

def emit_action(a):
    lines=["  - "+f"{FIELDS[0]}: {fmt_val(a[FIELDS[0]])}"]
    for f in FIELDS[1:]:
        lines.append(f"    {f}: {fmt_val(a[f])}")
    return "\n".join(lines)

def emit_asset(card, guid):
    cid,name,owner,cost,ctype,rarity,keywords,actions=card
    asset_name="Card_"+cid
    L=[]
    L.append("%YAML 1.1")
    L.append("%TAG !u! tag:unity3d.com,2011:")
    L.append("--- !u!114 &11400000")
    L.append("MonoBehaviour:")
    L.append("  m_ObjectHideFlags: 0")
    L.append("  m_CorrespondingSourceObject: {fileID: 0}")
    L.append("  m_PrefabInstance: {fileID: 0}")
    L.append("  m_PrefabAsset: {fileID: 0}")
    L.append("  m_GameObject: {fileID: 0}")
    L.append("  m_Enabled: 1")
    L.append("  m_EditorHideFlags: 0")
    L.append(f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}")
    L.append(f"  m_Name: {asset_name}")
    L.append("  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CardDefinitionSO")
    L.append(f"  CardId: {cid}")
    L.append(f'  DisplayName: "{esc(name)}"')
    L.append(f"  OwnerCharacterId: {owner}")
    L.append(f"  Cost: {cost}")
    L.append(f"  CardType: {ctype}")
    L.append(f"  Rarity: {rarity}")
    if keywords:
        L.append("  Keywords:")
        for k in keywords:
            L.append(f"  - {k}")
    else:
        L.append("  Keywords: []")
    if actions:
        L.append("  Actions:")
        for a in actions:
            L.append(emit_action(a))
    else:
        L.append("  Actions: []")
    L.append("  CardArt: {fileID: 0}")
    L.append("  CardFrame: {fileID: 0}")
    L.append("  CardIcon: {fileID: 0}")
    return "\n".join(L)+"\n"

def emit_meta(guid):
    return (f"fileFormatVersion: 2\n guid: {guid}\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for card,guid in zip(CARDS, GUIDS):
        cid=card[0]
        asset_name="Card_"+cid
        ap=os.path.join(OUT_DIR, asset_name+".asset")
        mp=os.path.join(OUT_DIR, asset_name+".asset.meta")
        with open(ap,"w",encoding="utf-8",newline="\n") as f:
            f.write(emit_asset(card,guid))
        with open(mp,"w",encoding="utf-8",newline="\n") as f:
            f.write(emit_meta(guid))
    print(f"Generated {len(CARDS)} cards into {OUT_DIR}")

if __name__=="__main__":
    main()
