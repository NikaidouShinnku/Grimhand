#!/usr/bin/env python3
"""Generate player cards + initial decks from Grimhand实际卡牌遗物总览表.xlsx (2026-06)."""
from __future__ import annotations

import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "Data"
CARDS = ROOT / "Cards"
CHARS = ROOT / "Characters"
SCRIPT_GUID = "d1e8e1f9605a6fb4fa4e4fa5586de5ee"
CHAR_SCRIPT_GUID = "84c30176d181a5747a85983cdd126a0c"

DealDamage, GainBlock, Heal, ApplyStatus, DrawCards = 0, 1, 2, 3, 7
BlockFromDmgPct, Reflect = 9, 8
DefaultEnemy, Self, FrontAlly = 0, 1, 2
AllyFront, AllyMiddle, AllyBack, AllEnemies = 9, 10, 11, 12
RandomEnemy = 13
ReachAny, ReachFrontMiddle = 0, 1
CondAttackOnSelf = 1
ApplyAnubisAvatar = 10
Attack, Defense, Status = 0, 1, 2
Common, Rare, Epic, Legendary = 0, 1, 2, 4


def load_guids() -> dict[str, str]:
    guids: dict[str, str] = {}
    for meta in CARDS.glob("Card_*.asset.meta"):
        m = re.match(r"Card_(.+)\.asset\.meta$", meta.name)
        if not m:
            continue
        card_id = m.group(1)
        text = meta.read_text(encoding="utf-8")
        gm = re.search(r"^guid: ([0-9a-f]+)$", text, re.M)
        if gm:
            guids[card_id] = gm.group(1)
    return guids


GUIDS = load_guids()


def ensure_guid(card_id: str) -> str:
    if card_id not in GUIDS:
        GUIDS[card_id] = uuid.uuid4().hex
    return GUIDS[card_id]


def action(**kw) -> dict:
    base = {
        "Type": 0, "Target": 0, "Value": 0, "StatusId": "", "Stacks": 1, "Duration": -1,
        "ScaleWithAttack": 0, "ScaleWithDefense": 0, "AttackScalePercent": 100,
        "DefenseScalePercent": 100, "Condition": 0, "Reach": 1, "SplashBehindTarget": 0,
        "SplashPowerPercent": 100, "BackRowPowerPercent": 100, "IgnoreDefPercent": 0,
        "BonusIfTargetHpBelowPercent": 0, "BonusIfTargetHpBelowFlat": 0,
        "BonusIfTargetHitThisTurnPercent": 0, "LifestealPercent": 0, "HealMaxHpPercent": 0,
        "OnKillHealAmount": 0,
    }
    base.update(kw)
    return base


def atk(flat, pct=100, target=DefaultEnemy, reach=ReachFrontMiddle, ignore=0, hp_below=0,
        hp_bonus=0, hit_bonus=0, lifesteal=0, on_kill=0, splash=False, splash_pct=80):
    return action(
        Type=DealDamage, Target=target, Value=flat, ScaleWithAttack=1,
        AttackScalePercent=pct,
        Reach=reach if target not in (AllEnemies, RandomEnemy) else ReachAny,
        IgnoreDefPercent=ignore, BonusIfTargetHpBelowPercent=hp_below,
        BonusIfTargetHpBelowFlat=hp_bonus, BonusIfTargetHitThisTurnPercent=hit_bonus,
        LifestealPercent=lifesteal, OnKillHealAmount=on_kill,
        SplashBehindTarget=1 if splash else 0, SplashPowerPercent=splash_pct,
    )


def def_block(flat, pct, target=Self):
    return action(Type=GainBlock, Target=target, Value=flat, ScaleWithDefense=1,
                   DefenseScalePercent=pct,
                   Reach=ReachAny if target not in (Self,) else ReachFrontMiddle)


def heal_pct(pct, target=FrontAlly):
    return action(Type=Heal, Target=target, HealMaxHpPercent=pct, Reach=ReachAny)


def self_dmg(v):
    return action(Type=DealDamage, Target=Self, Value=v)


def self_dmg_pct(pct):
    return action(Type=DealDamage, Target=Self, Value=0, HealMaxHpPercent=pct)


def draw(n):
    return action(Type=DrawCards, Target=Self, Value=n)


def status(sid, stacks, duration, target, reach=ReachFrontMiddle):
    return action(Type=ApplyStatus, Target=target, StatusId=sid, Stacks=stacks,
                  Duration=duration, Reach=reach if target not in (FrontAlly,) else ReachAny)


