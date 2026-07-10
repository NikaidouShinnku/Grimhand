# GRIMHAND — 玩家存档系统设计

**文档版本：** v1.3  
**日期：** 2026-07-10  
**状态：** P1 局外存档已实施；P2–P4 待做  
**对齐代码：** `Assets/_Project/Scripts`（以 `CampMetaState` / `CampRosterState` / `ExpeditionRunState` 为准）

**v1.3 变更：** 补充 §6.4 已实施 / 未实施对照表（与代码一致）。  
**v1.2 变更：** 收藏可超上限入库；超上限禁止出征与商店开包（后者玩法后续实装）。**P1 局外存档已实现。**

---

## 1. 设计目标

| 目标 | 说明 |
|------|------|
| 局外进度永久继承 | 角色等级、天赋、军营收藏牌、局外金币等跨会话保留 |
| 局内远征可恢复 | 单次远征进行中因 Bug / 休息 / 断线可续玩，但不改变已走过的路线选择 |
| 单存档 | 每名玩家一个持续更新的档案，无多槽位 |
| 数据不丢失 | 写盘原子化、备份、损坏可恢复 |
| Steam 就绪 | 本地存档结构与 Steam Cloud 兼容；成就按常规独立游戏做法预留 |

**不在本阶段设计数值：** 远征结束发放多少局外经验 / 局外金币，仅预留结算钩子。

---

## 2. 数据分层

```
PlayerProfile（唯一存档，持续更新）
├── Meta（局外，永久）
│   ├── campMeta              → CampMetaState（角色局外等级/经验/天赋）
│   ├── campCollection        → CampCollectionState（共用收藏库，见 §2.3）
│   ├── campRoster            → CampRosterState（出征编队 + 各角色祭坛卡池分配）
│   ├── accountGold           → int（局外金币，后续可改名；当前未实装玩法）
│   └── collectionCapacity    → int（收藏上限，默认 30；商店可升级，见 §2.3）
├── ActiveRun（局内，可选，仅当有一次进行中的远征时存在）
│   └── expeditionRun         → ExpeditionRunState 序列化
└── MetaInfo
    ├── saveVersion
    ├── lastSavedUtc
    ├── integrityHash           → HMAC，防 casual 改档（见 §5.4）
    ├── steamIdHash             → 可选，用于 Cloud 冲突提示
    └── playTimeSeconds         → 可选统计
```

### 2.1 局外（Meta）— 永久、实时保存

| 数据 | 现有类型 | 新局默认值 | 保存时机 |
|------|----------|------------|----------|
| 角色局外等级 / 经验 | `CharacterMetaProgress.OutOfRunLevel/Xp` | **等级 0，经验 0**（替换当前 Demo 的 Lv.10） | 天赋营确认、任何 Meta 变更后立即写盘 |
| 天赋选择 | `SelectedSlot1/2TalentId` | 空 | 同上 |
| **军营共用收藏库** | `CampCollectionState.Entries` | **默认套牌拆入共用库**（见 §2.3） | 收藏变更后立即写盘 |
| **角色祭坛卡池** | `CampMemberLoadout.AltarPoolCardIds` | 从共用库分配到各角色 | 军营 / 管理界面确认后写盘 |
| 出征编队 | `CampRosterState.Members` | 3 人编队（与现营地流程一致） | 同上 |
| 局外金币 | `AccountGold` | **0** | 任何增减后立即写盘 |
| 收藏上限 | `CollectionCapacity` | **30** | 商店扩容后写盘 |

**说明：**

- **共用收藏库**与**角色祭坛卡池**是两层结构（见 §2.3）。远征内战斗默认套牌仍来自 Encounter 配置 + 祭坛已提取的 `BonusCards`。
- `ExpeditionRunState.Gold` 为**局内远征金币**，保存在 `ActiveRun` 中，**不**写入局外 `AccountGold`。
- 远征结束结算：将部分收益转为局外经验 / 局外金币（数值待定），写入 Meta 后清空 `ActiveRun`。

