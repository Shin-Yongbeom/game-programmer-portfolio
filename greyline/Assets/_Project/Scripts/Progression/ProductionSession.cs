using System;
using System.IO;
using System.Linq;
using Greyline.Combat;
using Greyline.Enemies;
using Greyline.Interaction;
using Greyline.Player;
using Greyline.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greyline.Progression
{
    [DefaultExecutionOrder(-200)]
    public sealed class ProductionSession : MonoBehaviour
    {
        public const string QaSaveRootVariable = "PROJECT_PYONGYANG_QA_SAVE_ROOT";
        public static ProductionSession Current { get; private set; }
        public static bool GameplayBlocked => Current != null && (Current.MenuOpen || Time.frameCount <= Current.blockedThroughFrame);
        [SerializeField] private ProgressionCatalog catalog;
        [SerializeField] private CombatHealth player;
        private ProductionSaveData data;
        private ProductionSaveStore store;
        private static ProductionSaveData retrySnapshot;
        private static string retryPath;
        private EnemyEncounterCoordinator[] encounters;
        private int blockedThroughFrame = -1;
        private float previousTimeScale = 1;
        private CursorLockMode previousCursor;
        private bool previousCursorVisible;
        public bool MenuOpen { get; private set; }
        public bool IsReady { get; private set; }
        public bool Dirty { get; private set; }
        public string Message { get; private set; }
        public ProgressionCatalog Catalog => catalog;
        public CombatHealth Player => player;
        public int Points => data?.points ?? 0;
        public string CheckpointId => data?.checkpointId;
        public string SavePath => store?.Path;
        public bool CanSave => store != null && store.CanWrite;
        public event Action Changed;
        public void Configure(ProgressionCatalog definitions, CombatHealth actor) { catalog = definitions; player = actor; }
        public bool HasSkill(string id) => data != null && Array.IndexOf(data.skills, id) >= 0;
        public bool IsCleared(string id) => data != null && Array.IndexOf(data.clearedEncounters, id) >= 0;

        private void Awake()
        {
            Current = this;
            if (catalog == null || player == null) { Debug.LogError("Production session is missing its catalog/player."); enabled = false; return; }
            string root = Environment.GetEnvironmentVariable(QaSaveRootVariable);
            if (string.IsNullOrEmpty(root)) root = Application.persistentDataPath;
            store = new ProductionSaveStore(Path.Combine(root, "production-profile.json"));
            if (!store.TryLoad(catalog.catalogId, out data))
                data = new ProductionSaveData { catalogId = catalog.catalogId, points = catalog.startingPoints };
            Message = store.Status;
            if (retrySnapshot != null && retryPath == store.Path && retrySnapshot.catalogId == catalog.catalogId)
            {
                data = retrySnapshot; Dirty = true; Message = "Unsaved session retained; rest to retry saving.";
            }
            retrySnapshot = null; retryPath = null;
        }
        private void Start()
        {
            encounters = FindObjectsByType<EnemyEncounterCoordinator>(FindObjectsSortMode.None);
            ApplyTraining(true);
            var checkpoint = FindObjectsByType<ProductionCheckpoint>(FindObjectsSortMode.None).FirstOrDefault(c => c.Id == data.checkpointId);
            if (checkpoint != null)
            {
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.SetPositionAndRotation(checkpoint.SpawnPosition, Quaternion.Euler(0, checkpoint.SpawnYaw, 0));
                controller.enabled = true;
            }
            IsReady = true; Changed?.Invoke();
        }
        private void Update()
        {
            if (!IsReady) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.startButton.wasPressedThisFrame == true)
                SetMenuOpen(!MenuOpen);
            if (!MenuOpen && Keyboard.current?.backspaceKey.wasPressedThisFrame == true) RestartCheckpoint();
        }
        public void SetMenuOpen(bool open)
        {
            if (open == MenuOpen || !IsReady) return;
            MenuOpen = open;
            if (open)
            {
                previousTimeScale = Time.timeScale; previousCursor = Cursor.lockState; previousCursorVisible = Cursor.visible;
                Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else
            {
                Time.timeScale = previousTimeScale; Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible;
                blockedThroughFrame = Time.frameCount;
            }
            Changed?.Invoke();
        }
        public bool CanRest()
        {
            if (!IsReady || player.IsDead || !player.GetComponent<CharacterController>().isGrounded ||
                player.GetComponent<CombatDefense>().IsGuardBroken || player.GetComponent<PlayerCombat>().IsAttacking ||
                player.GetComponent<PlayerCombat>().IsChargingHeavy || player.GetComponent<PlayerDodge>().IsDodging ||
                player.GetComponent<ContextualCombat>().IsBusy) return false;
            foreach (var encounter in encounters)
                if (encounter != null && encounter.isActiveAndEnabled && encounter.ActiveEnemyCount > 0 &&
                    (encounter.IsEngaged || Vector3.ProjectOnPlane(player.transform.position - encounter.transform.position, Vector3.up).sqrMagnitude <
                        encounter.EngagementRadius * encounter.EngagementRadius)) return false;
            return true;
        }
        public bool CanBuy(string id)
        {
            var skill = catalog.FindSkill(id);
            return skill != null && CanSave && CanRest() && !HasSkill(id) && Points >= skill.cost &&
                (string.IsNullOrEmpty(skill.prerequisite) || HasSkill(skill.prerequisite));
        }
        public bool TryBuy(string id)
        {
            if (!CanBuy(id)) return false;
            var next = data.Copy();
            next.points -= catalog.FindSkill(id).cost;
            next.skills = next.skills.Append(id).ToArray();
            if (!store.TrySave(next)) { Signal(store.Status); return false; }
            data = next; Dirty = false; ApplyTraining(false); Signal("TRAINED  /  " + catalog.FindSkill(id).title); return true;
        }
        public bool CompleteEncounter(string id)
        {
            var reward = catalog.FindEncounter(id);
            if (reward == null || IsCleared(id)) return false;
            data.clearedEncounters = data.clearedEncounters.Append(id).ToArray();
            data.points += reward.points;
            Dirty = !store.TrySave(data);
            Signal(Dirty ? store.Status : reward.title + " CLEARED  /  +" + reward.points + " TRAINING");
            return true;
        }
        public bool TryCheckpoint(ProductionCheckpoint checkpoint)
        {
            if (checkpoint == null || GameplayBlocked || !CanRest() || !checkpoint.IsReachable(player.transform)) return false;
            var next = data.Copy(); next.checkpointId = checkpoint.Id;
            if (!store.TrySave(next)) { Signal(store.Status); return false; }
            data = next; Dirty = false; ApplyTraining(true); Signal("RESTED & SAVED  /  " + checkpoint.Title); return true;
        }
        public void RestartCheckpoint()
        {
            // Earned progression persists; the current attempt restarts at its authored safe location.
            if (Dirty && !store.TrySave(data))
            {
                // Disk failure must not trap a defeated player. Carry unsaved progression across
                // this scene retry in memory while keeping the committed disk profile untouched.
                retrySnapshot = data.Copy(); retryPath = store.Path;
            }
            Dirty = false; SetMenuOpen(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
        private void ApplyTraining(bool heal)
        {
            float health = catalog.baseHealth, posture = catalog.basePosture, heavy = 1;
            foreach (var skill in catalog.skills)
            {
                if (!HasSkill(skill.id)) continue;
                switch (skill.effect)
                {
                    case TrainingEffect.MaxHealth: health += skill.value; break;
                    case TrainingEffect.GuardCapacity: posture += skill.value; break;
                    case TrainingEffect.HeavyDamage: heavy += skill.value; break;
                }
            }
            player.SetMaximumHealth(health, heal);
            player.GetComponent<CombatDefense>().SetMaximumPosture(posture);
            player.GetComponent<PlayerCombat>().SetTrainingHeavyMultiplier(heavy);
        }
        private void Signal(string message) { Message = message; Changed?.Invoke(); }
        private void OnDisable()
        {
            if (MenuOpen) { Time.timeScale = previousTimeScale; Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible; }
            if (Current == this) Current = null;
        }
    }
}
