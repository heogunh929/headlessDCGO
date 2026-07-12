// TEST FIXTURE (not a real card). UNCAPPED [All Turns] "card added to zone" reactors that gain the owner 1 memory
// with NO once-per-turn cap and NO suspend cost — so the observable directly splits the F1-Tier1 OnAdd batch model
// that the real single-fire witnesses (BT8_090 / BT15_083, both suspend-cost) would mask:
//   * OnAddHand (self-scope, cause REQUIRED via CanTriggerOnHandAdded) — an effect adding N cards to hand in ONE
//     batch (one shared add-hand id) gains +1 IFF the collapse fires the reactor once (+N if per-CardMoved); two
//     INDEPENDENT hand-add batches gain +2; a NON-effect add (no cause id — a turn/mulligan draw) gains +0
//     (the CardEffect!=null gate). Player-scope: adding to the OPPONENT'S hand gains +0 (player != owner).
//   * OnAddSecurity (self-scope, NO cause) — AS-IS fires PER SINGLE card (per IAddSecurity), so N cards added to
//     security gain +N (NO batch collapse — the whole point of a separate assertion from OnAddHand). Player-scope:
//     adding to the OPPONENT'S security gains +0.
// Inert in actual play (no such card exists).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnAddCounter : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAddHand)
        {
            // AS-IS CanTriggerOnHandAdded: player == owner AND CardEffect != null (effect-driven) — the
            // cause-required player-specific form (a NON-effect add is rejected, batch collapses to one fire).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnHandAdded(ctx, card, card.Owner, cardEffectSourceCondition: null);
            Add(effects, card, EffectTiming.OnAddHand, CanUse,
                "[All Turns] When an effect adds cards to your hand, gain 1 memory (uncapped).");
        }

        if (timing == EffectTiming.OnAddSecurity)
        {
            // AS-IS CanTriggerWhenAddSecurity: player == owner (no cause). Per-card (no collapse).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenAddSecurity(ctx, card, player => player == card.Owner);
            Add(effects, card, EffectTiming.OnAddSecurity, CanUse,
                "[All Turns] When a card is added to your security, gain 1 memory (uncapped).");
        }

        return effects;
    }

    private static void Add(
        List<ICardEffect> effects, CardSource card, EffectTiming timing,
        Func<CardEffectResolveContext, bool> canUse, string description) =>
        effects.Add(new ActivatedEffect(
            card: card,
            timing: timing,
            canUse: canUse,
            canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
            body: new MemoryBody(1),
            maxCountPerTurn: null,   // UNCAPPED — no cap to mask a per-event over-fire.
            isOptional: false,
            description: description));
}
