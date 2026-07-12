// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnLoseSecurity reactor: whenever a card is removed
// from its OWNER'S security stack, the owner gains 1 memory — with NO once-per-turn cap. This is the witness the
// real BT15_037 / BT24_018 cannot be (both are [Once Per Turn], so their cap ALONE collapses N fires to one,
// hiding whether the F1-M1 P1-1 security-loss batch-collapse is doing anything). Uncapped, the observable splits:
//   * an effect trashing N security cards in ONE batch (one shared security-loss id) gains +1 IFF the collapse
//     fires the reactor once — +N if the bridge fired per CardMoved.
//   * an attack security CHECK of N cards (per-card, unstamped reveals, each in its own per-iteration window)
//     gains +N — proving the collapse does NOT wrongly merge the per-card check path.
// Self-scope (player == card.Owner), mirroring BT15_037's OnLoseSecurity player gate. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnLoseSecurityCounter : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnLoseSecurity)
        {
            // AS-IS CanUseCondition mirror: IsExistOnBattleArea && CanTriggerWhenLoseSecurity(player == card.Owner).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenLoseSecurity(ctx, card, player => player == card.Owner);

            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnLoseSecurity,
                canUse: CanUse,
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card),
                body: new MemoryBody(1),
                maxCountPerTurn: null,   // UNCAPPED — the whole point: no cap to mask a per-event over-fire.
                isOptional: false,
                description: "[All Turns] When a card is removed from your security stack, gain 1 memory (uncapped)."));
        }

        return effects;
    }
}
