namespace HeadlessDCGO.Engine.Headless.Services;

// 1:1 mirror of AS-IS DCGO/Assets/Scripts/Script/GameRandom.cs (Xoshiro256** + SplitMix64 seeding).
// Substrate: exposes the IRandomSource/IRandomSeedController surface the headless engine needs, but the
// SEQUENCE must be byte-identical to AS-IS GameRandom so deck shuffles / random picks match the original.
//   - NextInt   ≡ AS-IS Range(int)   : NextUInt32 (upper 32 bits of NextState) + 32-bit rejection sampling.
//   - NextDouble≡ AS-IS Range(float) : (NextUInt32 >> 8) * 1/2^24  (24-bit [0,1)).
//   - Shuffle   ≡ AS-IS ContinuousController.ShuffledDeckCards (Fisher-Yates: n-- from Count, k=Range(0,n+1)).
// Seed is int-scoped (AS-IS Seed(long)); for any int seed value `(ulong)(long)seed == (ulong)seedAsLong`, so
// the state is identical for the int-range seeds the engine uses.
//
// ── What randomness the ORIGINAL (Unity) engine uses, and what this seeded RNG covers ─────────────────────
// AS-IS splits randomness into two tiers, because it is a Photon head-to-head game:
//   (A) GameRandom — DETERMINISTIC, seeded, "both clients seed with the same value to produce identical
//       sequences" (GameRandom.cs header). Used ONLY for state that must be synchronized across the two
//       clients / reproducible:
//         • deck shuffle          — ContinuousController.ShuffledDeckCards (GameRandom.Range, :1534/1559)
//         • first-player decision — TurnStateMachine.cs:255 (GameRandom → gameContext.TurnPlayer)
//         • AI-bot probabilistic decisions — RandomUtility.IsSucceedProbability(p) (GameRandom.Range(0,1)):
//               whether to attack (0.5, SelectAttackEffect:438), mulligan/redraw (0.5, TurnStateMachine:388),
//               hatch digitama (0.85, :776), use an optional skill (0.9, OptionalSkill:113), etc.
//   (B) UnityEngine.Random / System.Random — NON-deterministic, wall-clock, local-only, NOT synchronized:
//         • VFX / audio (SecurityBreakGlass glass velocity, BGM), AI-bot SELECTIONS (which attack target /
//           defender / DigiXros index — SelectAttackEffect:440, TurnStateMachine:1009+), AI random deck pick
//           (CardObjectController:37/80/88), forced-selection search (IEnumerableExtension.GetRandom), and
//           matchmaking (LobbyManager_*). None of these need cross-client determinism.
//
// HEADLESS scope: the AI opponent is replaced by the RL agent / ChoiceProvider, so tier-(A)'s AI-bot decision
// draws and all of tier-(B) do NOT flow through this RNG. The ONLY live consumers here are deck/security/hand
// SHUFFLE (InMemoryZoneMover, ZoneState, and the mirror CardObjectController.CreatePlayerDecks deck build) and
// FIRST-PLAYER selection (the mirror TurnStateMachine.InitAsync, AS-IS TurnStateMachine.cs:256).
// Card play itself has no randomness (DCG is deterministic apart from shuffle + coin-flip), so a fixed seed
// reproduces an identical game and varying the per-episode seed varies the opening hand / turn order.
public sealed class GameRandomSource :
    IRandomSource,
    IRandomSeedController,
    IRandomStateReader
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public GameRandomSource(int seed = 0)
    {
        ResetSeed(seed);
    }

    public int CurrentSeed { get; private set; }

    public void ResetSeed(int seed)
    {
        CurrentSeed = seed;

        ulong state = unchecked((ulong)(long)seed);
        _s0 = SplitMix64(ref state);
        _s1 = SplitMix64(ref state);
        _s2 = SplitMix64(ref state);
        _s3 = SplitMix64(ref state);

        // Ensure state is not all-zero (degenerate case) — AS-IS GameRandom.Seed:26-27.
        if (_s0 == 0 && _s1 == 0 && _s2 == 0 && _s3 == 0)
        {
            _s0 = 1;
        }
    }

    // AS-IS GameRandom.Range(int min, int exclusiveMax) — [min, exclusiveMax) with rejection sampling, no bias.
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        uint range = (uint)(maxExclusive - minInclusive);

        if (range == 1)
        {
            return minInclusive;
        }

        uint threshold = (uint)((0x100000000UL - range) % range);

        uint raw;
        do
        {
            raw = NextUInt32();
        } while (raw < threshold);

        return minInclusive + (int)(raw % range);
    }

    // AS-IS GameRandom.Range(float 0,1) precision: 24-bit [0,1).
    public double NextDouble()
    {
        return (NextUInt32() >> 8) * (1.0 / (1 << 24));
    }

    // AS-IS ContinuousController.ShuffledDeckCards Fisher-Yates (ContinuousController.cs:1521/1546).
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        int n = items.Count;
        while (n > 0)
        {
            n--;

            int k = NextInt(0, n + 1);
            (items[n], items[k]) = (items[k], items[n]);
        }
    }

    // AS-IS GameRandom.NextUInt32: upper 32 bits of the Xoshiro256** result.
    private uint NextUInt32()
    {
        return (uint)(NextState() >> 32);
    }

    // AS-IS GameRandom.NextState (xoshiro256** step) — byte-identical.
    private ulong NextState()
    {
        ulong result = RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;

        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    private static ulong RotateLeft(ulong value, int offset)
    {
        return (value << offset) | (value >> (64 - offset));
    }

    private static ulong SplitMix64(ref ulong state)
    {
        ulong value = state += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
