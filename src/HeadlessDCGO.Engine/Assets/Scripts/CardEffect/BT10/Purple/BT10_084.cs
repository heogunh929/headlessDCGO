// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — BT10_084 (Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT10/Purple/BT10_084.cs (222 lines, 2 regions)
//    * [On Play]            :17-128 (timing == OnEnterFieldAnyone — 트래시에서 최대 2장 무상 플레이 +
//      Blocker 부여; AS-IS 자체가 EffectDiscription과 실제 CanSelectCardCondition 본문이 불일치 — Lv4 이하
//      + [BagraArmy] 트레잇 조건이 실제 로직, 텍스트는 다른 카드군 상속 — AS-IS 원본 버그/오기 그대로 보존).
//    * [Opponent's Turn]    :134-216 (timing == WhenWouldDigivolutionCardDiscarded — 다른 자기 디지몬 진화원이
//      트래시될 상황을 자기 진화원으로 대신 트래시)
//
// ② 프리미티브 매핑:
//    * E:SelectCardEffect Mode.Custom/Root.Trash + P:PlayPermanentCards + P:GainBlocker — [On Play]
//      (AS-IS :73-125; BT19_091/ST20_15 established idiom).
//    * P:CanTriggerOnTrashDigivolutionCard(Hashtable-overload, AS-IS 시그니처 그대로) — [Opponent's Turn] CanUse
//      게이트 (AS-IS :161-167; symbol_map row 160 OK, CardEffectCommons/CanUseEffects/OnTrashDigivolutionCard.cs:37
//      가 Hashtable 4-인자 AS-IS 시그니처를 그대로 노출).
//    * P:GetDiscardedCardsFromHashtable(Hashtable) — (AS-IS :184; symbol_map row 306 OK).
//    * E:SelectCardEffect Mode.Discard/Root.Custom(DigivolutionCards) — 자기 진화원 트래시 대체 (AS-IS :192-212).
//
// ③ 배선 관례 근거: [On Play]→OnEnterFieldAnyone+CanTriggerOnPlay 그대로. [Opponent's Turn]은 AS-IS 자체가
//    전용 타이밍(WhenWouldDigivolutionCardDiscarded), 방언 변환 대상 아님.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`/`yield return
//      ContinuousController.instance.StartCoroutine(X)`→`await X` (BT8_092/BT19_091 idiom).
//    * `card.Owner.TrashCards` → `new Player(card.Context, card.Owner).TrashCards` (symbol_map_guide §2.2).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (cardSources/
//      DigivolutionCards 접근에 가변 Permanent 필요; BT17_026 idiom). `Permanent.cardSources`는 미러에 1:1
//      존재(Permanent.cs "P6 stage A" — TopCard+DigivolutionCards+LinkedCards 합성 getter).
//    * `selectedCard.PermanentOfThisCard()` → 동일하게 `ICardEffect.ResolvePermanentOfThisCard(selectedCard)`.
//    * `permanent.DigivolutionCards`(customRootCardList용) → `.ToList()` (§2.5, IReadOnlyList→List 변환;
//      BT5_086:210 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT10.Purple;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT10_084 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play Digimon from trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Play 1 purple or yellow Digimon card with 6000 DP or less from your trash without paying its memory cost. If you have 1 or fewer security cards, you may play 1 level 6 or lower Digimon card with [Angel] or [Fallen Angel] in its traits from your trash without paying its memory cost instead.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    if (cardSource.IsDigimon)
                    {
                        if (cardSource.Level <= 4 && cardSource.HasLevel)
                        {
                            if (cardSource.CardTraits.Contains("BagraArmy"))
                            {
                                return true;
                            }

                            if (cardSource.CardTraits.Contains("Bagra Army"))
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
                if (CardEffectCommons.IsExistOnBattleArea(card))
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
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    int maxCount = Math.Min(2, new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                message: "Select cards to play.",
                                maxCount: maxCount,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                mode: SelectCardEffect.Mode.Custom,
                                root: SelectCardEffect.Root.Trash,
                                customRootCardList: null,
                                canLookReverseCard: true,
                                selectPlayer: card.Owner,
                                cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select cards to play.", "The opponent is selecting cards to play.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectCardEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    await CardEffectCommons.PlayPermanentCards(
                                cardSources: selectedCards,
                                activateClass: activateClass,
                                payCost: false,
                                isTapped: false,
                                root: SelectCardEffect.Root.Trash,
                                activateETB: true);

                    foreach (CardSource selectedCard in selectedCards)
                    {
                        await CardEffectCommons.GainBlocker(
                                                targetPermanent: ICardEffect.ResolvePermanentOfThisCard(selectedCard),
                                                effectDuration: EffectDuration.UntilOpponentTurnEnd,
                                                activateClass: activateClass);
                    }
                }
            }
        }

        #endregion

        #region Opponents Turn

        if (timing == EffectTiming.WhenWouldDigivolutionCardDiscarded)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash digivolution cards instead", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Opponent's Turn] When an effect would trash one of your other Digimon's digivolution cards, you may trash this Digimon's digivolution cards instead.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent != ICardEffect.ResolvePermanentOfThisCard(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerOnTrashDigivolutionCard(hashtable, PermanentCondition, cardEffect => true, cardSource => true))
                {
                    return true;
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (!CardEffectCommons.IsOwnerTurn(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<CardSource> discardedCards = CardEffectCommons.GetDiscardedCardsFromHashtable(_hashtable);

                if (discardedCards.Count > 0)
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).cardSources.Count >= discardedCards.Count)
                    {
                        discardedCards.ForEach(source => source.willBeRemoveSources = false);

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: (CardSource) => true,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: null,
                            message: "Select card(s) to trash.",
                            maxCount: discardedCards.Count,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Discard,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        await selectCardEffect.Activate();
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
