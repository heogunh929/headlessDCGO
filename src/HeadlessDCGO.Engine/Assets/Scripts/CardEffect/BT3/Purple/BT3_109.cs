// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_109.cs
// STOP: [Main] 1 of your Digimon gains "[On Deletion] Play this card without paying its memory cost. Any
// [On Play] effects on Digimon played with this effect don't activate" for the turn.
//
// The granted nested [On Deletion] effect's ActivateCoroutine1 calls
// CardEffectCommons.PlayPermanentCards(cardSources: {TopCard}, ..., root: SelectCardEffect.Root.Trash,
// activateETB: false) — the AS-IS "played card's [On Play] does NOT re-fire" flag.
//
// Headless's CardEffectCommons.PlayPermanentCards documents this exactly: "an activateETB=false
// suppression has no port surface (entry triggers derive from the zone move) ... a false caller is a
// STOP" and throws NotSupportedException if ever invoked with activateETB:false. The underlying
// MatchStateMutationSink.ApplyPlayCard mutation unconditionally invokes `_onCardEnteredPlay` on every
// PlayCardKind move (auto-registering + firing the played card's OnEnterFieldAnyone/[On Play] effects) —
// there is no mutation-level flag to suppress it. Both CardEffectFactory.SelectAndPlayFromZoneEffect and
// the underlying ActivatedSelectAndPlayEffect emit that same unconditional PlayCardKind mutation, so
// neither can express "play cost-free AND suppress this played card's [On Play]" either.
//
// Since the whole card's only branch (OptionSkill) hinges on granting this ETB-suppressed self-play
// effect, and no primitive can express activateETB:false, this card is left unregistered rather than
// silently dropping the "[On Play] effects don't activate" clause (a fidelity dilution — forbidden).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_109 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        // STOP: [Main] grant 1 of your Digimon a "[On Deletion] play this card cost-free, its [On Play]
        // doesn't activate" effect for the turn — see file-header STOP note (activateETB:false has no
        // headless port surface; PlayPermanentCards throws on it by design). Not registered.
        return new List<ICardEffect>();
    }
}
