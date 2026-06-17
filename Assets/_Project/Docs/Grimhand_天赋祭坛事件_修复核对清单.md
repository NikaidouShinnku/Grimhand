# Grimhand 修复核对清单 · 天赋 / 祭坛 / 事件

> **生成日期**：2026-05-27  
> **用法**：按编号逐项修复；修完勾 `[x] 修复`，测完勾 `[x] 核对`。  
> **说明**：「已修复」项仍建议回归测试后再勾核对。

---

## 建议使用修复顺序

| 阶段 | 编号范围 | 目标 |
|------|----------|------|
| 阶段 1 · 数据安全 | E-01~E-05, A-02, E-04 | 防丢牌、防软锁、防 exploit |
| 阶段 2 · HP / 属性 | X-01, T-02, T-03, F-05 | 单一真相源，战后 Sync |
| 阶段 3 · 事件时序 | X-02, E-10~E-14, E-11 | Planner 副作用延后 |
| 阶段 4 · 天赋 Meta | T-01, T-04~T-07, T-08~T-14 | 持久化、校验、公式核对 |
| 阶段 5 · 祭坛 UX | A-01~A-06, A-07~A-12 | 反馈、边界、测试 |
| 阶段 6 · 占位内容 | E-19~E-21, T-15~T-17 | 未实现事件 / 低优先级 |

---

## 跨系统

| 编号 | 严重度 | 修复 | 核对 | 问题摘要 / 位置 |
|------|--------|:----:|:----:|-----------------|
| X-01 | High | [ ] | [ ] | **HP 三层不同步**：战斗内 MaxHp、`member.MaxHp`、UI 重算值各自维护。涉及 `ApplyPartyProgress`、`CaptureParty`、`ApplyTeamHpBonus`、`ExpeditionPartyStatsRules`。 |
| X-02 | High | [ ] | [ ] | **`Msg()` / `TeamHpThen()` 副作用过早**：玩家点选项时就执行 `action`，而非交互步骤结束。`ExpeditionEventPlanner.cs`。 |

---

## 已修复 · 待核对

| 编号 | 严重度 | 修复 | 核对 | 问题摘要 / 位置 |
|------|--------|:----:|:----:|-----------------|
| F-01 | 已修复 | [ ] | [ ] | 战阵鼓舞 +10 被遗物 `TeamHpBonus` 门控，无遗物时天赋不生效。`RelicBattleRules.ApplyTeamHpBonus`。 |
| F-02 | 已修复 | [ ] | [ ] | 战后 `BonusCards` 因 `Party.Clear` 与 `CaptureParty` 共用列表而丢失。`ExpeditionEngine.OnBattleFinished`。 |
| F-03 | 已修复 | [ ] | [ ] | 祭坛三人需分别确认 → 已改为 `TryConfirmCardAltar` 批量应用全队 draft。 |
| F-04 | 已修复 | [ ] | [ ] | 收藏取出进度战后丢失 → `RunStartCampDecks` + `ExtractedCampCollectionIndices` 远征级持久化。 |
| F-05 | 部分修复 | [ ] | [ ] | 背包 HP 升级后丢失 +10 → `ExpeditionPartyStatsRules` + `GrantXpToParty`；战间 `member.MaxHp` 仍可能漂移。 |
| F-06 | 已修复 | [ ] | [ ] | 悬停角色框偶尔显示卡组 → `CombatantDetailPopupView` 已移除卡组列表。 |
| F-07 | 部分修复 | [ ] | [ ] | 铁匠融合：选牌 → ShowMessage → 再改卡组（时序已对）；`TryFuseCards` 本身仍有 bug（见 E-01~E-03）。 |

---

## 天赋

