// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnAttackTargetChanged memory probe with the ANYONE gate
// (CanTriggerOnPermanentAttackTargetSwitch(_ => true)) — reacts to ANY attacker's target switch, from a TOP permanent
// (isInheritedEffect defaults to false). Complements the inherited witnesses (EX10_002 / ST15_02) by exercising the
// TOP-instance scan of the EventBroadcast bridge. Uncapped so the observable is unmasked:
//   * one target change fires it exactly ONCE (+1) — one emit, one subject (the attacker), no batch.
//   * an inherited-only reactor as a TOP permanent stays silent (membership) while this top probe fires.
// Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnAttackTargetChangedCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnAttackTargetChanged)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAttackTargetChanged,
                canUse: ctx => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnPermanentAttackTargetSwitch(ctx, card, _ => true),  // anyone
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new MemoryBody(1),
                maxCountPerTurn: null,   // UNCAPPED
                isOptional: false,
                description: "[All Turns] When any attack target is switched, gain 1 memory (uncapped)."));
        }

        return effects;
    }
}
