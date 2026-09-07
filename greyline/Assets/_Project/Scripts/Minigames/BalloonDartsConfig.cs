using UnityEngine;

namespace Greyline.Minigames
{
    [CreateAssetMenu(menuName = "Greyline/Minigames/Balloon Darts Config")]
    public sealed class BalloonDartsConfig : ScriptableObject
    {
        [Min(1)] public int dartCount = 12;
        [Min(10f)] public float sessionSeconds = 45f;
        [Min(1)] public int normalScore = 10;
        [Min(1)] public int highValueScore = 50;
        [Min(1)] public int penaltyScore = 25;
        [Min(0)] public int minimumScoreToClear = 50;
        [Min(.01f)] public float aimSpeed = .65f;
        [Min(.1f)] public float releaseCycleSeconds = 1.2f;
        [Min(.001f)] public float maximumReleaseOffset = .08f;
        [Min(.01f)] public float throwCooldownSeconds = .3f;
        public string rewardKey = "minigame.balloon_darts.first_clear";

        public bool Validate(out string error)
        {
            if (dartCount < 1) { error = "dartCount must be at least 1."; return false; }
            if (sessionSeconds <= 0f || aimSpeed <= 0f || releaseCycleSeconds <= 0f || maximumReleaseOffset <= 0f || throwCooldownSeconds <= 0f)
            { error = "Timing and aim speeds must be positive."; return false; }
            if (normalScore < 0 || highValueScore < 0 || penaltyScore < 0 || minimumScoreToClear < 0)
            { error = "Scores and minimumScoreToClear cannot be negative."; return false; }
            if (minimumScoreToClear > dartCount * Mathf.Max(normalScore, highValueScore))
            { error = "minimumScoreToClear cannot be reached with the configured dart count and scores."; return false; }
            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            if (!Validate(out string error)) Debug.LogWarning("Invalid BalloonDartsConfig: " + error, this);
        }
    }

    public enum BalloonType { Normal, HighValue, Penalty }
}