| 编号 | 严重度 | 修复 | 核对 | 问题摘要 / 位置 |
|------|--------|:----:|:----:|-----------------|
| T-01 | Critical | [ ] | [ ] | **局外天赋无持久化**：`CampMetaState` 仅内存，重启丢失。`GameFlowController` / `CampMetaState`。 |
| T-02 | High | [ ] | [ ] | 战士战死后 `member.MaxHp` 不回落，战后未 `SyncPartyEffectiveMaxHp`。`OnBattleFinished`。 |
| T-03 | High | [ ] | [ ] | 战间 vs 下一场：鼓舞判定不一致（party `Hp=0` 无 +10，下一场 `StartHp=1` 又 +10）。 |
| T-04 | High | [ ] | [ ] | `InitPartyFromTemplate` / `InitPartyAtLevel` 不带 `SelectedTalentSlot*Id`（Boss 测试等路径无天赋）。 |
| T-05 | High | [ ] | [ ] | `OutOfRunLevel` 无增长逻辑，非 demo 场景天赋永久锁定。`TalentRules.IsUnlocked`。 |
| T-06 | High | [ ] | [ ] | 毒爆 `talent_mage_s2_lv10`：`stacks² × damage`，正常 tick 为 `stacks × damage`，与文案可能不符。 |
| T-07 | High | [ ] | [ ] | `CloneModifiers` 缺 `TeamHpBonus`、献祭相关 percent 等字段。 |
| T-08 | Medium | [ ] | [ ] | 天赋无角色归属校验，脏数据可装错角色。`TalentRules.TryToggleSelection` / `CollectTalentId`。 |
| T-09 | Medium | [ ] | [ ] | 等级不足时不自动卸下已选高等级天赋。 |
| T-10 | Medium | [ ] | [ ] | 余护甲回血 `talent_knight_s1_lv5`：文案像自身，代码对**所有有护甲友方**生效。 |
| T-11 | Medium | [ ] | [ ] | 绝地格挡 `talent_knight_s1_lv10`：文案「获得护甲」，实现为直接减伤、不增 Block。 |
| T-12 | Medium | [ ] | [ ] | 镜像护甲 `talent_mage_s1_lv1`：凡 `GainBlock` 效果均触发，不限于护甲类卡牌。 |
| T-13 | Medium | [ ] | [ ] | 无尽血刃注入 `Cost=1` 与资产不一致；catalog 缺失时为无效空牌。`TalentDatabase.ApplyRunStartEffects`。 |
| T-14 | Medium | [ ] | [ ] | `CampRunPartyApplier` 在 `meta==null` 时静默不写天赋 ID。 |
| T-15 | Low | [ ] | [ ] | 恶魔 slot2 缺 Lv.10 天赋，与战士/法老不对称。`TalentCatalog`。 |
| T-16 | Low | [ ] | [ ] | `ranger_s2_lv4` 重复设折扣标记（无功能影响）。 |
| T-17 | Low | [ ] | [ ] | `TalentCampOverlayView` 关闭时才 `OnMetaSaved`，与无持久化叠加易丢数据。 |

---

## 祭坛

| 编号 | 严重度 | 修复 | 核对 | 问题摘要 / 位置 |
|------|--------|:----:|:----:|-----------------|
| A-01 | Medium | [ ] | [ ] | 祭坛确认失败只写 `LastEventMessage`，UI 不读 → 满 10 张未选替换时点确认无反馈。 |
| A-02 | Medium | [ ] | [ ] | `ApplyCardAltarExtraction` 忽略 `TryReplaceAndAdd` 返回值 → 可能已 MarkExtracted 但卡未进组。 |
| A-03 | Medium | [ ] | [ ] | `TryConfirmCardAltar`：`pending` 为空仍 `CompleteCurrentNode`（可空过祭坛节点）。 |
| A-04 | Medium | [ ] | [ ] | `ResolvePendingConsumableOffer` 清 `PendingCardOffer`，与卡组满替换流程冲突。 |
| A-05 | Medium | [ ] | [ ] | `CaptureParty` 在 `Combatants==null` 时返回空列表，不走 `existingParty` 兜底。 |
| A-06 | Medium | [ ] | [ ] | `StartRun` 无 roster 时不填 `RunStartCampDecks`，祭坛收藏为空。 |
| A-07 | Low | [ ] | [ ] | 缺「满 10 张 + 祭坛选替换」集成测试。 |
| A-08 | Low | [ ] | [ ] | 任一角色未选替换则全队确认禁用，无法部分确认。 |
| A-09 | Low | [ ] | [ ] | 已取出槽位的 draft 静默 `continue`，少取仍完成节点。 |
| A-10 | Low | [ ] | [ ] | `TryRemoveExactEntry` 在 BonusIndex 失效时可能误删基础牌。 |
| A-11 | Low | [ ] | [ ] | `ExpeditionCardOfferContext.Altar` 枚举未用，祭坛与 `PendingCardOffer` 双轨。 |
| A-12 | Low | [ ] | [ ] | 祭坛成员 Tab 窄屏可能裁切。 |

---

## 事件

