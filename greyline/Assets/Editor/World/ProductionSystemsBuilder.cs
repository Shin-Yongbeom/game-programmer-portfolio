using System;
using System.Collections.Generic;
using System.Linq;
using Greyline.Interaction;
using Greyline.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greyline.World.EditorTools
{
    public static class ProductionSystemsBuilder
    {
        public const string CatalogPath = "Assets/_Project/Data/ProductionProgression.asset";
        public static ProgressionCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(CatalogPath);
            // Preserve authored content on later district rebuilds. New content belongs in this catalog.
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProgressionCatalog>();
                catalog.skills = new[] {
                    Skill("conditioning", "CONDITIONING", "+30 maximum health. Rest to recover the added capacity.", 1, TrainingEffect.MaxHealth, 30),
                    Skill("heavy-foundation", "HEAVY FOUNDATION", "+15% Heavy damage, including charged strikes.", 1, TrainingEffect.HeavyDamage, .15f),
                    Skill("rooted-guard", "ROOTED GUARD", "+24 guard capacity before posture breaks.", 1, TrainingEffect.GuardCapacity, 24),
                    Skill("heavy-mastery", "HEAVY MASTERY", "+20% additional Heavy damage.", 2, TrainingEffect.HeavyDamage, .2f, "heavy-foundation") };
                catalog.encounters = new[] {
                    new ProgressionCatalog.EncounterReward { id="university", title="UNIVERSITY", points=2 },
                    new ProgressionCatalog.EncounterReward { id="market", title="MARKET", points=2 },
                    new ProgressionCatalog.EncounterReward { id="plaza", title="WARDEN", points=3 } };
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (catalog.contentVersion == 0) SeedEncounterContent(catalog);
            ValidateCatalog(catalog);
            return catalog;
        }
        public static void Assemble(Scene scene, GameObject player)
        {
            var catalog = EnsureCatalog();
            var session = new GameObject("Production Session").AddComponent<ProductionSession>();
            session.Configure(catalog, player.GetComponent<Enemies.CombatHealth>());
            session.gameObject.AddComponent<UI.ProductionSessionView>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var input = player.GetComponent<InteractionInput>() ?? player.AddComponent<InteractionInput>(); input.Configure(actions);
            var driver = player.GetComponent<DistrictInteractionDriver>() ?? player.AddComponent<DistrictInteractionDriver>(); driver.Configure(input);
            Checkpoint(session.transform, "boulevard", "BOULEVARD FIELD STATION", ProductionDistrictBuilder.Spawn + new Vector3(1.5f, 0, 1.5f), ProductionDistrictBuilder.Spawn);
            Checkpoint(session.transform, "north-lane", "NORTH LANE FIELD STATION", new Vector3(65, .2f, 63), new Vector3(63, .2f, 62));
            Validate(scene);
        }
        private static ProgressionCatalog.Skill Skill(string id, string title, string description, int cost, TrainingEffect effect, float value, string prerequisite = "") =>
            new() { id=id, title=title, description=description, cost=cost, effect=effect, value=value, prerequisite=prerequisite };
        private static void SeedEncounterContent(ProgressionCatalog catalog)
        {
            var brawler = Profile("brawler", Enemies.EnemyArchetype.Brawler, 80, 12);
            var bruiser = Profile("bruiser", Enemies.EnemyArchetype.Bruiser, 100, 12);
            var boss = Profile("warden", Enemies.EnemyArchetype.Boss, 280, 18);
            foreach (var encounter in catalog.encounters)
            {
                encounter.sceneName = encounter.id == "market" ? "Market" : encounter.id == "plaza" ? "Plaza" : "University";
                encounter.position = encounter.id == "market" ? new Vector3(38,.2f,4) : encounter.id == "plaza" ? new Vector3(0,.2f,53) : new Vector3(-43,.2f,18);
                encounter.engagementRadius = encounter.id == "plaza" ? 17 : 13;
                encounter.leashRadius = encounter.id == "market" ? 30 : 24;
                int count = encounter.id == "market" ? 3 : encounter.id == "plaza" ? 1 : 2;
                encounter.enemies = Enumerable.Range(0,count).Select(i=>new ProgressionCatalog.EnemySpawn {
                    profile=encounter.id=="plaza" ? boss : i==count-1 ? bruiser : brawler,
                    offset=new Vector3((i-(count-1)*.5f)*3,0,3) }).ToArray();
            }
            catalog.contentVersion=1; EditorUtility.SetDirty(catalog);
        }
        private static Enemies.EnemyContentProfile Profile(string id, Enemies.EnemyArchetype role, float health, float damage)
        {
            string path="Assets/_Project/Data/Enemy_"+id+".asset";
            var profile=AssetDatabase.LoadAssetAtPath<Enemies.EnemyContentProfile>(path);
            if(profile!=null)return profile;
            profile=ScriptableObject.CreateInstance<Enemies.EnemyContentProfile>();
            profile.id=id;profile.archetype=role;profile.health=health;profile.damage=damage;
            if(role==Enemies.EnemyArchetype.Boss){profile.telegraph=.7f;profile.moveSpeed=2.1f;profile.visualScale=1.13f;}
            AssetDatabase.CreateAsset(profile,path);return profile;
        }
        private static void Checkpoint(Transform parent, string id, string title, Vector3 position, Vector3 spawn)
        {
            var station = new GameObject(title); station.transform.SetParent(parent); station.transform.position = position;
            station.AddComponent<ProductionCheckpoint>().Configure(id, title, spawn);
            // A compact field marker keeps the rest interaction legible without obstructing the route.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name = "Rest Marker"; marker.transform.SetParent(station.transform, false);
            marker.transform.localPosition = Vector3.up * .45f; marker.transform.localScale = new Vector3(.36f, .45f, .36f);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Environment/ProductionDistrict/District_green.mat");
            if (material != null) marker.GetComponent<Renderer>().sharedMaterial = material;
        }
        public static void Validate(Scene scene)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(CatalogPath);
            ValidateCatalog(catalog);
            var checkpoints = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProductionCheckpoint>(true)).ToArray();
            if (checkpoints.Length < 2 || checkpoints.Select(c=>c.Id).Distinct().Count()!=checkpoints.Length || checkpoints.Any(c=>string.IsNullOrWhiteSpace(c.Id)))
                throw new InvalidOperationException("Checkpoints require unique stable IDs and safe return locations.");
            var encounters = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PersistentEncounter>(true)).ToArray();
            if (encounters.Length != catalog.encounters.Length || encounters.Select(e=>e.Id).Distinct().Count()!=encounters.Length || encounters.Any(e=>catalog.FindEncounter(e.Id)==null))
                throw new InvalidOperationException("Encounter reward IDs must match the production catalog exactly.");
            Debug.Log($"PRODUCTION_SYSTEMS_VALIDATION_OK skills={catalog.skills.Length} checkpoints={checkpoints.Length} encounters={encounters.Length}");
        }
        public static void ValidateCatalog(ProgressionCatalog catalog)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(catalog.catalogId) || catalog.skills.Length==0 || catalog.startingPoints<0 || catalog.baseHealth<=0 || catalog.basePosture<=0)
                throw new InvalidOperationException("Invalid progression catalog.");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skill in catalog.skills)
            {
                if (skill==null || string.IsNullOrWhiteSpace(skill.id) || string.IsNullOrWhiteSpace(skill.title) || !unique.Add(skill.id) || skill.cost<=0 || skill.value<=0 || !Finite(skill.value) || !Enum.IsDefined(typeof(TrainingEffect),skill.effect))
                    throw new InvalidOperationException("Invalid skill ID/cost/effect.");
                var chain = new HashSet<string>(); var current = skill;
                while (!string.IsNullOrEmpty(current.prerequisite))
                {
                    if (!chain.Add(current.id) || (current=catalog.FindSkill(current.prerequisite))==null)
                        throw new InvalidOperationException("Skill prerequisite cycle or missing ID: " + skill.id);
                }
            }
            unique.Clear();
            foreach (var encounter in catalog.encounters)
            {
                if (encounter==null || string.IsNullOrWhiteSpace(encounter.id) || !unique.Add(encounter.id) || encounter.points<=0)
                    throw new InvalidOperationException("Invalid encounter reward ID/value.");
                if (encounter.enemies==null || encounter.enemies.Length==0 || string.IsNullOrWhiteSpace(encounter.sceneName) ||
                    encounter.engagementRadius<=0 || encounter.leashRadius<=encounter.engagementRadius || encounter.turnGap<0 ||
                    !Finite(encounter.engagementRadius) || !Finite(encounter.leashRadius) || !Finite(encounter.turnGap) || !Finite(encounter.position))
                    throw new InvalidOperationException("Invalid encounter roster/radii: "+encounter.id);
                foreach(var spawn in encounter.enemies)
                {
                    if(spawn==null || spawn.profile==null || !Finite(spawn.offset) || !Finite(spawn.yaw))throw new InvalidOperationException("Invalid enemy spawn/profile: "+encounter.id);
                    spawn.profile.Validate();
                }
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
