using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Tutorial;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    /// <summary>根据远征阶段推进新手教程提示。</summary>
    public static class TutorialCoachDriver
    {
        public static void TryShow(
            BattleSession session,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            if (session?.Expedition?.Run == null || screen == null || overlay == null)
                return;

            var run = session.Expedition.Run;
            if (!run.IsTutorialRun)
                return;

            // 强制点击类提示需在 PresentationLocked 时也能收尾（例如点了出牌）
            if (TryAdvanceAwaitingTips(run, session, screen, overlay))
                return;

            // 遗物介绍优先：首次领取遗物后必出，不被其他提示挡住
            if (TryShowRelicIntro(run, screen, overlay))
                return;

            if (session.PresentationLocked)
                return;

            if (overlay.IsShowing)
                return;

            if (TryShowRouteOrNodeTip(run, screen, overlay))
                return;

            if (TryShowRewardTips(run, screen, overlay))
                return;

            if (run.Phase == ExpeditionPhase.ShrineChoice)
            {
                TryShowAltarTips(run, screen, overlay);
                return;
            }

            if (run.Phase != ExpeditionPhase.InBattle || session.Engine?.State == null)
                return;

            if (session.Engine.State.Phase != TurnPhase.Planning)
                return;

            var floor = run.LastBattleFloor > 0
                ? run.LastBattleFloor
                : (run.Map?.NodesCompleted ?? 0) + 1;

            if (!run.LastBattleWasElite && floor <= 1)
                TryShowFirstBattleTips(run, screen, overlay);
            else if (run.LastBattleWasElite)
                TryShowEliteBattleTips(run, screen, overlay);
        }

        /// <summary>强制点击类提示：持续监听直至条件满足。</summary>
        static bool TryAdvanceAwaitingTips(
            ExpeditionRunState run,
            BattleSession session,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            // 精英战：选中防御架势后自动关掉提示
            if (NeedsAwaitDefensiveStance(run))
            {
                if (IsDefensiveStanceSelected(session))
                {
                    if (overlay.IsShowing)
                        overlay.Hide();
                    run.EventFlags.Add(ExpeditionTutorialTipIds.EliteDefend);
                    // 继续强制点「出牌」
                }
                else
                {
                    if (session.PresentationLocked)
                        return false;

                    if (!overlay.IsShowing)
                    {
                        overlay.ShowAwaitingTargetClick(
                            "应对攻击",
                            "战士本回合抽到了「防御架势」。【应对攻击】是指应对第一个指向自己的攻击牌；应对失败则无效。请选出这张牌。",
                            screen.GetTutorialHandCardRect(ExpeditionTutorialBattleSetup.DefensiveStanceId)
                            ?? screen.GetTutorialHandRect(),
                            TutorialPlateAnchor.AboveHighlight);
                    }

                    return true;
                }
            }

            // 精英战：选完防御架势后强制点「出牌」体验应对
            if (NeedsForceConfirmPlay(run))
            {
                if (HasConfirmedElitePlay(session))
                {
                    if (overlay.IsShowing)
                        overlay.Hide();
                    run.EventFlags.Add(ExpeditionTutorialTipIds.EliteConfirmPlay);
                    return true;
                }

                if (session.PresentationLocked)
                    return false;

                if (!overlay.IsShowing)
                {
                    overlay.ShowAwaitingTargetClick(
                        "出牌结算",
                        "已选好「防御架势」。请点击「出牌」。防御架势的应对攻击效果应会使敌人造成的伤害减半。",
                        screen.GetTutorialConfirmRect() ?? screen.GetTutorialPlayActionsRect(),
                        TutorialPlateAnchor.AboveHighlight);
                }
                else
                {
                    overlay.BringToFront();
                }

                return true;
            }

            // 精英战第二回合：强制先点开背包
            if (NeedsForceOpenBag(run, session))
            {
                if (session.PresentationLocked)
                    return false;

                if (screen.IsInventoryOpenForTutorial())
                {
                    if (overlay.IsShowing)
                        overlay.Hide();
                    run.EventFlags.Add(ExpeditionTutorialTipIds.OpenBagForConsumable);
                    return false;
                }

                if (!overlay.IsShowing)
                {
                    overlay.ShowAwaitingTargetClick(
                        "打开背包",
                        "请点击左侧背包。",
                        screen.GetTutorialInventoryButtonRect(),
                        TutorialPlateAnchor.Center);
                }
                else
                {
                    overlay.BringToFront();
                }

                return true;
            }

            // 祭坛：先点进刻印（Hub 高亮；进入刻印页后立刻关掉）
            if (run.Phase == ExpeditionPhase.ShrineChoice
                && !Has(run, ExpeditionTutorialTipIds.AltarOpenEngrave)
                && !Has(run, ExpeditionTutorialTipIds.AltarEngrave)
                && (run.CardAltar == null || !run.CardAltar.EngraveSlotUsed))
            {
                if (screen.IsAltarEngravingForTutorial())
                {
                    if (overlay.IsShowing)
                        overlay.Hide();
                    run.EventFlags.Add(ExpeditionTutorialTipIds.AltarOpenEngrave);
                    return false;
                }

                // 已不在 Hub（例如误进其他子页）也关掉卡住的高亮
                if (!screen.IsAltarHubForTutorial())
                {
                    if (overlay.IsShowing)
                        overlay.Hide();
                    return false;
                }

                if (session.PresentationLocked)
                    return false;

                if (!overlay.IsShowing)
                {
                    overlay.ShowAwaitingTargetClick(
                        "祭坛刻印",
                        "请点击右下角「刻印」。",
                        screen.GetTutorialAltarEngravingButtonRect(),
                        TutorialPlateAnchor.AboveHighlight);
                }
                else
                {
                    overlay.BringToFront();
                }

                return true;
            }

            // 首战奖励：点任意奖励后提示消失
            if (NeedsAwaitRewardClaim(run))
            {
                if (session.PresentationLocked)
                    return false;

                var rewards = run.PendingRewardPickup;
                // 点开卡包即算交互（不必等三选一结束）
                if (HasAnyRewardInteraction(rewards) || run.PendingCardPackOffer != null)
                {
                    if (overlay.IsShowing)
                        overlay.Hide();
                    run.EventFlags.Add(ExpeditionTutorialTipIds.RewardClaim);
                    return false;
                }

                var targets = new List<RectTransform>(2);
                var gold = screen.GetTutorialRewardGoldRect();
                var pack = screen.GetTutorialRewardPackRect();
                if (gold != null)
                    targets.Add(gold);
                if (pack != null)
                    targets.Add(pack);

                if (targets.Count == 0)
                    return true;

                if (!overlay.IsShowing)
                {
                    overlay.ShowAwaitingTargetClick(
                        "领取奖励",
                        "分别点击金币与卡包领取奖励。",
                        targets,
                        TutorialPlateAnchor.BelowHighlight);
                }
                else
                {
                    overlay.BringToFront();
                }

                return true;
            }

            return false;
        }

        static bool NeedsForceOpenBag(ExpeditionRunState run, BattleSession session) =>
            run.LastBattleWasElite
            && run.Phase == ExpeditionPhase.InBattle
            && Has(run, ExpeditionTutorialTipIds.EliteConfirmPlay)
            && !Has(run, ExpeditionTutorialTipIds.OpenBagForConsumable)
            && !Has(run, ExpeditionTutorialTipIds.Consumable)
            && (session.Engine?.State?.TurnNumber ?? 0) >= 2;

        static bool NeedsAwaitDefensiveStance(ExpeditionRunState run) =>
            run.LastBattleWasElite
            && run.Phase == ExpeditionPhase.InBattle
            && Has(run, ExpeditionTutorialTipIds.EliteAttack)
            && !Has(run, ExpeditionTutorialTipIds.EliteDefend);

        static bool NeedsForceConfirmPlay(ExpeditionRunState run) =>
            run.LastBattleWasElite
            && run.Phase == ExpeditionPhase.InBattle
            && Has(run, ExpeditionTutorialTipIds.EliteDefend)
            && !Has(run, ExpeditionTutorialTipIds.EliteConfirmPlay);

        static bool HasConfirmedElitePlay(BattleSession session)
        {
            if (session == null)
                return false;
            if (session.PresentationLocked)
                return true;

            var phase = session.Engine?.State?.Phase;
            return phase != null && phase != TurnPhase.Planning && phase != TurnPhase.Draw;
        }

        static bool NeedsAwaitRewardClaim(ExpeditionRunState run)
        {
            if (run.Phase != ExpeditionPhase.RewardPickup
                || Has(run, ExpeditionTutorialTipIds.RewardClaim))
                return false;

            var rewards = run.PendingRewardPickup;
            return rewards != null
                   && rewards.Kind == RewardPickupKind.BattleVictory
                   && rewards.HasAnyReward
                   && !rewards.IsFullyResolved;
        }

        static bool HasAnyRewardInteraction(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return false;

            if (rewards.GoldClaimed || rewards.GoldSkipped)
                return true;
            if (rewards.RelicClaimed || rewards.RelicSkipped)
                return true;
            if (rewards.CardClaimed || rewards.CardSkipped)
                return true;
            if (rewards.ConsumableClaimed || rewards.ConsumableSkipped)
                return true;
            if (rewards.StatClaimed || rewards.StatSkipped)
                return true;

            if (rewards.HasCardPacks)
            {
                foreach (var pack in rewards.CardPacks)
                {
                    if (pack.IsResolved)
                        return true;
                }
            }

            return false;
        }

        static bool IsDefensiveStanceSelected(BattleSession session)
        {
            var draft = session?.Engine?.Draft;
            var state = session?.Engine?.State;
            if (draft == null || state == null)
                return false;

            foreach (var id in draft.SelectedQueue)
            {
                var card = state.GetCard(id);
                if (card != null
                    && card.DefinitionId == ExpeditionTutorialBattleSetup.DefensiveStanceId)
                    return true;
            }

            return false;
        }

        static bool TryShowRouteOrNodeTip(
            ExpeditionRunState run,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            if (run.Phase == ExpeditionPhase.RouteSelect
                && (run.Map?.NodesCompleted ?? 0) == 0
                && !Has(run, ExpeditionTutorialTipIds.Intro))
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.Intro,
                    "新手教程",
                    "这将教会你基础的游戏玩法。",
                    null, screen);
                return true;
            }

            if (run.Phase == ExpeditionPhase.RewardPickup
                && run.PendingRewardPickup?.Kind == RewardPickupKind.Chest
                && !Has(run, ExpeditionTutorialTipIds.Chest))
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.Chest,
                    "宝箱",
                    "打开可获得遗物与消耗品。",
                    null, screen);
                return true;
            }

            if (run.Phase == ExpeditionPhase.EventChoice
                && !Has(run, ExpeditionTutorialTipIds.Event))
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.Event,
                    "事件",
                    "按提示做出选择即可。",
                    null, screen);
                return true;
            }

            if (run.Phase == ExpeditionPhase.ShopVisit
                && !Has(run, ExpeditionTutorialTipIds.Shop))
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.Shop,
                    "商店",
                    "用金币购买物品，也可直接离开。",
                    null, screen);
                return true;
            }

            return false;
        }

        static bool TryShowRewardTips(
            ExpeditionRunState run,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            if (run.Phase == ExpeditionPhase.RewardPickup)
            {
                // RelicIntro 已在 TryShowRelicIntro 优先处理

                if (run.PendingCardPackOffer != null)
                {
                    run.EventFlags.Add(ExpeditionTutorialTipIds.SawCardPack);
                    return false;
                }
            }

            if ((run.Phase == ExpeditionPhase.RewardPickup || run.Phase == ExpeditionPhase.RouteSelect)
                && Has(run, ExpeditionTutorialTipIds.SawCardPack)
                && !Has(run, ExpeditionTutorialTipIds.OpenBagAfterPack)
                && !screen.IsCardPackOverlayOpen())
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.OpenBagAfterPack,
                    "查看卡组",
                    "打开左侧背包可查看卡牌。每位角色卡组上限十张。",
                    screen.GetTutorialInventoryButtonRect(),
                    screen);
                return true;
            }

            return false;
        }

        /// <summary>首次点领取遗物后必出说明（优先于其他非强制提示）。</summary>
        static bool TryShowRelicIntro(
            ExpeditionRunState run,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            if (Has(run, ExpeditionTutorialTipIds.RelicIntro))
                return false;

            if (run.Phase != ExpeditionPhase.RewardPickup)
                return false;

            var rewards = run.PendingRewardPickup;
            if (rewards == null || !rewards.HasRelic || !rewards.RelicClaimed)
                return false;

            if (overlay.IsShowing)
                overlay.Hide();

            Show(
                run, overlay, ExpeditionTutorialTipIds.RelicIntro,
                "遗物",
                "遗物是整局都会生效的特殊道具，被动触发、永久存在，但每种遗物只能获取一个。",
                null, screen);
            return true;
        }

        static void TryShowAltarTips(
            ExpeditionRunState run,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            if (Has(run, ExpeditionTutorialTipIds.AltarOpenEngrave)
                && !Has(run, ExpeditionTutorialTipIds.AltarEngrave)
                && (run.CardAltar == null || !run.CardAltar.EngraveSlotUsed)
                && screen.IsAltarEngravingForTutorial())
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.AltarEngrave,
                    "祭坛刻印",
                    "刻印能把局内卡牌写入军营收藏，之后可带入对战。除局外金币外，也可用战斗进度、献祭等同稀有度等方式刻印。",
                    null, screen);
                return;
            }

            if (Has(run, ExpeditionTutorialTipIds.AltarEngrave)
                && !Has(run, ExpeditionTutorialTipIds.AltarEngravePick)
                && (run.CardAltar == null || !run.CardAltar.EngraveSlotUsed)
                && screen.IsAltarEngravingForTutorial())
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.AltarEngravePick,
                    "选择卡牌",
                    "请选择一张普通牌进行刻印。",
                    null, screen);
                return;
            }

            if (run.CardAltar != null
                && run.CardAltar.EngraveSlotUsed
                && !Has(run, ExpeditionTutorialTipIds.AltarExplore))
            {
                Show(
                    run, overlay, ExpeditionTutorialTipIds.AltarExplore,
                    "继续探索",
                    "刻印完成。祭坛还有其他功能，请自行探索。",
                    null, screen);
            }
        }

        static void TryShowFirstBattleTips(
            ExpeditionRunState run,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            if (!Has(run, ExpeditionTutorialTipIds.BattleObjective))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.BattleObjective,
                    "战斗目标", "本场战斗的目标是干掉所有敌人。", null, screen);
                return;
            }

            if (!Has(run, ExpeditionTutorialTipIds.Energy))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.Energy,
                    "能量",
                    "左下角是能量。出牌会消耗；每回合开始时回复四点。",
                    screen.GetTutorialEnergyRect(), screen);
                return;
            }

            if (!Has(run, ExpeditionTutorialTipIds.PartyHp))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.PartyHp,
                    "角色生命",
                    "我方角色下方的红心是生命值。归零则倒下；全队倒下即战败。",
                    screen.GetTutorialPlayerHpHeartsRect(), screen);
                return;
            }

            if (!Has(run, ExpeditionTutorialTipIds.Hand))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.Hand,
                    "手牌",
                    "鼠标移到卡牌上可查看效果。带有位置词条的卡牌需要选择目标，如【前/中】。",
                    screen.GetTutorialHandRect(), screen);
                return;
            }

            if (!Has(run, ExpeditionTutorialTipIds.IntentOrder))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.IntentOrder,
                    "意图与出招顺序",
                    "上方是本回合出招顺序。速度越高越先出手。例：战士速度高于敌人时，战士的牌会先结算；反之敌人先打。若同一角色出两张牌，出完第一张后须等其他角色都出完一张（或本回合不出牌）才能出第二张。",
                    screen.GetTutorialActionOrderRect(), screen);
                return;
            }

            if (!Has(run, ExpeditionTutorialTipIds.PlayCard))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.PlayCard,
                    "如何出牌",
                    "①点选手牌②需要目标时点选敌人③点「出牌」。点「空过」表示本回合不出牌、跳过规划。",
                    screen.GetTutorialPlayActionsRect() ?? screen.GetTutorialConfirmRect(), screen);
                return;
            }

            if (!Has(run, ExpeditionTutorialTipIds.ArmorStatus))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.ArmorStatus,
                    "护甲与状态",
                    "护甲会优先抵消伤害，正常会在本回合结束后消失。红心旁会显示护甲，角色脚边图标是状态效果。",
                    screen.GetTutorialPlayerFootHudRect(), screen);
            }
        }

        static void TryShowEliteBattleTips(
            ExpeditionRunState run,
            BattleScreenView screen,
            TutorialCoachOverlayView overlay)
        {
            if (!Has(run, ExpeditionTutorialTipIds.EliteAttack))
            {
                Show(run, overlay, ExpeditionTutorialTipIds.EliteAttack,
                    "敌人意图",
                    "看上方意图：骷髅精英这张攻击牌的目标是战士。",
                    screen.GetTutorialFirstActionOrderEntryRect() ?? screen.GetTutorialActionOrderRect(),
                    screen);
                return;
            }

            // EliteDefend / EliteConfirmPlay / OpenBag 由 TryAdvanceAwaitingTips 处理

            if (Has(run, ExpeditionTutorialTipIds.OpenBagForConsumable)
                && !Has(run, ExpeditionTutorialTipIds.Consumable))
            {
                if (!screen.IsInventoryOpenForTutorial())
                    screen.OpenInventoryForTutorial();

                Show(run, overlay, ExpeditionTutorialTipIds.Consumable,
                    "使用消耗品",
                    "右侧是消耗品栏，上限五个。战斗中使用后会消失，请谨慎使用。",
                    screen.GetTutorialFirstConsumableSlotRect()
                    ?? screen.GetTutorialConsumableStripRect()
                    ?? screen.GetTutorialInventoryButtonRect(),
                    screen,
                    TutorialPlateAnchor.AboveHighlight);
            }
        }

        static bool Has(ExpeditionRunState run, string tipId) =>
            run.EventFlags != null && run.EventFlags.Contains(tipId);

        static void Show(
            ExpeditionRunState run,
            TutorialCoachOverlayView overlay,
            string tipId,
            string title,
            string body,
            RectTransform highlight,
            BattleScreenView screen,
            TutorialPlateAnchor anchor = TutorialPlateAnchor.Center)
        {
            IReadOnlyList<RectTransform> targets = highlight != null
                ? new[] { highlight }
                : System.Array.Empty<RectTransform>();
            ShowMany(run, overlay, tipId, title, body, targets, screen, anchor);
        }

        static void ShowMany(
            ExpeditionRunState run,
            TutorialCoachOverlayView overlay,
            string tipId,
            string title,
            string body,
            IReadOnlyList<RectTransform> highlights,
            BattleScreenView screen,
            TutorialPlateAnchor anchor = TutorialPlateAnchor.Center)
        {
            overlay.Show(title, body, highlights, "知道了", () =>
            {
                run.EventFlags.Add(tipId);
                screen.Refresh();
            }, anchor);
        }
    }
}
