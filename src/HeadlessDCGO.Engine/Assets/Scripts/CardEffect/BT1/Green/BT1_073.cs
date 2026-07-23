// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_073.cs
// 1:1 mirror of the original BT1_073: inherited, [Your Turn] this Digimon gets DP +1000 for every
// opponent's Digimon that is suspended.
//   -> CardEffectFactory.ChangeSelfDPStaticEffect(Func<int>: () => 1000 * count(), isInheritedEffect: true,
//      card, condition = IsOwnerTurn && count() >= 1), count = opponent battle-area suspended Digimon.
// The AS-IS uses ChangeSelfDPStaticEffect<Func<int>>; the DP dynamic-value factory overload is now present
// (mirrors the existing ChangeSelfSAttackStaticEffect(Func<int>) overload).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_073 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            int Count() => CardEffectCommons.MatchConditionPermanentCount(card, permanent =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                permanent.IsSuspended);

            bool Condition() => CardEffectCommons.IsOwnerTurn(card) && Count() >= 1;

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: () => 1000 * Count(),
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
