using System;
using UnityEngine;

namespace Greyline.Minigames
{
    [CreateAssetMenu(menuName = "Greyline/Minigames/Courier Run Config")]
    public sealed class CourierRunConfig : ScriptableObject
    {
        [Min(1f)] public float puzzleSeconds = 10f;
        [Min(1)] public int nodeCount = 5;
        [Min(1)] public int sequenceLength = 3;
        [Min(1)] public int suspicionPerMistake = 1;
        [Min(1)] public int detectionThreshold = 3;
        [Min(0.1f)] public float suspicionRecoverySeconds = 2f;
        public string[] puzzleLabels = { "INDEX", "ROUTE", "KEY", "COPY", "VERIFY" };
        public int[] correctSequence = { 2, 5, 1 };
        public string fileName = "archive_17.dat";
        public string exchangeTarget = "Library Student";
        [Min(0.25f)] public float exchangeRadius = 1.25f;
        [Min(0.25f)] public float terminalRadius = 1.25f;
        [Min(0.25f)] public float exitRadius = 1.25f;
        public string rewardKey = "minigame.microsd.first_clear";

        public bool Validate(out string error)
        {
            if (puzzleSeconds <= 0f) { error = "puzzleSeconds must be positive."; return false; }
            if (nodeCount < 1 || nodeCount > 5) { error = "nodeCount must be between 1 and 5 for the current keyboard contract."; return false; }
            if (sequenceLength < 1 || correctSequence == null || correctSequence.Length < sequenceLength)
            { error = "correctSequence must contain sequenceLength entries."; return false; }
            for (int i = 0; i < sequenceLength; i++)
                if (correctSequence[i] < 1 || correctSequence[i] > nodeCount) { error = "correctSequence contains a node outside nodeCount."; return false; }
            if (suspicionPerMistake < 1 || detectionThreshold < 1 || suspicionRecoverySeconds <= 0f) { error = "Suspicion values must be positive."; return false; }
            if (exchangeRadius <= 0f || terminalRadius <= 0f || exitRadius <= 0f) { error = "Terminal, exchange, and exit radii must be positive."; return false; }
            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            if (!Validate(out string error)) Debug.LogWarning("Invalid CourierRunConfig: " + error, this);
        }
    }

    public enum CourierState { Inspect, Sneak, Puzzle, Exchange, Escape, Success, Failed, Exited }
    public enum SuspicionLevel { Normal, Suspicious, Detected }

    [Serializable]
    public struct CourierResult
    {
        public bool completed;
        public int suspicion;
        public string rewardKey;
        public CourierResult(bool completed, int suspicion, string rewardKey) { this.completed = completed; this.suspicion = suspicion; this.rewardKey = rewardKey; }
    }
}
