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
        [Tooltip("敌人技能池；非空时开战从池中随机 2-4 种技能组成 deck。")]
        public List<CardDefinitionSO> SkillPool = new();
        public int EnemyRandomDeckSize = 8;
        public int EnemySkillPickMin = 2;
        public int EnemySkillPickMax = 4;
        public List<string> Traits = new();
    }
}