### 2.3 军营收藏与祭坛卡池（v1.1）

#### 两层结构

| 层 | 含义 | 上限 | 重复卡 |
|----|------|------|--------|
| **共用收藏库** `CampCollection` | 玩家在营地拥有的全部卡牌「仓库」 | 默认 **30** 张（`CollectionCapacity`，全角色共用计数） | **允许**同一 `cardId` 出现多次 |
| **角色祭坛卡池** `AltarPool` | 分配到某角色名下、远征祭坛可提取的牌池 | 每角色独立列表（槽位暂定 10，与收藏上限无关） | 允许；从收藏库分配 |

```
CampCollection（共用，≤ CollectionCapacity）
  [天神下凡, 天神下凡, 盾击, …]     ← 可重复，总数不超过上限

CampRoster.Members[战士].AltarPool  ← 仅该角色祭坛可见
CampRoster.Members[法老].AltarPool
CampRoster.Members[恶魔].AltarPool
```

- **分配规则：** 收藏库中的牌「放入」某角色祭坛池时，仍归该角色所有；祭坛 UI 只显示该角色 `AltarPool` 内未提取的牌（与现 `RunStartCampDecks` / `ExtractedCampCollectionIndices` 对齐，数据源改为 `AltarPool`）。
- **与远征卡组：** 收藏库 / 祭坛池 **不等于** 战斗初始套牌；进战默认套牌 + 祭坛 `BonusCards` 规则不变。

#### 上限与升级

| 项 | 默认值 | 后续 |
|----|--------|------|
| `CollectionCapacity` | **30** | 商店出售「扩容」；升级后写入存档 |
| 单角色祭坛卡池槽位 | 10（暂定，与现 `CampRosterState.DeckSize` 对齐） | 可与收藏上限独立调整 |

#### 超上限行为（v1.2，后续实装玩法拦截）

收藏库 **`Entries.Count` 可以大于 `CollectionCapacity`**（仍允许入库 / 获得新卡）。

当 **超过上限** 时（`Count > CollectionCapacity`）：

| 行为 | 是否允许 |
|------|----------|
| 卡牌继续入库 / 获得 | **允许** |
| **开始远征**（传送门出发） | **禁止** |
| **商店开卡包** | **禁止**（后续实装） |
| 军营整理 / 天赋 / 其他营地浏览 | 允许 |

代码入口：`CampCollectionRules.IsOverCapacity` / `BlocksExpeditionStart` / `BlocksShopCardPack`（P1 先实现判定与出征拦截，商店开包留钩子）。

#### 新局默认

1. 按各角色 `CharacterDefinitionSO` 默认套牌，生成**初始收藏库条目**（可含重复 cardId）。
2. 初始收藏总数 **≤ 30**；超出时按 deterministic 规则截断（实施时定）。
3. 默认将各角色默认牌**同步分配**到对应 `AltarPool`。

#### 未实装（预留）

- **军营卡牌管理界面：** 收藏库 ↔ 角色祭坛池拖放 / 分配 / 移除；操作结束即 `SaveMeta()`。
- **商店升级收藏上限：** `CollectionCapacity += N`，数值后填。

#### 与现有代码的迁移

| 现字段 | 目标 |
|--------|------|
| `CampMemberLoadout.DeckCardIds` | 拆为 `AltarPoolCardIds`（角色祭坛池） |
| （无） | 新增 `CampCollectionState` + `CollectionCapacity` |
| `CampRosterState.DeckSize = 10` | 单角色祭坛池槽位；**勿**与收藏上限 30 混用 |

### 2.2 局内（ActiveRun）— 仅本次远征

