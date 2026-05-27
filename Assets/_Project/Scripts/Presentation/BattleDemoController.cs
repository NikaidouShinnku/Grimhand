using System.Collections.Generic;
using System.Text;
using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation
{
    public sealed class BattleDemoController : MonoBehaviour
    {
        const float ReferenceWidth = 1280f;
        const float ReferenceHeight = 720f;
        const float MaxContentWidth = 1100f;

        [SerializeField] BattleSetupSO battleSetup;

        BattleEngine _engine;
        readonly List<string> _log = new();
        Vector2 _mainScroll;
        Vector2 _logScroll;
        Vector2 _handScroll;

        GUIStyle _titleStyle;
        GUIStyle _labelStyle;
        GUIStyle _hintStyle;
        GUIStyle _buttonStyle;
        GUIStyle _cardButtonStyle;
        GUIStyle _slotStyle;
        GUIStyle _boxStyle;
        float _lastScale;

        void Start()
        {
            RestartBattle();
        }

        void RestartBattle()
        {
            var config = battleSetup != null
                ? battleSetup.ToBattleConfig()
                : DemoBattleFactory.CreateDefault3v3();
            config.Seed = Random.Range(1, int.MaxValue);
            _engine = new BattleEngine(config);
            _log.Clear();
            _engine.StartBattle();
            AppendEngineEvents();
            var players = 0;
            var enemies = 0;
            foreach (var c in config.Combatants)
            {
                if (c.Team == TeamSide.Player) players++;
                else enemies++;
            }

            var source = battleSetup != null ? "SO" : "代码";
            Log($"战斗开始 — {players}v{enemies} ({source}, 种子 {config.Seed})");
        }

        void OnGUI()
        {
            var scale = ComputeScale();
            EnsureStyles(scale);

            var matrixBackup = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var vw = Screen.width / scale;
            var vh = Screen.height / scale;
            var pad = 16f;
            var contentW = Mathf.Min(vw - pad * 2f, MaxContentWidth);
            var contentX = (vw - contentW) * 0.5f;

            if (_engine == null)
            {
                GUI.Label(new Rect(contentX, pad, contentW, 40), "战斗引擎未初始化…", _labelStyle);
                GUI.matrix = matrixBackup;
                return;
            }

            var state = _engine.State;
            var topH = 52f;
            var bottomH = 40f;
            var topY = pad;

            GUI.Box(new Rect(contentX, topY, contentW, topH), "", _boxStyle);
            GUI.Label(new Rect(contentX + 8, topY + 4, contentW - 16, 22),
                $"Grimhand Demo  |  回合{state.TurnNumber}  {state.Phase}  能量{GetEnergyLabel()}  {state.Outcome}",
                _titleStyle);
            GUI.Label(new Rect(contentX + 8, topY + 26, contentW - 16, 20),
                GetBattleSummary(state), _hintStyle);

            var scrollY = topY + topH + 6f;
            var scrollH = vh - scrollY - bottomH - pad;
            var innerW = contentW - 22f;
            var innerH = MeasureScrollContentHeight(state, innerW);

            _mainScroll = GUI.BeginScrollView(
                new Rect(contentX, scrollY, contentW, scrollH),
                _mainScroll,
                new Rect(0, 0, innerW, innerH));

            var cy = 0f;
            cy += DrawSectionBox(0, cy, innerW, "战场 (前/中/后排)", 78f, (bx, by, bw, bh) =>
            {
                DrawFormationBoardCompact(bx, by, bw);
            });

            if (state.Phase == TurnPhase.Planning && _engine.Draft.AwaitingTargetCardId != null)
            {
                cy += DrawSectionBox(0, cy, innerW, "选择目标", 40f, (bx, by, bw, bh) =>
                {
                    DrawTargetButtons(bx, by, bw);
                });
            }

            if (state.Phase == TurnPhase.Planning && state.EnemyIntents.Count > 0)
            {
                cy += DrawSectionBox(0, cy, innerW,
                    $"敌方意图 ({CountAliveEnemies(state)}敌 · 本回合{state.EnemyIntents.Count}张牌)",
                    58f, (bx, by, bw, bh) =>
                    {
                        DrawEnemyIntentsCompact(bx, by, bw);
                    });
            }

            const float cardW = 108f;
            const float cardH = 86f;
            const float handSectionH = 108f;
            cy += DrawSectionBox(0, cy, innerW,
                $"手牌 {state.PlayerHand.Count}/{state.Config.HandLimit}  (横向可滚)",
                handSectionH, (bx, by, bw, bh) =>
                {
                    _handScroll = GUI.BeginScrollView(
                        new Rect(bx, by, bw, bh),
                        _handScroll,
                        new Rect(0, 0, Mathf.Max(bw, state.PlayerHand.Count * (cardW + 6)), cardH));

                    var hx = 2f;
                    foreach (var card in state.PlayerHand)
                    {
                        var selected = _engine.Draft.IsSelected(card.InstanceId);
                        var polluted = CardRules.IsPolluted(card);
                        var canAfford = _engine.Draft.EnergyRemaining >= card.Cost;
                        GUI.enabled = state.Phase == TurnPhase.Planning && !polluted && (selected || canAfford);

                        var label = BuildCardLabelCompact(card);
                        if (polluted)
                            label = "[污]" + label;
                        if (selected)
                            label = "★" + label;

                        if (GUI.Button(new Rect(hx, 0, cardW, cardH), label, _cardButtonStyle))
                            _engine.ToggleCardSelection(card.InstanceId);

                        hx += cardW + 6f;
                    }

                    GUI.enabled = true;
                    GUI.EndScrollView();
                });

            const float logSectionH = 160f;
            cy += DrawSectionBox(0, cy, innerW, "事件日志 (区域内可滚)", logSectionH, (bx, by, bw, bh) =>
            {
                const float lineH = 18f;
                _logScroll = GUI.BeginScrollView(
                    new Rect(bx, by, bw, bh),
                    _logScroll,
                    new Rect(0, 0, bw - 4, _log.Count * lineH + 4));
                for (var i = 0; i < _log.Count; i++)
                    GUI.Label(new Rect(2, i * lineH, bw - 8, lineH), _log[i], _compactStyle);
                GUI.EndScrollView();
            });

            GUI.EndScrollView();

            var btnY = vh - pad - bottomH;
            var btnW = 118f;
            GUI.enabled = state.Phase == TurnPhase.Planning && _engine.Draft.SelectedQueue.Count > 0;
            if (GUI.Button(new Rect(contentX, btnY, btnW, bottomH - 4), "确认出牌", _buttonStyle))
            {
                _engine.CommitPlayerPlan();
                AppendEngineEvents();
            }

            GUI.enabled = state.Phase == TurnPhase.Planning;
            if (GUI.Button(new Rect(contentX + btnW + 6, btnY, btnW, bottomH - 4), "空过", _buttonStyle))
            {
                _engine.SkipPlayerTurn();
                AppendEngineEvents();
            }

            GUI.enabled = true;
            if (GUI.Button(new Rect(contentX + (btnW + 6) * 2, btnY, btnW, bottomH - 4), "重开战斗", _buttonStyle))
                RestartBattle();

            GUI.Label(new Rect(contentX + (btnW + 6) * 3 + 8, btnY + 8, contentW - (btnW + 6) * 3 - 16, 24),
                "滚轮浏览 | 攻击/减益需选敌", _hintStyle);

            GUI.matrix = matrixBackup;
        }

        GUIStyle _compactStyle;

        float MeasureScrollContentHeight(BattleState state, float innerW)
        {
            var h = 78f + 108f + 160f + 24f;
            if (state.Phase == TurnPhase.Planning && _engine.Draft.AwaitingTargetCardId != null)
                h += 40f;
            if (state.Phase == TurnPhase.Planning && state.EnemyIntents.Count > 0)
                h += 58f;
            return h;
        }

        float DrawSectionBox(float x, float y, float width, string title, float height, System.Action<float, float, float, float> drawBody)
        {
            GUI.Box(new Rect(x, y, width, height), "", _boxStyle);
            GUI.Label(new Rect(x + 6, y + 2, width - 12, 18), title, _compactStyle);
            drawBody(x + 4, y + 20, width - 8, height - 22);
            return height + 6f;
        }

        static float ComputeScale()
        {
            var scaleW = Screen.width / ReferenceWidth;
            var scaleH = Screen.height / ReferenceHeight;
            // 缩小以适配窗口，避免 1366x768 等分辨率下 UI 被放大裁切
            return Mathf.Clamp(Mathf.Min(scaleW, scaleH), 0.72f, 1f);
        }

        void EnsureStyles(float scale)
        {
            if (_titleStyle != null && Mathf.Approximately(_lastScale, scale))
                return;

            _lastScale = scale;
            var fontSize = 13;
            var titleSize = 14;
            var buttonSize = 14;
            var cardFont = 12;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                richText = false
            };

            _hintStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic
            };

            _compactStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 12,
                wordWrap = false
            };

            _titleStyle = new GUIStyle(_labelStyle) { fontSize = titleSize, fontStyle = FontStyle.Bold };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = buttonSize,
                padding = new RectOffset(6, 6, 4, 4)
            };

            _cardButtonStyle = new GUIStyle(_buttonStyle)
            {
                fontSize = cardFont,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(4, 4, 4, 4)
            };

            _slotStyle = new GUIStyle(_compactStyle)
            {
                fontSize = 11,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };

            _boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
        }

        static string GetBattleSummary(BattleState state)
        {
            var players = 0;
            var enemies = 0;
            foreach (var c in state.Combatants)
            {
                if (!c.IsAlive)
                    continue;
                if (c.Team == TeamSide.Player)
                    players++;
                else
                    enemies++;
            }

            return $"Demo · {players}我方 vs {enemies}敌方 · 攻击/减益选目标 · 空过=不出牌";
        }

        static int CountAliveEnemies(BattleState state)
        {
            var n = 0;
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Enemy && c.IsAlive)
                    n++;
            }

            return n;
        }

        static string SlotLabel(FormationSlot slot)
        {
            switch (slot)
            {
                case FormationSlot.Front: return "前排";
                case FormationSlot.Middle: return "中排";
                case FormationSlot.Back: return "后排";
                default: return slot.ToString();
            }
        }

        static string DescribeEffectTarget(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.DefaultEnemy: return "默认敌人(前排优先)";
                case EffectTarget.ManualSelected: return "手动选目标";
                case EffectTarget.EnemyFrontSlot: return "敌前排槽位";
                case EffectTarget.EnemyMiddleSlot: return "敌中排槽位";
                case EffectTarget.EnemyBackSlot: return "敌后排槽位";
                case EffectTarget.Self: return "自身";
                case EffectTarget.FrontAlly: return "前排友军";
                case EffectTarget.BackAlly: return "后排友军";
                default: return target.ToString();
            }
        }

        static EffectTarget GetPrimaryTarget(CardInstanceState card)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;
                return action.Target;
            }

            return EffectTarget.DefaultEnemy;
        }

        void DrawFormationBoardCompact(float x, float y, float width)
        {
            var slotW = (width - 12f) / 3f;
            var lineH = 16f;
            var slots = new[] { FormationSlot.Front, FormationSlot.Middle, FormationSlot.Back };

            GUI.Label(new Rect(x, y, 40, lineH), "我方", _compactStyle);
            for (var i = 0; i < slots.Length; i++)
                DrawFormationSlotLine(x + 40f + i * slotW, y, slotW - 4f, lineH, TeamSide.Player, slots[i]);

            var y2 = y + lineH + 4f;
            GUI.Label(new Rect(x, y2, 40, lineH), "敌方", _compactStyle);
            for (var i = 0; i < slots.Length; i++)
                DrawFormationSlotLine(x + 40f + i * slotW, y2, slotW - 4f, lineH, TeamSide.Enemy, slots[i]);

            var y3 = y2 + lineH + 4f;
            GUI.Label(new Rect(x, y3, width, lineH),
                "说明: 攻击/对敌状态需选目标 | 自身防/治疗/按槽位(缚足)自动 | 可点「空过」", _hintStyle);
        }

        void DrawFormationSlotLine(float x, float y, float w, float h, TeamSide team, FormationSlot slot)
        {
            var unit = FindCombatantInSlot(team, slot);
            var text = unit == null
                ? $"{SlotLabel(slot)}: —"
                : FormatUnitLine(unit);

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = team == TeamSide.Player
                ? new Color(0.3f, 0.5f, 0.75f, 1f)
                : new Color(0.7f, 0.32f, 0.32f, 1f);
            GUI.Label(new Rect(x, y, w, h), text, _slotStyle);
            GUI.backgroundColor = prev;
        }

        static string FormatUnitLine(CombatantState unit)
        {
            var status = FormatStatusList(unit);
            var core = $"{SlotLabel(unit.Slot)}:{unit.DisplayName} HP{unit.Hp}/{unit.MaxHp} 甲{unit.Block} 攻{unit.Attack} 速{StatusRules.GetEffectiveSpeed(unit)}";
            return string.IsNullOrEmpty(status) ? core : core + " " + status;
        }

        CombatantState FindCombatantInSlot(TeamSide team, FormationSlot slot)
        {
            foreach (var c in _engine.State.Combatants)
            {
                if (c.Team == team && c.IsAlive && c.Slot == slot)
                    return c;
            }

            return null;
        }

        static string FormatStatusList(CombatantState unit)
        {
            if (unit.Statuses.Count == 0)
                return "";

            var sb = new StringBuilder();
            for (var i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                if (i > 0) sb.Append(" ");
                sb.Append($"{s.StatusId}x{s.Stacks}");
            }

            return sb.ToString();
        }

        static string ShortOwner(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return "?";
            if (ownerId.StartsWith("char_"))
                return ownerId.Substring(5);
            return ownerId;
        }

        void DrawTargetButtons(float x, float y, float width)
        {
            var awaitingId = _engine.Draft.AwaitingTargetCardId;
            if (awaitingId == null)
                return;

            var card = _engine.State.GetCard(awaitingId.Value);
            if (card == null)
                return;

            var ownerId = PositionRules.GetOwnerCombatantId(_engine.State, card);
            var owner = ownerId != null ? _engine.State.GetCombatant(ownerId) : null;
            var targets = CardRules.GetValidTargetCandidates(_engine.State, card, owner);

            var bx = x;
            foreach (var target in targets)
            {
                var slotLabel = "";
                switch (target.Slot)
                {
                    case FormationSlot.Front: slotLabel = "前排"; break;
                    case FormationSlot.Middle: slotLabel = "中排"; break;
                    case FormationSlot.Back: slotLabel = "后排"; break;
                }
                var label = $"{target.DisplayName} {slotLabel} HP{target.Hp}";
                if (GUI.Button(new Rect(bx, y, 150, 22), label, _buttonStyle))
                {
                    _engine.Draft.TryAssignTargetAndSelect(target.Id);
                    AppendEngineEvents();
                }

                bx += 156f;
            }

            if (GUI.Button(new Rect(bx, y, 60, 22), "取消", _buttonStyle))
                _engine.Draft.CancelAwaitingTarget();
        }

        string GetEnergyLabel()
        {
            if (_engine == null)
                return "-";

            var state = _engine.State;
            return $"{state.EnergyCurrent} / {state.EnergyMax}";
        }

        string BuildCardLabelCompact(CardInstanceState card)
        {
            var ownerId = PositionRules.GetOwnerCombatantId(_engine.State, card);
            var owner = ownerId != null ? _engine.State.GetCombatant(ownerId) : null;
            var power = CardPowerRules.GetEffectivePower(card, owner);
            var powerLabel = CardPowerRules.GetPowerLabel(card);
            var targetShort = DescribePickSide(CardRules.GetRequiredTargetPick(card));
            var assignedId = _engine.Draft.GetAssignedTarget(card.InstanceId);
            if (!string.IsNullOrEmpty(assignedId))
            {
                var assigned = _engine.State.GetCombatant(assignedId);
                if (assigned != null)
                    targetShort += $"→{assigned.DisplayName}";
            }

            return $"{card.DisplayName}\n费{card.Cost} {powerLabel}{power} | {targetShort}\n{ShortOwner(card.OwnerCharacterId)}";
        }

        static string DescribePickSide(TargetPickSide side)
        {
            switch (side)
            {
                case TargetPickSide.Enemy: return "需选敌";
                case TargetPickSide.Ally: return "需选友";
                default: return "自动";
            }
        }

        static string ShortTargetLabel(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.DefaultEnemy: return "默认敌";
                case EffectTarget.ManualSelected: return "选手动";
                case EffectTarget.EnemyBackSlot: return "敌后排";
                case EffectTarget.EnemyFrontSlot: return "敌前排";
                case EffectTarget.Self: return "自身";
                default: return target.ToString();
            }
        }

        void DrawEnemyIntentsCompact(float x, float y, float width)
        {
            var slotW = (width - 12f) / Mathf.Max(1, _engine.State.EnemyIntents.Count);
            var ix = x;
            var order = 1;

            foreach (var intent in _engine.State.EnemyIntents)
            {
                var card = _engine.State.GetCard(intent.CardInstanceId);
                if (card == null)
                    continue;

                var owner = !string.IsNullOrEmpty(intent.OwnerCombatantId)
                    ? _engine.State.GetCombatant(intent.OwnerCombatantId)
                    : null;
                if (owner == null)
                {
                    var ownerId = PositionRules.GetOwnerCombatantId(_engine.State, card);
                    owner = ownerId != null ? _engine.State.GetCombatant(ownerId) : null;
                }

                var actorName = owner != null ? owner.DisplayName : "敌";
                string label;
                if (intent.IsHidden)
                    label = $"#{order} ?  {actorName}";
                else
                {
                    var effect = CardPowerRules.DescribeCardEffect(card, owner, false);
                    label = $"#{order} {card.DisplayName} 费{card.Cost} {effect} ({actorName})";
                }

                var prev = GUI.backgroundColor;
                GUI.backgroundColor = intent.IsHidden
                    ? new Color(0.45f, 0.45f, 0.45f, 1f)
                    : new Color(0.65f, 0.35f, 0.35f, 1f);
                GUI.Label(new Rect(ix, y, slotW - 4f, 32), label, _slotStyle);
                GUI.backgroundColor = prev;
                ix += slotW;
                order++;
            }
        }

        void AppendEngineEvents()
        {
            foreach (var e in _engine.Events)
            {
                string line;
                switch (e.Kind)
                {
                    case BattleEventKind.PhaseChanged:
                        line = $"→ 阶段: {e.Phase}";
                        break;
                    case BattleEventKind.EnergyChanged:
                        line = $"能量: {e.EnergyRemaining} / {e.EnergyMax}";
                        break;
                    case BattleEventKind.CardSelectedForPlay:
                        line = $"预选 #{e.CardInstanceId} (能量 {e.EnergyRemaining}/{e.EnergyMax})";
                        break;
                    case BattleEventKind.CardDeselectedFromPlay:
                        line = $"取消预选 #{e.CardInstanceId} (能量 {e.EnergyRemaining}/{e.EnergyMax})";
                        break;
                    case BattleEventKind.DeckPolluted:
                        line = $"牌堆污染: {e.CombatantId} ({e.Amount} 张)";
                        break;
                    case BattleEventKind.TargetSelectionRequired:
                        line = $"请选择目标: {e.Message}";
                        break;
                    case BattleEventKind.EnemyIntentPrepared:
                        line = $"敌方意图: {e.Message}";
                        break;
                    case BattleEventKind.StatusApplied:
                        line = FormatStatusLog(e);
                        break;
                    case BattleEventKind.StatusTickDamage:
                        line = $"状态伤害 {e.Amount} ({e.Message}) → {e.CombatantId}";
                        break;
                    case BattleEventKind.ReactionTriggered:
                        line = $"弹反/应对: {CombatantLabel(e.CombatantId)} — {e.Message}";
                        break;
                    case BattleEventKind.PositionSwapped:
                        line = $"换位: {e.CombatantId} ↔ {e.TargetId}";
                        break;
                    case BattleEventKind.PlanCommitted:
                        line = "玩家确认出牌";
                        break;
                    case BattleEventKind.TurnSkipped:
                        line = "玩家空过回合";
                        break;
                    case BattleEventKind.CardResolvedStarted:
                        line = $"结算: {e.Message} ({e.CardType})";
                        break;
                    case BattleEventKind.DamageApplied:
                        line = FormatDamageLog(e);
                        break;
                    case BattleEventKind.BlockGained:
                        line = $"护甲 +{e.Amount}: {e.CombatantId}";
                        break;
                    case BattleEventKind.HealApplied:
                        line = $"治疗 +{e.Amount}: {e.CombatantId}";
                        break;
                    case BattleEventKind.CharacterDied:
                        line = $"阵亡: {e.CombatantId}";
                        break;
                    case BattleEventKind.BattleEnded:
                        line = $"战斗结束: {e.Outcome}";
                        break;
                    case BattleEventKind.PortraitPoseChanged:
                        line = $"立绘 {e.CardType}: {e.CombatantId}";
                        break;
                    case BattleEventKind.PortraitIdleRestored:
                        line = $"立绘 Idle: {e.CombatantId}";
                        break;
                    default:
                        line = $"{e.Kind}: {e.Message}";
                        break;
                }
                Log(line);
            }

            _engine.ClearEvents();
            _logScroll.y = float.MaxValue;
        }

        void Log(string msg)
        {
            _log.Add(msg);
            if (_log.Count > 200)
                _log.RemoveAt(0);
            _logScroll.y = float.MaxValue;
        }

        string FormatDamageLog(BattleEvent e)
        {
            if (!string.IsNullOrEmpty(e.Message))
                return $"伤害 {e.Amount}: {e.Message}";

            return $"伤害 {e.Amount}: {CombatantLabel(e.CombatantId)} → {CombatantLabel(e.TargetId)}";
        }

        string FormatStatusLog(BattleEvent e)
        {
            var target = CombatantLabel(e.CombatantId);
            var slot = GetCombatantSlotLabel(e.CombatantId);
            return $"状态 {e.Message} x{e.Amount} → {target}{slot}";
        }

        string CombatantLabel(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return "?";
            var c = _engine.State.GetCombatant(combatantId);
            return c != null ? c.DisplayName : combatantId;
        }

        string GetCombatantSlotLabel(string combatantId)
        {
            var c = _engine.State.GetCombatant(combatantId);
            return c != null ? $"({SlotLabel(c.Slot)})" : "";
        }
    }
}
