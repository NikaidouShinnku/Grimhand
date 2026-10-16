// Only Unity asset shells are substituted. Formatter, resolver, card data conversion,
// snapshot and battle/event logic are compiled from production source unchanged.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class ScriptableObject { public static T CreateInstance<T>() where T:new() => new T(); }
    public class Object { public static void DestroyImmediate(object value) {} }
    public class Sprite { }
    public class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) {} }
}
namespace Grimhand.Content
{
    public class CharacterVisualCatalogSO { public UnityEngine.Sprite GetCardPortrait(string id) => null; }
    public class CharacterDefinitionSO
    {
        public List<CardDefinitionSO> Deck = new();
        public List<CardDefinitionSO> SkillPool = new();
    }
    public class BattleSetupSO { public List<CharacterDefinitionSO> Combatants = new(); }
    public class ExpeditionSetupSO { public List<CardDefinitionSO> PlayerCardCatalog = new(); }
}
