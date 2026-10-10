using System;
using System.Collections.Generic;

namespace LazyRead;

public sealed class GenerationEstimate
{
    private const double Overhead = 1.5;
    // Defaults approximate a recent NVIDIA GPU; the first measured passage replaces them.
    private double _secondsPerCharacter = 0.11, _audioPerCharacter = 0.06;
    private bool _measured, _measuredAudio;
    public double Predict(int characters) => Math.Clamp(Overhead + Math.Max(1, characters) * _secondsPerCharacter, 2, 180);
    public double PredictAudio(int characters) => Math.Max(1, characters) * _audioPerCharacter;
    public void Observe(int characters, double seconds)
    {
        if (characters <= 0 || !double.IsFinite(seconds) || seconds <= 0) return;
        var sample = Math.Clamp((seconds - Overhead) / characters, 0.01, 5);
        _secondsPerCharacter = _measured ? 0.6 * _secondsPerCharacter + 0.4 * sample : sample;
        _measured = true;
    }
    public void ObserveAudio(int characters, double seconds)
    {
        if (characters <= 0 || !double.IsFinite(seconds) || seconds <= 0) return;
        var sample = Math.Clamp(seconds / characters, 0.01, 1);
        _audioPerCharacter = _measuredAudio ? 0.6 * _audioPerCharacter + 0.4 * sample : sample;
        _measuredAudio = true;
    }
    public static string Remaining(double predictedSeconds, double elapsedSeconds) => elapsedSeconds < predictedSeconds
        ? $"≈ {Math.Ceiling(Math.Max(1, predictedSeconds - elapsedSeconds)):0}s"
        : "Finishing…";
}

public static class PlaybackBuffer
{
    // Margins absorb run-to-run variation in generation time and in the length of generated speech.
    public const double GenerationMargin = 1.15, AudioMargin = 0.9, Cushion = 1.0;
    // Seconds playback should still wait so every remaining passage is ready before playback reaches it.
    public static double RequiredDelay(double bufferedAudio, IReadOnlyList<(double Generate, double Audio)> remaining, double speed)
    {
        double ready = 0, audio = bufferedAudio, delay = 0;
        foreach (var (generate, length) in remaining)
        {
            ready += generate * GenerationMargin;
            delay = Math.Max(delay, ready + Cushion - audio / speed);
            audio += length * AudioMargin;
        }
        return delay;
    }
}
