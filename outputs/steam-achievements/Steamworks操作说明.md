# Grimhand Steam成就接入

AppID：5111710，Windows Depot：5111712（两者不可混用）。此目录为后台上传素材，不需要把图标路径写进游戏代码。未修改Steam后台，也未提交任何真实成就。

## 上传前

1. 制作新的Windows Build。成就逻辑在游戏代码中，旧Build不会触发新成就。
2. 用拥有本游戏许可的开发账号，从Steam客户端安装并运行测试分支。无需等商店正式发售；不要把steam_appid.txt放入正式发行包。
3. 先使用独立测试存档，不清空现有玩家存档或云档，不把开发者进度SavesDir打进发行包。

## 后台配置

1. 打开Steamworks中的Grimhand，选择 Edit Steamworks Settings → Stats & Achievements → Achievements。若出现新版编辑器入口，进入新版编辑器。
2. 配置语言 English 与 Simplified Chinese（schinese）。导出后台模板，确认列名与本目录CSV一致；若后台页面仍使用旧版编辑器，则按 achievement-catalog.json 逐项填写，API名称必须原样复制。
3. Bulk Import/Export 中选择本目录的 `5111710-achievements-definitions.csv` 与 `5111710-achievements-localization.csv`，以及 icons 文件夹内全部26张 `*_ACHIEVED.png`。无需上传 sources、样稿、预览图或提示词。早期CSV文件名前缀误写不影响已导入的成就，不要为改文件名删除或重建后台成就；实际映射由API名称决定。
4. 勾选 Automatically generate unachieved icons，自动生成未解锁灰度图；也可在每项图标处点击Generate。
5. 查看Pending Changes：应为26项成就，permission均为Client，API名称与代码一致，中文英文标题和说明均完整。不勾选删除现有成就；若后台已有同名API项，先检查将被修改的字段。
6. 点击Save imported data，然后回到Publish页面 Review Changes 并发布这些Steamworks设置。保存草稿不等于发布。
7. 商店页Basic Info → Supported Features 勾选Steam Achievements，保存并按后台要求发布商店页更改。

第一版全部放基础游戏集合，不绑定DLC，不配置进度统计条。游戏端自行判定达成条件，所以不必另建统计项。

## 验收

- 打开开发测试分支，确认Player.log出现Steam正确AppID初始化日志，而不是API初始化失败。
- 开始正式远征，赢一场普通战斗，检查「初露锋芒」。战败进入结算应解锁「一段旅途」；主动放弃不应解锁。
- 检查对应Steam成就页面与游戏日志，不只看弹窗：Overlay关闭时可能不显示弹窗。未发售应用的展示限制也可能影响库/社区可见性，需以API读取与日志为准。
- 验证训练场、教程、跳层测试不会触发战斗和远征成就。
- 测试Steam离线/退出后正常游玩，已达成记录应写入本地；联网后重新从Steam运行同一存档，应补交，不重复清除或回退已得成就。
- 换机通过云档读取后，在同一Steam账号上核对成就与未提交记录。真实服务器往返和双机云同步必须用测试账号实际验证；单元测试不能替代。

如需清除测试成就，仅对自己的开发测试账号使用Steam控制台 `achievement_clear 5111710 API_NAME`。不要对正式玩家执行 `reset_all_stats`。清除Steam后如果本地仍有达成记录，下次运行会补交，这是正常行为。没有加入玩家可用的成就清空按钮。

## 判定细则

- 一段旅途：正常完成或战败结束一次正式远征；主动放弃、教程、训练、跳层测试不计。
- 绝处逢生：至少两名真实玩家角色参战，胜利时仅一人存活，且必须是Boss战；召唤物不计。
- 留有余地：进入Boss战时至少有三个实际消耗品，本场一个也未使用，并胜利。
- 三张应对按三张不同敌方攻击牌计数，AOE不是按目标人数计数。
- 一击清场按同一张攻击牌的实际直接击杀累计，不把后续中毒伤害算进去。
- 好牌不怕晚：同一张继承牌连续跨过三个回合结束后实际使用；离手重抽不续算。
- 收藏类按当前收藏中不同卡牌定义计数，同名重复、升级、刻印版本不重复增加种类。
- 铭刻之路：成功刻印累计五张，同名的不同实例可以累计；移出收藏不减次数。首次迁移旧档时，仅用收藏中仍保留刻印来源标记的牌补入可证明的最低次数，无法恢复早已移出收藏的刻印历史，也不会把卡包牌算作刻印。
- 局外满级当前为Lv.10。群星齐耀固定要求这一版的战士、法老、恶魔、毒蛇女王、巫妖与裂斧，不因将来新增角色增加要求。
- 旧玩家只补发现有收藏、等级等可证明的成就，不因保留了巫妖/裂斧资格就补发Boss战成就。
- 本地已得记录随v8档案保存，Steam失败时重试；没有调用ResetAllStats或ClearAchievement的游戏逻辑。Steam账号与档案记录不一致时只暂停成就提交，不阻止游戏。
- 启动时选择较新云档/本地档后，仅对同一Steam账号的成就记录做并集，刻印累计取较大值而非相加，避免反复同步重复计数。金币、卡牌、编队及远征进度仍遵循原有的整档选择，不做跨档拼接。运行中发现更新的云档时仍会拒绝覆盖；请避免两台机器同时推进同一个档案。
- 首次从v7写入v8前，在存档目录的migration_backups中保留 `profile_v7_before_achievement_update.sav`。不删除旧进度，不要求玩家重置存档。

## 美术

icons：26张256×256 PNG，上传用。sources：26张生成原图，保留用于调整。all-26-preview.png：仅用于浏览，不上传。使用内置image_gen生成，源提示词见 icon-generation-plan.json 与 samples-v1/prompts.json；导出只做最近邻尺寸整理，不重新绘制。

## 官方参考

- https://partner.steamgames.com/doc/features/achievements/ach_guide
- https://partner.steamgames.com/doc/api/ISteamUserStats

后台页面可能更新；以上批量列名和图标命名按2026-10-19核对的官方文档准备。实际上传前仍需检查后台预览，不自动发布或删除已有设置。
