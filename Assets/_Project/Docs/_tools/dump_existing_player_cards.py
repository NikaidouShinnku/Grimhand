"""Map existing player card assets to (CardId, DisplayName, Owner, Cost, CardType, Rarity, Keywords, #Actions)."""
import os, re, json
from pathlib import Path

ROOT = Path(r"c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand\Assets\_Project\Data\Cards")

# parse simple YAML-ish fields from MonoBehaviours
def parse_asset(p: Path):
    txt = p.read_text(encoding="utf-8")
    def g(field, default=""):
        m = re.search(rf"^\s*{field}:\s*(.*?)$", txt, re.M)
        if not m:
            return default
        v = m.group(1).strip()
        # decode quoted unicode
        if v.startswith('"'):
            v = bytes(v[1:-1], "utf-8").decode("unicode_escape")
        return v
    def gfloat(field, default=""):
        m = re.search(rf"^\s*{field}:\s*(.*?)$", txt, re.M)
        return m.group(1).strip() if m else default
    # find Keywords list
    kw_block = re.search(r"Keywords:\s*\n((?:\s*-\s*.*\n)*)", txt)
    kws = []
    if kw_block:
        kws = [l.strip("- ").strip() for l in kw_block.group(1).splitlines() if l.strip()]
    # count Actions
    n_actions = len(re.findall(r"^\s*-\s*Type:\s*\d+", txt, re.M))
    return {
        "file": p.name,
        "CardId": g("CardId"),
        "DisplayName": g("DisplayName"),
        "OwnerCharacterId": g("OwnerCharacterId"),
        "Cost": g("Cost"),
        "CardType": g("CardType"),
        "Rarity": g("Rarity"),
        "Keywords": kws,
        "NumActions": n_actions,
    }

rows = []
for prefix in ("Card_w_", "Card_p_", "Card_d_"):
    for f in sorted(ROOT.glob(f"{prefix}*.asset")):
        rows.append(parse_asset(f))

# Print as TSV-ish
for r in rows:
    print(f"{r['OwnerCharacterId']:12} | {r['CardId']:28} | cost={r['Cost']:2} | type={r['CardType']} | rar={r['Rarity']} | kw={r['Keywords']} | nA={r['NumActions']} | name={r['DisplayName']}")

# dump json
out = Path(__file__).parent / "_existing_player_cards.json"
out.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"\nWrote {out}")
