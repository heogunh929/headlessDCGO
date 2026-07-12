// TEST FIXTURE (not a real card). A GATE-LESS OnEndAttack memory probe: CanUse checks ONLY the self/attacker
// membership (CanTriggerOnAttack — subject-based, zone-INDEPENDENT) and CanActivate is unconditional (NO
// IsExistOnBattleArea gate). This isolates the emit-time attacker-alive guard in AttackPipeline.AdvanceEndAttackAsync:
//   * WITH the guard: a self-deleted / battle-knocked-out attacker (now in the trash) produces NO OnEndAttack event,
//     so this reactor stays silent (+0).
//   * WITHOUT the guard: the queued event would still be published, ScanZones visits the trash, and this gate-less
//     reactor self-gates true (SubjectPermanentContains: TriggerEntityId == card) and OVER-fires (+1).
// The ordinary TfxOnEndAttackCounter carries an IsExistOnBattleArea CanActivate gate, so it can NOT detect a broken
// emit guard (its own gate rejects a trashed attacker regardless) — hence this gate-less sibling. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnEndAttackCounterNoGate : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndAttack)
        {
            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card), // self/attacker membership ONLY, zone-independent
                canActivate: () => true,                                        // NO IsExistOnBattleArea gate
                body: new MemoryBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[test] gate-less OnEndAttack memory probe (isolates the emit-time attacker-alive guard)."));
        }

        return effects;
    }
}
