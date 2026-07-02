# -*- coding: utf-8 -*-
"""Patch Cost/CardType/Rarity of v0.9 毒蛇/巫妖 card assets to match the v0.9 overview table exactly.
Only rewrites the three scalar fields (and adds x_cost keyword for l_realm_seal); preserves Actions.

CardType: 攻击=0 防御=1 状态=2
CardRarity: 白=Common0 绿=Rare1 蓝=SuperRare2 紫=Epic3 橙=Legendary4
"""
import os, re

DIR = r"c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand\Assets\_Project\Data\Cards"

# id -> (cost, cardType, rarity, extra_keywords)
# cost: int; for X-cost use 1 and add 'x_cost' keyword
T = {
 "v_snake_bite":      (1, 0, 0, []),       # 蛇牙撕咬 攻击 白
 "v_python_guard":    (1, 1, 0, []),       # 蟒蛇守护 防御 白
 "v_scale_harden":    (1, 1, 0, []),       # 鳞片硬化 防御 白
 "v_poison_touch":    (1, 0, 0, []),       # 剧毒之触 攻击 白
 "v_queen_authority": (1, 2, 0, []),       # 女王威信 状态 白
 "v_tail_strike":     (1, 0, 1, []),       # 蛇尾突袭 攻击 绿
 "v_poison_scale":    (1, 1, 1, []),       # 毒鳞 防御 绿
 "v_snake_king_blessing": (2, 1, 1, []),   # 蛇王护佑 防御 绿
 "v_venom_spit":      (2, 0, 1, []),       # 毒液喷吐 攻击 绿
 "v_detonate_venom":  (2, 2, 2, []),       # 引爆毒囊 状态 蓝
 "v_digest_venom":    (0, 2, 2, []),       # 消化剧毒 状态 蓝
 "v_shed_skin":       (1, 2, 2, []),       # 蜕皮 状态 蓝
 "v_tongue_sense":    (0, 2, 2, []),       # 蛇信感知 状态 蓝
 "v_queen_prevention":(1, 1, 2, []),       # 女王的预防 防御 蓝
 "v_venom_sac_burst": (2, 2, 2, []),       # 毒囊破裂 状态 蓝
 "v_venom_feast":     (4, 2, 3, []),       # 毒裂盛宴 状态 紫
 "v_python_constrict":(4, 0, 3, []),       # 巨蟒绞杀 攻击 紫
 "v_immortal_shed":   (2, 2, 3, []),       # 不朽蛇蜕 状态 紫
 "v_poison_feedback": (3, 2, 3, []),       # 剧毒反哺 状态 紫
 "v_all_snakes_heart":(5, 0, 4, []),       # 万蛇噬心 攻击 橙
 "v_pray_ancient_god":(3, 2, 4, []),       # 祈求远古蛇神 状态 橙
 "v_snake_god_response": (0, 2, 4, []),    # 蛇神的回应 状态 橙
 "l_ethereal_form":   (1, 1, 0, []),       # 虚化形态 防御 白
 "l_void_gaze":       (1, 2, 0, []),       # 空洞凝视 状态 白
 "l_charge":          (1, 2, 0, []),       # 蓄能 状态 白
 "l_ghost_claw":      (1, 0, 0, []),       # 幽灵爪击 攻击 白
 "l_gather_energy":   (0, 2, 0, []),       # 聚能 状态 白
 "l_spirit_walk":     (2, 1, 1, []),       # 灵体漫步 防御 绿
 "l_psionic_cannon":  (2, 0, 1, []),       # 灵能炮 攻击 绿
 "l_dread_whisper":   (0, 2, 1, []),       # 恐惧低语 状态 绿
 "l_soul_storm":      (3, 0, 2, []),       # 灵魂风暴 攻击 蓝
 "l_two_realms_walker": (1, 2, 2, []),     # 两界行者 状态 蓝
 "l_soul_devour":     (1, 2, 2, []),       # 灵魂吞噬 状态 蓝
 "l_psionic_body":    (2, 2, 2, []),       # 灵能体 状态 蓝
 "l_soul_reinforce":  (2, 2, 2, []),       # 灵魂强化 状态 蓝
 "l_realm_burst":     (3, 0, 2, []),       # 灵界爆发 攻击 蓝
 "l_psionic_focus":   (2, 2, 2, []),       # 灵能聚集 状态 蓝
 "l_realm_seal":      (1, 2, 2, ["x_cost"]),# 灵界封印 X费 状态 蓝
 "l_soul_elegy":      (4, 0, 3, []),       # 灵魂挽歌 攻击 紫
 "l_summon_card_spirit": (3, 2, 3, []),    # 召唤卡牌之灵 状态 紫
 "l_summon_chaos_spirit": (3, 2, 3, []),   # 召唤混乱之灵 状态 紫
 "l_wall_of_sighs":   (2, 1, 3, []),       # 叹息之墙 防御 紫
 "l_despair_soul":    (0, 0, 3, []),       # 绝望之魂 攻击 紫
 "l_realm_descent":   (8, 2, 4, []),       # 灵界降临 状态 橙
 "l_super_psionic_cannon": (10, 0, 4, []), # 超级·无敌·灵能·巨炮 攻击 橙
 "l_eternal_void":    (5, 2, 4, []),       # 永恒虚无 状态 橙
}

def patch(path, cost, ctype, rarity, extra_kw):
    with open(path, "r", encoding="utf-8") as f:
        txt = f.read()
    lines = txt.split("\n")

    # 1) Patch top-level scalar fields (Cost/CardType/Rarity appear before Actions).
    patched = {"cost": False, "type": False, "rarity": False}
    for i, ln in enumerate(lines):
        if ln.startswith("  Cost: ") and not patched["cost"]:
            lines[i] = f"  Cost: {cost}"; patched["cost"] = True
        elif ln.startswith("  CardType: ") and not patched["type"]:
            lines[i] = f"  CardType: {ctype}"; patched["type"] = True
        elif ln.startswith("  Rarity: ") and not patched["rarity"]:
            lines[i] = f"  Rarity: {rarity}"; patched["rarity"] = True

    # 2) Inject extra keywords (x_cost) into the Keywords block if provided.
    if extra_kw:
        out = []
        i = 0
        while i < len(lines):
            ln = lines[i]
            if ln.startswith("  Keywords:"):
                existing = []
                j = i + 1
                while j < len(lines) and lines[j].startswith("  - "):
                    existing.append(lines[j][4:].strip())
                    j += 1
                merged = existing + [k for k in extra_kw if k not in existing]
                out.append("  Keywords:")
                for k in merged:
                    out.append(f"  - {k}")
                i = j
                continue
            out.append(ln)
            i += 1
        lines = out

    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    return all(patched.values())

def main():
    ok = 0
    for cid, (cost, ctype, rarity, extra) in T.items():
        ap = os.path.join(DIR, "Card_" + cid + ".asset")
        if not os.path.exists(ap):
            print("MISSING", cid); continue
        if patch(ap, cost, ctype, rarity, extra):
            ok += 1
        else:
            print("PATCH FAILED", cid)
    print(f"Patched {ok}/{len(T)} cards")

if __name__ == "__main__":
    main()