| 数据 | 现有类型 | 生命周期 |
|------|----------|----------|
| 远征阶段 / 地图 / 路线 | `ExpeditionRunState` 全套 | 远征开始创建；完成 / 失败 / 放弃后删除 |
| 局内金币 | `run.Gold` | 同上 |
| 队伍 HP、卡组、遗物、消耗品等 | `Party` / `Relics` / … | 同上 |
| 祭坛取牌进度 | `ExtractedCampCollectionIndices` / `RunStartCampDecks` | 同上 |

**续玩规则（已确认）：**

1. 玩家**不能**回到地图重选已经走过的节点路线。
2. 断线 / 重启后，从**最近一次安全 checkpoint** 恢复。
3. 若断于**战斗中**（`Phase == InBattle`）：不保存战斗半回合状态；恢复时**重开当前这场战斗**（同一遭遇、保留进入战斗前的远征状态：HP、卡组、遗物、局内金币等）。
4. 若断于**非战斗节点**（选路后、奖励、祭坛、商店、事件等）：恢复到该 Phase 的入口 UI，选项与随机结果以存档为准，不可「悔棋」换路。

---

## 3. 安全 Checkpoint 定义

仅在以下 Phase **稳定落地后** 写入 `ActiveRun`（称为安全点）：

| Phase | 恢复行为 |
|-------|----------|
| `RouteSelect` | 显示当前地图，保留 `PendingRoutes`；玩家只能继续选**当前层**未执行的选项，不能改历史层 |
| `RewardPickup` | 恢复奖励界面与 `PendingRewardPickup` |
| `EventChoice` / `EventAftermath` / `EventInteraction` | 恢复事件状态机当前步 |
| `ShrineChoice` | 恢复祭坛（含 `CardAltar` 草稿） |
| `ShopVisit` | 恢复商店库存与已购状态 |
| `InBattle` | **不存战斗内快照**；存档中记录 `resumeBattle = true` + 当前 `CurrentBattleConfig` 标识；加载时重新 `StartExpeditionBattle()` |

**不写入 ActiveRun 的时机：**

- 战斗回合进行中（规划/结算动画中间）
- 任何「二选一尚未确认」的瞬时 UI 状态（应先完成确认或回滚到上一个安全点再存）

**额外触发写盘（局内）：**

- 每个安全 checkpoint 达成后
- 应用失去焦点 / 暂停菜单 / 退出到桌面（`OnApplicationPause` / `OnApplicationQuit`）
- 远征胜利（`RunComplete`）或失败（`RunFailed`）处理完 Meta 结算后 **删除** `ActiveRun` 并写 Meta

---

## 4. 新局与缺档

### 4.1 首次启动 / 无存档

创建 `PlayerProfile` 默认值：

```
saveVersion          = 1
accountGold          = 0
collectionCapacity   = 30
campCollection       = 由默认套牌生成的初始条目（总数 ≤ 30，可含重复 cardId）
campRoster           = 3 人编队 + 各角色 AltarPool 默认分配
campMeta             = 所有可玩角色 OutOfRunLevel=0, OutOfRunXp=0, 天赋空
activeRun            = null
```

可玩角色列表与 `CampRosterBuilder.PlayableCharacterIds` / `TalentCatalog.PlayableCharacterIds` 保持一致。

### 4.2 读档失败

1. 若 `profile.json` 损坏：尝试加载 `profile.bak`（上一份成功备份）
2. 若备份仍失败：保留 `.corrupt` 副本供排查，生成**新局默认档**，并提示玩家（避免静默清档）
3. 开发构建可输出详细日志；发行版用简短本地化文案

---

## 5. 文件格式与路径

### 5.1 格式

- **JSON**（推荐 Newtonsoft.Json 或 Unity `JsonUtility` + 手写 DTO，避免直接序列化含 `Dictionary` 的运行时对象）
- 顶层 DTO：`PlayerProfileSaveData`（与运行时模型解耦，便于版本迁移）

### 5.2 路径（本地）

