using System.Runtime.CompilerServices;

namespace Strata.Sim;

/// <summary>Small deterministic generator (xorshift64*). One instance per chunk per tick keeps results identical
/// no matter how chunks are scheduled across threads.</summary>
public struct Rng
{
    ulong _s;

    public Rng(ulong seed) => _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    public static ulong Mix(ulong a, ulong b, ulong c)
    {
        ulong z = a * 0x9E3779B97F4A7C15UL ^ (b + 0xBF58476D1CE4E5B9UL) * 0x94D049BB133111EBUL ^ (c + 0x2545F4914F6CDD1DUL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Next()
    {
        _s ^= _s >> 12; _s ^= _s << 25; _s ^= _s >> 27;
        return _s * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform in [0, 1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Float() => (Next() >> 40) * (1f / (1 << 24));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Chance(float p) => Float() < p;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Int(int n) => (int)((Next() >> 33) % (ulong)n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Sign() => (Next() & 1) == 0 ? -1 : 1;
}
