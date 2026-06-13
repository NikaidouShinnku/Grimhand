using System.Collections.Generic;
using Grimhand.Battle.Model;
using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Grimhand/Character Definition")]
    public class CharacterDefinitionSO : ScriptableObject
    {
        public string CharacterId = "char_id";
        public string DisplayName = "角色";
        public TeamSide Team = TeamSide.Player;
        public FormationSlot Slot = FormationSlot.Front;
        public int Level = 1;
        public int MaxHp = 30;
        public int BaseAttack = 5;
        public int BaseDefense = 2;
        public int Speed = 5;
        public List<CardDefinitionSO> Deck = new();
        [Tooltip("敌人技能池：列表里每种牌默认 1 张；重复引用同一卡牌即多张。开战时所有小怪牌汇入同一抽牌堆并洗牌。")]
        public List<CardDefinitionSO> SkillPool = new();
        public List<string> Traits = new();
    }
}
