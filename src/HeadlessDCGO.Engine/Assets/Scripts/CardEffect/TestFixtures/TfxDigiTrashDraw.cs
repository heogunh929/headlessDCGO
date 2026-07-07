// TEST FIXTURE (not a real card). "When one of your opponent's Digimon's digivolution cards is trashed,
// draw 1" — a TRIGGERED ACTIVATED effect at OnDigivolutionCardDiscarded, gated like the AS-IS BT2_085
// (CanTriggerOnTrashDigivolutionCard + opponent-host permanentCondition). Exercises the EVENT-BROADCAST
// bridge: the listener is NOT the event subject (the host Digimon is), so it only fires if the bridge
// scans the field and threads the driving event (subject + discardedCardIds) into the gate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDigiTrashDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDigivolutionCardDiscarded)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDigivolutionCardDiscarded,
                canUse: ctx => CardEffectCommons.CanTriggerOnTrashDigivolutionCard(
                    ctx, card,
                    permanentCondition: permanent => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card),
                    cardEffectSourceCondition: null,
                    cardCondition: null),
                canActivate: null,
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "When one of your opponent's Digimon's digivolution cards is trashed, draw 1."));
        }

        return effects;
    }
}
