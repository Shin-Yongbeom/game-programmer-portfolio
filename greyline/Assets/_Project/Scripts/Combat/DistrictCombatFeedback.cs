using Greyline.CameraSystem;
using Greyline.Enemies;
using Greyline.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greyline.Combat
{
    /// <summary>Local combat presentation: impact sound, camera impulse, threat tells and health.</summary>
    public sealed class DistrictCombatFeedback : MonoBehaviour
    {
        [SerializeField] private CombatHealth player;
        private CombatHealth[] enemies;
        private CombatDefense defense;
        private PlayerCombat combat;
        private ContextualCombat contextual;
        private Camera view;
        private ThirdPersonOrbitCamera orbit;
        private AudioSource audioSource;
        private AudioClip impactSound;
        private string message;
        private float messageUntil;
        private float damageUntil;
        private GUIStyle label;
        private GUIStyle title;
        public void Configure(CombatHealth playerHealth) => player = playerHealth;
        private void Start()
        {
            view = Camera.main;
            orbit = view != null ? view.GetComponent<ThirdPersonOrbitCamera>() : null;
            defense = player.GetComponent<CombatDefense>();
            combat = player.GetComponent<PlayerCombat>();
            contextual = player.GetComponent<ContextualCombat>();
            enemies = FindObjectsByType<CombatHealth>(FindObjectsSortMode.None);
            foreach (CombatHealth enemy in enemies) enemy.Damaged += OnHit;
            if (defense != null) defense.Resolved += OnDefense;
            if (contextual != null) contextual.Finished += OnFinished;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.volume = .25f;
            // Original short, seeded noise/transient. No downloaded audio dependency.
            const int rate = 22050;
            float[] samples = new float[3307];
            uint seed = 76123;
            for (int i = 0; i < samples.Length; i++)
            {
                seed = seed * 1664525 + 1013904223;
                float t = i / (float)rate;
                float noise = ((seed >> 8) / 16777215f) * 2f - 1f;
                samples[i] = (Mathf.Sin(t * 2 * Mathf.PI * 95) * .6f + noise * .35f) * Mathf.Exp(-t * 38f);
            }
            impactSound = AudioClip.Create("Original combat transient", samples.Length, 1, rate, false);
            impactSound.SetData(samples, 0);
        }

        private void Update()
        {
            if (Progression.ProductionSession.Current == null && Keyboard.current?.backspaceKey.wasPressedThisFrame == true)
                SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private void OnHit(DamageInfo hit)
        {
            orbit?.AddImpulse(hit.ReactionType == HitReactionType.Light ? .045f : .09f);
            if (audioSource != null && impactSound != null)
            {
                audioSource.pitch = hit.ReactionType == HitReactionType.Light ? 1.2f : .8f;
                audioSource.PlayOneShot(impactSound);
            }
            if (hit.Source != player.gameObject) damageUntil = Time.unscaledTime + .22f;
        }
        private void OnDefense(string result) { message = result; messageUntil = Time.unscaledTime + .8f; orbit?.AddImpulse(.035f); }
        private void OnFinished(string result, Vector3 point) { message = result; messageUntil = Time.unscaledTime + 1.5f; orbit?.AddImpulse(.12f); }

        private void OnGUI()
        {
            if (Progression.ProductionSession.Current?.MenuOpen == true) return;
            if (player == null) return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(.93f,.93f,.87f) } };
            title ??= new GUIStyle(label) { fontSize = 21, fontStyle = FontStyle.Bold };
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            DrawRect(new Rect(22,594,294,104), new Color(.025f,.045f,.055f,.9f));
            GUI.Label(new Rect(38,605,270,26), player.IsDead ? "DEFEATED" : "PYONGYANG  /  FIELD PRACTICE", label);
            DrawRect(new Rect(38,637,260,9), new Color(.2f,.23f,.24f));
            DrawRect(new Rect(38,637,260 * player.HealthNormalized,9), new Color(.76f,.88f,.68f));
            GUI.Label(new Rect(38,654,100,23), $"HP  {player.CurrentHealth:0}", label);
            if (defense != null)
            {
                GUI.Label(new Rect(148,654,160,23), defense.IsGuardBroken ? "GUARD BROKEN" : defense.IsGuarding ? "GUARD" : "POSTURE", label);
                DrawRect(new Rect(148,681,150,4), new Color(.2f,.23f,.24f));
                DrawRect(new Rect(148,681,150 * defense.PostureNormalized,4), new Color(.95f,.62f,.25f));
            }
            DrawRect(new Rect(805,614,452,84), new Color(.025f,.045f,.055f,.87f));
            GUI.Label(new Rect(820,623,425,22), "LMB  combo    RMB  hold heavy    R  guard / counter", label);
            GUI.Label(new Rect(820,647,425,22), "Shift  run    Space  jump    C  crouch    Alt  dodge", label);
            GUI.Label(new Rect(820,671,425,22), "F  finisher    E  rest    Esc  training / pause", label);
            if (combat != null && combat.IsChargingHeavy)
            {
                GUI.Label(new Rect(528,545,225,27), "HEAVY  /  RELEASE TO STRIKE", label);
                DrawRect(new Rect(535,575,210,6), new Color(.2f,.23f,.24f));
                DrawRect(new Rect(535,575,210 * combat.HeavyChargeNormalized,6), new Color(1,.68f,.25f));
            }
            if (contextual != null && !string.IsNullOrEmpty(contextual.Prompt)) GUI.Label(new Rect(525,505,380,35), contextual.Prompt, title);
            if (Time.unscaledTime < messageUntil) GUI.Label(new Rect(520,155,420,35), message, title);
            if (player.IsDead) GUI.Label(new Rect(454,310,500,50), "DEFEATED  /  BACKSPACE TO RETRY", title);
            if (Time.unscaledTime < damageUntil)
            {
                Color tint = new Color(.8f,.08f,.035f,.4f * (damageUntil-Time.unscaledTime)/.22f);
                DrawRect(new Rect(0,0,8,720), tint); DrawRect(new Rect(1272,0,8,720), tint);
            }
            GUI.matrix = previous;
            if (view != null && enemies != null) foreach (CombatHealth enemy in enemies)
            {
                if (enemy == null || enemy == player || enemy.IsDead || Vector3.Distance(enemy.transform.position, player.transform.position) > 22) continue;
                Vector3 p = view.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.2f);
                if (p.z < 0) continue;
                EnemyAttack attack = enemy.GetComponent<EnemyAttack>();
                Color tell = attack != null && attack.IsUnblockable ? new Color(1,.2f,.8f) : new Color(1,.72f,.2f);
                DrawRect(new Rect(p.x-40,Screen.height-p.y,80,5), new Color(.05f,.06f,.07f,.85f));
                DrawRect(new Rect(p.x-40,Screen.height-p.y,80 * enemy.HealthNormalized,5), new Color(.88f,.32f,.23f));
                if (attack != null && attack.IsTelegraphing)
                {
                    GUI.color = tell;
                    GUI.Label(new Rect(p.x-45,Screen.height-p.y-30,170,26), attack.IsUnblockable ? "DODGE !" : "COUNTER !", label);
                    GUI.color = Color.white;
                }
                if (attack != null && attack.Archetype == EnemyArchetype.Boss)
                    GUI.Label(new Rect(p.x-65,Screen.height-p.y+9,180,25), "WARDEN  /  PHASE " + attack.BossPhase, label);
            }
        }
        private static void DrawRect(Rect rect, Color color) { Color old=GUI.color; GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old; }
        private void OnDestroy()
        {
            if (enemies != null) foreach (CombatHealth enemy in enemies) if (enemy != null) enemy.Damaged -= OnHit;
            if (defense != null) defense.Resolved -= OnDefense;
            if (contextual != null) contextual.Finished -= OnFinished;
            if (impactSound != null) Destroy(impactSound);
        }
    }
}
