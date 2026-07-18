// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2A 정본 카드 — Omnimon (BT5_086, Digimon / White / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT5/White/BT5_086.cs (176 lines, 3 regions)
//    * [Blitz] ESS/self               :14-17  (AS-IS timing == OnEnterFieldAnyone, BlitzSelfEffect
//                                              isWhenDigivolving:true)
//    * [When Digivolving] Unsuspend   :19-55  (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving
//                                              gate; 미러 방언 = WhenDigivolving 전용 키 — IUnsuspendPermanents)
//    * [All Turns] Prevent-removal    :57-172 (AS-IS timing == WhenPermanentWouldBeDeleted ||
//                                              WhenReturntoHandAnyone || WhenReturntoLibraryAnyone)
//
// ② 프리미티브 매핑 (감사 축 이름):
//    * K:Blitz (BlitzSelfEffect)             — [Blitz] self ESS (AS-IS :16; Blitz.cs:19)
//    * T:WhenReturntoHandAnyone              — [All Turns] 트리거 키 (AS-IS :57)
//    * T:WhenReturntoLibraryAnyone           — [All Turns] 트리거 키 (AS-IS :57)
//    * (+T:WhenPermanentWouldBeDeleted       — [All Turns] 3-way OR의 첫 키 (AS-IS :57))
//    * [When Digivolving] unsuspend (IUnsuspendPermanents) — [WD] 몸통 (AS-IS :49-54)
//    * [All Turns] prevent-removal-by-trashing-Lv6 (ITrashDigivolutionCards + willBeRemoveField /
//      HideDeleteEffect / HideHandBounceEffect / HideDeckBounceEffect + SelectCardEffect Mode.Custom /
//      Root.Custom) — AS-IS :118-171
//    * CanNotTrashFromDigivolutionCards guard — CanSelectCardCondition (AS-IS :76)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone :19에
//      두지만 미러 DigivolveAction은 WhenDigivolving만 해소, 이중-키 등록 금지 — rule 3). CanUseCondition의
//      CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :31-34 그대로 유지.
//    * [Blitz] self ESS는 AS-IS 그대로 OnEnterFieldAnyone(BlitzSelfEffect 팩토리 1:1).
//    * [All Turns] prevent-removal은 AS-IS 3-way 타이밍(WhenPermanentWouldBeDeleted || WhenReturntoHandAnyone
//      || WhenReturntoLibraryAnyone) 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      `yield return StartCoroutine(X)`→`await X` (BT17_026 idiom).
//    * SelectCardCoroutine IEnumerator(`yield return null`) → `Task SelectCardCoroutine(CardSource cs){
//      selectedCards.Add(cs); return Task.CompletedTask; }` (선언 위치는 AS-IS :153-157 그대로 유지).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` — 미러 CardSource.
//      PermanentOfThisCard()는 PermanentView 반환이라 실제 Permanent 표면(DigivolutionCards/willBeRemoveField)
//      접근에는 Resolve* 브릿지 필요(BT17_026 idiom).
//    * `new IUnsuspendPermanents(list, activateClass).Unsuspend()` — 미러 ctor(CardController.cs:1931)는
//      (List<Permanent>, ICardEffect?) 그대로; cause id는 cardEffect에서 파생.
//    * `new ITrashDigivolutionCards(selectedPermanent, selectedCards, activateClass).TrashDigivolutionCards()`
//      → 미러 ctor(CardController.cs:1123)는 (permanent, cards, causeEffectSourceId, cardEffect) — 확립 관례상
//      `activateClass.EffectSourceCard?.InstanceId` + `activateClass` 삽입.
//    * `customRootCardList: selectedPermanent.DigivolutionCards` — 미러 DigivolutionCards는 IReadOnlyList이므로
//      `.ToList()` (BT17_026 :563 idiom); SetUp은 List<CardSource>? 요구.
//    * `card.Owner.Enemy` (술어 내 owner 비교) → `new Player(card.Context, card.Owner).Enemy?.PlayerId`
//      (BT8_057 확립 idiom; 미러 CardSource.Owner는 bare HeadlessPlayerId).
//    * `HideDeleteEffect()`/`HideHandBounceEffect()`/`HideDeckBounceEffect()` = UI 연출 — 미러 확립 관례상
//      스트립(Decoy.cs/Evade.cs 동일; 미러 Permanent에 해당 메서드 없음). 로드-베어링 `willBeRemoveField = false`는
//      유지(sweep survivor-fix가 읽는 실효 플래그).
//    * `selectCardEffect.SetNotShowCard()` — 미러 SelectCardEffect.cs:340에 존재(UI-only)하여 호출 위치 유지.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT5.White;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT5_086 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Blitz

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(CardEffectFactory.BlitzSelfEffect(isInheritedEffect: false, card: card, condition: null, isWhenDigivolving: true));
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:19)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanUnsuspend(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
            }
        }

        #endregion

        #region All Turns - Prevent removal

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted || timing == EffectTiming.WhenReturntoHandAnyone || timing == EffectTiming.WhenReturntoLibraryAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Prevent this Digimon from being deleted or returned to hand or deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetHashString("Substitute_BT5_086");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] If an opponent's effect would delete this Digimon or return it to its owner's hand or deck, you may prevent it from leaving play by trashing a level 6 Digimon card in this card's digivolution cards.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.Level == 6)
                    {
                        if (!cardSource.CanNotTrashFromDigivolutionCards(activateClass))
                        {
                            if (cardSource.HasLevel)
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
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card))
                    {
                        if (CardEffectCommons.IsByEffect(hashtable, cardEffect => cardEffect.EffectSourceCard != null && cardEffect.EffectSourceCard.Owner == new Player(card.Context, card.Owner).Enemy?.PlayerId))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                    if (selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        List<CardSource> selectedCards = new List<CardSource>();

                        int maxCount = Math.Min(1, selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition));

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                                    canTargetCondition: CanSelectCardCondition,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    canNoSelect: () => true,
                                    selectCardCoroutine: SelectCardCoroutine,
                                    afterSelectCardCoroutine: null,
                                    message: "Select 1 card to discard.",
                                    maxCount: maxCount,
                                    canEndNotMax: false,
                                    isShowOpponent: true,
                                    mode: SelectCardEffect.Mode.Custom,
                                    root: SelectCardEffect.Root.Custom,
                                    customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                                    canLookReverseCard: true,
                                    selectPlayer: card.Owner,
                                    cardEffect: activateClass);

                        selectCardEffect.SetNotShowCard();
                        await selectCardEffect.Activate();

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);
                            return Task.CompletedTask;
                        }

                        if (selectedCards.Count == 1)
                        {
                            selectedPermanent.willBeRemoveField = false;

                            // AS-IS :163-165 HideDeleteEffect()/HideHandBounceEffect()/HideDeckBounceEffect() = UI 연출 —
                            // 미러 확립 관례상 스트립(Decoy.cs/Evade.cs 동일; 미러 Permanent에 해당 메서드 없음).
                            // 로드-베어링 willBeRemoveField=false만 유지(sweep survivor-fix가 읽는 실효 플래그).

                            await new ITrashDigivolutionCards(selectedPermanent, selectedCards, activateClass.EffectSourceCard?.InstanceId, activateClass).TrashDigivolutionCards();
                        }
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
