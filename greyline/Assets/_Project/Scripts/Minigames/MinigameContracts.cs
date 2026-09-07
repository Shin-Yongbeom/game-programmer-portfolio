using System;

namespace Greyline.Minigames
{
    public enum MinigameState { Idle, Running, Success, Failed, Exited }

    [Serializable]
    public struct MinigameRewardResult
    {
        public bool completed;
        public int score;
        public int bestStreak;
        public string rewardKey;

        public MinigameRewardResult(bool completed, int score, int bestStreak, string rewardKey)
        {
            this.completed = completed;
            this.score = score;
            this.bestStreak = bestStreak;
            this.rewardKey = rewardKey;
        }
    }

    public interface IMinigameSession
    {
        MinigameState State { get; }
        void StartSession();
        void RestartSession();
        void ExitSession();
    }
}