def respond(reduction, reflect=0):
    acts = [action(Type=BlockFromDmgPct, Target=Self, Value=reduction, Condition=CondAttackOnSelf)]
    if reflect > 0:
        acts.append(action(Type=Reflect, Target=4, Value=reflect, Condition=CondAttackOnSelf))
    return acts


def team_attack_up(stacks, duration):
    return [
        status("attack_up", stacks, duration, AllyFront, ReachAny),
        status("attack_up", stacks, duration, AllyMiddle, ReachAny),
        status("attack_up", stacks, duration, AllyBack, ReachAny),
    ]


def team_def_block(pct):
    return [
        def_block(0, pct, AllyFront),
        def_block(0, pct, AllyMiddle),
        def_block(0, pct, AllyBack),
    ]


def render_action(a: dict) -> str:
    lines = [f"  - Type: {a['Type']}"]
    for k, v in a.items():
        if k == "Type":
            continue
        if k == "StatusId":
            lines.append(f"    StatusId: {v}")
        elif isinstance(v, str):
            lines.append(f"    {k}: {v}")
        else:
            lines.append(f"    {k}: {v}")
    return "\n".join(lines)


def render_card(card_id, display, owner, cost, ctype, rarity, keywords, actions):
    kw_block = "  Keywords: []" if not keywords else "  Keywords:\n" + "\n".join(f"  - {k}" for k in keywords)
    act_lines = "\n".join(render_action(a) for a in actions)
    return f"""%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}
  m_Name: Card_{card_id}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CardDefinitionSO
  CardId: {card_id}
  DisplayName: "{display}"
  OwnerCharacterId: {owner}
  Cost: {cost}
  CardType: {ctype}
  Rarity: {rarity}
{kw_block}
  Actions:
{act_lines}
  CardArt: {{fileID: 0}}
  CardFrame: {{fileID: 0}}
  CardIcon: {{fileID: 0}}
"""


def write_meta(path: Path, guid: str):
    path.with_suffix(".asset.meta").write_text(
        f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n"
        f"  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n"
        f"  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8",
    )


def write_card(card_id, **spec):
    path = CARDS / f"Card_{card_id}.asset"
    path.write_text(render_card(card_id, **spec), encoding="utf-8")
    meta = path.with_suffix(".asset.meta")
    guid = ensure_guid(card_id)
    if not meta.exists():
        write_meta(path, guid)
    print(f"  {card_id}")


def deck_refs(entries):
    return "\n".join(
        f"  - {{fileID: 11400000, guid: {ensure_guid(cid)}, type: 2}}"
        for cid, count in entries for _ in range(count)
    )


def write_character(name, char_id, display, slot, hp, atk, defn, spd, deck_entries):
    text = f"""%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {CHAR_SCRIPT_GUID}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: Grimhand.Content::Grimhand.Content.CharacterDefinitionSO
  CharacterId: {char_id}
  DisplayName: "{display}"
  Team: 0
  Slot: {slot}
  Level: 1
  MaxHp: {hp}
  BaseAttack: {atk}
  BaseDefense: {defn}
  Speed: {spd}
  Deck:
{deck_refs(deck_entries)}
  SkillPool: []
  EnemyRandomDeckSize: 8
  EnemySkillPickMin: 2
  EnemySkillPickMax: 4
"""
    (CHARS / f"{name}.asset").write_text(text, encoding="utf-8")
    print(f"  character {name}")


