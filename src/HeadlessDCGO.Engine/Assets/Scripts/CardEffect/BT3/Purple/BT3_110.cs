// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_110.cs
// STOP (both blocks): BT3/Purple.
//
// [Main] Play 1 purple level 5 Digimon card from your trash without paying its memory cost. Any [On Play]
// effects on the Digimon played with this effect don't activate.
//   AS-IS ActivateCoroutine calls CardEffectCommons.PlayPermanentCards(..., activateETB: false) — same
//   documented gap as BT3_109 (CardEffectCommons.PlayPermanentCards throws NotSupportedException on
//   activateETB:false; the underlying MatchStateMutationSink.ApplyPlayCard mutation unconditionally fires
//   the played card's [On Play] via `_onCardEnteredPlay`, with no suppression flag; neither
//   SelectAndPlayFromZoneEffect nor ActivatedSelectAndPlayEffect can express it either). Dropping the "[On
//   Play] effects don't activate" clause to approximate with a regular cost-free play would be a fidelity
//   dilution (forbidden).
//
// [Security] Activate this card's own [Main] Option skill from security.
//   CardEffectCommons.AddActivateMainOptionSecurityEffect(card, ref cardEffects, effectName) exists and
//   matches AS-IS 1:1, but its entire purpose is to RE-RUN the [Main] branch above — which is itself
//   unregistered (see above). Wiring this block alone would register a "security skill" that silently
//   resolves to nothing (ReuseMainOptionEffect replays an empty OptionSkill list), misrepresenting the
//   card's actual behavior rather than reflecting the true gap. Left unregistered together with [Main].
//
// Per the primitive-gap rule (no new primitives, no engine edits, no throw), this card is left
// unregistered.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_110 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        // STOP: [Main] play 1 purple level-5 Digimon from trash cost-free, its [On Play] suppressed —
        // see file-header STOP note (activateETB:false has no headless port surface).
        // STOP: [Security] reuse the [Main] skill — depends entirely on the unregistered [Main] above;
        // registering it alone would silently no-op. Not registered.
        return new List<ICardEffect>();
    }
}
