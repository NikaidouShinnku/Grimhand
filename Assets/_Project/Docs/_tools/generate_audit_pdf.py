# -*- coding: utf-8 -*-
"""Generate Grimhand talent/altar/event audit checklist PDF (reportlab)."""
from __future__ import annotations

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[1]
OUT_PDF = ROOT / "Grimhand_Talent_Altar_Event_Audit_Checklist.pdf"
FONT_PATH = Path(r"C:\Windows\Fonts\simhei.ttf")

ISSUES = [
    ("Cross-system", "跨系统", [
        ("X-01", "High", "HP 三层不同步：战斗内 / member.MaxHp / UI 重算（ApplyPartyProgress、CaptureParty、ApplyTeamHpBonus、ExpeditionPartyStatsRules）"),
        ("X-02", "High", "Msg() / TeamHpThen() 副作用过早：选项点击时执行 action，而非交互步骤结束（ExpeditionEventPlanner）"),
    ]),
    ("Fixed", "已修复 · 待核对", [
        ("F-01", "Fixed", "战阵鼓舞 +10 被遗物 TeamHpBonus 门控（RelicBattleRules.ApplyTeamHpBonus）"),
        ("F-02", "Fixed", "战后 BonusCards 因 Party.Clear 丢失（ExpeditionEngine.OnBattleFinished）"),
        ("F-03", "Fixed", "祭坛三人同时确认（TryConfirmCardAltar 批量应用）"),
        ("F-04", "Fixed", "收藏取出进度持久化（RunStartCampDecks + ExtractedCampCollectionIndices）"),
        ("F-05", "Partial", "背包 HP 升级后丢失 +10（ExpeditionPartyStatsRules + GrantXpToParty；战间快照仍可能漂移）"),
        ("F-06", "Fixed", "悬停角色显示卡组（CombatantDetailPopupView 已移除卡组列表）"),
        ("F-07", "Partial", "铁匠融合选牌时序（ShowMessage 延迟应用；TryFuseCards 仍有 bug）"),
    ]),
    ("Talent", "天赋", [
        ("T-01", "Critical", "局外天赋无持久化：CampMetaState 仅内存，重启丢失（GameFlowController / CampMetaState）"),
        ("T-02", "High", "战士战死后 member.MaxHp 不回落，未 SyncPartyEffectiveMaxHp（OnBattleFinished）"),
        ("T-03", "High", "战间 vs 下一场：鼓舞判定不一致（Hp=0 无 +10，下一场 StartHp=1 又 +10）"),
        ("T-04", "High", "InitPartyFromTemplate / InitPartyAtLevel 不带 SelectedTalentSlot*Id"),
        ("T-05", "High", "OutOfRunLevel 无增长逻辑，非 demo 天赋永久锁定"),
        ("T-06", "High", "毒爆 talent_mage_s2_lv10：stacks 平方 x damage，与文案/正常 tick 不符"),
        ("T-07", "High", "CloneModifiers 缺 TeamHpBonus、献祭 percent 等字段"),
        ("T-08", "Medium", "天赋无角色归属校验（TryToggleSelection / CollectTalentId）"),
        ("T-09", "Medium", "等级不足时不自动卸下已选天赋"),
        ("T-10", "Medium", "余护甲回血 talent_knight_s1_lv5 作用全队而非自身"),
        ("T-11", "Medium", "绝地格挡 talent_knight_s1_lv10：减伤而非增加 Block"),
        ("T-12", "Medium", "镜像护甲 talent_mage_s1_lv1：凡 GainBlock 均触发"),
        ("T-13", "Medium", "无尽血刃注入 Cost=1 与资产不一致，catalog 缺失时无效"),
        ("T-14", "Medium", "CampRunPartyApplier meta==null 时静默丢天赋"),
        ("T-15", "Low", "恶魔 slot2 缺 Lv.10 天赋，树不对称"),
        ("T-16", "Low", "ranger_s2_lv4 重复设折扣标记（无功能影响）"),
        ("T-17", "Low", "TalentCampOverlayView 关闭时才 OnMetaSaved"),
    ]),
    ("Altar", "祭坛", [
        ("A-01", "Medium", "祭坛确认失败无 UI 反馈，LastEventMessage 未显示（ExpeditionCardAltarOverlayView）"),
        ("A-02", "Medium", "ApplyCardAltarExtraction 忽略 TryReplaceAndAdd 返回值，可能 MarkExtracted 但卡未进组"),
        ("A-03", "Medium", "TryConfirmCardAltar pending 为空仍 CompleteCurrentNode（可跳过节点）"),
        ("A-04", "Medium", "ResolvePendingConsumableOffer 清 PendingCardOffer，与卡组满替换冲突"),
        ("A-05", "Medium", "CaptureParty Combatants==null 时不走 existingParty 兜底"),
        ("A-06", "Medium", "StartRun 无 roster 时不填 RunStartCampDecks，祭坛收藏为空"),
        ("A-07", "Low", "缺满 10 张祭坛替换集成测试"),
        ("A-08", "Low", "任一角色未选替换则全队确认禁用，无法部分确认"),
        ("A-09", "Low", "已取出槽位 draft 静默 continue"),
        ("A-10", "Low", "TryRemoveExactEntry BonusIndex 失效时可能误删基础牌"),
        ("A-11", "Low", "ExpeditionCardOfferContext.Altar 枚举死代码，双轨设计"),
        ("A-12", "Low", "祭坛成员 Tab 窄屏可能裁切"),
    ]),
    ("Event", "事件", [
        ("E-01", "Critical", "融合同队员两张 Bonus 卡：按 index 顺序删导致错位（TryFuseCards）"),
        ("E-02", "Critical", "TryFuseCards 忽略 TryRemoveExactEntry 返回值"),
        ("E-03", "Critical", "融合卡组满：先删两张再 PendingCardOffer，放弃则净亏两张"),
        ("E-04", "Critical", "旅者礼物：诅咒可 AbandonCardOffer 跳过，遗物照拿（PlanTravelerGift）"),
        ("E-05", "Critical", "ShowMessage 后 ApplyPendingCardAction 失败导致事件软锁（EventInteractSequenceView）"),
        ("E-06", "High", "融合结果 owner 随机，与选牌角色无关"),
        ("E-07", "High", "TryRollCardRewardForMember 失败时 clone 素材并改 OwnerCharacterId，可能错配专属卡"),
        ("E-08", "High", "旅者礼物 PendingCardOffer 与 EventInteraction UI 叠层"),
        ("E-09", "High", "ApplyPendingCardAction 失败无恢复路径"),
        ("E-10", "Medium", "TeamHpThen / TeamHealThen 的 after 在 HP 动画前执行"),
        ("E-11", "Medium", "PlanAbyssSacrifice DeferredOutcome=Msg 构造时即 ATK+1"),
        ("E-12", "Medium", "PlanBuyRandomCard 先扣金 roll 失败无退款"),
        ("E-13", "Medium", "旅者礼物诅咒在 ShowMessage 前已入队，文案不同步"),
        ("E-14", "Medium", "赌徒大赌 roll 空遗物仍显示获得稀有遗物"),
        ("E-15", "Medium", "融合后卡组满无日志提示需处理替换"),
        ("E-16", "Medium", "事件战失败未清 PendingEventBattleBonusXp"),
        ("E-17", "Low", "Legendary+Legendary 融合池空时净损一张"),
        ("E-18", "Low", "RequiredFusionType 字段未使用"),
        ("E-19", "Low", "知识祭坛 / 灵魂祭坛选项仍为占位"),
        ("E-20", "Low", "血祭坛选项 A 固定改 Party[0]"),
        ("E-21", "Low", "混沌祭坛 RemoveRandomBonusCard 非随机队员"),
    ]),
]

