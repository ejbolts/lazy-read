using System;

namespace LazyRead;

public sealed class GenerationEstimate
{
    private double _secondsPerCharacter = 0.16;
    public double Predict(int characters) => Math.Clamp(1.5 + Math.Max(1, characters) * _secondsPerCharacter, 2, 180);
    public void Observe(int characters, double seconds)
    {
        if (characters <= 0 || !double.IsFinite(seconds) || seconds <= 0) return;
        var sample = Math.Clamp(seconds / characters, 0.01, 5);
        _secondsPerCharacter = 0.6 * _secondsPerCharacter + 0.4 * sample;
    }
    public static string Remaining(double predictedSeconds, double elapsedSeconds) => elapsedSeconds < predictedSeconds
        ? $"≈ {Math.Ceiling(Math.Max(1, predictedSeconds - elapsedSeconds)):0}s"
        : "Finishing…";
}
