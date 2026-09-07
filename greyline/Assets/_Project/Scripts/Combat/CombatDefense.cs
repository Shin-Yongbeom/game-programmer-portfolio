using System;
using Greyline.Core;
using Greyline.Enemies;
using Greyline.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greyline.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(CombatHealth))]
    public sealed class CombatDefense : MonoBehaviour, IDamageFilter
    {
        [SerializeField, Min(.05f)] private float counterWindow = .2f;
        [SerializeField, Min(1f)] private float maxPosture = 60f;
        [SerializeField, Min(0f)] private float postureRecovery = 18f;
        [SerializeField, Min(.1f)] private float guardBreakDuration = 1.1f;
        private CombatHealth health;
        private PlayerCombat combat;
        private PlayerDodge dodge;
        private ThirdPersonPlayerMotor motor;
        private CharacterAnimationDriver driver;
        private ContextualCombat contextual;
        private bool held;
        private float guardStartedAt = -100f;
        private float nextCounterAt;
        private float guardBrokenUntil;
        private float lastBlockAt = -100f;
        private float posture;
        public bool IsGuardBroken => Time.time < guardBrokenUntil;
        public bool IsGuarding => held && enabled && !IsGuardBroken && health != null && !health.IsDead &&
            (combat == null || (!combat.IsAttacking && !combat.IsChargingHeavy)) &&
            (dodge == null || !dodge.IsDodging) && (contextual == null || !contextual.IsBusy);
        public float PostureNormalized => Mathf.Clamp01(posture / maxPosture);
        public float MaximumPosture => maxPosture;
        public void SetMaximumPosture(float value) { maxPosture = Mathf.Max(1, value); posture = Mathf.Min(posture, maxPosture); }
        public int CounterCount { get; private set; }
        public string LastResult { get; private set; }
        public event Action<string> Resolved;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            combat = GetComponent<PlayerCombat>();
            dodge = GetComponent<PlayerDodge>();
            motor = GetComponent<ThirdPersonPlayerMotor>();
            driver = GetComponent<CharacterAnimationDriver>();
            contextual = GetComponent<ContextualCombat>();
        }

        private void Update()
        {
            if (Progression.ProductionSession.GameplayBlocked) return;
            SetGuardHeld(Keyboard.current?.rKey.isPressed == true || Gamepad.current?.leftTrigger.isPressed == true);
            if (!IsGuarding && Time.time > lastBlockAt + .65f)
                posture = Mathf.MoveTowards(posture, 0f, postureRecovery * Time.deltaTime);
            driver?.SetGuarding(IsGuarding);
        }

        public bool SetGuardHeld(bool value)
        {
            if (value && Progression.ProductionSession.GameplayBlocked) return false;
            if (value && motor != null && motor.IsCrouching && !motor.TryStandForAction()) value = false;
            if (value && !held && Time.time >= nextCounterAt && !IsGuardBroken)
            {
                guardStartedAt = Time.time;
                nextCounterAt = Time.time + .45f;
            }
            held = value;
            return IsGuarding;
        }

        public bool FilterDamage(DamageInfo incoming, out DamageInfo accepted)
        {
            accepted = incoming;
            if (!IsGuarding || incoming.Source == null) return true;
            Vector3 toSource = Vector3.ProjectOnPlane(incoming.Source.transform.position - transform.position, Vector3.up);
            if (toSource.sqrMagnitude < .001f || Vector3.Dot(transform.forward, toSource.normalized) < .35f) return true;
            lastBlockAt = Time.time;
            var counterable = incoming.Source.GetComponent<ICounterable>();
            if ((incoming.Flags & CombatHitFlags.Counterable) != 0 && Time.time - guardStartedAt <= counterWindow &&
                counterable != null && counterable.CanBeCountered)
            {
                guardStartedAt = -100f;
                posture = Mathf.Max(0f, posture - 15f);
                CounterCount++;
                counterable.ReceiveCounter(new CounterRequest(gameObject, incoming));
                incoming.Source.GetComponent<IDamageable>()?.ApplyDamage(new DamageInfo(gameObject, "counter", 0, 8f,
                    incoming.Source.transform.position + Vector3.up, -incoming.Direction, HitReactionType.Heavy, .25f, .12f, CombatHitFlags.None));
                Signal("COUNTER");
                return false;
            }

            posture += incoming.Amount * (incoming.ReactionType == HitReactionType.Light ? 1f : 1.7f);
            if (posture >= maxPosture || (incoming.Flags & CombatHitFlags.Uninterruptible) != 0)
            {
                posture = maxPosture;
                guardBrokenUntil = Time.time + guardBreakDuration;
                held = false;
                combat?.CancelCurrentAction();
                Signal("GUARD BREAK");
                return true;
            }

            Signal("BLOCK");
            return false;
        }

        private void Signal(string result) { LastResult = result; Resolved?.Invoke(result); }
        private void OnDisable() { held = false; driver?.SetGuarding(false); }
    }
}