```
{Application.persistentDataPath}/saves/
  profile.json          ← 当前档
  profile.bak           ← 轮换备份（写成功前一份）
  profile.tmp           ← 写入中间文件（写完校验后替换 profile.json）
  corrupt/              ← 损坏文件归档（带时间戳）
```

### 5.3 写盘流程（保证不丢档）

```
1. 内存中组装 PlayerProfileSaveData
2. 计算 integrityHash（见 §5.4）
3. 序列化为 JSON 字符串
4. 写入 profile.tmp
5. fsync / Flush（平台允许时）
6. 若 profile.json 存在 → 复制为 profile.bak（覆盖旧 bak）
7. 原子替换：Delete(profile.json) + Move(profile.tmp → profile.json)
8. 失败则保留 profile.tmp，下次启动尝试恢复
```

**局外 Meta 变更：** 走完整上述流程（实时保存，可 debounce 300ms 合并连续编辑，但营地「确认」类操作必须立即落盘）。

**局内 ActiveRun：** 仅在安全 checkpoint 走上述流程；战斗内不频繁写盘。

### 5.4 防篡改与合法性校验（常规方案）

> **定位：** 单机 PvE + Steam 成就。目标不是「绝对防破解」，而是 **防止随手改 JSON**、**防止非法数据进游戏**，并与 Steam 服务端成就互补。

#### 推荐组合（本项目采用）

| 层级 | 手段 | 作用 |
|------|------|------|
| **完整性** | 存档 **HMAC-SHA256**（`integrityHash` = HMAC(canonicalJson, appSecret)） | 改金币/等级后 hash 对不上 → 读档拒绝或回退 bak |
| **合法性** | 读档后 **SaveValidator** 校验 | 即使 hash 被伪造，仍拒绝不合理数据 |
| **备份** | `profile.bak` + atomic write | 防断电损坏 |
| **成就** | Steam 成就走 **Steam 服务器**（Phase 4） | 改本地档不影响 Steam 已解锁成就 |

`appSecret`：编译进游戏的固定字节（可轻度混淆），**不**存于存档。

#### SaveValidator 校验项（读档必跑）

| 检查 | 规则 |
|------|------|
| 版本 | `saveVersion` 已知且可迁移 |
| 局外金币 | `accountGold >= 0`，`< 合理上限`（如 999_999_999） |
| 角色等级 / 经验 | `>= 0`，等级 ≤ 配置表最大 |
| 天赋 ID | 存在于 `TalentCatalog`，且满足解锁等级 |
| 收藏库 | `entries.Count` 可 **>** `collectionCapacity`（允许超上限入库）；校验 cardId 合法 |
| 收藏容量 | `collectionCapacity >= 30`（默认下限），`<= 硬顶`（如 999） |
| 祭坛池 | cardId 合法；`OwnerCharacterId` 匹配（`CampDeckOwnershipRules`） |
| 重复卡 | **允许**；只校验条数与 ID，不校验唯一性 |
| ActiveRun | Phase 合法、Party ≤ 3、局内金币 ≥ 0 |

**校验失败策略：**

1. `integrityHash` 不匹配 → 尝试 `profile.bak`；仍失败 → corrupt 归档 + 新局
2. hash 匹配但 Validator 失败 → 同上，**不**静默钳制到非法值

#### 可选增强（非 P1 必须）

- AES 加密整个 JSON（调试不便，一般不如 HMAC + 校验）
- Steam Cloud 与本机相同校验逻辑

#### 不做的（单机常规）

- 不要求在线验证才能玩
- 不为主流程做服务器存档（除非以后做排行榜 / 对战）

---

## 6. 代码架构（实施清单）

### 6.1 模块与文件（`Assets/_Project/Scripts/`）

