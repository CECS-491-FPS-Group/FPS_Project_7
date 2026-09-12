using UnityEngine;

/// <summary>
/// Integer-only PRNG. Every operation is exact on any platform, so two clients seeded
/// identically produce identical worlds without replicating placement data.
/// </summary>
public struct DeterministicRandom
{
    uint state;

    public DeterministicRandom(uint seed)
    {
        state = seed;
    }

    public static DeterministicRandom ForChunk(int worldSeed, int chunkX, int chunkY, int stream)
    {
        return new DeterministicRandom(Hash((uint)worldSeed, (uint)chunkX, (uint)chunkY, (uint)stream));
    }

    public static uint Hash(uint a, uint b, uint c, uint d)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = Mix(h ^ a);
            h = Mix(h ^ b);
            h = Mix(h ^ c);
            h = Mix(h ^ d);
            return h;
        }
    }

    static uint Mix(uint z)
    {
        unchecked
        {
            z = (z ^ (z >> 16)) * 0x21F0AAADu;
            z = (z ^ (z >> 15)) * 0x735A2D97u;
            return z ^ (z >> 15);
        }
    }

    public uint NextUInt()
    {
        unchecked
        {
            state += 0x9E3779B9u;
            return Mix(state);
        }
    }

    public float NextFloat()
    {
        return (NextUInt() >> 8) * (1f / 16777216f);
    }

    public float NextFloat(float min, float max)
    {
        return min + (max - min) * NextFloat();
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        int range = maxExclusive - minInclusive;
        if (range <= 0)
        {
            return minInclusive;
        }
        return minInclusive + (int)(NextUInt() % (uint)range);
    }

    public bool NextChance(float probability)
    {
        return NextFloat() < probability;
    }

    public Vector2 NextInsideUnitCircle()
    {
        float angle = NextFloat() * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(NextFloat());
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    public Quaternion NextYaw()
    {
        float halfAngle = NextFloat() * Mathf.PI;
        return new Quaternion(0f, Mathf.Sin(halfAngle), 0f, Mathf.Cos(halfAngle));
    }
}
