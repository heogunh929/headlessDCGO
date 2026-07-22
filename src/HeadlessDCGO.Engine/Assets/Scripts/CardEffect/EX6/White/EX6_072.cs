// Source: DCGO/Assets/Scripts/CardEffect/EX6/White/EX6_072.cs
// TRUE AS-IS-verbatim port (RD-S3-EX6_072 — the live WITNESS of the temp-material DNA family; the raw
// CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash helper's full interactive flow is
// load-bearing here). 1:1 mirror of the original EX6_072 (EX6/White, "Omnimon Zwart Defeat" Option), all three
// timing blocks:
//   [None]      IgnoreColorConditionClass — ignore this card's color requirements while the OPPONENT has a Lv.6+
//               battle-area Digimon (the ported kind-class, verbatim; BT9_109 wiring idiom).
//   [Main]      1 of your Lv.6 Digimon + 1 hand card may DNA-digivolve into a Lv.7 Digimon card in the hand —
//               the raw DNA-from-hand helper (co-eval → materialise → jogress → rollback). See DNADigivolveEffects.cs.
//   [Security]  Return 1 Lv.6+ Digimon card from your trash to the hand, then add this card to the hand
//               (SelectCardEffect Mode.AddHand + AddThisCardToHand).
//
// Substrate translation only (established idioms): IEnumerator→async Task, `ContinuousController.instance.
// StartCoroutine(X)`→`await X`; `card.Owner.Enemy` (Player) → `new Player(card.Context, card.Owner).Enemy`;
// `card.Owner.TrashCards` → `new Player(card.Context, card.Owner).TrashCards`; `cardSource.IsLevel6`/
// `permanent.TopCard.IsLevel6` → `HasLevel && Level == 6` (AS-IS CardSource.IsLevel6, CardSource.cs:2941);
// `CanTriggerOptionMainEffect(hashtable, card)`/`CanTriggerSecurityEffect(hashtable, card)` → the Hashtable
// overloads (OptionEffect.cs / SecurityEffect.cs); `AddThisCardToHand(card, activateClass)` → the mirror
// `(card, sourceCard)` shape (BT9_109 convention, sourceCard = the effect's source card).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX6.White;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX6_072 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Ignore Color Condition
        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);

            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                Player? enemy = new Player(card.Context, card.Owner).Enemy;
                if (enemy != null && enemy.GetBattleAreaDigimons().Count((permanent) => permanent.TopCard.HasLevel && permanent.TopCard.Level >= 6) >= 1)
                {
                    return true;
                }

                return false;
            }

            bool CardCondition(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    return true;
                }

                return false;
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DNA Digivolve", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription() => "[Main] 1 of your level 6 Digimon and 1 card in the hand may DNA digivolve into a level 7 Digimon card in the hand.";

            bool IsLevel7Card(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && cardSource.HasLevel
                    && cardSource.Level == 7;
            }

            bool IsCardInHand(CardSource cardSource)
            {
                return true;
            }

            bool IsLevel6Permanent(Permanent permanent)
            {
                return permanent.IsDigimon
                    && permanent.TopCard.HasLevel && permanent.TopCard.Level == 6;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash(
                    IsLevel7Card,
                    IsLevel6Permanent,
                    IsCardInHand,
                    true,
                    true,
                    true,
                    activateClass,
                    null);
            }
        }
        #endregion

        #region Security
        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 Digimon from trash to hand then add this card to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Return 1 level 6 or higher Digimon card from your trash to the hand. Then, add this card to the hand.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.HasLevel && cardSource.Level >= 6)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    int maxCount = Math.Min(1, new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => false,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 Digimon to add to your hand.",
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

                await CardEffectCommons.AddThisCardToHand(card, card);
            }
        }
        #endregion

        return cardEffects;
    }
}
