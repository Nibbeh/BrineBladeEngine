using System;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Services
{
    /// <summary>
    /// Minimal adapter over System.Random to satisfy IRandom.
    /// </summary>
    public sealed class DefaultRandom : IRandom
    {
        private readonly Random _rng;

        public DefaultRandom() : this(Random.Shared) { }

        public DefaultRandom(Random rng)
            => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        // Exclusive upper bound (matches System.Random)
        public int Next(int minValue, int maxValue) => _rng.Next(minValue, maxValue);

        public double NextDouble() => _rng.NextDouble();
    }
}

