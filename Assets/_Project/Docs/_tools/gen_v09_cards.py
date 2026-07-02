"""Generate the 21 missing v0.9 player card assets (+ .meta) for 战士/法老/恶魔."""
import uuid
from pathlib import Path

CARDS_DIR = Path(r"c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand\Assets\_Project\Data\Cards")
SCRIPT_GUID = "d1e8e1f9605a6fb4fa4e4fa5586de5ee"

# Action field order — matches existing Card_d_blood_hand.asset exactly
ACTION_FIELDS = [
    "Type", "Target", "Value", "StatusId", "Stacks", "Duration",
    "ScaleWithAttack", "ScaleWithDefense", "AttackScalePercent", "DefenseScalePercent",
    "Condition", "Reach", "SplashBehindTarget", "SplashPowerPercent", "BackRowPowerPercent",
    "IgnoreDefPercent", "BonusIfTargetHpBelowPercent", "BonusIfTargetHpBelowFlat",
    "BonusIfTargetHitThisTurnPercent", "BonusIfTargetHasStatusId", "BonusIfTargetHasStatusFlat",
    "LifestealPercent", "HealMaxHpPercent", "OnKillHealAmount", "HitCount",
    "AlternateAttackScalePercent", "AlternateValue", "UseAlternateIfTargetHasDebuff",
    "AlternateAttackScaleIfActorUsedAttack", "AlternateValueIfActorUsedAttack",
    "DamageMultiplierPercentIfRespondArmed", "SelfDamageFlat", "RepeatPerEnemyAttackCardThisTurn",
    "FallbackBlockDefenseScalePercent", "FallbackBlockValue", "SummonCharacterId",
    "GrantInvulnerableOnRespondArm", "LifestealUnblockedOnly",
    "HpLossStepPercent", "HpLossStepValue", "AlternateValueIfHealed",
]

# Defaults matching existing cards (every field present)
def defaults():
    return {
        "Type": 0, "Target": 0, "Value": 0, "StatusId": "", "Stacks": 1, "Duration": -1,
        "ScaleWithAttack": 0, "ScaleWithDefense": 0, "AttackScalePercent": 100, "DefenseScalePercent": 100,
        "Condition": 0, "Reach": 1, "SplashBehindTarget": 0, "SplashPowerPercent": 100, "BackRowPowerPercent": 100,
        "IgnoreDefPercent": 0, "BonusIfTargetHpBelowPercent": 0, "BonusIfTargetHpBelowFlat": 0,
        "BonusIfTargetHitThisTurnPercent": 0, "BonusIfTargetHasStatusId": "", "BonusIfTargetHasStatusFlat": 0,
        "LifestealPercent": 0, "HealMaxHpPercent": 0, "OnKillHealAmount": 0, "HitCount": 1,
        "AlternateAttackScalePercent": 0, "AlternateValue": 0, "UseAlternateIfTargetHasDebuff": 0,
        "AlternateAttackScaleIfActorUsedAttack": 0, "AlternateValueIfActorUsedAttack": 0,
        "DamageMultiplierPercentIfRespondArmed": 100, "SelfDamageFlat": 0, "RepeatPerEnemyAttackCardThisTurn": 0,
        "FallbackBlockDefenseScalePercent": 100, "FallbackBlockValue": 0, "SummonCharacterId": "",
        "GrantInvulnerableOnRespondArm": 0, "LifestealUnblockedOnly": 0,
        "HpLossStepPercent": 0, "HpLossStepValue": 0, "AlternateValueIfHealed": 0,
    }

def act(**over):
    d = defaults()
    d.update(over)
    return d

