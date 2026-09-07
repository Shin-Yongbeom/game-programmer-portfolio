using System;
using UnityEngine;

namespace Greyline.Progression
{
    public enum TrainingEffect { MaxHealth, HeavyDamage, GuardCapacity }
    [CreateAssetMenu(menuName = "Greyline/Production/Progression Catalog")]
    public sealed class ProgressionCatalog : ScriptableObject
    {
        [Serializable] public sealed class Skill
        {
            public string id, title, description, prerequisite;
            public int cost = 1;
            public TrainingEffect effect;
            public float value;
        }
        [Serializable] public sealed class EncounterReward
        {
            public string id, title;
            public int points = 2;
            public string sceneName;
            public Vector3 position;
            public float engagementRadius=13, leashRadius=24, turnGap=.4f;
            public EnemySpawn[] enemies = Array.Empty<EnemySpawn>();
        }
        [Serializable] public sealed class EnemySpawn
        {
            public Enemies.EnemyContentProfile profile;
            public Vector3 offset;
            public float yaw=180;
        }
        [HideInInspector] public int contentVersion;
        public string catalogId = "ryumyong-district";
        public int startingPoints = 1;
        public float baseHealth = 160, basePosture = 60;
        public Skill[] skills = Array.Empty<Skill>();
        public EncounterReward[] encounters = Array.Empty<EncounterReward>();
        public Skill FindSkill(string id) => Array.Find(skills, s => s.id == id);
        public EncounterReward FindEncounter(string id) => Array.Find(encounters, e => e.id == id);
    }
}
