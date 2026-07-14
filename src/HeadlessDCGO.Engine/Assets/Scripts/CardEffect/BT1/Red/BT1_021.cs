// R6-A CUTOVER STOP (design item RD-R6-02): kept in old-model ActivatedEffect. The AS-IS ActivateCoroutine adds a
// player-scope end-of-turn "-3 memory" via `card.Owner.UntilEachTurnEndEffects.Add(GetCardEffect)` — a live
// Player-field list that has NO mirror surface (Player.UntilEachTurnEndEffects does not exist as a settable list),
// and routing it through AddEffectToPlayer would break the AS-IS list-stacking semantics (attack twice = -6). Same
// new-model player-scope EoT grant-store gap as BT1_090 (RD-P6C3-C1). Left as ActivatedEffect +
// MemoryGainThenScheduledReversalBody (unique-id stacking-safe reversal). Engine-file change is Opus-gated.
// 1:1 mirror of the original BT1_021 (BT1/Red) — a Digimon.
//   [When Attacking] Gain 3 memory. At end of turn lose 3 memory.
// AS-IS (BT1_021.cs): ONE ActivateClass on OnAllyAttack — CanUseCondition = CanTriggerOnAttack (:22-25),
// CanActivateCondition = IsExistOnBattleArea (:27-30), ORDER=-1, ISOPTIONAL=false. The ActivateCoroutine
// (:32-49) gains +3 THEN registers the "-3 at end of turn" as a one-shot player effect
// (card.Owner.UntilEachTurnEndEffects.Add(GetCardEffect) returning EoTLose3Memory at OnEndTurn) — the loss
// exists ONLY per activation (attack twice = two -3 entries; never attack = no loss). A previous headless
// version statically declared the OnEndTurn loss on the card itself, which fired EVERY turn end while the
// card was in play regardless of attacking (C-5 adversarial review drive-by P2-9) — corrected to the
// activation-registered shape via MemoryGainThenScheduledReversalBody (the BT1_090 pattern: gain now +
// AddEffectToPlayer(UntilEachTurnEnd, OnEndTurn) one-shot reversal, exactly the AS-IS coroutine order).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_021 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                // AS-IS CanUseCondition (:22-25).
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                // AS-IS CanActivateCondition (:27-30).
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card),
                body: new MemoryGainThenScheduledReversalBody(3),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Attacking] Gain 3 memory. At end of turn lose 3 memory."));
        }

        return cardEffects;
    }
}