| 类型 | 路径 | 状态 |
|------|------|------|
| `CampCollectionState` | `Expedition/Model/CampCollectionState.cs` | **已实施** |
| `CampCollectionRules` | `Expedition/CampCollectionRules.cs` | **已实施** |
| `CampCollectionBuilder` | `Expedition/CampCollectionBuilder.cs` | **已实施** |
| `PlayerProfileState` | `Persistence/PlayerProfileState.cs` | **已实施** |
| `PlayerProfileSaveData` + DTO | `Persistence/PlayerProfileSaveData.cs` | **已实施** |
| `SaveDataMapper` | `Persistence/SaveDataMapper.cs` | **已实施** |
| `SaveValidationContext` | `Persistence/SaveValidationContext.cs` | **已实施** |
| `SaveValidator` | `Persistence/SaveValidator.cs` | **已实施** |
| `SaveIntegrity` | `Persistence/SaveIntegrity.cs` | **已实施** |
| `ISaveStorage` | `Persistence/ISaveStorage.cs` | **已实施** |
| `LocalFileSaveStorage` | `Persistence/LocalFileSaveStorage.cs` | **已实施** |
| `SaveService` | `Persistence/SaveService.cs` | **已实施** |
| `SaveLoadResult` | `Persistence/SaveLoadResult.cs` | **已实施** |
| `PlayerProfileFactory` | `Presentation/Camp/PlayerProfileFactory.cs` | **已实施** |
| `SaveValidationContextBuilder` | `Presentation/Camp/SaveValidationContextBuilder.cs` | **已实施** |
| `PlayerSaveDebugMenu` | `Editor/PlayerSaveDebugMenu.cs` | **已实施** |
| `SaveMigration` | — | **未实施** |
| `PlayerSaveTests` | `Tests/Battle/PlayerSaveTests.cs` | **已实施** |
| 程序集 | `Persistence/Grimhand.Persistence.asmdef` | **已实施** |

### 6.2 接入点

| 位置 | 改动 | 状态 |
|------|------|------|
| `GameFlowController.Start` | `SaveService.LoadOrCreate` → 注入 Meta / Roster / Collection | **已实施** |
| `GameFlowController.OnMetaSaved` / `OnRosterSaved` | `SaveProfile()` | **已实施** |
| `GameFlowController.OnApplicationPause` / `Quit` | `SaveProfile()` | **已实施** |
| `GameFlowController.BeginExpedition` | 超上限拦截 Toast | **已实施** |
| `CampMetaState.CreateNewProfile()` | 新局 Lv.0（替代 Demo Lv.10） | **已实施** |
| `ExpeditionEngine` 各 Phase 落地 | `SaveRunCheckpoint()` | **未实施（P2）** |
| 战斗开始 / 战斗结束 checkpoint | ActiveRun 写盘 | **未实施（P2）** |
| `RunComplete` / `RunFailed` | `ApplyRunSettlement()` + 清 ActiveRun | **未实施（P3）** |
| 主菜单 / 启动 | `activeRun != null` →「继续远征」 | **未实施（P2）** |
| 商店开包超上限拦截 | `CampCollectionRules.BlocksShopCardPack` | **未实施（规则钩子已有）** |
| 收藏入库超上限 | 玩法允许入库 | **未实施（无获得卡牌流程）** |
| 军营卡牌管理 UI | 收藏 ↔ 祭坛池分配 | **未实施** |
| 商店扩容 `CollectionCapacity` | 数值 + UI | **未实施** |
| Steam Cloud / 成就 | Phase 4 | **未实施** |

### 6.3 与现有类的关系

- **保留** `CampMetaState`、`CampRosterState`、`ExpeditionRunState` 为运行时权威对象。
- **废弃** 正式流程中的 `CampMetaState.CreateDefaultDemo()`（Lv.10）；仅 Editor / 测试工具可用。
- **`AccountGold`** 已挂在 `PlayerProfileState.AccountGold`；与 `ExpeditionRunState.Gold` 严格区分。
- **`CampMemberLoadout.DeckCardIds`** 仍兼作角色祭坛池（文档目标名 `AltarPoolCardIds` 尚未重命名）。

