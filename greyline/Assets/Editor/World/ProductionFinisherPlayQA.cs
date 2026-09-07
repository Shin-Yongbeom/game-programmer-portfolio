using System;
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
    /// <summary>Live paired motion, physical approach and cancellation regressions; no captures.</summary>
    public static class ProductionFinisherPlayQA
    {
        private static int stage, contacts;
        private static float started;
        private static bool observedContact;
        private static ContextualCombat contextual;
        private static CombatHealth victim;
        private static EnemyAttack[] enemies;
        private static bool[] enabledEnemies;
        private static GameObject template, obstacle;
        private static CharacterController player;
        private static Vector3 actorStart;
        private static float Elapsed => Time.time - started;

        public static void Begin()
        {
            stage = 0; contacts = 0; observedContact = false; started = Time.time;
            contextual = GameObject.FindWithTag("Player").GetComponent<ContextualCombat>();
            player = contextual.GetComponent<CharacterController>();
            enemies = Object.FindObjectsByType<EnemyAttack>(FindObjectsSortMode.None);
            enabledEnemies = enemies.Select(e => e.enabled).ToArray();
            template = enemies.First(e => e.Archetype == EnemyArchetype.Brawler).gameObject;
            foreach (var enemy in enemies) enemy.enabled = false;
            player.enabled = false;
            player.transform.SetPositionAndRotation(ProductionDistrictBuilder.Spawn, Quaternion.identity);
            player.enabled = true;
            actorStart = player.transform.position;
            contextual.Finished += OnContact;
            CreateOpponent();
        }

        private static void CreateOpponent()
        {
            if (victim != null) Object.DestroyImmediate(victim.gameObject);
            var clone = Object.Instantiate(template);
            clone.name = "Paired Finisher QA Opponent";
            clone.transform.SetPositionAndRotation(player.transform.position + Vector3.forward * 2.4f, Quaternion.Euler(0, 90, 0));
            clone.GetComponent<EnemyAttack>().ConfigureEncounter(null);
            victim = clone.GetComponent<CombatHealth>();
            victim.Configure(100);
            victim.ApplyDamage(new DamageInfo(player.gameObject, 75, victim.transform.position, Vector3.forward, HitReactionType.Light, 0, 0));
            Physics.SyncTransforms();
        }
        private static void OnContact(string label, Vector3 position) => contacts++;
        private static void Next() { stage++; started = Time.time; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        public static bool Tick(ProductionDistrictPlayQA.Report report)
        {
            switch (stage)
            {
                case 0:
                    if (Elapsed < .35f) break;
                    // The contact ray at chest height is clear, but a knee-high solid blocks approach.
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.position = player.transform.position + new Vector3(0, .35f, .8f);
                    obstacle.transform.localScale = new Vector3(2, .7f, .2f);
                    Physics.SyncTransforms();
                    Require(!contextual.TryFinish(), "A low solid must reject finisher approach even with a clear chest ray.");
                    Object.DestroyImmediate(obstacle);
                    // Nearby side props cannot grant environmental damage without an aligned push.
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.position = victim.transform.position + new Vector3(1.5f, .4f, 0);
                    obstacle.transform.localScale = new Vector3(.8f, .8f, .8f);
                    obstacle.AddComponent<EnvironmentalFinisher>().Configure("SIDE PROP");
                    Physics.SyncTransforms();
                    contextual.RefreshTarget();
                    Require(contextual.AvailableTarget == victim && contextual.Prompt.Contains("CLINCH KNEE"), "A side prop must retain the unarmed finisher.");
                    Object.DestroyImmediate(obstacle);
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.F));
                    Next(); break;
                case 1:
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    Require(contextual.IsBusy, "Real F input must begin the finisher.");
                    Next(); break;
                case 2:
                    if (contextual.StrikeElapsed > .12f && !victim.IsDead)
                    {
                        Require(victim.GetComponent<EnemyAttack>().IsFinisherHeld, "Finisher victim must be locked out of AI actions.");
                        Require(victim.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("FinisherHeld"), "Finisher victim must present the paired hold.");
                        Require(Vector3.Dot(victim.transform.forward, player.transform.forward) < -.95f, "The pair must face each other.");
                    }
                    if (!observedContact && victim.IsDead)
                    {
                        var animator = player.GetComponentInChildren<Animator>();
                        var info = animator.GetCurrentAnimatorStateInfo(0);
                        var set = AssetDatabase.LoadAssetAtPath<AnimationPresentationSet>(Character.EditorTools.ProductionAnimationAuthoring.SetPath);
                        var motion = set.Find("Finisher");
                        Require(info.IsName("Finisher") && Mathf.Abs(info.normalizedTime * motion.clip.length - motion.contactSeconds) < .12f,
                            "Clinch knee must reach its authored contact at lethal damage.");
                        Require(Vector3.Distance(player.transform.position, victim.transform.position) < 1.1f, "Finisher cannot connect remotely.");
                        Transform hip = victim.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
                        float kneeGap = Mathf.Min(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position, hip.position),
                            Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position, hip.position));
                        Require(kneeGap < .65f, $"Knee must reach the partner's body region; gap={kneeGap:0.00}m.");
                        observedContact = true;
                    }
                    if (Elapsed < 1.65f) break;
                    Require(observedContact && contacts == 1 && !contextual.IsBusy && !contextual.IsInvulnerable, "Finisher must resolve exactly once and release the player.");
                    Require(Vector3.Distance(actorStart, player.transform.position) > 1.2f, "Remote start must visibly approach its partner.");
                    report.checks.Add("Real F input approaches an aligned partner, plays a clinch knee at lethal contact and recovers once; side props and low blockers cannot fake an environmental finish.");
                    CreateOpponent(); Next(); break;
                case 3:
                    if (Elapsed < .3f) break;
                    Require(contextual.TryFinish(), "Cancellation fixture must start a valid finisher.");
                    Next(); break;
                case 4:
                    if (Elapsed < .06f) break;
                    obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacle.transform.position = player.transform.position + new Vector3(0, .6f, .65f);
                    obstacle.transform.localScale = new Vector3(2, 1.2f, .2f);
                    Physics.SyncTransforms(); Next(); break;
                case 5:
                    if (Elapsed < .75f) break;
                    Require(!contextual.IsBusy && !victim.GetComponent<EnemyAttack>().IsFinisherHeld && victim.CurrentHealth == 25 && contacts == 1,
                        "A newly obstructed approach must cancel without damage or leaving the partner locked.");
                    Object.DestroyImmediate(obstacle); Physics.SyncTransforms();
                    Require(contextual.TryFinish(), "Player must regain finisher control after obstruction cancellation.");
                    Next(); break;
                case 6:
                    if (Elapsed < .4f) break;
                    Require(contextual.IsBusy && Physics.GetIgnoreCollision(player, victim.GetComponent<Collider>()), "Clinch must scope collision suppression to its two participants.");
                    contextual.enabled = false;
                    Require(!contextual.IsBusy && !contextual.IsInvulnerable && !victim.GetComponent<EnemyAttack>().IsFinisherHeld,
                        "Disabling the finisher component must release both participants and invulnerability.");
                    Require(!Physics.GetIgnoreCollision(player, victim.GetComponent<Collider>()), "Clinch cancellation must restore participant collision.");
                    Require(Vector3.Distance(player.transform.position, victim.transform.position) >= .88f, "Clinch cancellation must separate the living pair before collision restoration.");
                    contextual.enabled = true;
                    victim.Configure(280);
                    victim.ApplyDamage(new DamageInfo(player.gameObject, 180, victim.transform.position, Vector3.forward, HitReactionType.Light, 0, 0));
                    contextual.RefreshTarget();
                    Require(contextual.AvailableTarget == null, "Finisher prompt must not offer a nonlethal finish on a high-health boss-sized target.");
                    player.Move(Vector3.up * 2);
                    Require(!contextual.TryFinish(), "Airborne finisher starts must be rejected.");
                    contextual.Finished -= OnContact;
                    Object.DestroyImmediate(victim.gameObject);
                    for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null) enemies[i].enabled = enabledEnemies[i];
                    report.checks.Add("Finisher obstruction/disable cancels without stale victim locks or damage; airborne and nonlethal high-health starts are rejected.");
                    return true;
            }
            return false;
        }
    }
}
