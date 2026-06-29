#!/usr/bin/env python3
"""Restore broken card Actions from v0.8 descriptions (post apply_v08_overview corruption)."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Data" / "Cards"

ACTION_FIELDS = """    ScaleWithAttack: 0
    ScaleWithDefense: 0
    AttackScalePercent: 100
    DefenseScalePercent: 100
    Condition: {condition}
    Reach: {reach}
    SplashBehindTarget: 0
    SplashPowerPercent: 100
    BackRowPowerPercent: 100
    IgnoreDefPercent: 0
    BonusIfTargetHpBelowPercent: 0
    BonusIfTargetHpBelowFlat: 0
    BonusIfTargetHitThisTurnPercent: 0
    BonusIfTargetHasStatusId: 
    BonusIfTargetHasStatusFlat: 0
    LifestealPercent: 0
    HealMaxHpPercent: 0
    OnKillHealAmount: 0
    HitCount: 1
    AlternateAttackScalePercent: 0
    AlternateValue: 0
    UseAlternateIfTargetHasDebuff: 0
    AlternateAttackScaleIfActorUsedAttack: 0
    AlternateValueIfActorUsedAttack: 0
    DamageMultiplierPercentIfRespondArmed: {respond_mul}
    SelfDamageFlat: {self_dmg}
    RepeatPerEnemyAttackCardThisTurn: 0
    FallbackBlockDefenseScalePercent: {fallback_def_scale}
    FallbackBlockValue: {fallback_block}
    SummonCharacterId: {summon_id}
    GrantInvulnerableOnRespondArm: 0
    LifestealUnblockedOnly: 0"""


def action_block(
    *,
    type_id: int,
    target: int,
    value: int = 0,
    status_id: str = "",
    stacks: int = 1,
    duration: int = -1,
    condition: int = 0,
    reach: int = 1,
    respond_mul: int = 100,
    self_dmg: int = 0,
    fallback_def_scale: int = 100,
    fallback_block: int = 0,
    summon_id: str = "",
) -> str:
    fields = ACTION_FIELDS.format(
        condition=condition,
        reach=reach,
        respond_mul=respond_mul,
        self_dmg=self_dmg,
        fallback_def_scale=fallback_def_scale,
        fallback_block=fallback_block,
        summon_id=summon_id,
    )
    return (
        f"  - Type: {type_id}\n"
        f"    Target: {target}\n"
        f"    Value: {value}\n"
        f"    StatusId: {status_id}\n"
        f"    Stacks: {stacks}\n"
        f"    Duration: {duration}\n"
        f"{fields}\n"
    )


def replace_actions(path: Path, actions: str, keywords: list[str] | None = None) -> None:
    text = path.read_text(encoding="utf-8")
    new_actions = "  Actions:\n" + actions.rstrip() + "\n"
    text = re.sub(r"  Actions:\n.*?(?=  CardArt:)", new_actions, text, count=1, flags=re.S)
    if keywords is not None:
        if keywords:
            kw = "  Keywords:\n" + "\n".join(f"  - {k}" for k in keywords) + "\n"
        else:
            kw = "  Keywords: []\n"
        text = re.sub(r"  Keywords:\s*\n(?:  - .+\n)*", kw, text)
        text = re.sub(r"  Keywords: \[\]\n", kw, text)
    path.write_text(text, encoding="utf-8")


def main() -> None:
    fixes: list[tuple[str, str, list[str] | None]] = [
        (
            "Card_g_blood_scratch.asset",
            action_block(type_id=0, target=0, value=4, reach=1)
            + action_block(
                type_id=3, target=1, status_id="attack_up", stacks=3, duration=1
            ),
            None,
        ),
        (
            "Card_d_blood_armor.asset",
            action_block(type_id=0, target=1, value=3)
            + action_block(type_id=1, target=1, value=12),
            ["sacrifice"],
        ),
        ("Card_m_bone_shield.asset", action_block(type_id=1, target=1, value=10), []),
        (
            "Card_m_bat_ambush.asset",
            action_block(type_id=0, target=0, value=11, respond_mul=300),
            ["respond_status"],
        ),
        (
            "Card_m_bone_spear.asset",
            action_block(type_id=0, target=0, value=27, reach=3),
            None,
        ),
        (
            "Card_m_final_guard.asset",
            action_block(type_id=9, target=1, value=50, condition=1)
            + action_block(type_id=1, target=1, value=8)
            + action_block(type_id=12, target=1, value=99),
            ["parry"],
        ),
        ("Card_m_gargoyle_claw.asset", action_block(type_id=0, target=0, value=11), None),
        ("Card_m_gargoyle_sunder.asset", action_block(type_id=0, target=0, value=10, reach=0), None),
        (
            "Card_m_golem_crack_fist.asset",
            action_block(type_id=0, target=0, value=10),
            None,
        ),
        (
            "Card_m_king_summon_workshop.asset",
            action_block(
                type_id=3, target=1, status_id="bone_workshop", stacks=1, duration=-1
            ),
            ["exhaust"],
        ),
        ("Card_m_magic_lightning.asset", action_block(type_id=0, target=0, value=8, reach=0), None),
        (
            "Card_m_raise_bones.asset",
            action_block(
                type_id=14,
                target=1,
                summon_id="char_skeleton",
                fallback_block=6,
            ),
            ["exhaust"],
        ),
        (
            "Card_m_final_summon.asset",
            action_block(
                type_id=3, target=1, status_id="final_summon_pending", stacks=1, duration=2
            ),
            ["exhaust"],
        ),
        ("Card_m_soul_strike.asset", action_block(type_id=0, target=0, value=10), None),
        (
            "Card_m_spider_fatal_bind.asset",
            action_block(type_id=0, target=0, value=18, reach=3),
            ["exhaust"],
        ),
    ]

    for name, actions, keywords in fixes:
        path = CARDS / name
        if not path.exists():
            print(f"SKIP missing {name}")
            continue
        replace_actions(path, actions, keywords)
        print(f"fixed {name}")


if __name__ == "__main__":
    main()