PHASES = [
    ("阶段 1 · 数据安全", "E-01~E-05, A-02, E-04", "防丢牌、防软锁、防 exploit"),
    ("阶段 2 · HP / 属性", "X-01, T-02, T-03, F-05", "单一真相源，战后 Sync"),
    ("阶段 3 · 事件时序", "X-02, E-10~E-14, E-11", "Planner 副作用延后"),
    ("阶段 4 · 天赋 Meta", "T-01, T-04~T-07, T-08~T-14", "持久化、校验、公式核对"),
    ("阶段 5 · 祭坛 UX", "A-01~A-06, A-07~A-12", "反馈、边界、测试"),
    ("阶段 6 · 占位内容", "E-19~E-21, T-15~T-17", "未实现事件 / 低优先级"),
]

TESTS = [
    "祭坛：满 10 张替换、确认失败 UI、空 draft 引擎确认",
    "融合：同队员双 Bonus、失败回滚、满组放弃、owner 规则",
    "事件：ShowMessage 软锁、TravelerGift 诅咒不可 skip",
    "天赋：各钩子抽样、SyncRunStateFromBattle、战后 HP 同步",
    "Meta：CampMetaState 序列化/加载、OutOfRunLevel 成长",
]

SEV_COLOR = {
    "Critical": colors.HexColor("#DC5046"),
    "High": colors.HexColor("#E68C3C"),
    "Medium": colors.HexColor("#DCB43C"),
    "Low": colors.HexColor("#78A0C8"),
    "Fixed": colors.HexColor("#50A064"),
    "Partial": colors.HexColor("#649682"),
}


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("SimHei", str(FONT_PATH)))


