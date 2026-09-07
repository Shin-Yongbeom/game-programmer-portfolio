using System;
using System.IO;
using System.Linq;
using Greyline.Combat;
using Greyline.Enemies;
using Greyline.Interaction;
using Greyline.Player;
using Greyline.Progression;
using Greyline.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace Greyline.World.EditorTools
{
    public static class ProductionSystemsPlayQA
    {
        private static int stage, previousPlayer;
        private static float started, simulationTime, previousScale;
        private static Vector3 playerPosition;
        private static Quaternion cameraRotation;
        private static ProductionSession session;
        private static ProductionSessionView view;
        private static CombatHealth opponent;
        private static float Elapsed=>Time.unscaledTime-started;
        public static void Begin()
        {
            stage=0; started=Time.unscaledTime;
            session=ProductionSession.Current; view=session.GetComponent<ProductionSessionView>();
            Require(session!=null && session.IsReady,"Production session did not initialize.");
            Require(session.SavePath.StartsWith(Path.GetFullPath(ProductionDistrictPlayQA.OutputDirectory),StringComparison.OrdinalIgnoreCase),"QA must never use the human save profile.");
            Teleport(ProductionDistrictBuilder.Spawn);
            ProductionSaveValidation.Validate();
        }
        public static bool Tick(ProductionDistrictPlayQA.Report report)
        {
            switch(stage)
            {
                case 0:
                    if(Elapsed<.4f)break;
                    DefeatEncounter("university");
                    Require(session.IsCleared("university") && session.Points==3 && !session.CompleteEncounter("university"),"Encounter clear must award once and autosave.");
                    playerPosition=session.Player.transform.position;cameraRotation=Camera.main.transform.rotation;
                    previousScale=Time.timeScale;simulationTime=Time.time;
                    Keys(Key.Escape);Next();break;
                case 1:
                    Keys();
                    Require(session.MenuOpen && view.IsMenuVisible && Time.timeScale==0,"Escape must open real UI and pause simulation.");
                    Require(EventSystem.current.currentSelectedGameObject!=null,"Menu requires keyboard/gamepad focus.");
                    Require(!session.Player.GetComponent<PlayerCombat>().BeginHeavyCharge() && !session.Player.GetComponent<PlayerDodge>().RequestDodge(),"Menu must block public combat commands.");
                    Keys(Key.W,Key.Space,Key.R,Key.F);Next();break;
                case 2:
                    if(Elapsed<.2f)break;
                    Require(Vector3.Distance(playerPosition,session.Player.transform.position)<.01f && Quaternion.Angle(cameraRotation,Camera.main.transform.rotation)<.1f && Time.time-simulationTime<.04f,"Menu inputs must not move the player/camera or advance simulation.");
                    Keys();
                    Require(view.SkillButtonCount==session.Catalog.skills.Length && !session.CanBuy("heavy-mastery"),"UI must bind catalog skills and enforce prerequisites.");
                    view.SkillButton("conditioning").onClick.Invoke();
                    view.SkillButton("heavy-foundation").onClick.Invoke();
                    Require(session.HasSkill("conditioning") && session.HasSkill("heavy-foundation") && session.Points==1 && !session.TryBuy("conditioning"),"Purchases must spend once and persist.");
                    Require(!session.CanBuy("heavy-mastery"),"Insufficient points must reject mastery.");
                    view.SkillButton("rooted-guard").onClick.Invoke();
                    Require(session.Player.MaxHealth==190 && session.Player.GetComponent<CombatDefense>().MaximumPosture==84 && Mathf.Approximately(session.Player.GetComponent<PlayerCombat>().TrainingHeavyMultiplier,1.15f),"Training effects must reach actual combat components.");
                    // Route through the focused uGUI resume control's Submit handler.
                    EventSystem.current.SetSelectedGameObject(view.ResumeButton.gameObject);
                    ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                    Require(!session.MenuOpen && Time.timeScale==previousScale && ProductionSession.GameplayBlocked,"Resume must restore timeScale and consume its input frame.");
                    Next();break;
                case 3:
                    if(Elapsed<.2f)break;
                    Require(!ProductionSession.GameplayBlocked && !session.Player.GetComponent<PlayerCombat>().IsAttacking,"Menu submit must not leak a gameplay attack.");
                    var checkpoint=Object.FindObjectsByType<ProductionCheckpoint>(FindObjectsSortMode.None).First(c=>c.Id=="north-lane");
                    Teleport(checkpoint.SpawnPosition);Next();break;
                case 4:
                    if(Elapsed<.25f)break;
                    // Through the production interaction router and shared E binding.
                    Keys(Key.E);Next();break;
                case 5:
                    Keys();
                    Require(session.CheckpointId=="north-lane" && session.Player.CurrentHealth==190 && File.Exists(session.SavePath),"E must rest, heal and persist the authored checkpoint.");
                    DefeatEncounter("market");
                    Require(session.Points==2 && session.TryBuy("heavy-mastery") && session.Points==0,"Second encounter reward must unlock prerequisite-backed mastery.");
                    Teleport(ProductionDistrictBuilder.Spawn);
                    var source=Object.FindObjectsByType<EnemyAttack>(FindObjectsSortMode.None).First(e=>e.Archetype==EnemyArchetype.Boss);
                    var clone=Object.Instantiate(source.gameObject);clone.name="Training Damage QA Opponent";
                    clone.GetComponent<EnemyAttack>().ConfigureEncounter(null);clone.GetComponent<EnemyAttack>().enabled=false;
                    clone.transform.SetPositionAndRotation(session.Player.transform.position+Vector3.forward*1.9f,Quaternion.Euler(0,180,0));
                    opponent=clone.GetComponent<CombatHealth>();opponent.Configure(150);Physics.SyncTransforms();
                    Next();break;
                case 6:
                    if(Elapsed<.35f)break;
                    Require(session.Player.GetComponent<PlayerCombat>().BeginHeavyCharge(),"Trained Heavy must remain playable.");
                    Next();break;
                case 7:
                    session.Player.GetComponent<PlayerCombat>().ReleaseHeavyCharge();Next();break;
                case 8:
                    if(Elapsed<1.3f)break;
                    Require(Mathf.Abs((150-opponent.CurrentHealth)-62.1f)<.5f,$"Training must scale real Heavy contact once; damage={150-opponent.CurrentHealth}.");
                    Object.DestroyImmediate(opponent.gameObject);
                    report.checks.Add("Pause UI freezes input/simulation, consumes resume input, purchases catalog skills once, enforces costs/prerequisites and changes real Heavy damage/health/posture.");
                    previousPlayer=session.Player.GetInstanceID(); session.RestartCheckpoint();Next();break;
                case 9:
                    if(Elapsed<.5f)break;
                    session=ProductionSession.Current;
                    Require(session!=null && session.IsReady && session.Player.GetInstanceID()!=previousPlayer,"Checkpoint restart must replace the scene/player.");
                    var saved=Object.FindObjectsByType<ProductionCheckpoint>(FindObjectsSortMode.None).First(c=>c.Id=="north-lane");
                    Require(Vector3.Distance(session.Player.transform.position,saved.SpawnPosition)<.5f && session.Player.CurrentHealth==190,"Reload must restore the selected safe checkpoint and trained health.");
                    Require(session.Points==0 && session.Catalog.skills.All(s=>session.HasSkill(s.id)) && session.IsCleared("market") && session.IsCleared("university"),"Reload must restore training and clear ledger.");
                    Require(Object.FindObjectsByType<EnemyAttack>(FindObjectsSortMode.None).Length==1 && Object.FindObjectsByType<ProductionSession>(FindObjectsSortMode.None).Length==1 && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Cleared encounters must stay cleared without duplicate session/UI input owners.");
                    Require(Time.timeScale==previousScale,"Reload must preserve pre-menu timeScale.");
                    report.checks.Add("Encounter rewards autosave once; E checkpoint/rest and a scene reload restore all skills/points and cleared encounters under an isolated QA profile. Save corruption/write-failure fixtures pass.");
                    using (var locked = new FileStream(session.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        DefeatEncounter("plaza");
                        Require(session.Dirty, "A failed autosave must mark live progression as unsaved.");
                        session.RestartCheckpoint();
                    }
                    Next();break;
                case 10:
                    if (Elapsed < .5f) break;
                    session = ProductionSession.Current;
                    Require(session.Dirty && session.Points == 3 && session.IsCleared("plaza"), "Failed writes must not lose in-memory progression on checkpoint retry.");
                    var station = Object.FindObjectsByType<ProductionCheckpoint>(FindObjectsSortMode.None).First(c=>c.Id=="north-lane");
                    Require(session.TryCheckpoint(station) && !session.Dirty, "Rest must retry the failed save after disk access recovers.");
                    report.checks.Add("An actual locked-file autosave failure retains earned progress through scene retry, marks it unsaved, and commits successfully when resting after access recovers.");
                    return true;
            }
            return false;
        }
        private static void DefeatEncounter(string id)
        {
            var group=Object.FindObjectsByType<PersistentEncounter>(FindObjectsSortMode.None).First(e=>e.Id==id);
            foreach(var enemy in group.GetComponentsInChildren<EnemyAttack>())
                enemy.GetComponent<CombatHealth>().ApplyDamage(new DamageInfo(session.Player.gameObject,9999,enemy.transform.position,Vector3.forward,HitReactionType.Heavy,0,0));
        }
        private static void Teleport(Vector3 position)
        {
            var controller=session.Player.GetComponent<CharacterController>();controller.enabled=false;
            controller.transform.SetPositionAndRotation(position,Quaternion.identity);controller.enabled=true;Physics.SyncTransforms();
        }
        private static void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));
        private static void Next(){stage++;started=Time.unscaledTime;}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
