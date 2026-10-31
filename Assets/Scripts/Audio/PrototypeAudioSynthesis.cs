using System;
using UnityEngine;

namespace Kartikaris.Audio
{
    /// <summary>
    /// Creates compact deterministic prototype clips at runtime. Clips are built
    /// once by the owning sound service rather than allocating during gameplay.
    /// </summary>
    public static class PrototypeAudioSynthesis
    {
        private const int SampleRate = 44100;

        public static AudioClip CreateCannonBoom(string clipName)
        {
            const float duration = 1.35f;
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            System.Random noise = new System.Random(173);

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float bodyEnvelope = Mathf.Exp(-3.7f * time);
                float noiseEnvelope = Mathf.Exp(-10f * time);
                float frequency = Mathf.Lerp(64f, 29f, time / duration);
                float body = Mathf.Sin(2f * Mathf.PI * frequency * time);
                float subBody = Mathf.Sin(2f * Mathf.PI * frequency * 0.53f * time);
                float crack = NextSigned(noise) * Mathf.Exp(-42f * time);
                float tail = NextSigned(noise) * noiseEnvelope;
                samples[index] = Mathf.Clamp(
                    (0.58f * body + 0.24f * subBody) * bodyEnvelope
                    + 0.52f * crack
                    + 0.2f * tail,
                    -1f,
                    1f);
            }

            return CreateClip(clipName, samples);
        }

        public static AudioClip CreateRadarBeep(string clipName)
        {
            const float duration = 0.09f;
            const float firstFrequency = 920f;
            const float secondFrequency = 1180f;
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float attack = Mathf.Clamp01(time / 0.004f);
                float release = Mathf.Clamp01((duration - time) / 0.025f);
                float frequency = Mathf.Lerp(firstFrequency, secondFrequency, time / duration);
                samples[index] = 0.32f
                    * Mathf.Sin(2f * Mathf.PI * frequency * time)
                    * attack
                    * release;
            }

            return CreateClip(clipName, samples);
        }

        public static AudioClip CreateTreeBreak(string clipName)
        {
            const float duration = 0.28f;
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            System.Random noise = new System.Random(619);

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float snapEnvelope = Mathf.Exp(-28f * time);
                float woodEnvelope = Mathf.Exp(-9f * time);
                float snap = NextSigned(noise) * snapEnvelope;
                float wood = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(155f, 72f, time / duration) * time);
                samples[index] = Mathf.Clamp(0.72f * snap + 0.38f * wood * woodEnvelope, -1f, 1f);
            }

            return CreateClip(clipName, samples);
        }

        public static AudioClip CreateTreeImpact(string clipName)
        {
            const float duration = 0.52f;
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            System.Random noise = new System.Random(947);

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float bodyEnvelope = Mathf.Exp(-7f * time);
                float debrisEnvelope = Mathf.Exp(-15f * time);
                float frequency = Mathf.Lerp(92f, 42f, time / duration);
                float thud = Mathf.Sin(2f * Mathf.PI * frequency * time);
                float debris = NextSigned(noise);
                samples[index] = Mathf.Clamp(
                    0.62f * thud * bodyEnvelope + 0.26f * debris * debrisEnvelope,
                    -1f,
                    1f);
            }

            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateClip(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float NextSigned(System.Random random)
        {
            return (float)(random.NextDouble() * 2.0 - 1.0);
        }
    }
}
