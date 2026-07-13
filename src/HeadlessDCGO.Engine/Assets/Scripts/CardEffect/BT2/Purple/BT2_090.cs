// 1:1 mirror of BT2_090 (BT2/Purple Tamer).
//   [Start of Your Turn] Set memory to 3.
//   [On Play] Return 1 purple Digimon card or purple Option card from your trash to your hand.
//   [Security] Play this card without paying its cost.
//   AS-IS: SetMemoryTo3TamerEffect on OnStartTurn; ActivateClass on OnEnterFieldAnyone
//   (CanUseCondition=CanTriggerOnPlay, CanActivateCondition=isExistOnField&&HasMatchConditionOwnersCardInTrash,
//   CanSelectCardCondition=(IsOption||IsDigimon)&&Owner==card.Owner&&HasCardColor(Purple),
//   mode=AddHand, root=Trash, maxCount=1, canEndNotMax=false); PlaySelfTamerSecurityEffect on SecuritySkill.
//   Headless mirror: same structural mapping as BT1_011 (SelectAndAddToHandFromZoneEffect for OnEnterFieldAnyone);
//   CanTriggerOnPlay / IsExistOnBattleArea structurally satisfied by PlayCardAction path.
//   CanSelectCardCondition: candidate.IsOption||candidate.IsDigimon via real CardSource accessors,
//   candidate.HasCardColor("Purple") from section-9 real accessor. Owner scope covered by OwnersCard trash zone.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_090 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return (candidate.IsOption || candidate.IsDigimon) && candidate.HasCardColor("Purple");
            }

            cardEffects.Add(CardEffectFactory.SelectAndAddToHandFromZoneEffect(
                card,
                fromZone: ChoiceZone.Trash,
                canTarget: CanSelectCardCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[On Play] Return 1 purple Digimon card or purple Option card from your trash to your hand."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}