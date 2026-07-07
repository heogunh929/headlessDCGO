// TEST FIXTURE (not a real card). "[Your Turn] when one of your OPPONENT'S Digimon is suspended, gain 1
// memory" — a TRIGGERED ACTIVATED effect at OnTappedAnyone gated by CanTriggerWhenPermanentSuspends over an
// opponent-Digimon predicate. Exercises the EVENT-BROADCAST bridge for OnTappedAnyone: the reacting card is a
// DIFFERENT card than the suspended subject, so it only fires if OnTappedAnyone broadcasts (was previously
// subject-scoped). Same cross-card gap-class as ST4_14 (fixture uses a plain MemoryBody, no suspend cost, to
// isolate the bridge-delivery check).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOppSuspendMemory : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnTappedAnyone)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnTappedAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerWhenPermanentSuspends(
                    ctx, card, permanent => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)),
                canActivate: null,
                body: new MemoryBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Your Turn] when an opponent's Digimon is suspended, gain 1 memory."));
        }

        return effects;
    }
}
