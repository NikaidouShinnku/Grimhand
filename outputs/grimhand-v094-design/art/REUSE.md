# 复用资源

audio-reuse内五个WAV直接来自原项目Assets/The Grimhands Asset/sound，没有修改，没有宣称为新制作的专属音效。攻击暂复用battle_attack_warrior；防御、加甲、护甲受击和HP受击分别沿用同名通用声音。接入时仍需核对战斧命中节奏与音量，不保证无需调整。

effects-reuse/warrior_damage_effect.png直接来自原项目同名效果图。可先复用现有近战命中特效；它不能替代角色实际挥斧动作。

32张新牌沿用原项目的卡框、能量/类型图标、字体与牌面布局，统一使用unity-png/berserker_card_profile.png，不为每张牌另生成一张不一致的插画。通用UI和卡框不重复复制，仍由现有项目提供。

source内为生成候选原图，含已被排除的错误“半身像”和“待机第二帧”；请勿批量导入source。接入选用unity-png目录。pixel目录为小尺寸逻辑网格源文件，preview为节奏/姿态预览，不代替运行时配置。
