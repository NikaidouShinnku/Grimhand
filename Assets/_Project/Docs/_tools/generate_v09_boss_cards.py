#!/usr/bin/env python3
"""Generate CardDefinitionSO / CharacterDefinitionSO assets for V09 bosses."""
from __future__ import annotations

import os
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "Data"
CARDS = ROOT / "Cards"
CHARS = ROOT / "Characters"
CARD_SCRIPT = "d1e8e1f9605a6fb4fa4e4fa5586de5ee"
CHAR_SCRIPT = "84c30176d181a5747a85983cdd126a0c"

# EffectActionType ordinals
DealDamage = 0
GainBlock = 1
ApplyStatus = 3
GainBlockFromLastDamagePercent = 9
SummonOrGainBlock = 14
DealDamageRandomCharacterAlly = 43
StripBlockThenDealDamage = 44
SwapRandomEnemies = 45
AdjustSelfStatusRandom = 46
ApplyAttackUpPerSelfStatusStack = 47
LockRisingTideStacks = 48

# EffectTarget
DefaultEnemy = 0
Self = 1
AllEnemies = 12
RandomEnemy = 13
RandomEnemies = 14
RandomAllyByCharacterId = 16

# TargetReach
Any = 0
FrontAndMiddle = 1

# ReactionCondition
NoneCond = 0
LastActionAttackOnSelf = 1

# CardType / Rarity
Attack, Defense, Status = 0, 1, 2
Epic = 3

# FormationSlot
Front, Middle, Back = 1, 2, 3

POISON = "poison"
SLOW = "slow"
VULN = "vulnerable"
DEF_DOWN = "defense_down_pct"
ATK_UP = "attack_up_pct"
BRAND = "brand_mark"
RISING = "rising_tide"
TIDE_EMP = "tide_empower"


def uescape(s: str) -> str:
    return "".join(f"\\u{ord(c):04X}" for c in s)


def action(**kwargs) -> dict:
    base = dict(
        Type=0,
        Target=0,
        Value=0,
        StatusId="",
        Stacks=1,
        Duration=-1,
        ScaleWithAttack=0,
        ScaleWithDefense=0,
        AttackScalePercent=100,
        DefenseScalePercent=100,
        Condition=0,
        Reach=FrontAndMiddle,
        SplashBehindTarget=0,
        SplashPowerPercent=100,
        BackRowPowerPercent=100,
        IgnoreDefPercent=0,
        BonusIfTargetHpBelowPercent=0,
        BonusIfTargetHpBelowFlat=0,
        BonusIfTargetHitThisTurnPercent=0,
        BonusIfTargetHasStatusId="",
        BonusIfTargetHasStatusFlat=0,
        BonusIfActorFasterThanAllEnemiesFlat=0,
        LifestealPercent=0,
        HealMaxHpPercent=0,
        OnKillHealAmount=0,
        HitCount=1,
        AlternateAttackScalePercent=0,
        AlternateValue=0,
        UseAlternateIfTargetHasDebuff=0,
        UseAlternateIfTargetHasAnyStatus=0,
        AlternateAttackScaleIfActorUsedAttack=0,
        AlternateValueIfActorUsedAttack=0,
        DamageMultiplierPercentIfRespondArmed=100,
        SelfDamageFlat=0,
        RepeatPerEnemyAttackCardThisTurn=0,
        FallbackBlockDefenseScalePercent=100,
        FallbackBlockValue=0,
        SummonCharacterId="",
        GrantInvulnerableOnRespondArm=0,
        LifestealUnblockedOnly=0,
        HpLossStepPercent=0,
        HpLossStepValue=0,
        AlternateValueIfHealed=0,
        TokenCardId="",
        CostReduction=0,
        ChancePercent=0,
        UseAlternateIfActorNotHitThisTurn=0,
        SelfBlockAboveThreshold=0,
        AlternateValueIfSelfBlockAbove=0,
        RepeatPerStatusId="",
        RespondSideEffectAllyDamage=0,
        RespondSideEffectAllyCharacterId="",
    )
    base.update(kwargs)
    return base


def fmt_action(a: dict) -> str:
    lines = []
    for k, v in a.items():
        if isinstance(v, str):
            lines.append(f"    {k}: {v}")
        else:
            lines.append(f"    {k}: {v}")
    return "\n".join(lines)


