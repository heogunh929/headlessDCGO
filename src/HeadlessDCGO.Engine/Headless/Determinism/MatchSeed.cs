// ============================================================================================================
// THE ONE RESEED THAT MAKES A MATCH REPRODUCIBLE (roadmap step 3.1).
//
// WHY THIS WORKS. The AS-IS already funnels every gameplay random draw — shuffles, probabilities — through
// `GameRandom` (Script/GameRandom.cs, Xoshiro256**), and the card layer draws no randomness at all
// [census 2026-07-29]. What is NOT deterministic is only the SEED: `RandomUtility.GetSecureRandom()` reads the
// OS entropy pool, at two init sites and once in the match-start handshake (`TurnStateMachine.Init` sends it
// over the "SetRandom" RPC, TurnStateMachine.cs:224, so both Photon clients would shuffle identically).
//
// WHY THIS TIMING. Last Seed() wins, and the handshake is the last one before play: `SetRandomCoroutine` sets
// `DoneSetRandom` when it has seeded (ContinuousController.cs:1334-1340), TurnStateMachine parks on that flag
// (:227) and only AFTERWARDS builds and shuffles the decks (`CreatePlayerDecks`, :233). The flag flips inside
// a driver tick; the parked wait resumes on the NEXT tick. A harness that checks between ticks therefore sees
// exactly the window after the AS-IS's own seed and before the first draw.
//
// The mirror stays untouched: substituting the RPC argument would forge an AS-IS message, and transforming
// `GetSecureRandom` is a copy-pipeline change nothing needs. The harness overwrites the seed in the window the
// AS-IS itself provides.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Determinism;

/// <summary>Pins a match's random streams to a caller-chosen seed. Call <see cref="TryPin"/> once per tick
/// until it reports the reseed happened.</summary>
public static class MatchSeed
{
    /// <summary>Reseeds <c>GameRandom</c> and the <c>UnityEngine.Random</c> shim at the first tick where the
    /// AS-IS shared-seed handshake has completed, and not before — earlier would be overwritten by the
    /// handshake, later would miss the deck shuffle. Returns false while the window has not arrived.</summary>
    public static bool TryPin(int seed)
    {
        if (ContinuousController.instance?.DoneSetRandom != true)
        {
            return false;
        }

        GameRandom.Seed(seed);
        UnityEngine.Random.InitState(seed);

        return true;
    }
}
