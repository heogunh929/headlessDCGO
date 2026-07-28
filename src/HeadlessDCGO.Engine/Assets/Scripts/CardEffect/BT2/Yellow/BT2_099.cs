// Source: DCGO/Assets/Scripts/CardEffect/BT2/Yellow/BT2_099.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_099 (BT2/Yellow, an Option).
//   [None] Play cost reduced by the number of your yellow Tamers in the battle area (dynamic ChangeCostClass).
//   [Main] 1 of your opponent's Digimon gets -12000 DP for the turn.
// FIDELITY NOTE: the PREVIOUS pass had DROPPED the entire `EffectTiming.None` play-cost-reduction block
// (with a comment claiming `ChangeCostClass`/dynamic-amount registration was unavailable) — this is WRONG,
// `ChangeCostClass`/`SetUpChangeCostClass` IS a real, already-mirrored AS-IS kind-class (see BT2_023.cs,
// same pattern) — restored below verbatim. Also replaces the prohibited old-model
// `CardEffectFactory.SelectAndBuffDpEffect(...)` call on OptionSkill with the literal AS-IS inline
// `new ActivateClass()` structure + `GManager.instance.GetComponent<SelectPermanentEffect>()` (Mode.Custom)
// selection pattern (bridge W4).
// Substrate translations: `card.Owner.HandCards.Contains(card)` -> `CardEffectCommons.IsExistOnHand(card)`;
// `card.Owner.GetBattleAreaPermanents().Count(permanent => permanent.TopCard.CardColors.Contains(CardColor.
// Yellow) && permanent.IsTamer)` -> `new Player(card.Context, card.Owner).GetBattleAreaPermanents()
// .Count(permanent => permanent.TopCard.CardColors.Contains("Yellow") && permanent.IsTamer)` (Player
// reconstruction + `CardColor.Yellow` (AS-IS enum) -> `"Yellow"` string idiom; `Permanent`/`CardSource.
// CardColors` are real mirror members, used directly); AS-IS `Func<Permanent,bool> CanSelectPermanentCondition`
// (Option select target) -> the established entity-id predicate idiom.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_099 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            int count()
            {
                return new Player(card.Context, card.Owner).GetBattleAreaPermanents().Count((permanent) => permanent.TopCard.CardColors.Contains("Yellow") && permanent.IsTamer);
            }

            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect($"Play Cost -", CanUseCondition, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => false);

            cardEffects.Add(changeCostClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (count() >= 1)
                    {
                        changeCostClass.SetEffectName($"Play Cost -{count()}");

                        return true;
                    }
                }

                return false;
            }



            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (CardSourceCondition(cardSource))
                {
                    if (RootCondition(root))
                    {
                        if (PermanentsCondition(targetPermanents))
                        {
                            Cost -= count();
                        }
                    }
                }

                return Cost;
            }

            bool PermanentsCondition(List<Permanent> targetPermanents)
            {
                return true;
            }

            bool CardSourceCondition(CardSource cardSource)
            {
                return cardSource == card;
            }

            bool RootCondition(SelectCardEffect.Root root)
            {
                return true;
            }

            bool isUpDown()
            {
                return true;
            }
        }

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] 1 of your opponent's Digimon gets -12000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, permanent.InstanceId);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP -12000.", "The opponent is selecting 1 Digimon that will get DP -12000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: -12000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
