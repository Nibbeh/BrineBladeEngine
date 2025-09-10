using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Services;

public sealed class SeededRandom : IRandom
{
    private readonly Random _rng;

    public SeededRandom(int seed) => _rng = new Random(seed);

    public int Next(int minValue, int maxValue) => _rng.Next(minValue, maxValue);
    public double NextDouble() => _rng.NextDouble();
}

