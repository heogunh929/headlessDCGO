// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EX4_020 (Digimon / Blue) — DigiXros / Material Save ("GreyKnightsmon" line)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX4/Blue/EX4_020.cs (327 lines, no #region markers, 4 timing 블록)
//    * [On Play]                  :14-157  (OnEnterFieldAnyone — 자신 <Rush>(UntilEachTurnEnd) → DigiXros 시
//      상대 디지몬 1체의 진화원 최대 2장 트래시)
//    * MaterialSave               :159-162 (WhenPermanentWouldBeDeleted — CardEffectFactory.MaterialSaveEffect(2))
//    * DigiXros 조건 등록          :164-237 (None — AddDigiXrosConditionClass: Greymon(Blue) + MailBirdramon, 2재료)
//    * [When Attacking]           :239-322 (OnAllyAttack, ESS/SetIsInheritedEffect — [GreyKnightsmon]이면 진화원
//      3장 이하 상대 디지몬 1체 CanNotAttack(UntilOpponentTurnEnd))
//
// ② 검증 프리미티브: GainRush(async Task), IsDijiXros(hashtable form), HasMatchConditionPermanent(card,Permanent-pred) /
//    MatchConditionPermanentCount(card,Id-pred), SelectPermanentEffect.SetUp(full/id form), SelectCardEffect.SetUp,
//    ITrashDigivolutionCards.TrashDigivolutionCards(), CanNotTrashFromDigivolutionCards(activateClass),
//    MaterialSaveEffect, AddDigiXrosConditionClass/SetUpAddDigiXrosConditionClass/SetNotShowUI, DigiXrosCondition/
//    DigiXrosConditionElement, CardNames_DigiXros, GainCanNotAttack(async Task), CanNotBeAffected, HasNoElement.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)`/`yield return
//      StartCoroutine(X)`→`await X`; 중첩 IEnumerator local func(SelectPermanentCoroutine/SelectCardCoroutine)→
//      async Task / Task; lone `yield return null`→`return Task.CompletedTask`(BT9_111 idiom).
//    * `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092 관례).
//    * SelectPermanentEffect canTargetCondition id-adapter — PermanentOf(id)+CanSelectPermanentById; AS-IS
//      card-less `MatchConditionPermanentCount(pred)`→미러 id-form `(card, CanSelectPermanentById)`,
//      card-less `HasMatchConditionPermanent(pred)`→미러 permanent-form `(card, CanSelectPermanentCondition)`
//      (LM_054 / BT21_059 idiom).
//    * `HasCardColor(CardColor.Blue)`→`HasCardColor("Blue")` (BT2_044 헤더).
//    * `selectedPermanent.DigivolutionCards`(IReadOnlyList)→`.ToList()` for customRootCardList (BT9_111 idiom).
//    * `new ITrashDigivolutionCards(perm, cards, activateClass)`→미러 ctor(perm, cards, causeEffectSourceId,
//      cardEffect): `activateClass.EffectSourceCard?.InstanceId` 삽입(BT5_086 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX4.Blue;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX4_020 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("This Digimon gains Rush and trash digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] This Digimon gains <Rush> for the turn. (This Digimon may attack the turn it was played.) If DigiXrosing, trash up to 2 digivolution cards of 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return !cardSource.CanNotTrashFromDigivolutionCards(activateClass);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.GainRush(
                                targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                                effectDuration: EffectDuration.UntilEachTurnEnd,
                                activateClass: activateClass);

                if (CardEffectCommons.IsDijiXros(_hashtable, card, (digixrosCount) => digixrosCount >= 1))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectPermanentById,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will trash digivolution cards.", "The opponent is selecting 1 Digimon that will trash digivolution cards.");

                        await selectPermanentEffect.Activate();

                        async Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            Permanent selectedPermanent = permanent;

                            if (selectedPermanent != null)
                            {
                                if (selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1 && !selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                                {
                                    maxCount = Math.Min(2, selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition));

                                    List<CardSource> selectedCards = new List<CardSource>();

                                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                                    selectCardEffect.SetUp(
                                                canTargetCondition: CanSelectCardCondition,
                                                canTargetCondition_ByPreSelecetedList: null,
                                                canEndSelectCondition: CanEndSelectCondition,
                                                canNoSelect: () => false,
                                                selectCardCoroutine: SelectCardCoroutine,
                                                afterSelectCardCoroutine: null,
                                                message: "Select digivolution cards to trash.",
                                                maxCount: maxCount,
                                                canEndNotMax: true,
                                                isShowOpponent: true,
                                                mode: SelectCardEffect.Mode.Custom,
                                                root: SelectCardEffect.Root.Custom,
                                                customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                                                canLookReverseCard: true,
                                                selectPlayer: card.Owner,
                                                cardEffect: activateClass);

                                    selectCardEffect.SetUpCustomMessage("Select digivolution cards to trash.", "The opponent is selecting digivolution cards to trash.");

                                    await selectCardEffect.Activate();

                                    bool CanEndSelectCondition(List<CardSource> cardSources)
                                    {
                                        if (CardEffectCommons.HasNoElement(cardSources))
                                        {
                                            return false;
                                        }

                                        return true;
                                    }

                                    Task SelectCardCoroutine(CardSource cardSource)
                                    {
                                        selectedCards.Add(cardSource);

                                        return Task.CompletedTask;
                                    }

                                    if (selectedCards.Count >= 1)
                                    {
                                        await new ITrashDigivolutionCards(
                                            selectedPermanent,
                                            selectedCards,
                                            activateClass.EffectSourceCard?.InstanceId,
                                            activateClass).TrashDigivolutionCards();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.MaterialSaveEffect(card: card, materialSaveCount: 2));
        }

        if (timing == EffectTiming.None)
        {
            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect($"DigiXros", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            cardEffects.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            DigiXrosCondition GetDigiXros(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    DigiXrosConditionElement element = new DigiXrosConditionElement(CanSelectCardCondition, "Blue Greymon");

                    bool CanSelectCardCondition(CardSource cardSource)
                    {
                        if (cardSource != null)
                        {
                            if (cardSource.Owner == card.Owner)
                            {
                                if (cardSource.IsDigimon)
                                {
                                    if (cardSource.CardNames_DigiXros.Contains("Greymon"))
                                    {
                                        if (cardSource.HasCardColor("Blue"))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    DigiXrosConditionElement element1 = new DigiXrosConditionElement(CanSelectCardCondition1, "MailBirdramon");

                    bool CanSelectCardCondition1(CardSource cardSource)
                    {
                        if (cardSource != null)
                        {
                            if (cardSource.Owner == card.Owner)
                            {
                                if (cardSource.IsDigimon)
                                {
                                    if (cardSource.CardNames_DigiXros.Contains("MailBirdramon"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }

                    List<DigiXrosConditionElement> elements = new List<DigiXrosConditionElement>() { element, element1 };

                    DigiXrosCondition digiXrosCondition = new DigiXrosCondition(elements, null, 2);

                    return digiXrosCondition;
                }

                return null;
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Opponent's 1 Digimon can't attack", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If this Digimon is [GreyKnightsmon], 1 of your opponent's Digimon with 3 or fewer digivolution cards can't attack until the end of your opponent's turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DigivolutionCards.Count <= 3)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard.CardNames.Contains("GreyKnightsmon"))
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.", "The opponent is selecting 1 Digimon that will get effects.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.GainCanNotAttack(
                            targetPermanent: permanent,
                            defenderCondition: null,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass,
                            effectName: "Can't Attack");
                    }
                }
            }
        }

        return cardEffects;
    }
}
