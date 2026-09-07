using NUnit.Framework;
using UnityEngine;

namespace Greyline.Minigames.Tests
{
    public sealed class MinigameConfigValidationTests
    {
        [Test]
        public void CourierConfig_DefaultIsValid()
        {
            CourierRunConfig config = ScriptableObject.CreateInstance<CourierRunConfig>();
            Assert.That(config.Validate(out string error), Is.True, error);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CourierConfig_RejectsNonPositiveSuspicionRecovery()
        {
            CourierRunConfig config = ScriptableObject.CreateInstance<CourierRunConfig>();
            config.suspicionRecoverySeconds = 0f;
            Assert.That(config.Validate(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CourierConfig_RejectsSequenceNodeOutsideNodeCount()
        {
            CourierRunConfig config = ScriptableObject.CreateInstance<CourierRunConfig>();
            config.nodeCount = 3;
            config.sequenceLength = 2;
            config.correctSequence = new[] { 1, 5 };
            Assert.That(config.Validate(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void NeolttwigiConfig_DefaultIsValid()
        {
            NeolttwigiConfig config = ScriptableObject.CreateInstance<NeolttwigiConfig>();
            Assert.That(config.Validate(out string error), Is.True, error);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void NeolttwigiConfig_RejectsGoodWindowNotGreaterThanPerfectWindow()
        {
            NeolttwigiConfig config = ScriptableObject.CreateInstance<NeolttwigiConfig>();
            config.perfectWindow = .2f;
            config.goodWindow = .2f;
            Assert.That(config.Validate(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void NeolttwigiConfig_BeatSecondsAtAdvancesThroughConfiguredPhases()
        {
            NeolttwigiConfig config = ScriptableObject.CreateInstance<NeolttwigiConfig>();
            config.speedPhases = 3;
            config.phaseSeconds = 15f;
            config.phaseOneBeatSeconds = 1.05f;
            config.phaseTwoBeatSeconds = .82f;
            config.phaseThreeBeatSeconds = .64f;

            Assert.That(config.BeatSecondsAt(0f), Is.EqualTo(1.05f));
            Assert.That(config.BeatSecondsAt(16f), Is.EqualTo(.82f));
            Assert.That(config.BeatSecondsAt(31f), Is.EqualTo(.64f));
            Assert.That(config.BeatSecondsAt(1000f), Is.EqualTo(.64f), "Beat interval must not exceed the last configured phase.");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void BalloonDartsConfig_DefaultIsValid()
        {
            BalloonDartsConfig config = ScriptableObject.CreateInstance<BalloonDartsConfig>();
            Assert.That(config.Validate(out string error), Is.True, error);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void BalloonDartsConfig_RejectsUnreachableClearScore()
        {
            BalloonDartsConfig config = ScriptableObject.CreateInstance<BalloonDartsConfig>();
            config.dartCount = 5;
            config.normalScore = 10;
            config.highValueScore = 10;
            config.minimumScoreToClear = 100;
            Assert.That(config.Validate(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CourierWatcher_SneakingReducesEffectiveViewDistance()
        {
            Assert.That(CourierWatcher.EffectiveViewDistance(6f, false, .5f), Is.EqualTo(6f));
            Assert.That(CourierWatcher.EffectiveViewDistance(6f, true, .5f), Is.EqualTo(3f));
        }

        [Test]
        public void CourierWatcher_SneakMultiplierIsClamped()
        {
            Assert.That(CourierWatcher.EffectiveViewDistance(10f, true, 5f), Is.EqualTo(10f), "A multiplier above 1 must not extend range beyond the base.");
            Assert.That(CourierWatcher.EffectiveViewDistance(10f, true, 0f), Is.EqualTo(1f), "A multiplier at/below zero must not collapse detection range to zero.");
        }

        [Test]
        public void NeolttwigiGame_AdvanceBeatValue_NormalBeatAdvancesByOneInterval()
        {
            float next = NeolttwigiGame.AdvanceBeatValue(1.0f, 1.02f, .22f, _ => .64f);
            Assert.That(next, Is.EqualTo(1.64f).Within(.0001f));
        }

        [Test]
        public void NeolttwigiGame_AdvanceBeatValue_CatchesUpFullyAfterHitch()
        {
            // A 3-second frame hitch while the beat interval is .64s would take several beats
            // to catch nextBeat back up to elapsed; AdvanceBeatValue must land past the current
            // goodWindow in one call so a single hitch cannot be re-flagged as a miss on the
            // following frames.
            const float goodWindow = .22f;
            float next = NeolttwigiGame.AdvanceBeatValue(1.0f, 4.0f, goodWindow, _ => .64f);
            Assert.That(next + goodWindow, Is.GreaterThanOrEqualTo(4.0f));
        }
    }
}