def fmt_action(a, indent="  "):
    lines = []
    lines.append(f"{indent}- Type: {a['Type']}")
    lines.append(f"{indent}  Target: {a['Target']}")
    lines.append(f"{indent}  Value: {a['Value']}")
    lines.append(f"{indent}  StatusId: {a['StatusId']}")
    lines.append(f"{indent}  Stacks: {a['Stacks']}")
    lines.append(f"{indent}  Duration: {a['Duration']}")
    lines.append(f"{indent}  ScaleWithAttack: {a['ScaleWithAttack']}")
    lines.append(f"{indent}  ScaleWithDefense: {a['ScaleWithDefense']}")
    lines.append(f"{indent}  AttackScalePercent: {a['AttackScalePercent']}")
    lines.append(f"{indent}  DefenseScalePercent: {a['DefenseScalePercent']}")
    lines.append(f"{indent}  Condition: {a['Condition']}")
    lines.append(f"{indent}  Reach: {a['Reach']}")
    lines.append(f"{indent}  SplashBehindTarget: {a['SplashBehindTarget']}")
    lines.append(f"{indent}  SplashPowerPercent: {a['SplashPowerPercent']}")
    lines.append(f"{indent}  BackRowPowerPercent: {a['BackRowPowerPercent']}")
    lines.append(f"{indent}  IgnoreDefPercent: {a['IgnoreDefPercent']}")
    lines.append(f"{indent}  BonusIfTargetHpBelowPercent: {a['BonusIfTargetHpBelowPercent']}")
    lines.append(f"{indent}  BonusIfTargetHpBelowFlat: {a['BonusIfTargetHpBelowFlat']}")
    lines.append(f"{indent}  BonusIfTargetHitThisTurnPercent: {a['BonusIfTargetHitThisTurnPercent']}")
    lines.append(f"{indent}  BonusIfTargetHasStatusId: {a['BonusIfTargetHasStatusId']}")
    lines.append(f"{indent}  BonusIfTargetHasStatusFlat: {a['BonusIfTargetHasStatusFlat']}")
    lines.append(f"{indent}  LifestealPercent: {a['LifestealPercent']}")
    lines.append(f"{indent}  HealMaxHpPercent: {a['HealMaxHpPercent']}")
    lines.append(f"{indent}  OnKillHealAmount: {a['OnKillHealAmount']}")
    lines.append(f"{indent}  HitCount: {a['HitCount']}")
    lines.append(f"{indent}  AlternateAttackScalePercent: {a['AlternateAttackScalePercent']}")
    lines.append(f"{indent}  AlternateValue: {a['AlternateValue']}")
    lines.append(f"{indent}  UseAlternateIfTargetHasDebuff: {a['UseAlternateIfTargetHasDebuff']}")
    lines.append(f"{indent}  AlternateAttackScaleIfActorUsedAttack: {a['AlternateAttackScaleIfActorUsedAttack']}")
    lines.append(f"{indent}  AlternateValueIfActorUsedAttack: {a['AlternateValueIfActorUsedAttack']}")
    lines.append(f"{indent}  DamageMultiplierPercentIfRespondArmed: {a['DamageMultiplierPercentIfRespondArmed']}")
    lines.append(f"{indent}  SelfDamageFlat: {a['SelfDamageFlat']}")
    lines.append(f"{indent}  RepeatPerEnemyAttackCardThisTurn: {a['RepeatPerEnemyAttackCardThisTurn']}")
    lines.append(f"{indent}  FallbackBlockDefenseScalePercent: {a['FallbackBlockDefenseScalePercent']}")
    lines.append(f"{indent}  FallbackBlockValue: {a['FallbackBlockValue']}")
    lines.append(f"{indent}  SummonCharacterId: {a['SummonCharacterId']}")
    lines.append(f"{indent}  GrantInvulnerableOnRespondArm: {a['GrantInvulnerableOnRespondArm']}")
    lines.append(f"{indent}  LifestealUnblockedOnly: {a['LifestealUnblockedOnly']}")
    lines.append(f"{indent}  HpLossStepPercent: {a['HpLossStepPercent']}")
    lines.append(f"{indent}  HpLossStepValue: {a['HpLossStepValue']}")
    lines.append(f"{indent}  AlternateValueIfHealed: {a['AlternateValueIfHealed']}")
    return "\n".join(lines)

# CardType: 0=Attack 1=Defense 2=Status
# EffectTarget: 0=DefaultEnemy 1=Self 2=FrontAlly 4=LastActionActor 12=AllEnemies
# ReactionConditionType: 0=None 1=LastActionAttackOnSelf
# TargetReach: 0=Any 1=FrontAndMiddle
# EffectActionType: 0=DealDamage 1=GainBlock 2=Heal 3=ApplyStatus 7=DrawCards
#   9=GainBlockFromLastDamagePercent 16=ConsumeBlockDealDamage 17=ParryImmuneAndSlowAttacker
#   18=DamagePerRespondCount 19=DoubleStatusStacks 20=RecycleExhaustCardsFromDiscard
#   21=DealDamageScaledByActorHpLoss 22=DealDamageAlternateIfHealedThisTurn 23=DealDamageBonusPerTargetDebuffStack