def build_styles():
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "title",
            fontName="SimHei",
            fontSize=20,
            leading=26,
            textColor=colors.HexColor("#141E32"),
            spaceAfter=8,
        ),
        "subtitle": ParagraphStyle(
            "subtitle",
            fontName="SimHei",
            fontSize=10,
            leading=15,
            textColor=colors.HexColor("#333333"),
            spaceAfter=10,
        ),
        "section": ParagraphStyle(
            "section",
            fontName="SimHei",
            fontSize=12,
            leading=16,
            textColor=colors.white,
            backColor=colors.HexColor("#234B78"),
            spaceBefore=8,
            spaceAfter=6,
            leftIndent=6,
        ),
        "cell": ParagraphStyle(
            "cell",
            fontName="SimHei",
            fontSize=8,
            leading=11,
            alignment=TA_LEFT,
        ),
        "head": ParagraphStyle(
            "head",
            fontName="SimHei",
            fontSize=8.5,
            leading=11,
            textColor=colors.HexColor("#1A1A1A"),
        ),
        "body": ParagraphStyle(
            "body",
            fontName="SimHei",
            fontSize=10,
            leading=15,
        ),
    }


def issue_table(rows, styles) -> Table:
    data = [[
        Paragraph("<b>编号</b>", styles["head"]),
        Paragraph("<b>严重度</b>", styles["head"]),
        Paragraph("<b>修复</b>", styles["head"]),
        Paragraph("<b>核对</b>", styles["head"]),
        Paragraph("<b>问题摘要 / 位置</b>", styles["head"]),
    ]]
    style_cmds = [
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#E6EBF2")),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#BBBBBB")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("ALIGN", (0, 0), (3, -1), "CENTER"),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]
    for i, (code, sev, summary) in enumerate(rows, start=1):
        data.append([
            Paragraph(code, styles["cell"]),
            Paragraph(sev, styles["cell"]),
            Paragraph("[ ]", styles["cell"]),
            Paragraph("[ ]", styles["cell"]),
            Paragraph(summary.replace("&", "&amp;"), styles["cell"]),
        ])
        bg = SEV_COLOR.get(sev, colors.lightgrey)
        style_cmds.append(("BACKGROUND", (1, i), (1, i), bg))
        if sev in ("Fixed", "Partial", "Critical", "High"):
            style_cmds.append(("TEXTCOLOR", (1, i), (1, i), colors.white))

    col_widths = [14 * mm, 18 * mm, 12 * mm, 12 * mm, 124 * mm]
    table = Table(data, colWidths=col_widths, repeatRows=1)
    table.setStyle(TableStyle(style_cmds))
    return table


def build() -> None:
    register_fonts()
    styles = build_styles()
    OUT_PDF.parent.mkdir(parents=True, exist_ok=True)

    doc = SimpleDocTemplate(
        str(OUT_PDF),
        pagesize=A4,
        leftMargin=14 * mm,
        rightMargin=14 * mm,
        topMargin=16 * mm,
        bottomMargin=16 * mm,
        title="Grimhand Audit Checklist",
    )

    story = []
    story.append(Paragraph("Grimhand 修复核对清单", styles["title"]))
    story.append(Paragraph("天赋 · 祭坛 · 事件", styles["title"]))
    story.append(Spacer(1, 4))
    story.append(Paragraph(
        "用途：按编号逐项修复，完成后在「修复」与「核对」列打勾。<br/>"
        "生成日期：2026-05-27<br/>"
        "说明：「已修复」项仍建议回归测试后再勾选核对。",
        styles["subtitle"],
    ))

    story.append(Paragraph("建议使用修复顺序", styles["section"]))
    phase_data = [["阶段", "编号范围", "目标"]]
    for phase, codes, note in PHASES:
        phase_data.append([phase, codes, note])
    phase_table = Table(phase_data, colWidths=[38 * mm, 42 * mm, 100 * mm])
    phase_table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#E6EBF2")),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#BBBBBB")),
        ("FONTNAME", (0, 0), (-1, -1), "SimHei"),
        ("FONTSIZE", (0, 0), (-1, -1), 8.5),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
    ]))
    story.append(phase_table)
    story.append(Spacer(1, 8))

    for _, title_cn, rows in ISSUES:
        story.append(Paragraph(title_cn, styles["section"]))
        story.append(issue_table(rows, styles))
        story.append(Spacer(1, 6))

    story.append(PageBreak())
    story.append(Paragraph("回归测试清单（全部修复后勾选）", styles["section"]))
    test_rows = [(f"TEST-{i:02d}", "Medium", t) for i, t in enumerate(TESTS, 1)]
    story.append(issue_table(test_rows, styles))
    story.append(Spacer(1, 10))

    story.append(Paragraph("最终核对签字", styles["section"]))
    story.append(Paragraph(
        "修复负责人：________________　　日期：________________<br/>"
        "测试负责人：________________　　日期：________________<br/><br/>"
        "全部 Critical / High 已关闭：　[ ] 是　　[ ] 否<br/>"
        "全部项已勾选「核对」列：　　　[ ] 是　　[ ] 否<br/><br/>"
        "备注：<br/>"
        "________________________________________________________________________<br/>"
        "________________________________________________________________________",
        styles["body"],
    ))

    doc.build(story)


if __name__ == "__main__":
    build()
    print("PDF generated")
