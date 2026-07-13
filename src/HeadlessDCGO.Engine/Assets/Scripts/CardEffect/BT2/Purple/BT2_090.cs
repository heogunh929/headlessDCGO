// Source: DCGO/Assets/Scripts/CardEffect/BT2/Purple/BT2_090.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_090 (BT2/Purple Tamer).
//   [Start of Your Turn] Set memory to 3.
//   [On Play] Return 1 purple Digimon card or purple Option card from your trash to your hand.
//   [Security] Play this card without paying its cost.
// OnStartTurn/SecuritySkill blocks already used the REAL AS-IS `CardEffectFactory.SetMemoryTo3TamerEffect`/
// `PlaySelfTamerSecurityEffect` calls (verified present in DCGO CardEffectFactory) — unchanged. Replaces the
// PREVIOUS pass's old-model `CardEffectFactory.SelectAndAddToHandFromZoneEffect(...)` call on OnEnterFieldAnyone
// (an invented helper — explicitly prohibited/retired) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectCardEffect>()` + full AS-IS `SetUp(...)` + `await ...Activate()`
// pattern (bridge, see BT1_011.cs).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `isExistOnField(card)` (inherited static CEntity_Effect helper, used unqualified exactly as AS-IS);
// `card.Owner.TrashCards.Count(CanSelectCardCondition)` (AS-IS `Player` field) -> mirror zone-state read +
// per-id CardSource reconstruction (`((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner,
// ChoiceZone.Trash).Count(id => CanSelectCardCondition(new CardSource(...)))`, same idiom as BT1_011/BT1_010);
// `CardColor.Purple` (AS-IS enum) -> `"Purple"` (mirror `CardSource.HasCardColor(string)` idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
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
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 card from trash to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Return 1 purple Digimon card or purple Option card from your trash to your hand.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.IsOption || cardSource.IsDigimon)
                    {
                        if (cardSource.Owner == card.Owner)
                        {
                            if (cardSource.HasCardColor("Purple"))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (isExistOnField(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        int maxCount = Math.Min(1, ((IZoneStateReader)card.Context.ZoneMover)
                            .GetCards(card.Owner, ChoiceZone.Trash)
                            .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner))));

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to add to your hand.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.AddHand,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        await selectCardEffect.Activate();
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
