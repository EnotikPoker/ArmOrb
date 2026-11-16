using System;
using UnityEngine;

namespace ArmOrb
{
    public enum ArenaSize { Small, Medium, Large }
    [Serializable]
    public sealed class MatchOptions
    {
        public ArenaSize arena = ArenaSize.Medium;
        public bool gravity = true;
        public float playbackSpeed = 1;
        public static float ValidSpeed(float value) => value <= 0.75f ? 0.5f : value < 1.5f ? 1 : 2;
        public void Load()
        {
            arena = (ArenaSize)Mathf.Clamp(PlayerPrefs.GetInt("ArmOrb.Arena", 1), 0, 2);
            gravity = PlayerPrefs.GetInt("ArmOrb.Gravity", 1) == 1;
            playbackSpeed = ValidSpeed(PlayerPrefs.GetFloat("ArmOrb.Speed", 1));
        }
        public void Save()
        {
            PlayerPrefs.SetInt("ArmOrb.Arena", (int)arena);
            PlayerPrefs.SetInt("ArmOrb.Gravity", gravity ? 1 : 0);
            PlayerPrefs.SetFloat("ArmOrb.Speed", playbackSpeed);
            PlayerPrefs.Save();
        }
    }
    public sealed class SimulationClock
    {
        public readonly double StepSeconds;
        double remainder;
        public float Interpolation => (float)(remainder / StepSeconds);
        public SimulationClock(int frequency) => StepSeconds = 1.0 / frequency;
        public int Consume(double realSeconds, float multiplier)
        {
            remainder += Math.Max(0, realSeconds) * multiplier;
            int ticks = (int)Math.Floor((remainder + 1e-9) / StepSeconds);
            remainder = Math.Max(0, remainder - ticks * StepSeconds);
            return ticks;
        }
    }
}
