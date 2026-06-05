using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    public sealed class ExpeditionEventOutcome
    {
        public string Message { get; set; } = "";
        public bool StartsCombat { get; set; }
        public int CombatEncounterIndex { get; set; }
        public bool AdvanceNode { get; set; } = true;
        public ExpeditionRewardPickup PendingRewardPickup { get; set; }
    }

    public static class ExpeditionEventResolver
    {
        public static ExpeditionEventOutcome ResolveChoice(
            ExpeditionRunState run,
            ExpeditionConfig config,
            string eventId,
            int choiceIndex,
            BattleRng rng)
        {
            if (!ExpeditionEventCatalog.TryGet(eventId, out var definition))
                return new ExpeditionEventOutcome { Message = "事件已结束。" };

            if (choiceIndex < 0 || choiceIndex >= definition.Choices.Count)
                return new ExpeditionEventOutcome { Message = "无效选择。" };

            run.UsedEventIds.Add(eventId);

            return eventId switch
            {
                ExpeditionEventIds.MysteriousTraveler => ResolveMysteriousTraveler(run, choiceIndex, rng),
                ExpeditionEventIds.AncientTemple => ResolveAncientTemple(run, choiceIndex),
                ExpeditionEventIds.InjuredAdventurer => ResolveInjuredAdventurer(run, choiceIndex, rng),
                ExpeditionEventIds.MagicSpring => ResolveMagicSpring(run, choiceIndex, rng),
                ExpeditionEventIds.GamblerDice => ResolveGamblerDice(run, choiceIndex, rng),
                ExpeditionEventIds.MirrorPhantom => ResolveMirrorPhantom(run, choiceIndex, config),
                ExpeditionEventIds.CursedBookshelf => ResolveCursedBookshelf(run, choiceIndex, rng),
                ExpeditionEventIds.AdventurerRevenge => ResolveAdventurerRevenge(run, choiceIndex, config),
                ExpeditionEventIds.TrainingDummy => ResolveTrainingDummy(run, choiceIndex),
                ExpeditionEventIds.SoulRift => ResolveSoulRift(run, choiceIndex, rng),
                ExpeditionEventIds.WanderingSmith => ResolveWanderingSmith(run, choiceIndex),
                ExpeditionEventIds.TiredCamp => ResolveTiredCamp(run, choiceIndex, rng),
                ExpeditionEventIds.JadeWorkshop => ResolveJadeWorkshop(run, choiceIndex),
                ExpeditionEventIds.AncientFurnace => ResolveAncientFurnace(run, choiceIndex),
                ExpeditionEventIds.AbyssWhisper => ResolveAbyssWhisper(run, choiceIndex, rng),
                _ => new ExpeditionEventOutcome { Message = "事件结束。" }
            };
        }

        public static ExpeditionEventOutcome ResolveShrineChoice(
            ExpeditionRunState run,
            string shrineId,
            int choiceIndex,
            BattleRng rng)
        {
            if (!ExpeditionShrineCatalog.TryGet(shrineId, out var definition) ||
                choiceIndex < 0 ||
                choiceIndex >= definition.Choices.Count)
            {
                return new ExpeditionEventOutcome { Message = "你离开了祭坛。" };
            }

            var choice = definition.Choices[choiceIndex];
            if (choice.Label == "C" ||
                (choice.Description != null && choice.Description.Contains("安全离开")))
            {
                return new ExpeditionEventOutcome { Message = "你离开了祭坛。" };
            }

            return shrineId switch
            {
                ExpeditionShrineIds.Blood => ResolveBloodShrine(run, choiceIndex, rng),
                ExpeditionShrineIds.Knowledge => ResolveKnowledgeShrine(run, choiceIndex, rng),
                ExpeditionShrineIds.Soul => ResolveSoulShrine(run, choiceIndex, rng),
                ExpeditionShrineIds.Chaos => ResolveChaosShrine(run, choiceIndex, rng),
                _ => new ExpeditionEventOutcome { Message = "祭坛无回应。" }
            };
        }

        public static ExpeditionEventOutcome ResolveShopChoice(ExpeditionRunState run, int choiceIndex)
        {
            return choiceIndex switch
            {
                0 when run.Gold >= 25 => BuyHeal(run),
                1 when run.Gold >= 20 => BuyRemoveCard(run),
                2 when run.Gold >= 10 => BuyConsumable(run, ConsumableIds.SmallHealingPotion, 10),
                3 when run.Gold >= 20 => BuyConsumable(run, ConsumableIds.LargeHealingPotion, 20),
                4 when run.Gold >= 15 => BuyConsumable(run, ConsumableIds.SmokeBomb, 15),
                _ => new ExpeditionEventOutcome { Message = "你没有购买任何东西。" }
            };
        }

        static ExpeditionEventOutcome ResolveMysteriousTraveler(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 when run.Gold >= 30 => SpendGold(run, 30, "购得一张卡牌。"),
                1 => GrantRelicOrMessage(run, rng, "旅者的礼物带来了遗物，但诅咒牌混入了牌堆。"),
                _ => new ExpeditionEventOutcome { Message = "你拒绝交易，悄然离开。" }
            };
        }

        static ExpeditionEventOutcome ResolveAncientTemple(ExpeditionRunState run, int choice)
        {
            return choice switch
            {
                0 => WithTeamHpPercent(run, -10, "祈祷生效：远征期间全队 ATK+1。", () =>
                {
                    run.Modifiers.TeamAttackBonus += 1;
                }),
                1 => WithPickup(
                    ExpeditionRewardPickupFactory.Gold(50, "亵渎圣堂"),
                    "亵渎圣堂获得 50 金币，但神罚将至。",
                    () => run.Modifiers.DivinePunishmentActive = true),
                _ => new ExpeditionEventOutcome { Message = "你静默离开神殿。" }
            };
        }

        static ExpeditionEventOutcome ResolveInjuredAdventurer(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => ResolveInjuredAdventurerHelp(run, rng),
                1 => WithPickup(
                    ExpeditionRewardPickupFactory.Gold(20, "搜刮冒险者"),
                    "你搜刮了冒险者。",
                    () =>
                    {
                        run.Modifiers.LootedInjuredAdventurer = true;
                        run.EventFlags.Add("looted_adventurer");
                    }),
                _ => new ExpeditionEventOutcome { Message = "你选择无视，继续赶路。" }
            };
        }

        static ExpeditionEventOutcome ResolveMagicSpring(ExpeditionRunState run, int choice, BattleRng rng)
        {
            if (choice == 2)
                return new ExpeditionEventOutcome { Message = "你没有触碰泉水。" };

            if (choice == 1)
            {
                AddConsumables(run, ConsumableIds.SpringBottle, 2);
                return new ExpeditionEventOutcome { Message = "你带走了 2 瓶泉水。" };
            }

            var roll = rng.NextIndex(100);
            if (roll < 60)
                return HealTeamPercent(run, 25, "泉水治愈了队伍。");
            if (roll < 85)
            {
                run.Modifiers.TeamAttackBonus += 2;
                return new ExpeditionEventOutcome { Message = "一名队员永久 ATK+2。" };
            }

            return WithTeamHpPercent(run, -15, "泉水有毒，全队失去 15% HP。");
        }

        static ExpeditionEventOutcome ResolveGamblerDice(ExpeditionRunState run, int choice, BattleRng rng)
        {
            if (choice == 2)
                return new ExpeditionEventOutcome { Message = "你没有参与赌博。" };

            if (choice == 0)
            {
                if (run.Gold < 20)
                    return new ExpeditionEventOutcome { Message = "金币不足。" };

                run.Gold -= 20;
                if (rng.NextIndex(100) < 50)
                {
                    return WithPickup(
                        ExpeditionRewardPickupFactory.Gold(50, "小赌获胜"),
                        "小赌获胜：+50 金币。");
                }

                return new ExpeditionEventOutcome { Message = "小赌失败，金币打了水漂。" };
            }

            var all = run.Gold;
            run.Gold = 0;
            var big = rng.NextIndex(100);
            if (big < 40)
            {
                return WithPickup(
                    ExpeditionRewardPickupFactory.Gold(all * 2, "大赌翻倍"),
                    "大赌翻倍！");
            }

            if (big < 70)
                return new ExpeditionEventOutcome { Message = "大赌输光所有金币。" };

            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            return WithPickup(
                ExpeditionRewardPickupFactory.Relic(relicId, "大赌遗物"),
                "大赌获得稀有遗物！");
        }

        static ExpeditionEventOutcome ResolveMirrorPhantom(ExpeditionRunState run, int choice, ExpeditionConfig config)
        {
            return choice switch
            {
                0 => new ExpeditionEventOutcome
                {
                    Message = "镜中挑战开始！",
                    StartsCombat = true,
                    CombatEncounterIndex = config.CombatEncounters.Count > 0 ? 0 : 0
                },
                1 => WithMessage("镜之碎片落入手心。", () =>
                    AddConsumables(run, ConsumableIds.MirrorShard, 1)),
                _ => new ExpeditionEventOutcome { Message = "你离开了魔镜。" }
            };
        }

        static ExpeditionEventOutcome ResolveCursedBookshelf(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => WithMemberHpLoss(run, 10, "阅读获得一张蓝色卡牌。", rng),
                1 => WithMessage("带走古卷残页。", () => AddConsumables(run, ConsumableIds.ScrollPage, 1)),
                _ => new ExpeditionEventOutcome { Message = "你合上了书。" }
            };
        }

        static ExpeditionEventOutcome ResolveAdventurerRevenge(ExpeditionRunState run, int choice, ExpeditionConfig config)
        {
            return choice switch
            {
                0 when run.Gold >= 40 => SpendGold(run, 40, "对方接受赔偿，并告知前方路况。", () =>
                {
                    run.Modifiers.ForeseenLayerCount = System.Math.Max(run.Modifiers.ForeseenLayerCount, 3);
                    RevealForeseenLayers(run);
                }),
                1 => new ExpeditionEventOutcome
                {
                    Message = "冒险者发起复仇战！",
                    StartsCombat = true,
                    CombatEncounterIndex = config.CombatEncounters.Count > 0 ? 0 : 0
                },
                _ => WithTeamHpPercent(run, -5, "你在混乱中逃离。")
            };
        }

        static ExpeditionEventOutcome ResolveTrainingDummy(ExpeditionRunState run, int choice)
        {
            return choice switch
            {
                0 => WithTeamHpPercent(run, -10, "全队 DEF+1。", () => run.Modifiers.TeamDefenseBonus += 1),
                1 => WithMemberHpPercent(run, -20, "特训成功：该角色 ATK+2。", () =>
                    run.Modifiers.TeamAttackBonus += 2),
                _ => HealTeamPercent(run, 10, "短暂休息后继续前行。")
            };
        }

        static ExpeditionEventOutcome ResolveSoulRift(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => WithMessage("裂隙能量涌入：能量上限 +1。", () => run.Modifiers.EnergyCapBonus += 1),
                1 => GrantRelicOrMessage(run, rng, "裂隙被封印，留下一件遗物。"),
                _ => new ExpeditionEventOutcome { Message = "你绕行而过。" }
            };
        }

        static ExpeditionEventOutcome ResolveWanderingSmith(ExpeditionRunState run, int choice)
        {
            return choice switch
            {
                0 when run.Gold >= 15 => SpendGold(run, 15, "铁匠强化了一张卡牌（效果 +20%）。"),
                1 => new ExpeditionEventOutcome { Message = "卡牌融合完成（占位：获得更高品质牌）。" },
                _ => new ExpeditionEventOutcome { Message = "你谢绝了铁匠。" }
            };
        }

        static ExpeditionEventOutcome ResolveTiredCamp(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => WithMessage("深度休息：跳过下一层路线，全队回复 30% HP。", () =>
                {
                    run.Modifiers.SkipNextRouteSelect = true;
                    HealTeamPercentSilent(run, 30);
                }),
                1 => HealTeamPercent(run, 15, "简单休息后恢复了一些体力。"),
                _ => WithPickup(
                    ExpeditionRewardPickupFactory.Gold(rng.NextInt(10, 26), "营地搜刮"),
                    "搜刮营地。")
            };
        }

        static ExpeditionEventOutcome ResolveJadeWorkshop(ExpeditionRunState run, int choice)
        {
            if (!run.Relics.Contains(RelicIds.JadeStone))
                return new ExpeditionEventOutcome { Message = "你没有合适的材料。" };

            return choice switch
            {
                0 => EvolveRelic(run, RelicIds.JadeStone, RelicIds.JadeRing, "翡翠戒指"),
                1 => EvolveRelic(run, RelicIds.JadeStone, RelicIds.JadeDagger, "翡翠短刀"),
                _ => new ExpeditionEventOutcome { Message = "你离开了工坊。" }
            };
        }

        static ExpeditionEventOutcome ResolveAncientFurnace(ExpeditionRunState run, int choice)
        {
            if (!run.Relics.Contains(RelicIds.BurningBoots))
                return new ExpeditionEventOutcome { Message = "熔炉对你的装备没有反应。" };

            return choice switch
            {
                0 => WithTeamHpPercent(run, -10, "淬火完成：燃烬之靴进化为赤红烈焰靴。", () =>
                    EvolveRelicSilent(run, RelicIds.BurningBoots, RelicIds.CrimsonBurningBoots)),
                _ => new ExpeditionEventOutcome { Message = "你保留了原样的靴子。" }
            };
        }

        static ExpeditionEventOutcome ResolveAbyssWhisper(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => WithDemonHpPercent(run, -20, "恶魔听懂了低语，获得一张专属卡牌。"),
                1 => new ExpeditionEventOutcome { Message = "记忆献祭完成：全队 ATK+1。" }
                    .Also(() => run.Modifiers.TeamAttackBonus += 1),
                _ => new ExpeditionEventOutcome { Message = "你捂耳离开。" }
            };
        }

        static ExpeditionEventOutcome ResolveBloodShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => WithMemberHpPercent(run, -50, "血祭完成：该角色 ATK+3。", () =>
                    run.Modifiers.TeamAttackBonus += 3),
                1 => ResolveBloodShrineRelic(run, rng),
                _ => new ExpeditionEventOutcome { Message = "你离开血之祭坛。" }
            };
        }

        static ExpeditionEventOutcome ResolveBloodShrineRelic(ExpeditionRunState run, BattleRng rng)
        {
            WithTeamHpPercentSilent(run, -15);
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            return WithPickup(
                ExpeditionRewardPickupFactory.Relic(relicId, "血之祭坛"),
                "集体血祭完成，获得随机遗物。");
        }

        static ExpeditionEventOutcome ResolveKnowledgeShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => new ExpeditionEventOutcome { Message = "献祭卡牌后，从三张蓝卡中选一张（占位）。" },
                1 => new ExpeditionEventOutcome { Message = "献祭高级卡牌，获得一张紫色卡牌（占位）。" },
                _ => new ExpeditionEventOutcome { Message = "你离开知识祭坛。" }
            };
        }

        static ExpeditionEventOutcome ResolveSoulShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => GrantRelicPickup(run, rng, "献祭遗物，获得更高阶遗物。", "灵魂祭坛"),
                1 => new ExpeditionEventOutcome { Message = "等级重置，该角色获得 ATK+3 DEF+2 HP+15（占位）。" },
                _ => new ExpeditionEventOutcome { Message = "你离开灵魂祭坛。" }
            };
        }

        static ExpeditionEventOutcome ResolveChaosShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            if (choice >= 1)
                return new ExpeditionEventOutcome { Message = "你拒绝混沌仪式。" };

            var cost = rng.NextIndex(3);
            if (cost == 0) WithTeamHpPercentSilent(run, -20);
            else if (cost == 1) run.Gold = System.Math.Max(0, run.Gold - 20);
            else RemoveRandomBonusCard(run, rng);

            if (rng.NextIndex(100) < 10)
                return new ExpeditionEventOutcome { Message = "混沌仪式毫无收获。" };

            run.Modifiers.TeamAttackBonus += 1;

            if (rng.NextIndex(100) < 50)
            {
                var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
                return WithPickup(
                    ExpeditionRewardPickupFactory.Relic(relicId, "混沌祭坛"),
                    "混沌祭坛给出了未知的回报。");
            }

            return new ExpeditionEventOutcome { Message = "混沌祭坛给出了未知的回报。" };
        }

        static ExpeditionEventOutcome BuyHeal(ExpeditionRunState run)
        {
            run.Gold -= 25;
            HealTeamPercentSilent(run, 25);
            return new ExpeditionEventOutcome { Message = "商人提供了治疗服务。" };
        }

        static ExpeditionEventOutcome BuyRemoveCard(ExpeditionRunState run)
        {
            run.Gold -= 20;
            return new ExpeditionEventOutcome { Message = "删牌服务完成（占位）。" };
        }

        static ExpeditionEventOutcome BuyConsumable(ExpeditionRunState run, string consumableId, int cost)
        {
            run.Gold -= cost;
            AddConsumables(run, consumableId, 1);
            ConsumableDatabase.TryGet(consumableId, out var def);
            var suffix = string.IsNullOrEmpty(run.PendingConsumableOfferId)
                ? ""
                : "（栏位已满，请选择替换或放弃）";
            return new ExpeditionEventOutcome { Message = $"购买 {def?.DisplayName ?? consumableId}{suffix}" };
        }

        static ExpeditionEventOutcome SpendGold(ExpeditionRunState run, int amount, string message, System.Action extra = null)
        {
            run.Gold -= amount;
            extra?.Invoke();
            return new ExpeditionEventOutcome { Message = message };
        }

        static ExpeditionEventOutcome GrantRelicOrMessage(ExpeditionRunState run, BattleRng rng, string message)
        {
            AddCurseCard(run);
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            if (string.IsNullOrEmpty(relicId))
                return new ExpeditionEventOutcome { Message = message };

            return WithPickup(
                ExpeditionRewardPickupFactory.Relic(relicId, "遗物奖励"),
                message);
        }

        static ExpeditionEventOutcome ResolveInjuredAdventurerHelp(ExpeditionRunState run, BattleRng rng)
        {
            WithTeamHpPercentSilent(run, -15);
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            return WithPickup(
                ExpeditionRewardPickupFactory.Relic(relicId, "冒险者的谢礼"),
                "冒险者感激地留下一件遗物。");
        }

        static ExpeditionEventOutcome WithPickup(
            ExpeditionRewardPickup pickup,
            string message,
            System.Action extra = null)
        {
            extra?.Invoke();
            if (pickup == null || !pickup.HasAnyReward)
                return new ExpeditionEventOutcome { Message = message };

            pickup.Kind = RewardPickupKind.EventOrShrine;
            if (string.IsNullOrEmpty(pickup.HeaderText))
                pickup.HeaderText = "拾取奖励";

            return new ExpeditionEventOutcome
            {
                Message = message,
                PendingRewardPickup = pickup
            };
        }

        static ExpeditionEventOutcome GrantRelicPickup(
            ExpeditionRunState run,
            BattleRng rng,
            string message,
            string header)
        {
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            return WithPickup(ExpeditionRewardPickupFactory.Relic(relicId, header), message);
        }

        static ExpeditionEventOutcome EvolveRelic(ExpeditionRunState run, string from, string to, string name)
        {
            EvolveRelicSilent(run, from, to);
            return new ExpeditionEventOutcome { Message = $"进化完成：{name}。" };
        }

        static void EvolveRelicSilent(ExpeditionRunState run, string from, string to)
        {
            run.Relics.Remove(from);
            if (!run.Relics.Contains(to))
                run.Relics.Add(to);
        }

        static void AddCurseCard(ExpeditionRunState run)
        {
            if (run.Party.Count == 0)
                return;

            run.Party[0].BonusCards.Add(new CardTemplate
            {
                DefinitionId = "curse_chaos_touch",
                DisplayName = "混沌之触",
                OwnerCharacterId = run.Party[0].CharacterDefinitionId,
                Cost = 1,
                CardType = CardType.Status
            });
        }

        static void RemoveRandomBonusCard(ExpeditionRunState run, BattleRng rng)
        {
            foreach (var member in run.Party)
            {
                if (member.BonusCards.Count == 0)
                    continue;

                member.BonusCards.RemoveAt(rng.NextIndex(member.BonusCards.Count));
                return;
            }
        }

        static void AddConsumables(ExpeditionRunState run, string id, int count)
        {
            if (!ConsumableInventory.TryAddMany(run.ConsumableSlots, id, count, out var pending) &&
                !string.IsNullOrEmpty(pending))
            {
                run.PendingConsumableOfferId = pending;
            }
        }

        static ExpeditionEventOutcome HealTeamPercent(ExpeditionRunState run, int percent, string message)
        {
            HealTeamPercentSilent(run, percent);
            return new ExpeditionEventOutcome { Message = message };
        }

        static void HealTeamPercentSilent(ExpeditionRunState run, int percent)
        {
            foreach (var member in run.Party)
            {
                var heal = System.Math.Max(1, member.MaxHp * percent / 100);
                member.Hp = System.Math.Min(member.MaxHp, member.Hp + heal);
            }
        }

        static ExpeditionEventOutcome WithTeamHpPercent(ExpeditionRunState run, int percent, string message, System.Action extra = null)
        {
            foreach (var member in run.Party)
            {
                var loss = System.Math.Max(1, member.Hp * System.Math.Abs(percent) / 100);
                if (percent < 0)
                    member.Hp = System.Math.Max(1, member.Hp - loss);
                else
                    member.Hp = System.Math.Min(member.MaxHp, member.Hp + loss);
            }

            extra?.Invoke();
            return new ExpeditionEventOutcome { Message = message };
        }

        static void WithTeamHpPercentSilent(ExpeditionRunState run, int percent)
        {
            foreach (var member in run.Party)
            {
                var loss = System.Math.Max(1, member.Hp * System.Math.Abs(percent) / 100);
                member.Hp = System.Math.Max(1, member.Hp - loss);
            }
        }

        static ExpeditionEventOutcome WithMemberHpPercent(ExpeditionRunState run, int percent, string message, System.Action extra)
        {
            if (run.Party.Count > 0)
            {
                var member = run.Party[0];
                var delta = System.Math.Max(1, member.Hp * System.Math.Abs(percent) / 100);
                member.Hp = percent < 0
                    ? System.Math.Max(1, member.Hp - delta)
                    : System.Math.Min(member.MaxHp, member.Hp + delta);
            }

            extra?.Invoke();
            return new ExpeditionEventOutcome { Message = message };
        }

        static ExpeditionEventOutcome WithMemberHpLoss(ExpeditionRunState run, int flat, string message, BattleRng rng)
        {
            if (run.Party.Count > 0)
            {
                var idx = rng.NextIndex(run.Party.Count);
                run.Party[idx].Hp = System.Math.Max(1, run.Party[idx].Hp - flat);
            }

            return new ExpeditionEventOutcome { Message = message };
        }

        static ExpeditionEventOutcome WithDemonHpPercent(ExpeditionRunState run, int percent, string message)
        {
            foreach (var member in run.Party)
            {
                if (member.CharacterDefinitionId != "char_ranger")
                    continue;

                var loss = System.Math.Max(1, member.Hp * System.Math.Abs(percent) / 100);
                member.Hp = System.Math.Max(1, member.Hp - loss);
            }

            return new ExpeditionEventOutcome { Message = message };
        }

        static ExpeditionEventOutcome WithMessage(string message, System.Action action)
        {
            action?.Invoke();
            return new ExpeditionEventOutcome { Message = message };
        }

        static void RevealForeseenLayers(ExpeditionRunState run)
        {
            if (run.Map == null)
                return;

            var start = run.Map.NodesCompleted + 1;
            for (var i = 0; i < run.Modifiers.ForeseenLayerCount; i++)
            {
                var layer = run.Map.GetLayer(start + i);
                if (layer != null)
                    layer.IsRevealed = true;
            }
        }

        static ExpeditionEventOutcome Also(this ExpeditionEventOutcome outcome, System.Action action)
        {
            action?.Invoke();
            return outcome;
        }
    }
}
