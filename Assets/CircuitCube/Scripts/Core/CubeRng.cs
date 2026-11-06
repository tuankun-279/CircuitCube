using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>mulberry32: same numbers as the web prototype for the same seed.</summary>
    public sealed class CubeRng
    {
        uint _s;

        public CubeRng(uint seed) { _s = seed; }

        public double Next()
        {
            unchecked
            {
                _s += 0x6D2B79F5u;
                uint t = _s;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int Range(int n) => (int)(Next() * n);

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
