using System;

namespace StardustAtelier.Core
{
    // [실제 코드에 남길 주석] PCG-XSH-RR 64/32. 값 형식이므로 복사하면 난수 상태도 함께 복사됩니다.
    public struct Pcg32
    {
        private ulong state;
        private ulong increment;
        public Pcg32(ulong seed, ulong stream)
        {
            state = 0; increment = unchecked((stream << 1) | 1);
            NextUInt(); state = unchecked(state + seed); NextUInt();
        }
        public uint NextUInt()
        {
            ulong previous = state;
            state = unchecked(previous * 6364136223846793005UL + increment);
            uint shifted = (uint)(((previous >> 18) ^ previous) >> 27);
            int rotation = (int)(previous >> 59);
            return (shifted >> rotation) | (shifted << ((-rotation) & 31));
        }
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException("exclusiveMax");
            uint bound = (uint)exclusiveMax;
            uint threshold = unchecked(0u - bound) % bound;
            uint value;
            do { value = NextUInt(); } while (value < threshold);
            return (int)(value % bound);
        }
    }
}
