// TEST FIXTURE (not a real card). The OPPONENT-scope mirror of TfxOnAddCounter: UNCAPPED [All Turns] "card added
// to zone" reactors that gain the OWNER 1 memory when the OPPONENT'S hand / security grows (player != owner) — the
// AS-IS "when a card is added to your OPPONENT'S hand/security" player-scope form. Used to prove the F1-Tier1 OnAdd
// activated bridge fires POSITIVELY on an opponent-scope gate (the existing suite only had opponent-scope negatives
// against a SELF-scope reactor). NO once-per-turn cap and NO suspend cost so the observable is the raw fire count.
//   * OnAddHand (opponent-scope, cause REQUIRED) — fires +1 when an EFFECT adds cards to the opponent's hand.
//   * OnAddSecurity (opponent-scope, NO cause) — fires +1 PER card added to the opponent's security (per-card).
// Inert in actual play (no such card exists).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnAddCounterOpp : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAddHand)
        {
            // AS-IS CanTriggerWhenAddHand player-scope OPPONENT form: subject owner != card.Owner, effect-driven
            // (cause != null — the AS-IS cardEffect != null idiom).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenAddHand(ctx, card,
                    playerCondition: player => player != card.Owner,
                    cardEffectCondition: cause => cause is not null);
            Add(effects, card, EffectTiming.OnAddHand, CanUse,
                "[All Turns] When an effect adds cards to your OPPONENT'S hand, gain 1 memory (uncapped).");
        }

        if (timing == EffectTiming.OnAddSecurity)
        {
            // AS-IS CanTriggerWhenAddSecurity player-scope OPPONENT form: subject owner != card.Owner. Per-card.
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenAddSecurity(ctx, card, player => player != card.Owner);
            Add(effects, card, EffectTiming.OnAddSecurity, CanUse,
                "[All Turns] When a card is added to your OPPONENT'S security, gain 1 memory (uncapped).");
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