def write_meta(path: Path, guid: str) -> None:
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def write_card(card_id: str, name: str, owner: str, cost: int, card_type: int,
               keywords: list[str], actions: list[dict], guid: str) -> None:
    asset = CARDS / f"Card_{card_id}.asset"
    meta = CARDS / f"Card_{card_id}.asset.meta"
    if not keywords:
        kw_block = "[]"
    else:
        kw_block = "\n" + "\n".join(f"  - {k}" for k in keywords)

    if not actions:
        act_block = "[]"
    else:
        chunks = []
        for a in actions:
            body = fmt_action(a)
            first, *rest = body.split("\n")
            chunk = "  - " + first.strip()
            if rest:
                chunk += "\n" + "\n".join(rest)
            chunks.append(chunk)
        act_block = "\n" + "\n".join(chunks)

    yaml = f"""%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {CARD_SCRIPT}, type: 3}}
  m_Name: Card_{card_id}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CardDefinitionSO
  CardId: {card_id}
  DisplayName: "{uescape(name)}"
  OwnerCharacterId: {owner}
  Cost: {cost}
  CardType: {card_type}
  Rarity: {Epic}
  Keywords: {kw_block}
  Actions: {act_block}
  CardArt: {{fileID: 0}}
  CardFrame: {{fileID: 0}}
  CardIcon: {{fileID: 0}}
"""
    asset.write_text(yaml, encoding="utf-8")
    if not meta.exists():
        write_meta(meta, guid)
    print("card", card_id)


def write_char(asset_name: str, char_id: str, name: str, slot: int,
               hp: int, atk: int, defense: int, spd: int, traits: list[str], guid: str) -> None:
    asset = CHARS / f"{asset_name}.asset"
    meta = CHARS / f"{asset_name}.asset.meta"
    trait_block = "[]" if not traits else "\n" + "\n".join(f"  - {t}" for t in traits)
    yaml = f"""%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {CHAR_SCRIPT}, type: 3}}
  m_Name: {asset_name}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CharacterDefinitionSO
  CharacterId: {char_id}
  DisplayName: "{uescape(name)}"
  Team: 1
  Slot: {slot}
  Level: 1
  MaxHp: {hp}
  BaseAttack: {atk}
  BaseDefense: {defense}
  Speed: {spd}
  Deck: []
  SkillPool: []
  Traits: {trait_block}
"""
    if not traits:
        yaml = yaml.replace("  Traits: []\n", "  Traits: []\n")
    asset.write_text(yaml, encoding="utf-8")
    if not meta.exists():
        write_meta(meta, guid)
    print("char", char_id)