CARDS = [
    # 战士 char_knight
    dict(file="Card_w_shield_slam.asset", card_id="w_shield_slam", name="护盾猛击", owner="char_knight",
         cost=1, ctype=0, rarity=1, kws=[],
         actions=[act(Type=16, Target=0, Reach=1, Value=1)]),
    dict(file="Card_w_strategic_retreat.asset", card_id="w_strategic_retreat", name="以退为进", owner="char_knight",
         cost=1, ctype=2, rarity=1, kws=[],
         actions=[act(Type=3, Target=1, StatusId="slow", Stacks=3, Duration=1),
                  act(Type=1, Target=1, Value=15)]),
    dict(file="Card_w_parry_counter.asset", card_id="w_parry_counter", name="见招拆招", owner="char_knight",
         cost=2, ctype=1, rarity=1, kws=["parry"],
         actions=[act(Type=17, Target=4, Condition=1, Stacks=2, Duration=2)]),
    dict(file="Card_w_respond_stance.asset", card_id="w_respond_stance", name="应对姿态", owner="char_knight",
         cost=2, ctype=2, rarity=2, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="respond_stance", Stacks=1, Duration=-1)]),
    dict(file="Card_w_battle_will.asset", card_id="w_battle_will", name="战意觉醒", owner="char_knight",
         cost=3, ctype=2, rarity=2, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="battle_will", Stacks=1, Duration=-1)]),
    dict(file="Card_w_heavy_armor.asset", card_id="w_heavy_armor", name="重甲强化", owner="char_knight",
         cost=3, ctype=1, rarity=2, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="heavy_armor", Stacks=1, Duration=-1)]),
    dict(file="Card_w_tactician_finisher.asset", card_id="w_tactician_finisher", name="战术大师的终结技", owner="char_knight",
         cost=3, ctype=0, rarity=3, kws=[],
         actions=[act(Type=18, Target=0, Reach=1, Value=5)]),
    dict(file="Card_w_burning_fury.asset", card_id="w_burning_fury", name="怒火焚身", owner="char_knight",
         cost=2, ctype=0, rarity=3, kws=[],
         actions=[act(Type=21, Target=0, Reach=1, Value=10, HpLossStepPercent=5, HpLossStepValue=1)]),
    dict(file="Card_w_final_bulwark.asset", card_id="w_final_bulwark", name="最终壁垒", owner="char_knight",
         cost=4, ctype=2, rarity=4, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="final_bulwark", Stacks=1, Duration=-1)]),
    dict(file="Card_w_last_stand.asset", card_id="w_last_stand", name="背水一战", owner="char_knight",
         cost=7, ctype=2, rarity=4, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="last_stand", Stacks=1, Duration=2),
                  act(Type=3, Target=1, StatusId="attack_up_pct", Stacks=20, Duration=2)]),
    # 法老 char_mage
    dict(file="Card_p_rot_touch.asset", card_id="p_rot_touch", name="腐烂之触", owner="char_mage",
         cost=2, ctype=0, rarity=1, kws=[],
         actions=[act(Type=23, Target=0, Reach=1, Value=12, Stacks=2)]),
    dict(file="Card_p_plague_spread.asset", card_id="p_plague_spread", name="瘟疫蔓延", owner="char_mage",
         cost=2, ctype=2, rarity=2, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="plague_spread", Stacks=1, Duration=-1)]),
    dict(file="Card_p_curse_deepen.asset", card_id="p_curse_deepen", name="诅咒加深", owner="char_mage",
         cost=2, ctype=2, rarity=2, kws=[],
         actions=[act(Type=19, Target=0, Reach=0)]),
    dict(file="Card_p_holy_infusion.asset", card_id="p_holy_infusion", name="神圣灌注", owner="char_mage",
         cost=0, ctype=2, rarity=3, kws=["x_cost", "exhaust"],
         actions=[act(Type=3, Target=1, StatusId="holy_infusion_pending", Stacks=1, Duration=-1)]),
    dict(file="Card_p_rot_avatar.asset", card_id="p_rot_avatar", name="腐朽化身", owner="char_mage",
         cost=4, ctype=2, rarity=4, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="rot_avatar", Stacks=1, Duration=-1)]),
    dict(file="Card_p_holy_cycle.asset", card_id="p_holy_cycle", name="神圣轮回", owner="char_mage",
         cost=5, ctype=2, rarity=4, kws=["exhaust"],
         actions=[act(Type=20, Target=1)]),
    # 恶魔 char_ranger
    dict(file="Card_d_blood_bite.asset", card_id="d_blood_bite", name="鲜血撕咬", owner="char_ranger",
         cost=1, ctype=0, rarity=1, kws=[],
         actions=[act(Type=22, Target=0, Reach=1, Value=7, AlternateValueIfHealed=14)]),
    dict(file="Card_d_dark_tear.asset", card_id="d_dark_tear", name="黑暗撕裂", owner="char_ranger",
         cost=2, ctype=1, rarity=2, kws=["parry"],
         actions=[act(Type=9, Target=1, Condition=1, Value=50),
                  act(Type=3, Target=4, Condition=1, StatusId="armor_down", Stacks=20, Duration=-1)]),
    dict(file="Card_d_blood_frenzy.asset", card_id="d_blood_frenzy", name="鲜血狂欢", owner="char_ranger",
         cost=3, ctype=2, rarity=3, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="blood_frenzy", Stacks=1, Duration=-1)]),
    dict(file="Card_d_bloodline_legacy.asset", card_id="d_bloodline_legacy", name="血族传承", owner="char_ranger",
         cost=3, ctype=2, rarity=3, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="bloodline_legacy", Stacks=1, Duration=-1)]),
    dict(file="Card_d_blood_sharing.asset", card_id="d_blood_sharing", name="分血仪式", owner="char_ranger",
         cost=7, ctype=2, rarity=4, kws=["exhaust"],
         actions=[act(Type=3, Target=1, StatusId="blood_sharing", Stacks=1, Duration=-1)]),
]