| 编号 | 严重度 | 修复 | 核对 | 问题摘要 / 位置 |
|------|--------|:----:|:----:|-----------------|
| E-01 | Critical | [ ] | [ ] | **融合同队员两张 Bonus 卡**：按 index 顺序删，删第一张后 index 错位。`TryFuseCards`。 |
| E-02 | Critical | [ ] | [ ] | `TryFuseCards` 忽略 `TryRemoveExactEntry` 返回值，只删一张仍继续发奖。 |
| E-03 | Critical | [ ] | [ ] | 融合时卡组满：先删两张再 `PendingCardOffer`，放弃则净亏两张。 |
| E-04 | Critical | [ ] | [ ] | **旅者礼物**：诅咒可 `AbandonCardOffer` 跳过，遗物照拿。`PlanTravelerGift`。 |
| E-05 | Critical | [ ] | [ ] | **ShowMessage 软锁**：`ApplyPendingCardAction` 失败后步骤不前进、协程不重跑。 |
| E-06 | High | [ ] | [ ] | 融合结果 owner 随机（`rng.NextIndex`），与所选两张牌角色无关。 |
| E-07 | High | [ ] | [ ] | `TryRollCardRewardForMember` 失败时 clone 素材并改 `OwnerCharacterId`，可能 B 持有 A 专属卡。 |
| E-08 | High | [ ] | [ ] | 旅者礼物：`PendingCardOffer` 与 `EventInteraction` UI 叠层抢输入。 |
| E-09 | High | [ ] | [ ] | `ApplyPendingCardAction` 失败无恢复/重试路径。 |
| E-10 | Medium | [ ] | [ ] | `TeamHpThen` / `TeamHealThen` 的 `after` 在 HP 动画**之前**执行（古神殿、训练木桩等）。 |
| E-11 | Medium | [ ] | [ ] | `PlanAbyssSacrifice`：`DeferredOutcome=Msg(..., ATK+1)` 在构造时即 +1，早于选牌移除。 |
| E-12 | Medium | [ ] | [ ] | `PlanBuyRandomCard` 先扣 30 金，roll 全失败无退款。 |
| E-13 | Medium | [ ] | [ ] | 旅者礼物诅咒在 ShowMessage 前已入队，文案与真实状态不同步。 |
| E-14 | Medium | [ ] | [ ] | 赌徒大赌 roll 空遗物仍显示「获得稀有遗物」。 |
| E-15 | Medium | [ ] | [ ] | 融合后卡组满无日志提示需处理替换 overlay。 |
| E-16 | Medium | [ ] | [ ] | 事件战失败未清 `PendingEventBattleBonusXp`。 |
| E-17 | Low | [ ] | [ ] | Legendary+Legendary 融合池空时净损一张。 |
| E-18 | Low | [ ] | [ ] | `RequiredFusionType` 字段未使用。 |
| E-19 | Low | [ ] | [ ] | 知识祭坛 / 灵魂祭坛选项仍为占位文案。 |
| E-20 | Low | [ ] | [ ] | 血祭坛选项 A 固定改 `Party[0]`，非所选角色。 |
| E-21 | Low | [ ] | [ ] | 混沌祭坛 `RemoveRandomBonusCard` 按 party 顺序非真随机。 |

---

## 回归测试清单（全部修复后勾选）

| 编号 | 修复 | 核对 | 测试项 |
|------|:----:|:----:|--------|
| TEST-01 | [ ] | [ ] | 祭坛：满 10 张替换、确认失败 UI、空 draft 引擎确认 |
| TEST-02 | [ ] | [ ] | 融合：同队员双 Bonus、失败回滚、满组放弃、owner 规则 |
| TEST-03 | [ ] | [ ] | 事件：ShowMessage 软锁、TravelerGift 诅咒不可 skip |
| TEST-04 | [ ] | [ ] | 天赋：各钩子抽样、SyncRunStateFromBattle、战后 HP 同步 |
| TEST-05 | [ ] | [ ] | Meta：`CampMetaState` 序列化/加载、`OutOfRunLevel` 成长 |

---

## 最终核对

- [ ] 全部 **Critical / High** 已关闭  
- [ ] 上表全部项已勾「核对」  
- 修复负责人：________　日期：________  
- 测试负责人：________　日期：________  
- 备注：

---

*共 59 项问题 + 5 项回归测试。PDF 版若乱码可忽略，以此 Markdown 为准。*
