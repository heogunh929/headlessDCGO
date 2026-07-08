// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_046.cs — a Digimon (continuous, timing None).
// 1:1 mirror of the original BT3_046.
//   Your opponent can't gain memory from effects other than Tamer effects.
//   AS-IS: CannotAddMemoryClass on EffectTiming.None. CanUseCondition = IsExistOnBattleArea(card) (active
//   guard). PlayerCondition(player) = player == card.Owner.Enemy (scope = the opponent). CardEffectCondition
//   (cardEffect) = cardEffect.EffectSourceCard != null && !cardEffect.IsTamerEffect (block every CAUSING
//   effect whose source card exists and is NOT a Tamer effect — i.e. Tamer-sourced memory gains still go
//   through).
// Headless mirror: CardEffectFactory.CanNotAddMemoryStaticEffect(scopePlayer: CardEffectCommons.OpponentOf
// (card), isInheritedEffect:false, card, condition:IsExistOnBattleArea, causingEffectPredicate: cs =>
// !cs.IsTamer) — the causingEffectPredicate parameter is the 1:1 mirror of AS-IS CardEffectCondition (only
// invoked with a non-null causing CardSource, folding the AS-IS null-check).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_046 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition() => CardEffectCommons.IsExistOnBattleArea(card);

            bool CausingEffectCondition(CardSource sourceCard) => !sourceCard.IsTamer;

            cardEffects.Add(CardEffectFactory.CanNotAddMemoryStaticEffect(
                scopePlayer: CardEffectCommons.OpponentOf(card),
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                causingEffectPredicate: CausingEffectCondition));
        }

        return cardEffects;
    }
}
