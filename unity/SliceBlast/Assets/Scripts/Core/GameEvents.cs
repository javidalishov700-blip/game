using System;
using UnityEngine;

namespace SliceBlast.Core
{
    public enum PlacementKind : byte
    {
        Perfect = 0,
        Sliced = 1,
        Shielded = 2,
        Missed = 3,
        SpecialFailed = 4
    }

    public struct PlacementEvent
    {
        public BlockType Type;
        public PlacementKind Kind;
        public Vector3 Position;
        public int Streak;
    }

    public struct BlastEvent
    {
        public int Layers;
        public int Multiplier;
        public int Bonus;
        public Vector3 Epicenter;
        public Vector3 NextTop;
        public bool FromNeon;
    }

    public struct RewardEvent
    {
        public BlockType Source;
        public string Headline;
        public string Detail;
        public Color Color;
        public Vector3 Position;
        public int Points;
    }

    /// <summary>
    /// The signal bus. Gameplay raises, presentation listens; neither holds a reference to
    /// the other. Static so any system can subscribe without wiring, cleared on load so a
    /// domain reload never leaves a dead subscriber behind.
    /// </summary>
    public static class GameEvents
    {
        public static event Action RunStarted;
        public static event Action<int, int> ScoreChanged;
        public static event Action<PlacementEvent> BlockPlaced;
        public static event Action<BlastEvent> BlastFired;
        public static event Action<RewardEvent> RewardGranted;
        public static event Action<bool> ShieldChanged;
        public static event Action<float, float> MultiplierTimerChanged;
        public static event Action<int, int> RunEnded;
        public static event Action<bool> PauseChanged;
        public static event Action<Vector3> GlitchPulsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            RunStarted = null;
            ScoreChanged = null;
            BlockPlaced = null;
            BlastFired = null;
            RewardGranted = null;
            ShieldChanged = null;
            MultiplierTimerChanged = null;
            RunEnded = null;
            PauseChanged = null;
            GlitchPulsed = null;
        }

        public static void RaiseRunStarted() => RunStarted?.Invoke();

        public static void RaiseScoreChanged(int score, int multiplier) => ScoreChanged?.Invoke(score, multiplier);

        public static void RaiseBlockPlaced(PlacementEvent placement) => BlockPlaced?.Invoke(placement);

        public static void RaiseBlastFired(BlastEvent blast) => BlastFired?.Invoke(blast);

        public static void RaiseReward(RewardEvent reward) => RewardGranted?.Invoke(reward);

        public static void RaiseShieldChanged(bool active) => ShieldChanged?.Invoke(active);

        public static void RaiseMultiplierTimer(float remaining, float total) => MultiplierTimerChanged?.Invoke(remaining, total);

        public static void RaiseRunEnded(int score, int best) => RunEnded?.Invoke(score, best);

        public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);

        public static void RaiseGlitchPulse(Vector3 position) => GlitchPulsed?.Invoke(position);
    }
}