### 6.4 P1 已实施行为摘要

| 能力 | 行为 |
|------|------|
| 读档 | `profile.json` → hash 校验 → Validator → 运行时模型；失败试 `profile.bak`；仍失败 corrupt 归档 + 新局 |
| 写盘 | tmp → bak 轮换 → 原子替换；`lastSavedUtc` + `integrityHash` 写入 |
| 新局 | Lv.0 Meta、AccountGold=0、CollectionCapacity=30、Collection 由默认编队生成 |
| 实时保存 | 天赋营确认、军营确认、暂停、退出 |
| 超上限出征 | `Count > CollectionCapacity` 时传送门 Toast 拦截 |
| Editor | `Grimhand → Save →` Log Path / Open Folder / Delete / Save Now |

### 6.5 P1 已知限制 / 未做项

| 项 | 说明 |
|----|------|
| `activeRun` | DTO 与 Mapper **未**序列化局内远征；P2 再补 |
| `SaveMigration` | 仅 `saveVersion = 1`；无 v1→v2 链 |
| Debounce | Meta 每次变更立即写盘，无 300ms 合并 |
| 读档 UI 提示 | Fallback 新局仅 Console 日志，无玩家弹窗 |
| 收藏 ↔ 祭坛池 | 两层数据模型已有 Collection，但**无**管理 UI；Roster 编辑不改变 Collection |
| `AltarPoolCardIds` 字段重命名 | 仍用 `DeckCardIds` |
| 商店开包 / 入库超上限 | 规则函数已有，玩法未接 |

---

## 7. 远征结算钩子（数值待定）

```csharp
// 伪代码 — 实施时放在 ExpeditionEngine 或 RunSettlementRules
void ApplyRunSettlement(ExpeditionRunState run, CampMetaState meta, ref int accountGold)
{
    // TODO: 胜利/失败分支
    // meta.Characters[id].OutOfRunXp += ???
    // LevelUpRules.Apply(meta)  // 若有
    // accountGold += ???
}
```

本阶段只实现：**被调用、写 Meta、清 ActiveRun、Save**；具体公式后续策划填表。

---

## 8. Steam 科普与接入（Phase 2，本方案预留）

### 8.1 Steam 成就是什么

- **成就（Achievements）**：Steam 平台记录的永久徽章，例如「首次通关」「收集 10 张遗物」。
- **与存档分离**：成就状态存在 Steam 服务器，**不要**只靠本地 JSON 判定是否解锁（否则换电脑/云存档不同步会乱）。
- **常规做法**：游戏内发生条件 → 调用 `SteamUserStats.SetAchievement("ACH_ID")` → `StoreStats()` 上传。

**建议首批成就（示例，可删减）：**

| ID | 说明 |
|----|------|
| `FIRST_VICTORY` | 首次远征胜利 |
| `DEFEAT_BOSS` | 击败 Boss |
| `FULL_COLLECTION` | 军营收藏库达到当前上限 |
| `CHAR_LEVEL_10` | 任意角色局外等级 10 |

实施顺序：**存档稳定后再接**，避免成就与旧档逻辑打架。

### 8.2 Steam Cloud

- 在 Steamworks 后台勾选 Cloud，同步 `saves/` 目录（或仅 `profile.json` + `profile.bak`）。
- 启动时：本地 vs Cloud **比 `lastSavedUtc`**，取较新；若冲突且时间接近，弹窗让玩家选（常规独立游戏做法）。
- `ISaveStorage` 增加 `SteamCloudSaveStorage` 装饰器，优先 Cloud 合并后再写本地。

### 8.3 Steam 统计（Stats）

- 可选：累计「远征次数」「总击杀」等，用于成就进度条。
- 与 `PlayerProfile` 独立；读档不影响 Stats 完整性。

### 8.4 依赖

- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) 或 Facepunch.Steamworks
- AppID、成就 API 名在 Steam Partner 后台配置