def main():
    print("Writing 42 player cards...")
    KNIGHT, MAGE, DEMON = "char_knight", "char_mage", "char_ranger"

    # --- Warrior ---
    write_card("w_basic_slash", display="基础斩击", owner=KNIGHT, cost=1, ctype=Attack, rarity=Common,
               keywords=[], actions=[atk(5, 80)])
    write_card("w_shield_block", display="举盾格挡", owner=KNIGHT, cost=1, ctype=Defense, rarity=Common,
               keywords=[], actions=[def_block(3, 80)])
    write_card("w_defensive_stance", display="防御架势", owner=KNIGHT, cost=1, ctype=Defense, rarity=Common,
               keywords=["parry"], actions=respond(50))
    write_card("w_power_cleave", display="猛力劈砍", owner=KNIGHT, cost=2, ctype=Attack, rarity=Common,
               keywords=[], actions=[atk(7, 120, hp_below=50, hp_bonus=10)])
    write_card("w_pommel_strike", display="剑柄猛击", owner=KNIGHT, cost=1, ctype=Attack, rarity=Common,
               keywords=["respond_status"], actions=[atk(5, 100)])
    write_card("w_taunt", display="嘲讽挑衅", owner=KNIGHT, cost=2, ctype=Defense, rarity=Rare,
               keywords=[], actions=[status("taunt", 1, 1, Self), def_block(0, 120)])
    write_card("w_iron_parry", display="铁壁弹反", owner=KNIGHT, cost=2, ctype=Defense, rarity=Rare,
               keywords=["parry"], actions=respond(30, 100))
    write_card("w_charge", display="战士冲锋", owner=KNIGHT, cost=3, ctype=Attack, rarity=Rare,
               keywords=[], actions=[atk(10, 160, ignore=50)])
    write_card("w_blade_storm", display="剑刃风暴", owner=KNIGHT, cost=3, ctype=Attack, rarity=Epic,
               keywords=[], actions=[atk(3, 100, target=RandomEnemy, reach=ReachAny)] * 5)
    write_card("w_war_cry", display="战吼鼓舞", owner=KNIGHT, cost=1, ctype=Status, rarity=Common,
               keywords=[], actions=team_attack_up(3, 1))
    write_card("w_guardian", display="誓死守护", owner=KNIGHT, cost=2, ctype=Defense, rarity=Rare,
               keywords=[], actions=[status("guard", 1, 1, Self)])
    write_card("w_fatal_strike", display="致命打击", owner=KNIGHT, cost=3, ctype=Attack, rarity=Rare,
               keywords=[], actions=[atk(8, 180, hit_bonus=50)])
    write_card("w_unyielding", display="不屈意志", owner=KNIGHT, cost=0, ctype=Status, rarity=Epic,
               keywords=["exhaust"], actions=[status("unyielding", 1, -1, Self)])
    write_card("w_god_descends", display="天神下凡", owner=KNIGHT, cost=5, ctype=Status, rarity=Legendary,
               keywords=["exhaust"], actions=[status("god_descends", 1, -1, Self)])

    # --- Pharaoh ---
    write_card("p_sand_ray", display="沙暴射线", owner=MAGE, cost=1, ctype=Attack, rarity=Common,
               keywords=[], actions=[atk(5, 80)])
    write_card("p_bless", display="祈祷祝福", owner=MAGE, cost=1, ctype=Status, rarity=Common,
               keywords=[], actions=[heal_pct(5)])
    write_card("p_pharaoh_curse", display="法老诅咒", owner=MAGE, cost=1, ctype=Status, rarity=Common,
               keywords=[], actions=[status("slow", 2, 2, DefaultEnemy)])
    write_card("p_solar_wrath", display="太阳之怒", owner=MAGE, cost=2, ctype=Attack, rarity=Rare,
               keywords=["aoe"], actions=[atk(5, 70, target=AllEnemies)])
    write_card("p_lifesteal", display="生命汲取", owner=MAGE, cost=2, ctype=Attack, rarity=Rare,
               keywords=[], actions=[atk(6, 100, lifesteal=50)])
    write_card("p_decree", display="法老权令", owner=MAGE, cost=2, ctype=Status, rarity=Rare,
               keywords=[], actions=[
                   draw(2),
                   status("attack_up", 3, 1, FrontAlly, ReachAny),
                   status("defense_up", 2, 1, FrontAlly, ReachAny),
               ])
    write_card("p_undead_curse", display="亡灵诅咒", owner=MAGE, cost=3, ctype=Attack, rarity=Epic,
               keywords=["poison"], actions=[
                   atk(7, 120, reach=ReachAny),
                   status("necrotic_poison", 1, 3, DefaultEnemy, ReachAny),
               ])
    write_card("p_scarab_shield", display="圣甲虫护盾", owner=MAGE, cost=1, ctype=Defense, rarity=Common,
               keywords=[], actions=[def_block(0, 120, FrontAlly)])
    write_card("p_sand_barrier", display="沙尘结界", owner=MAGE, cost=2, ctype=Defense, rarity=Common,
               keywords=[], actions=team_def_block(100))
    write_card("p_revive_bless", display="复活祝福", owner=MAGE, cost=3, ctype=Status, rarity=Epic,
               keywords=["exhaust"], actions=[status("revive_blessing", 1, -1, FrontAlly, ReachAny)])
    write_card("p_solar_judgment", display="日光审判", owner=MAGE, cost=4, ctype=Attack, rarity=Rare,
               keywords=[], actions=[atk(10, 200, reach=ReachAny)])
    write_card("p_anubis_avatar", display="阿努比斯化身", owner=MAGE, cost=6, ctype=Status, rarity=Legendary,
               keywords=["exhaust"], actions=[action(Type=ApplyAnubisAvatar, Target=Self)])
    write_card("p_solar_god_wrath", display="太阳神之怒", owner=MAGE, cost=4, ctype=Attack, rarity=Epic,
               keywords=["aoe"], actions=[
                   atk(8, 80, target=AllEnemies),
                   status("slow", 2, 2, DefaultEnemy, ReachAny),
               ])
    write_card("p_solar_blessing", display="太阳神的庇佑", owner=MAGE, cost=4, ctype=Defense, rarity=Epic,
               keywords=[], actions=team_def_block(50))

    # --- Demon ---
    write_card("d_shadow_claw", display="暗影爪击", owner=DEMON, cost=1, ctype=Attack, rarity=Common,
               keywords=[], actions=[atk(5, 80)])
    write_card("d_devil_touch", display="恶魔之触", owner=DEMON, cost=1, ctype=Attack, rarity=Common,
               keywords=[], actions=[atk(4, 50, lifesteal=100)])
    write_card("d_blood_armor", display="鲜血铠甲", owner=DEMON, cost=1, ctype=Defense, rarity=Common,
               keywords=["sacrifice"], actions=[self_dmg(3), def_block(5, 20)])
    write_card("d_blood_tail", display="血尾贯穿", owner=DEMON, cost=2, ctype=Attack, rarity=Rare,
               keywords=[], actions=[atk(5, 100, splash=True, splash_pct=80)])
    write_card("d_blood_flame", display="血焰爆发", owner=DEMON, cost=2, ctype=Attack, rarity=Rare,
               keywords=["sacrifice"], actions=[self_dmg(8), atk(10, 130)])
    write_card("d_soul_rip", display="灵魂撕裂", owner=DEMON, cost=2, ctype=Attack, rarity=Rare,
               keywords=[], actions=[atk(6, 80, ignore=100, reach=ReachAny)])
    write_card("d_dark_sacrifice", display="暗黑献祭", owner=DEMON, cost=3, ctype=Attack, rarity=Epic,
               keywords=["sacrifice"], actions=[self_dmg(15), atk(14, 170)])
    write_card("d_demon_pact", display="恶魔契约", owner=DEMON, cost=2, ctype=Status, rarity=Common,
               keywords=["sacrifice"], actions=[self_dmg(5), draw(2), status("attack_up", 3, 1, Self)])
    write_card("d_vamp_aura", display="吸血光环", owner=DEMON, cost=1, ctype=Status, rarity=Common,
               keywords=[], actions=[status("vamp_aura", 30, 1, Self)])
    write_card("d_curse_chain", display="诅咒之链", owner=DEMON, cost=2, ctype=Attack, rarity=Rare,
               keywords=[], actions=[
                   atk(5, 100, reach=ReachAny),
                   status("attack_down", 3, 2, DefaultEnemy, ReachAny),
               ])
    write_card("d_hell_fire", display="地狱烈焰", owner=DEMON, cost=3, ctype=Attack, rarity=Rare,
               keywords=["aoe", "sacrifice"], actions=[self_dmg(8), atk(6, 100, target=AllEnemies)])
    write_card("d_demon_lord", display="魔王降临", owner=DEMON, cost=4, ctype=Attack, rarity=Epic,
               keywords=["sacrifice"], actions=[self_dmg(20), atk(15, 200, reach=ReachAny, on_kill=30)])
    write_card("d_endless_blade", display="无尽血刃", owner=DEMON, cost=3, ctype=Attack, rarity=Legendary,
               keywords=["sacrifice"], actions=[self_dmg_pct(25), atk(10, 150, reach=ReachAny)])
    write_card("d_final_blood_ritual", display="最终鲜血仪式", owner=DEMON, cost=3, ctype=Status, rarity=Legendary,
               keywords=["exhaust"], actions=[status("final_blood_ritual", 1, -1, Self)])

    # 不写入 Character_*.asset 初始牌组（含测试卡 w_author_realm_strike）。
    # 牌组由 BalanceV2ContentGenerator / Grimhand → Generate Demo ScriptableObjects 维护。
    print("Done.")


if __name__ == "__main__":
    main()
