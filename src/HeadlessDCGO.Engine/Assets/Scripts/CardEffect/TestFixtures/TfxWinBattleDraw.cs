// TEST FIXTURE (not a real card). "[End of Battle] if this card's permanent won the battle, draw 1" — a
// TRIGGERED ACTIVATED effect at OnEndBattle gated by CanTriggerWhenWinBattle. Exercises the EVENT-BROADCAST
// bridge for OnEndBattle: the reacting card must read the driving event's "event.winnerIds" metadata, which
// only reaches it if OnEndBattle broadcasts the driving event (was previously a boundary timing that
// discarded it). Same shape/gap-class as ST4_11.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxWinBattleDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndBattle)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndBattle,
                canUse: ctx => CardEffectCommons.CanTriggerWhenWinBattle(ctx, card),
                canActivate: null,
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[End of Battle] if this card's permanent won the battle, draw 1."));
        }

        return effects;
    }
}