**本阶段（Phase 1）可不引 Steam SDK**，仅保证存档路径与单文件结构适合 Cloud。

---

## 9. 版本迁移

```json
{
  "saveVersion": 1,
  "lastSavedUtc": "2026-07-10T12:00:00Z",
  "integrityHash": "base64-hmac...",
  "accountGold": 0,
  "collectionCapacity": 30,
  "collectionEntries": ["card_id_a", "card_id_a", "card_id_b"],
  "characters": [],
  "rosterMembers": [
    {
      "characterDefinitionId": "char_knight",
      "displayName": "战士",
      "deckCardIds": ["card_id_a", "card_id_b"]
    }
  ]
}
```

> **P1 实际 DTO 字段名：** `collectionEntries`（非嵌套 `campCollection`）、`characters` / `rosterMembers`（非嵌套 `campMeta` / `campRoster`）。`activeRun` 尚未写入 JSON。

- 每次改 DTO 字段：`saveVersion++`，在 `SaveMigration` 写 `MigrateV1ToV2`。
- 原则：**只增字段、旧字段保留默认值**，避免破坏性升级。

---

## 10. 实施阶段

| 阶段 | 内容 | 验收 |
|------|------|------|
| **P1 局外存档** | DTO + Validator + HMAC + Meta 实时保存；共用收藏 30 + 角色祭坛池；新局默认 | **已实施** — 改天赋/军营后重启仍在；改 JSON 触发 hash 失败 |
| **P2 局内 Checkpoint** | ActiveRun 序列化；安全点写盘；继续远征；战斗中恢复 = 重开该战 | 选路后杀进程，续玩不能改历史路线；战中杀进程，重开同一场战斗 |
| **P3 结算钩子** | RunComplete/Failed → 空结算 + 清 ActiveRun | 远征结束回营地，无残留 ActiveRun |
| **P4 Steam** | Cloud 同步 + 基础成就 | 换机 Cloud 恢复；解锁成就在 Steam 客户端可见 |

---

## 11. 测试要点

### 11.1 Unity 手动测试（存档延续性）

**存档路径**

