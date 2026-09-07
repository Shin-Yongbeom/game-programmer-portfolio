using UnityEditor;
using UnityEngine;

namespace Greyline.Character.EditorTools
{
    /// <summary>
    /// Play-mode preview panel for the Character Sandbox. Plays one Mixamo clip at a time on the
    /// Y Bot Animator via Animator.Play, so start / end pose, foot planting, root drift, orientation
    /// and looping can be judged clip by clip. No combo logic, no state machine wiring.
    /// </summary>
    public sealed class CharacterSandboxClipPlayer : EditorWindow
    {
        private const string YBotObjectName = "Y Bot";

        private Animator cachedAnimator;
        private float playbackSpeed = 1f;
        private bool freezeAtStart;
        private float transitionDuration = 0.08f;
        private float fightIdleExitTime = 0.65f;
        private float jabExitTime = 0.78f;
        private float crossExitTime = 0.86f;
        private bool comboPlaying;
        private int comboStep;

        private static readonly string[] ComboStates = { "FightIdle", "Jab", "Cross", "FightIdle" };

        [MenuItem("Greyline/Character/Clip Player")]
        public static void Open()
        {
            GetWindow<CharacterSandboxClipPlayer>(false, "Char Clip Player", true);
        }

        private void OnEnable() => EditorApplication.update += UpdateCombo;

        private void OnDisable()
        {
            EditorApplication.update -= UpdateCombo;
            comboPlaying = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Open CharacterSandbox.unity and enter Play Mode. Each button plays one clip from " +
                "its first frame on the Y Bot.",
                MessageType.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Not in Play Mode.");
                return;
            }

            Animator animator = ResolveAnimator();
            if (animator == null)
            {
                EditorGUILayout.HelpBox("No 'Y Bot' Animator found in the open scene.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            playbackSpeed = EditorGUILayout.Slider("Playback Speed", playbackSpeed, 0f, 1.5f);
            if (EditorGUI.EndChangeCheck())
            {
                animator.speed = playbackSpeed;
            }

            freezeAtStart = EditorGUILayout.ToggleLeft("Freeze on first frame (speed 0 after play)", freezeAtStart);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controller", EditorStyles.boldLabel);
            if (GUILayout.Button("Use CharacterSandbox Controller"))
            {
                SetController(animator, CharacterSandbox.ControllerPath);
            }

            if (GUILayout.Button("Use PlayerCombat Controller"))
            {
                SetController(animator, CharacterSandbox.CombatControllerPath);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Presentation candidate", EditorStyles.boldLabel);
            transitionDuration = EditorGUILayout.Slider("Transition (s)", transitionDuration, 0f, 0.25f);
            fightIdleExitTime = EditorGUILayout.Slider("FightIdle exit (normalized)", fightIdleExitTime, 0.25f, 1f);
            jabExitTime = EditorGUILayout.Slider("Jab exit (normalized)", jabExitTime, 0.5f, 1f);
            crossExitTime = EditorGUILayout.Slider("Cross exit (normalized)", crossExitTime, 0.5f, 1f);

            if (GUILayout.Button("Play FightIdle → Jab → Cross → FightIdle", GUILayout.Height(30f)))
            {
                PlayCombo(animator);
            }

            if (GUILayout.Button("Stop Presentation"))
            {
                comboPlaying = false;
                animator.speed = 0f;
            }

            EditorGUILayout.Space();
            foreach (CharacterSandbox.ClipSpec spec in CharacterSandbox.Bundle)
            {
                if (GUILayout.Button(spec.State, GUILayout.Height(26f)))
                {
                    animator.speed = playbackSpeed;
                    animator.Play(spec.State, 0, 0f);
                    animator.Update(0f);
                    if (freezeAtStart)
                    {
                        animator.speed = 0f;
                    }
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Resume (speed = 1)"))
            {
                playbackSpeed = 1f;
                animator.speed = 1f;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            EditorGUILayout.LabelField("Normalized time", (info.normalizedTime % 1f).ToString("F2"));
            EditorGUILayout.LabelField("Loop", info.loop.ToString());
            Repaint();
        }

        private void PlayCombo(Animator animator)
        {
            comboPlaying = true;
            comboStep = 0;
            animator.speed = playbackSpeed;
            animator.Play(ComboStates[0], 0, 0f);
            animator.Update(0f);
        }

        private static void SetController(Animator animator, string path)
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (controller == null)
            {
                Debug.LogError($"Character Sandbox: controller missing at {path}.");
                return;
            }

            animator.runtimeAnimatorController = controller;
            animator.Play("FightIdle", 0, 0f);
            animator.Update(0f);
        }

        private void UpdateCombo()
        {
            if (!comboPlaying || !Application.isPlaying)
            {
                return;
            }

            Animator animator = ResolveAnimator();
            if (animator == null || comboStep >= ComboStates.Length - 1)
            {
                comboPlaying = false;
                return;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            float exit = comboStep == 0 ? fightIdleExitTime : comboStep == 1 ? jabExitTime : crossExitTime;
            if (info.IsName(ComboStates[comboStep]) && info.normalizedTime >= exit)
            {
                comboStep++;
                animator.CrossFadeInFixedTime(ComboStates[comboStep], transitionDuration, 0, 0f);
            }
        }

        private Animator ResolveAnimator()
        {
            if (cachedAnimator != null)
            {
                return cachedAnimator;
            }

            GameObject ybot = GameObject.Find(YBotObjectName);
            cachedAnimator = ybot != null ? ybot.GetComponentInChildren<Animator>() : null;
            return cachedAnimator;
        }
    }
}