def main() -> None:
    guids = [uuid.uuid4().hex for _ in range(30)]
    gi = 0

    def next_guid() -> str:
        nonlocal gi
        g = guids[gi]
        gi += 1
        return g

    cards = [
        # Warden
        ("m_warden_punishment_combo", "刑法连击", "char_warden", 1, Attack, [], [
            action(Type=DealDamage, Target=DefaultEnemy, Value=30, Reach=FrontAndMiddle,
                   SplashBehindTarget=1, SplashPowerPercent=50),
        ]),
        ("m_warden_brand", "刻上烙印", "char_warden", 1, Status, [], [
            action(Type=ApplyStatus, Target=RandomEnemy, StatusId=BRAND, Stacks=1, Duration=-1),
        ]),
        ("m_warden_iron_gate", "铁壁牢门", "char_warden", 1, Defense, ["parry"], [
            action(Type=GainBlockFromLastDamagePercent, Target=Self, Value=70,
                   Condition=LastActionAttackOnSelf,
                   RespondSideEffectAllyDamage=30,
                   RespondSideEffectAllyCharacterId="char_prison_cage"),
        ]),
        ("m_warden_open_cage", "打开囚笼", "char_warden", 2, Status, [], [
            action(Type=DealDamageRandomCharacterAlly, Target=RandomAllyByCharacterId,
                   SummonCharacterId="char_prison_cage", Value=150),
        ]),
        ("m_warden_oppression", "压迫气场", "char_warden", 2, Status, ["aoe", "slow"], [
            action(Type=ApplyStatus, Target=AllEnemies, StatusId=SLOW, Stacks=2, Duration=2, Reach=Any),
            action(Type=ApplyStatus, Target=AllEnemies, StatusId=DEF_DOWN, Stacks=20, Duration=2, Reach=Any),
        ]),
        ("m_warden_iron_sanction", "铁腕制裁", "char_warden", 3, Attack, [], [
            action(Type=DealDamage, Target=DefaultEnemy, Value=30, Reach=FrontAndMiddle),
            action(Type=ApplyStatus, Target=DefaultEnemy, StatusId=VULN, Stacks=100, Duration=2),
        ]),
        ("m_warden_lock", "上锁", "char_warden", 2, Status, [], [
            action(Type=ApplyStatus, Target=DefaultEnemy, StatusId=DEF_DOWN, Stacks=100, Duration=2,
                   Reach=FrontAndMiddle),
        ]),
        ("m_warden_judgment", "审判裁决", "char_warden", 3, Status, ["aoe"], [
            action(Type=ApplyStatus, Target=AllEnemies, StatusId=BRAND, Stacks=1, Duration=-1, Reach=Any),
        ]),
        # Dark Knight
        ("m_dark_knight_wither", "凋零刺击", "char_dark_knight", 1, Attack, [], [
            action(Type=DealDamage, Target=DefaultEnemy, Value=25, Reach=FrontAndMiddle,
                   BonusIfTargetHasStatusId=POISON, BonusIfTargetHasStatusFlat=15),
        ]),
        ("m_dark_knight_soul_drain", "灵魂吸取", "char_dark_knight", 1, Attack, [], [
            action(Type=DealDamage, Target=DefaultEnemy, Value=15, Reach=FrontAndMiddle, LifestealPercent=100),
        ]),
        ("m_dark_knight_shield", "黑暗护盾", "char_dark_knight", 1, Defense, [], [
            action(Type=GainBlock, Target=Self, Value=20),
        ]),
        ("m_dark_knight_plague", "瘟疫之潮", "char_dark_knight", 2, Status, ["aoe", "poison"], [
            action(Type=ApplyStatus, Target=AllEnemies, StatusId=POISON, Stacks=5, Duration=-1, Reach=Any),
        ]),
        ("m_dark_knight_command_dead", "号令亡者", "char_dark_knight", 2, Status, ["exhaust", "summon"], [
            action(Type=SummonOrGainBlock, Target=Self, SummonCharacterId="char_spider_lady",
                   FallbackBlockValue=15),
        ]),
        ("m_dark_knight_snowball", "雪上加霜", "char_dark_knight", 2, Attack, ["aoe"], [
            action(Type=DealDamage, Target=AllEnemies, Value=10, Reach=Any,
                   BonusIfTargetHasStatusId=POISON, BonusIfTargetHasStatusFlat=10),
        ]),
        # Ocean Goddess
        ("m_ocean_corrupted_net", "腐化电网", "char_corrupted_ocean_goddess", 1, Attack, ["aoe"], [
            action(Type=DealDamage, Target=AllEnemies, Value=20, Reach=Any),
        ]),
        ("m_ocean_shield", "海洋神盾", "char_corrupted_ocean_goddess", 1, Defense, [], [
            action(Type=GainBlock, Target=Self, Value=30),
        ]),
        ("m_ocean_tide_power", "潮汐神力", "char_corrupted_ocean_goddess", 2, Status, [], [
            action(Type=ApplyAttackUpPerSelfStatusStack, Target=Self, StatusId=ATK_UP,
                   Stacks=20, Duration=2, RepeatPerStatusId=RISING),
        ]),
        ("m_ocean_vortex", "漩涡吸引", "char_corrupted_ocean_goddess", 1, Status, [], [
            action(Type=SwapRandomEnemies, Target=AllEnemies, Value=1),
            action(Type=ApplyStatus, Target=RandomEnemies, Value=2, StatusId=POISON, Stacks=5, Duration=-1),
        ]),
        ("m_ocean_abyss_devour", "深渊吞噬", "char_corrupted_ocean_goddess", 2, Attack, [], [
            action(Type=StripBlockThenDealDamage, Target=DefaultEnemy, Value=12, Stacks=5, Reach=Any),
        ]),
        ("m_ocean_goddess_wrath", "女神之怒", "char_corrupted_ocean_goddess", 3, Status, ["orange"], [
            action(Type=LockRisingTideStacks, Target=Self, Duration=2),
        ]),
        ("m_ocean_tide_control", "潮汐掌握", "char_corrupted_ocean_goddess", 1, Status, [], [
            action(Type=AdjustSelfStatusRandom, Target=Self, StatusId=RISING),
        ]),
        ("m_ocean_demon_tide", "魔化潮汐", "char_corrupted_ocean_goddess", 2, Status, ["exhaust"], [
            action(Type=ApplyStatus, Target=Self, StatusId=TIDE_EMP, Stacks=1, Duration=-1),
        ]),
    ]

    for cid, name, owner, cost, ctype, kws, acts in cards:
        write_card(cid, name, owner, cost, ctype, kws, acts, next_guid())

    chars = [
        ("Character_Warden", "char_warden", "典狱长", Back, 250, 22, 8, 5, ["warden_cage_master"]),
        ("Character_Dark_Knight", "char_dark_knight", "黑暗骑士", Front, 350, 25, 10, 8, ["dark_knight_poison_aura"]),
        ("Character_Corrupted_Ocean_Goddess", "char_corrupted_ocean_goddess", "腐化海洋女神", Front, 400, 20, 10, 6,
         ["ocean_goddess_tide"]),
        ("Character_Prison_Cage", "char_prison_cage", "囚笼", Middle, 150, 0, 5, 5, ["prison_cage"]),
    ]
    for args in chars:
        write_char(*args, next_guid())


if __name__ == "__main__":
    main()