- 菜单：**Grimhand → Save → Log Save Path**（路径会复制到剪贴板）
- 典型位置：`%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\saves\`
- 文件：`profile.json`（当前）、`profile.bak`（上一份成功备份）

**A. 基础延续性**

1. **Grimhand → Save → Delete Save (New Game)** 清空旧档。
2. 进入 Play Mode，打开**天赋营**，确认角色为 **Lv.0**（不再是 Demo 的 Lv.10）。
3. 在天赋营选一项天赋并确认 → 退出 Play Mode → 再次 Play。
4. 预期：天赋选择仍在；Console 出现 `已从 profile.json 读档` 或无明显 Fallback 日志。

**B. 军营 / 编队**

1. 打开**军营**，调换角色或换牌 → 保存退出 overlay。
2. 停止 Play 再进入 → 编队与牌组与上次一致。

**C. 写盘时机**

1. Play 中改天赋后 **Grimhand → Save → Save Now (Play Mode)**。
2. 用记事本打开 `profile.json`，确认 `lastSavedUtc` 更新、`characters` / `rosterMembers` 与游戏内一致。

**D. 防篡改**

1. 完全退出 Play Mode（确保文件未被占用）。
2. 打开 `profile.json`，把 `"accountGold": 0` 改成 `99999`（**不要**改 hash）。
3. 再 Play → Console 应提示从 **backup** 恢复或 Fallback 新局；不应静默保留 99999。

**E. 超上限出征拦截（规则已接，入库 UI 后续）**

1. 退出 Play，在 JSON 的 `collectionEntries` 数组中手动追加条目直至 **超过** `collectionCapacity`（30）。
2. 若 hash 失效，可先 Play 一次让游戏正常写盘，再只追加条目并重算 hash（或改完后删主档只留 bak 测恢复）。
3. 更简单：在 Test Runner 跑 `CampCollectionRules_BlocksExpeditionWhenOverCapacity`。
4. 出征时预期 Toast：**「军营收藏 x/30 超出上限，请整理后再出发。」**

**F. 删档重测**

- **Grimhand → Save → Delete Save (New Game)** → 再 Play = 全新 Lv.0 档。

**自动化**

- Test Runner → `Grimhand.Battle.Tests` → `PlayerSaveTests`（RoundTrip / Tamper / 超上限规则）。

### 11.2 检查清单

- [ ] 新档：等级 0、局外金币 0、收藏库为默认套牌且 ≤30
- [ ] 收藏库可存重复 cardId；**超过 capacity 仍可通过 Validator**（出征/开包玩法拦截另做）
- [ ] 角色祭坛池与收藏库分配独立；祭坛仅见本角色池
- [ ] 篡改 `accountGold` 或 `integrityHash` → 读档失败并回退 bak
- [ ] Meta 变更后立即杀进程，数据不丢
- [ ] 远征各 Phase checkpoint 恢复 UI 正确
- [ ] 战斗中强退 → 重开战斗，HP/卡组/遗物与进战前一致
- [ ] 写盘中断（模拟：写 tmp 后崩溃）→ 启动仍可用 bak 或 tmp 恢复
- [ ] 远征结束 → ActiveRun 清除，Meta 保留
- [ ] saveVersion 迁移：用 v1 文件启动 v2 客户端不崩溃

---

## 12. 术语对照

| 玩家/UI 用语 | 代码字段（建议） | 说明 |
|--------------|------------------|------|
| 局外金币 | `AccountGold` | 营地永久货币，实时保存 |
| 远征金币 / 局内金币 | `ExpeditionRunState.Gold` | 仅 ActiveRun |
| 局外等级 / 经验 | `OutOfRunLevel` / `OutOfRunXp` | CampMeta |
| 军营共用收藏 | `CampCollection.Entries` + `CollectionCapacity` | 默认 30 张，全角色共用计数，可重复 |
| 角色祭坛卡池 | `AltarPoolCardIds` | 分角色，祭坛提取来源 |
| 继续远征 | `activeRun != null` | 单存档内唯一进行中远征 |
| 收藏上限升级 | `CollectionCapacity` | 商店扩容，写入存档 |

---

## 13. 已确认的产品决策

### 2026-07-10

1. 局内进度保存：**是**，仅服务当前远征，用于 Bug / 休息 / 断线。
2. 断线续玩：**不能重选节点**；战斗内断线 = **重开当前战斗**。
3. 局外内容：**全部实时自动保存**（卡牌收藏、角色等级、局外金币等）。
4. 远征结束给局外经验与局外金币：**是**，数值后补，先留钩子。
5. 存档数量：**单档**，持续更新。
6. 新局默认：等级 0、局外金币 0、卡牌仅默认套牌。
7. Steam：成就 / Cloud 按常规独立游戏，**Phase 4** 再接。

### 2026-07-10（v1.1 补充）

8. **军营共用收藏上限默认 30**，全角色共用计数；**允许重复 cardId**。
9. **角色祭坛卡池分角色存放**；从收藏库分配到各角色，祭坛 UI 仍按角色隔离。
10. **商店可升级收藏上限**（未实装，存档字段 `CollectionCapacity` 预留）。
11. **军营卡牌管理界面**（未实装，分配/移除操作后实时 Save）。
12. **防改档：** HMAC 完整性 + 读档 Validator；成就走 Steam 服务端（Phase 4）。

### 2026-07-10（v1.2 补充）

13. 收藏 **可超过 30 入库**；超上限时 **禁止开始远征、禁止商店开包**（后者后续实装）；整理后可再出发。

---

**规格来源：** 本文档；数值与成就 ID 可在附录增量更新。实施进度以 §6.4 / §6.5 与 §10 为准。
