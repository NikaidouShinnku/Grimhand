namespace Grimhand.Core
{
    public sealed class BattleRng
    {
        readonly int _seed;
        ulong _state;

        public BattleRng(int seed)
        {
            _seed = seed;
            _state = (ulong)(uint)seed;
            if (_state == 0)
                _state = 1;
        }

        BattleRng(int seed, ulong state)
        {
            _seed = seed;
            _state = state == 0 ? 1 : state;
        }

        public BattleRng Copy() => new BattleRng(_seed, _state);

        public int Seed => _seed;

        public ulong State
        {
            get => _state;
            set => _state = value == 0 ? 1 : value;
        }

        public void RestoreState(ulong state) => State = state;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            var range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        public int NextIndex(int count) => NextInt(0, count);

        public ulong NextUInt()
        {
            // xorshift64*
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 2685821657736338717UL;
        }
    }
}
