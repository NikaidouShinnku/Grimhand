# -*- coding: utf-8 -*-
"""Generate v0.91 red-highlighted card .asset + .meta files."""
import os
import uuid

OUT_DIR = r"c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand\Assets\_Project\Data\Cards"
SCRIPT_GUID = "d1e8e1f9605a6fb4fa4e4fa5586de5ee"

# CardType: Attack=0 Defense=1 Status=2
# Rarity: Common=0 Rare=1 SuperRare=2 Epic=3 Legendary=4
# EffectActionType RevealEnemyIntent=41
# EffectTarget Self=1

CARDS = [
    ("w_thorn_armor", "荆棘护甲", "char_knight", 1, 1, 1, [], []),
    ("w_retaliatory_strike", "报复打击", "char_knight", 2, 0, 2, [], []),
    ("w_battle_roar", "战斗咆哮", "char_knight", 2, 2, 2, ["exhaust"], []),
    ("w_fearless_charge", "无畏冲锋", "char_knight", 3, 0, 3, [], []),
    ("w_regroup", "重整旗鼓", "char_knight", 1, 2, 3, ["quick_start", "exhaust"], []),
    ("p_sand_prophecy", "沙之预言", "char_mage", 0, 2, 0, ["quick_start"],
     [(41, 1, 1, "", 1, -1)]),
    ("p_soul_bond", "灵魂纽带", "char_mage", 1, 2, 1, ["quick_start"],
     [(3, 2, 0, "soul_bond", 50, 1)]),
    ("p_doom_prophecy", "末日预言", "char_mage", 2, 2, 3, [], []),
    ("p_sand_foresight", "沙之预知", "char_mage", 1, 2, 3, ["quick_start"], []),
    ("p_life_spring", "生命之泉", "char_mage", 4, 2, 4, ["exhaust"], []),
    ("d_pain_convert", "苦痛转化", "char_ranger", 1, 1, 1, [], []),
    ("d_blood_thirst", "血之渴望", "char_ranger", 0, 2, 3, ["quick_start", "exhaust", "sacrifice"], []),
    ("d_demon_echo", "魔神回响", "char_ranger", 6, 0, 4, ["inherit"], []),
    ("v_keen_snake_eye", "锐利蛇眼", "char_snake_queen", 0, 2, 0, ["quick_start"],
     [(41, 1, 1, "", 1, -1)]),
    ("v_poison_mist", "毒雾弥漫", "char_snake_queen", 2, 2, 1, [], []),
    ("v_snake_nest", "千蛇窟", "char_snake_queen", 4, 2, 3, ["exhaust"], []),
    ("v_queen_kiss", "女王之吻", "char_snake_queen", 3, 2, 4, ["exhaust"], []),
    ("l_ethereal_shield", "灵质护盾", "char_lich_queen", 1, 1, 0, [], []),
    ("l_psionic_scry", "灵能预知", "char_lich_queen", 1, 2, 1, ["quick_start"], []),
    ("l_psionic_arrow_rain", "灵能箭雨", "char_lich_queen", 2, 0, 1, [], []),
    ("l_memory_eternal_void", "记忆苏醒·永恒虚无", "char_lich_queen", 5, 2, 4, ["exhaust"], []),
    ("l_memory_psionic_mastery", "记忆苏醒·灵能掌握", "char_lich_queen", 4, 2, 4, ["exhaust"], []),
    ("l_memory_time_distortion", "记忆苏醒·时空紊乱", "char_lich_queen", 2, 2, 4, ["exhaust"], []),
]

ACTION_TEMPLATE = """  - Type: {type}
    Target: {target}
    Value: {value}
    StatusId: {status}
    Stacks: {stacks}
    Duration: {duration}
    ScaleWithAttack: 0
    ScaleWithDefense: 0
    AttackScalePercent: 100
    DefenseScalePercent: 100
    Condition: 0
    Reach: 1
    SplashBehindTarget: 0
    SplashPowerPercent: 100
    BackRowPowerPercent: 100
    IgnoreDefPercent: 0
    BonusIfTargetHpBelowPercent: 0
    BonusIfTargetHpBelowFlat: 0
    BonusIfTargetHitThisTurnPercent: 0
    BonusIfTargetHasStatusId: 
    BonusIfTargetHasStatusFlat: 0
    BonusIfActorFasterThanAllEnemiesFlat: 0
    LifestealPercent: 0
    HealMaxHpPercent: 0
    OnKillHealAmount: 0
    HitCount: 1
    AlternateAttackScalePercent: 0
    AlternateValue: 0
    UseAlternateIfTargetHasDebuff: 0
    AlternateAttackScaleIfActorUsedAttack: 0
    AlternateValueIfActorUsedAttack: 0
    DamageMultiplierPercentIfRespondArmed: 100
    SelfDamageFlat: 0
    RepeatPerEnemyAttackCardThisTurn: 0
    FallbackBlockDefenseScalePercent: 100
    FallbackBlockValue: 0
    SummonCharacterId: 
    GrantInvulnerableOnRespondArm: 0
    LifestealUnblockedOnly: 0
    HpLossStepPercent: 0
    HpLossStepValue: 0
    AlternateValueIfHealed: 0
    TokenCardId: 
    CostReduction: 0
"""

ASSET_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: SCRIPT_GUID_PLACEHOLDER, type: 3}}
  m_Name: Card_{card_id}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CardDefinitionSO
  CardId: {card_id}
  DisplayName: "{display_name}"
  OwnerCharacterId: {owner}
  Cost: {cost}
  CardType: {card_type}
  Rarity: {rarity}
  Keywords:{keywords}
  Actions:
{actions}  CardArt: {{fileID: 0}}
  CardFrame: {{fileID: 0}}
  CardIcon: {{fileID: 0}}
"""

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def unicode_escape(s: str) -> str:
    return "".join(f"\\u{ord(c):04X}" for c in s)


def format_keywords(keywords):
    if not keywords:
        return " []"
    return "\n" + "\n".join(f"  - {k}" for k in keywords)


def format_actions(actions):
    if not actions:
        return "\n"
    return "\n".join(
        ACTION_TEMPLATE.format(
            type=t, target=target, value=value, status=status,
            stacks=stacks, duration=duration)
        for t, target, value, status, stacks, duration in actions
    )


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for card_id, display, owner, cost, ctype, rarity, keywords, actions in CARDS:
        asset_path = os.path.join(OUT_DIR, f"Card_{card_id}.asset")
        meta_path = asset_path + ".meta"
        content = ASSET_TEMPLATE.format(
            card_id=card_id,
            display_name=unicode_escape(display),
            owner=owner,
            cost=cost,
            card_type=ctype,
            rarity=rarity,
            keywords=format_keywords(keywords),
            actions=format_actions(actions),
        ).replace("SCRIPT_GUID_PLACEHOLDER", SCRIPT_GUID)
        with open(asset_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
        if not os.path.exists(meta_path):
            guid = uuid.uuid4().hex
            with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
                f.write(META_TEMPLATE.format(guid=guid))
        print("wrote", card_id)


if __name__ == "__main__":
    main()