def yaml_escape(s):
    # ASCII-only \uXXXX escape for non-ascii (Unity style)
    out = []
    for ch in s:
        if ord(ch) > 127:
            out.append(f"\\u{ord(ch):04X}")
        else:
            out.append(ch)
    return "".join(out)


def write_asset(card):
    name_escaped = yaml_escape(card["name"])
    lines = []
    lines.append("%YAML 1.1")
    lines.append("%TAG !u! tag:unity3d.com,2011:")
    lines.append("--- !u!114 &11400000")
    lines.append("MonoBehaviour:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append("  m_GameObject: {fileID: 0}")
    lines.append("  m_Enabled: 1")
    lines.append("  m_EditorHideFlags: 0")
    lines.append(f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}")
    lines.append(f"  m_Name: {card['file'].replace('.asset','')}")
    lines.append("  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CardDefinitionSO")
    lines.append(f"  CardId: {card['card_id']}")
    lines.append(f"  DisplayName: \"{name_escaped}\"")
    lines.append(f"  OwnerCharacterId: {card['owner']}")
    lines.append(f"  Cost: {card['cost']}")
    lines.append(f"  CardType: {card['ctype']}")
    if card["kws"]:
        lines.append("  Keywords:")
        for kw in card["kws"]:
            lines.append(f"  - {kw}")
    else:
        lines.append("  Keywords: []")
    lines.append(f"  Rarity: {card['rarity']}")
    if card["actions"]:
        lines.append("  Actions:")
        for a in card["actions"]:
            lines.append(fmt_action(a))
    else:
        lines.append("  Actions: []")
    lines.append("  CardArt: {fileID: 0}")
    lines.append("  CardFrame: {fileID: 0}")
    lines.append("  CardIcon: {fileID: 0}")
    return "\n".join(lines) + "\n"


def write_meta(guid):
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n"
    )


def main():
    created = 0
    for card in CARDS:
        asset_path = CARDS_DIR / card["file"]
        meta_path = CARDS_DIR / (card["file"] + ".meta")
        guid = uuid.uuid4().hex
        asset_path.write_text(write_asset(card), encoding="utf-8")
        meta_path.write_text(write_meta(guid), encoding="utf-8")
        created += 1
        print(f"Wrote {card['file']}  ({card['name']})  guid={guid}")
    print(f"\nTotal created: {created} cards (+ meta)")


if __name__ == "__main__":
    main()
