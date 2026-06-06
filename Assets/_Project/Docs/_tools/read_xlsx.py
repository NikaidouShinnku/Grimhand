import zipfile
import xml.etree.ElementTree as ET
import re
from pathlib import Path

xlsx = Path(r"c:\Users\Kelthuzad\Desktop\Gramhand实际卡牌遗物表.xlsx")
out = Path(__file__).resolve().parents[1] / "xlsx_dump.txt"

NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def col_row(ref):
    m = re.match(r"([A-Z]+)([0-9]+)", ref)
    col = 0
    for c in m.group(1):
        col = col * 26 + (ord(c) - 64)
    return col, int(m.group(2))


with zipfile.ZipFile(xlsx) as z:
    shared = []
    if "xl/sharedStrings.xml" in z.namelist():
        root = ET.fromstring(z.read("xl/sharedStrings.xml"))
        for si in root.findall(".//m:si", NS):
            texts = [t.text or "" for t in si.findall(".//m:t", NS)]
            shared.append("".join(texts))

    wb = ET.fromstring(z.read("xl/workbook.xml"))
    sheets = []
    for sh in wb.findall(".//m:sheet", NS):
        sheets.append(
            (
                sh.attrib.get("name"),
                sh.attrib.get(
                    "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"
                ),
            )
        )

    rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
    rid_to = {rel.attrib["Id"]: rel.attrib["Target"] for rel in rels}

    lines = []
    for name, rid in sheets:
        path = "xl/" + rid_to[rid].lstrip("/")
        if not path.startswith("xl/worksheets/"):
            continue
        root = ET.fromstring(z.read(path))
        rows = {}
        for c in root.findall(".//m:c", NS):
            ref = c.attrib.get("r", "")
            col, row = col_row(ref)
            v = c.find("m:v", NS)
            if v is None:
                continue
            val = v.text or ""
            if c.attrib.get("t") == "s":
                val = shared[int(val)]
            rows.setdefault(row, {})[col] = val

        lines.append(f"=== SHEET: {name} ===")
        if not rows:
            lines.append("(empty)")
            continue
        max_row = max(rows)
        max_col = max(max(r.keys()) for r in rows.values())
        for r in range(1, max_row + 1):
            line = [str(rows.get(r, {}).get(c, "")) for c in range(1, max_col + 1)]
            if any(x.strip() for x in line):
                lines.append("\t".join(line))
        lines.append("")

out.write_text("\n".join(lines), encoding="utf-8")
print(f"Wrote {out}")
