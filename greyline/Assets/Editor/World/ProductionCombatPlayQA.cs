using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Greyline.Combat;
using Greyline.Core;
using Greyline.Enemies;
using Greyline.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Greyline.World.EditorTools
{
    [InitializeOnLoad]
    public static class ProductionCombatPlayQA
    {
        private static int stage;
        private static float started;
        private static PlayerCombat combat;
        private static ThirdPersonPlayerMotor motor;
        private static CombatHealth health, victim;
        private static CombatDefense defense;
        private static ContextualCombat contextual;
        private static EnemyAttack enemy;
        private static EnemyAttack[] originalEnemies;
        private static GameObject ceiling;
        private static float originalEnemyHp, previousPlayerHp;
        private static float fullChargeDamage, restartTimeScale;
        private static bool heavyContactObserved;
        private static float standingCameraY, standingHeadY;
        private static int dodgeIndex;
        private static Vector3 dodgeStart, testedDodgeDirection;
        private static readonly Vector3[] DodgeDirections = { Vector3.forward, Vector3.left, Vector3.right, Vector3.back };
        private static readonly string[] DodgeStates = { "DodgeForward", "DodgeLeft", "DodgeRight", "DodgeBackward" };
        private static int previousPlayerInstance;
        private static bool restartKeyReleased;
        private static int initialCounters;
        private static string capture;
        private static readonly HashSet<int> observedAttackers = new();
        private static readonly HashSet<string> observedBossMotions = new();
        private static EnemyEncounterCoordinator market;
        private static Keyboard qaKeyboard;
        private static bool ownsKeyboard;
        private static InputSettings originalInputSettings;
        private static InputSettings.BackgroundBehavior originalBackgroundBehavior;
        private static InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
        static ProductionCombatPlayQA()
        {
            ProductionDistrictPlayQA.AdditionalPlayChecks = Tick;
            EditorApplication.playModeStateChanged += change =>
            {
                if(change==PlayModeStateChange.EnteredPlayMode){stage=0;capture=null;}
                if(change==PlayModeStateChange.ExitingPlayMode)
                {
                    if(ownsKeyboard && qaKeyboard!=null)InputSystem.RemoveDevice(qaKeyboard);
                    qaKeyboard=null;ownsKeyboard=false;
                    if(originalInputSettings!=null)
                    {
                        originalInputSettings.backgroundBehavior=originalBackgroundBehavior;
                        originalInputSettings.editorInputBehaviorInPlayMode=originalEditorInputBehavior;
                    }
                    originalInputSettings=null;
                }
            };
        }
        private static bool Tick(ProductionDistrictPlayQA.Report report)
        {
            switch (stage)
            {
                case 0:
                    originalInputSettings=InputSystem.settings;
                    originalBackgroundBehavior=originalInputSettings.backgroundBehavior;
                    originalEditorInputBehavior=originalInputSettings.editorInputBehaviorInPlayMode;
                    // Keep the settings instance alive for Input System's Play Mode state restore.
                    originalInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    originalInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    qaKeyboard=Keyboard.current;ownsKeyboard=qaKeyboard==null;
                    if(ownsKeyboard)qaKeyboard=InputSystem.AddDevice<Keyboard>("District QA Keyboard");
                    InputSystem.EnableDevice(qaKeyboard);
                    GameObject player = GameObject.FindWithTag("Player");
                    combat=player.GetComponent<PlayerCombat>(); motor=player.GetComponent<ThirdPersonPlayerMotor>();
                    health=player.GetComponent<CombatHealth>(); defense=player.GetComponent<CombatDefense>(); contextual=player.GetComponent<ContextualCombat>();
                    Require(defense!=null && contextual!=null,"Production combat components missing.");
                    originalEnemies=Object.FindObjectsByType<EnemyAttack>(FindObjectsSortMode.None);
                    Require(originalEnemies.Length==6,"District must contain six encounter enemies.");
                    foreach(var e in originalEnemies)e.enabled=false;
                    var clone=Object.Instantiate(originalEnemies.First(e=>e.Archetype==EnemyArchetype.Brawler).gameObject);
                    clone.name="Play QA Opponent";
                    enemy=clone.GetComponent<EnemyAttack>(); enemy.ConfigureEncounter(null);
                    enemy.Configure(player.transform,2.6f,.7f,.23f,.8f,.9f,12);
                    victim=clone.GetComponent<CombatHealth>(); victim.Configure(100);
                    var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(ProductionDistrictBuilder.Spawn,Quaternion.identity);cc.enabled=true;
                    clone.transform.SetPositionAndRotation(player.transform.position+Vector3.forward*1.9f,Quaternion.Euler(0,180,0));
                    Require(motor.SetCrouching(true),"Crouch request failed.");
                    ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="QA Low Ceiling";
                    ceiling.transform.position=player.transform.position+new Vector3(0,1.58f,0);ceiling.transform.localScale=new Vector3(2,.2f,2);
                    Physics.SyncTransforms();
                    Require(!motor.TryStandForAction() && motor.IsStandBlocked,"Standing should be blocked under a low ceiling.");
                    Require(!combat.BeginHeavyCharge() && !player.GetComponent<PlayerDodge>().RequestDodge() && !defense.SetGuardHeld(true),"Standing combat must not bypass crouch headroom.");
                    report.checks.Add("Crouch lowers the real capsule; a low ceiling rejects standing combat.");
                    Advance();break;
                case 1:
                    if(Elapsed<.35f)break;
                    Capture("07_Crouch",report);
                    if(Elapsed<.6f)break;
                    Object.DestroyImmediate(ceiling);Physics.SyncTransforms();
                    Require(motor.TryStandForAction(),"Standing must recover after ceiling removal.");
                    Require(combat.BeginHeavyCharge(),"Heavy charge request failed.");
                    Advance();break;
                case 2:
                    if(Elapsed<.6f)break;
                    var animator=motor.GetComponentInChildren<Animator>();
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("HeavyCharge"),"Heavy charge has no visible Animator state.");
                    Capture("08_HeavyCharge",report);
                    originalEnemyHp=victim.CurrentHealth;
                    if(Elapsed<.9f)break;
                    heavyContactObserved=false;combat.ReleaseHeavyCharge();Advance();break;
                case 3:
                    if(!heavyContactObserved && victim.CurrentHealth<originalEnemyHp)
                    {
                        AssertContactMotion("ChargedUppercut");heavyContactObserved=true;
                    }
                    if(Elapsed>.2f && Elapsed<.6f)
                        Require(motor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("ChargedUppercut"),"Full charge must select its distinct ChargedUppercut presentation.");
                    if(Elapsed<1.25f)break;
                    Require(heavyContactObserved,"Charged contact animation was not observed.");
                    fullChargeDamage=originalEnemyHp-victim.CurrentHealth;
                    Require(fullChargeDamage>70 && fullChargeDamage<75,"Full heavy charge must apply one scaled hit.");
                    report.checks.Add("Real-time charge enters HeavyCharge; release applies one 1.6x heavy contact.");
                    victim.Configure(100);
                    enemy.transform.SetPositionAndRotation(motor.transform.position+Vector3.forward*1.9f,Quaternion.Euler(0,180,0));
                    Physics.SyncTransforms();
                    Require(combat.BeginHeavyCharge(),"Tapped heavy charge request failed after full-heavy recovery.");
                    stage=30;started=Time.time;break;
                case 30:
                    // One actual simulation frame of holding is below the minimum charge duration.
                    Require(Elapsed<.12f,"Tapped-heavy test did not release inside the minimum charge window.");
                    heavyContactObserved=false;combat.ReleaseHeavyCharge();Advance();break;
                case 31:
                    if(!heavyContactObserved && victim.CurrentHealth<100){AssertContactMotion("HeavyPunchCombo");heavyContactObserved=true;}
                    if(Elapsed>.2f && Elapsed<.6f)
                        Require(motor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("HeavyPunchCombo"),"Tapped heavy must retain its distinct HeavyPunchCombo presentation.");
                    if(Elapsed<1.25f)break;
                    Require(heavyContactObserved,"Tapped contact animation was not observed.");
                    float tapDamage=100-victim.CurrentHealth;
                    Require(tapDamage>43 && tapDamage<49,$"Tapped heavy must apply one base contact, got {tapDamage}.");
                    Require(fullChargeDamage-tapDamage>20,"Full-charge heavy must clearly exceed tapped-heavy final damage.");
                    report.checks.Add("Tapped HeavyPunchCombo applies base damage; full ChargedUppercut applies clearly higher damage.");
                    // Test a real enemy telegraph/contact with a timed guard, through the same Input System used by a player.
                    victim.Configure(100); previousPlayerHp=health.CurrentHealth;initialCounters=defense.CounterCount;
                    enemy.enabled=true;
                    stage=4;started=Time.time;break;
                case 4:
                    if(enemy.IsTelegraphing && enemy.StateProgress>.8f)
                    {
                        SetKeys(new KeyboardState(Key.R));
                        Advance();
                    }
                    else if(Elapsed>7)throw new InvalidOperationException($"Enemy failed to telegraph a live melee attack: {enemy.StateName}, distance={Vector3.Distance(enemy.transform.position,motor.transform.position):0.000}.");
                    break;
                case 5:
                    if(defense.CounterCount>initialCounters)
                    {
                        Require(health.CurrentHealth==previousPlayerHp,"A successful live counter leaked damage.");
                        report.checks.Add("Timed R input counters a real enemy telegraph/contact without player damage.");
                        Capture("09_Counter",report);
                        SetKeys(new KeyboardState());
                        enemy.enabled=false;
                        victim.ApplyDamage(new DamageInfo(motor.gameObject,victim.CurrentHealth-25, victim.transform.position,Vector3.forward,HitReactionType.Light,0,0));
                        // A prop behind the opponent must be physically reached by the push.
                        ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="QA Finisher Bench";
                        ceiling.transform.position=victim.transform.position+Vector3.forward*1.6f+Vector3.up*.4f;
                        ceiling.transform.localScale=new Vector3(1,.8f,1);ceiling.AddComponent<EnvironmentalFinisher>().Configure("BENCH TAKEDOWN");
                        Physics.SyncTransforms();Advance();
                    }
                    else if(Elapsed>2)throw new InvalidOperationException($"Live counter failed: result={defense.LastResult} guarding={defense.IsGuarding} key={qaKeyboard.rKey.isPressed} enabled={qaKeyboard.enabled} current={Keyboard.current?.name} hp={health.CurrentHealth}.");
                    break;
                case 6:
                    if(Elapsed<.35f)break;
                    Require(contextual.TryFinish(),$"Nearby vulnerable target should allow environmental finisher. hp={victim.HealthNormalized} distance={Vector3.Distance(victim.transform.position,motor.transform.position)} attacking={combat.IsAttacking} charging={combat.IsChargingHeavy} guardBroken={defense.IsGuardBroken} playerDead={health.IsDead} crouch={motor.IsCrouching} target={contextual.AvailableTarget?.name} ray={string.Join(",",Physics.RaycastAll(motor.transform.position+Vector3.up,(victim.transform.position-motor.transform.position).normalized,Vector3.Distance(victim.transform.position,motor.transform.position),~0,QueryTriggerInteraction.Ignore).Select(h=>h.collider.name))}");Advance();break;
                case 7:
                    if(Elapsed>.45f && Elapsed<.8f)
                    {
                        Require(motor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("FinisherPush"),"Environmental finisher did not reach the push presentation.");
                        Require(victim.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("FinisherHeld"),"Victim must hold a synchronized reaction, not idle during the push.");
                        Require(!motor.GetComponent<PlayerDodge>().RequestDodge(),"Dodge must not move the player out of a committed finisher.");
                    }
                    if(Elapsed<1.45f)break;
                    Require(victim.IsDead,"Environmental finisher must defeat the vulnerable opponent on contact.");
                    Require(ceiling.GetComponent<EnvironmentalFinisher>().IsAtContact(victim.transform.position) && ceiling.GetComponent<EnvironmentalFinisher>().ImpactCount==1,"Environmental damage requires one actual prop contact.");
                    Capture("10_EnvironmentalFinisher",report);
                    report.checks.Add("Aligned environmental push holds its victim and reaches a solid prop before one lethal contact.");
                    Advance();break;
                case 8:
                    if(Elapsed<3f)break;
                    var corpseReaction=victim.GetComponent<CombatHitReaction>();
                    Require(corpseReaction.IsDeathPoseHeld,"Corpse must finish once and hold its final death pose.");
                    Require(victim.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"A defeated enemy's standing colliders must be disabled.");
                    var corpseAnimator=victim.GetComponentInChildren<Animator>();
                    float corpseLow=new[]{HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftHand,HumanBodyBones.RightHand}
                        .Min(b=>corpseAnimator.GetBoneTransform(b).position.y);
                    Require(Physics.Raycast(new Vector3(victim.transform.position.x,victim.transform.position.y+1,victim.transform.position.z),Vector3.down,out RaycastHit corpseGround,3f),"Corpse ground reference missing.");
                    Require(Mathf.Abs(corpseLow-corpseGround.point.y)<.18f,$"Corpse contact must settle on the ground, lowest={corpseLow:0.00} ground={corpseGround.point.y:0.00}.");
                    report.checks.Add("Defeated enemy holds one death pose, settles within 18cm of ground and disables standing collision.");
                    contextual.RefreshTarget();
                    Require(contextual.AvailableTarget==null && !contextual.TryFinish(),"A defeated opponent must not remain a finisher target.");
                    report.checks.Add("Defeated opponent is excluded from contextual target selection.");
                    // Crouch/charge/counter/finisher images must be from this run, not stale paths.
                    if (ProductionDistrictPlayQA.CaptureEnabled) foreach(string name in new[]{"07_Crouch","08_HeavyCharge","09_Counter","10_EnvironmentalFinisher"})
                    {
                        string path=Path.Combine(ProductionDistrictPlayQA.OutputDirectory,name+".png");
                        Require(File.Exists(path) && new FileInfo(path).Length>1024,"Missing combat screenshot: "+name);
                    }
                    Object.DestroyImmediate(enemy.gameObject);Object.DestroyImmediate(ceiling);
                    foreach(var e in originalEnemies)e.enabled=true;
                    combat.CancelCurrentAction();health.Configure(500);SetKeys(new KeyboardState());
                    market=Object.FindObjectsByType<EnemyEncounterCoordinator>(FindObjectsSortMode.None).First(c=>c.name.StartsWith("Market"));
                    Teleport(market.transform.position);
                    observedAttackers.Clear();Advance();break;
                case 9:
                    var crowd=originalEnemies.Where(e=>e.Encounter==market).ToArray();
                    Require(crowd.Count(e=>e.IsTelegraphing||e.IsActive)<=1,"Multiple market attackers committed simultaneously.");
                    foreach(var e in crowd)if(e.IsTelegraphing||e.IsActive)observedAttackers.Add(e.GetInstanceID());
                    if(Elapsed<8)break;
                    Require(observedAttackers.Count>=2,"Live crowd failed to hand the attack turn to a second opponent.");
                    Require(health.CurrentHealth<health.MaxHealth,"Live crowd never connected a melee contact.");
                    report.checks.Add("Three live market enemies share one attack turn; at least two distinct opponents attack and connect.");
                    Teleport(GameObject.Find("EncounterAnchors").transform.Find("Escape").position);Advance();break;
                case 10:
                    if(Elapsed<.75f)break;
                    Require(!market.IsEngaged && market.ActiveAttacker==null,"Market pursuit failed to disengage beyond its local leash.");
                    report.checks.Add("Escaping to the north service lane releases the market attack turn and ends pursuit.");
                    enemy=originalEnemies.First(e=>e.Archetype==EnemyArchetype.Boss);
                    Teleport(enemy.transform.position+Vector3.back*2);
                    enemy.GetComponent<CombatHealth>().ApplyDamage(new DamageInfo(motor.gameObject,150,enemy.transform.position,Vector3.forward,HitReactionType.Heavy,0,0));
                    observedBossMotions.Clear();
                    Advance();break;
                case 11:
                    if(enemy.IsActive && enemy.StateProgress>.3f)
                    {
                        var bossView=enemy.GetComponent<DistrictEnemyPresentation>();
                        var bossAnimator=enemy.GetComponentInChildren<Animator>();
                        Require(bossView.PresentedState==enemy.PresentationAttackState && bossAnimator.GetCurrentAnimatorStateInfo(0).IsName(enemy.PresentationAttackState),"Live boss contact must use the matching jab/heavy/sweep motion.");
                        observedBossMotions.Add(bossView.PresentedState);
                    }
                    if(observedBossMotions.Count<3)
                    {
                        if(Elapsed>22)throw new InvalidOperationException("Boss did not present all three live attack motions: "+string.Join(",",observedBossMotions));
                        break;
                    }
                    Require(enemy.BossPhase==2 && enemy.Encounter.IsEngaged,"Boss did not enter phase two in live Play.");
                    report.checks.Add("Warden enters phase two and presents distinct jab, heavy and leg-sweep motions at live contact.");
                    foreach(var e in originalEnemies)e.enabled=false;
                    ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="QA Camera Obstruction";
                    ceiling.transform.position=motor.transform.position+Vector3.back*2+Vector3.up*1.5f;
                    ceiling.transform.localScale=new Vector3(4,3,.25f);Physics.SyncTransforms();Advance();break;
                case 12:
                    if(Elapsed<.25f)break;
                    Require(Camera.main.GetComponent<Greyline.CameraSystem.ThirdPersonOrbitCamera>().CurrentDistance<2.2f,"Camera failed to pull in before a solid wall.");
                    report.checks.Add("Orbit camera retracts before a solid wall in live Play.");
                    Object.DestroyImmediate(ceiling);
                    foreach(var e in originalEnemies)e.enabled=true;
                    health.Configure(160);Teleport(ProductionDistrictBuilder.Spawn);
                    combat.BeginHeavyCharge();
                    health.ApplyDamage(new DamageInfo(enemy.gameObject,1000,motor.transform.position,Vector3.back,HitReactionType.Heavy,0,0));
                    Advance();break;
                case 13:
                    if(Elapsed<.3f)break;
                    Require(health.IsDead && !combat.IsChargingHeavy && !combat.IsAttacking && !defense.IsGuarding,"Defeat failed to cancel combat actions.");
                    Require(motor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Death"),"Defeat must enter the death presentation instead of remaining upright.");
                    report.checks.Add("Defeat cancels charge/attack/guard and reaches the live Death animation state.");
                    previousPlayerInstance=motor.gameObject.GetInstanceID();restartTimeScale=Time.timeScale;restartKeyReleased=false;
                    // Leave the event to the next normal player-loop input update so the real HUD
                    // Backspace edge binding receives it, rather than directly loading the scene.
                    InputSystem.QueueStateEvent(qaKeyboard,new KeyboardState(Key.Backspace));
                    Advance();break;
                case 14:
                    if(!restartKeyReleased)
                    {
                        InputSystem.QueueStateEvent(qaKeyboard,new KeyboardState());
                        restartKeyReleased=true;
                    }
                    GameObject restartedPlayer=GameObject.FindWithTag("Player");
                    if(restartedPlayer==null || restartedPlayer.GetInstanceID()==previousPlayerInstance)
                    {
                        if(Elapsed>3)throw new InvalidOperationException("Backspace input did not reload the defeated player's scene.");
                        break;
                    }
                    if(Elapsed<.5f)break;
                    motor=restartedPlayer.GetComponent<ThirdPersonPlayerMotor>();combat=restartedPlayer.GetComponent<PlayerCombat>();
                    health=restartedPlayer.GetComponent<CombatHealth>();defense=restartedPlayer.GetComponent<CombatDefense>();
                    Require(Mathf.Approximately(Time.timeScale,restartTimeScale),"Restart changed timeScale.");
                    Require(motor!=null && motor.enabled && combat!=null && combat.enabled && combat.HasInputAsset && health!=null && !health.IsDead && health.CurrentHealth==health.MaxHealth,"Restart failed to restore a live, enabled player and combat input asset.");
                    Require(Object.FindObjectsByType<DistrictCombatFeedback>(FindObjectsSortMode.None).Length==1,"Restart must restore exactly one combat HUD.");
                    originalEnemies=Object.FindObjectsByType<EnemyAttack>(FindObjectsSortMode.None);
                    Require(originalEnemies.Length==6 && originalEnemies.All(e=>e.enabled && !e.GetComponent<CombatHealth>().IsDead),"Restart must restore six active living encounter enemies.");
                    SetKeys(new KeyboardState(Key.R));Advance();break;
                case 15:
                    if(Elapsed<.1f)break;
                    Require(defense.IsGuarding,"Guard input failed after scene restart.");
                    SetKeys(new KeyboardState());
                    report.checks.Add("Real Backspace input reloads after defeat: full-health player, live guard input, one HUD, six enemies and unchanged timeScale.");
                    stage=35;started=Time.time;break;
                case 35:
                    if(Elapsed<.4f)break;
                    standingCameraY=Camera.main.transform.position.y-motor.transform.position.y;
                    standingHeadY=motor.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position.y-motor.transform.position.y;
                    SetKeys(new KeyboardState(Key.C,Key.W));stage=16;started=Time.time;break;
                case 16:
                    if(Elapsed<.4f)break;
                    Require(motor.IsCrouching && motor.PlanarSpeed>.4f,"Live C + W input must move the crouched capsule.");
                    var crouchAnimator=motor.GetComponentInChildren<Animator>();
                    Require(crouchAnimator.GetCurrentAnimatorStateInfo(0).IsName("CrouchIdle"),"Moving crouch must use the continuous crouch blend tree.");
                    Require(crouchAnimator.GetBoneTransform(HumanBodyBones.Head).position.y-motor.transform.position.y<standingHeadY-.2f,$"Crouch must lower rendered head: standing={standingHeadY:0.00} crouching={crouchAnimator.GetBoneTransform(HumanBodyBones.Head).position.y-motor.transform.position.y:0.00} IK={crouchAnimator.GetComponent<Core.CrouchFootPlacement>()?.BodyLowering:0.00}.");
                    Require(Camera.main.transform.position.y-motor.transform.position.y<standingCameraY-.25f,"Crouch camera pivot must follow the lowered posture.");
                    float lowFoot=Mathf.Min(crouchAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,crouchAnimator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                    Require(Physics.Raycast(motor.transform.position+Vector3.up,Vector3.down,out RaycastHit crouchGround,3f) && Mathf.Abs(lowFoot-crouchGround.point.y)<.22f,"Lowered crouch body must preserve a planted foot near ground.");
                    SetKeys(new KeyboardState());Advance();break;
                case 17:
                    if(Elapsed<.4f)break;
                    Require(!motor.IsCrouching,"Releasing C must restore standing posture.");
                    report.checks.Add("Live crouch movement blends within one state, lowers head/camera and returns to standing.");
                    dodgeIndex=0;stage=18;started=Time.time;break;
                case 18:
                    Teleport(ProductionDistrictBuilder.Spawn);
                    dodgeStart=motor.transform.position;testedDodgeDirection=DodgeDirections[dodgeIndex];
                    Require(motor.GetComponent<PlayerDodge>().RequestDodge(testedDodgeDirection),"Directional dodge request failed.");
                    Advance();break;
                case 19:
                    if(Elapsed>.08f && Elapsed<.18f)
                    {
                        Require(motor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName(DodgeStates[dodgeIndex]),"Dodge must select the matching directional animation.");
                        Vector3 hipsOffset=Vector3.ProjectOnPlane(motor.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips).position-motor.transform.position,Vector3.up);
                        Require(hipsOffset.magnitude<.75f,$"{DodgeStates[dodgeIndex]} visual must remain with its controller; hips offset={hipsOffset}.");
                        float before=health.CurrentHealth;
                        health.ApplyDamage(new DamageInfo(motor.gameObject,5,motor.transform.position,Vector3.forward,HitReactionType.Light,0,0));
                        Require(health.CurrentHealth==before,"Directional dodge animation must preserve gameplay i-frames.");
                    }
                    if(Elapsed<.9f)break;
                    Vector3 travelled=Vector3.ProjectOnPlane(motor.transform.position-dodgeStart,Vector3.up);
                    Require(Vector3.Dot(travelled,testedDodgeDirection)>2.5f && travelled.magnitude<3.4f,"Dodge pose must accompany the intended displacement without root-motion drift.");
                    Require(!motor.GetComponent<PlayerDodge>().IsDodging,"Dodge must recover.");
                    if(++dodgeIndex<4){stage=18;started=Time.time;break;}
                    report.checks.Add("Four directional dodge states accompany matching displacement and preserve i-frames/recovery.");
                    ProductionFinisherPlayQA.Begin();stage=20;break;
                case 20:
                    if (ProductionFinisherPlayQA.Tick(report)) { ProductionSystemsPlayQA.Begin(); stage=21; }
                    break;
                case 21:return ProductionSystemsPlayQA.Tick(report);
            }
            return false;
        }
        private static float Elapsed=>Time.time-started;
        private static void AssertContactMotion(string state)
        {
            var set=AssetDatabase.LoadAssetAtPath<Core.AnimationPresentationSet>(Character.EditorTools.ProductionAnimationAuthoring.SetPath);
            var motion=set.Find(state);
            var info=motor.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0);
            float sampledTime=info.normalizedTime*motion.clip.length;
            Require(info.IsName(state) && Mathf.Abs(sampledTime-motion.contactSeconds)<.12f,
                $"{state} native playback must reach authored contact with damage; actual={sampledTime:0.000} target={motion.contactSeconds:0.000}.");
        }
        private static void SetKeys(KeyboardState keys)
        {
            InputSystem.QueueStateEvent(qaKeyboard,keys);
            InputSystem.Update();
        }
        private static void Teleport(Vector3 position)
        {
            var controller=motor.GetComponent<CharacterController>();controller.enabled=false;
            motor.transform.SetPositionAndRotation(position,Quaternion.identity);controller.enabled=true;Physics.SyncTransforms();
        }
        private static void Advance(){stage++;started=Time.time;}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Capture(string name,ProductionDistrictPlayQA.Report report)
        {
            if (!ProductionDistrictPlayQA.CaptureEnabled) return;
            if(capture==name)return;
            capture=name;
            string path=Path.Combine(ProductionDistrictPlayQA.OutputDirectory,name+".png");
            if(File.Exists(path))File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);report.screenshots.Add(path);
        }
    }
}
