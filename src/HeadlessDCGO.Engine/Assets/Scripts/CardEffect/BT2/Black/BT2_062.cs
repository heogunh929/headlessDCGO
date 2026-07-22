// Source: DCGO/Assets/Scripts/CardEffect/BT2/Black/BT2_062.cs — 1:1 headless mirror (W2-LevelEvoCost witness).
// Diaboromon (BT2_062, Digimon / Black). SINGLE [None]-timing static: while it is your turn and this Digimon is
// on the battle area, a [Diaboromon] Digimon card in your hand costs 1 less to digivolve into this Digimon.
//   The prior STOP ("ChangeDigivolutionCostStaticEffect 헤드리스 시그니처에 cardCondition/rootCondition 인자가
//   없음") is RETIRED: the ported CardEffectFactory.ChangeDigivolutionCostStaticEffect<T> (Script/CardEffectFactory/
//   ChangeDigivolutionCost.cs) now carries the full AS-IS parameter set — cardCondition (Diaboromon·Digimon·in-hand)
//   and rootCondition (Root.Hand) — so the "Diaboromon 카드 한정" gates port 1:1 with no forced mapping.
//   READ side (not-inert): the returned ChangeCostClass (IsChangePayingCost()==true) is folded by
//   CardSource.GetChangedPayingCost (CardSource.cs:1321) over the moving card's own None-timing EffectList while it
//   is not yet a permanent — the exact seat DigivolveAction.GetPayingCostWithBaseCost consults (DigivolveAction.cs:604).
// Substrate translations only: `targetPermanent == card.PermanentOfThisCard()` -> `targetPermanent ==
//   ICardEffect.ResolvePermanentOfThisCard(card)` (the Permanent == operator, BT9_013/BT25_004 idiom);
//   `cardSource.Owner.HandCards.Contains(cardSource)` -> `new Player(card.Context, cardSource.Owner).HandCards
//   .Contains(cardSource)` (BT20_025 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_062 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsOwnerTurn(card) && CardEffectCommons.IsExistOnBattleArea(card);
            }

            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent == ICardEffect.ResolvePermanentOfThisCard(card);
            }

            bool CardSourceCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon && cardSource.CardNames.Contains("Diaboromon") && new Player(card.Context, cardSource.Owner).HandCards.Contains(cardSource);
            }

            bool RootCondition(SelectCardEffect.Root root)
            {
                return root == SelectCardEffect.Root.Hand;
            }

            cardEffects.Add(CardEffectFactory.ChangeDigivolutionCostStaticEffect<int>(
                changeValue: -1,
                permanentCondition: PermanentCondition,
                cardCondition: CardSourceCondition,
                rootCondition: RootCondition,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                setFixedCost: false));
        }

        return cardEffects;
    }
}
