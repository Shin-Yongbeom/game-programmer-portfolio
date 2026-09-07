using UnityEngine;

namespace Greyline.Minigames
{
    [CreateAssetMenu(menuName = "Greyline/Minigames/Neolttwigi Config")]
    public sealed class NeolttwigiConfig : ScriptableObject
    {
        [Min(10f)] public float sessionSeconds = 45f;
        [Min(.05f)] public float perfectWindow = .10f;
        [Min(.05f)] public float goodWindow = .22f;
        [Min(1)] public int missesToFail = 3;
        [Min(1)] public int speedPhases = 3;
        [Min(.1f)] public float phaseSeconds = 15f;
        [Min(.1f)] public float phaseOneBeatSeconds = 1.05f;
        [Min(.1f)] public float phaseTwoBeatSeconds = .82f;
        [Min(.1f)] public float phaseThreeBeatSeconds = .64f;
        public string rewardKey = "minigame.neolttwigi.first_clear";

        public bool Validate(out string error)
        {
            if (sessionSeconds < 10f) { error = "sessionSeconds must be at least 10 seconds."; return false; }
            if (perfectWindow <= 0f || goodWindow <= perfectWindow) { error = "goodWindow must be greater than perfectWindow."; return false; }
            if (missesToFail < 1) { error = "missesToFail must be at least 1."; return false; }
            if (speedPhases < 1 || speedPhases > 3) { error = "speedPhases must be between 1 and 3."; return false; }
            if (phaseSeconds <= 0f || phaseOneBeatSeconds <= 0f || phaseTwoBeatSeconds <= 0f || phaseThreeBeatSeconds <= 0f)
            { error = "Phase and beat durations must be positive."; return false; }
            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            if (!Validate(out string error)) Debug.LogWarning("Invalid NeolttwigiConfig: " + error, this);
        }

        public float BeatSecondsAt(float elapsed)
        {
            int phase = Mathf.Clamp(Mathf.FloorToInt(elapsed / phaseSeconds), 0, Mathf.Max(0, speedPhases - 1));
            if (phase == 0) return phaseOneBeatSeconds;
            if (phase == 1) return phaseTwoBeatSeconds;
            return phaseThreeBeatSeconds;
        }
    }
}
