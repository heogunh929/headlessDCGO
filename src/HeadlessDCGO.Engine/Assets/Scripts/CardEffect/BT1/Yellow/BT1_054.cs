// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_054.cs
// AS-IS: [When Attacking] ActivateClass on EffectTiming.OnAllyAttack.
//   CanUseCondition   = CanTriggerOnAttack(hashtable, card)                         [ported: exists]
//   CanActivateCondition = IsExistOnBattleArea(card)                                [ported: exists]
//                          && HasMatchConditionPermanent(CanSelectPermanentCondition) [ported: exists, and
//                             subsumed by SelectAndBuffDpEffect's own no-op-on-empty selection, see BT1_055]
//                          && card.Owner.MemoryForPlayer >= 3                        [STOP: see below]
//   CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) [ported: exists,
//                             as CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)]
//   ActivateCoroutine = SelectPermanentEffect(Mode.Custom, maxCount=Min(1,count)) -> ChangeDigimonDP(-2000,
//                             EffectDuration.UntilEachTurnEnd)                        [ported: exists, as
//                             CardEffectFactory.SelectAndBuffDpEffect(card, canTarget, maxCount:1,
//                             changeValue:-2000, duration:UntilEachTurnEnd) — same shape BT1_055/BT1_062 use]
//
// STOP: `card.Owner.MemoryForPlayer >= 3` (does the card owner currently hold 3+ memory) has no headless
// query primitive. Confirmed gap — docs/audit/porting_translation_cheatsheet.md section 2 (line 45) and
// section 4 (line 66) both flag `MemoryForPlayer >= N` as a "Primitive gap (confirmed) — no query commons
// that reads memory value as a condition", explicitly reserved for strong-model pre-development (not
// per-card invention/STOP-bridging). `EngineContext.MemoryController.Current.Current` exists but is a
// SHARED, TURN-PLAYER-RELATIVE signed value (see AceOverflowGate / CanActivateBlitz doc comments), not a
// per-player "MemoryForPlayer" query commons — substituting it directly would be inventing an unaudited
// primitive and risks silently diverging from AS-IS semantics outside the OnAllyAttack window. Per the
// fidelity rule (mirror AS-IS predicates as-is, no loosening/omission), this card's single effect cannot be
// registered without dropping that precondition, so the effect is left unregistered pending the memory-value
// query primitive (strong-model backlog).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_054 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [When Attacking] gate requires `card.Owner.MemoryForPlayer >= 3` — no headless memory-value
        // query primitive exists (confirmed gap, docs/audit/porting_translation_cheatsheet.md:45,66).
        // Left unregistered rather than dropping/approximating the precondition.

        return cardEffects;
    }
}
