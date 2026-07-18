// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S5 카드 — AD1_006 (Digimon / Red, "Shoutmon X7")
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/AD1/Red/AD1_006.cs (351 lines, 6 regions)
//    * Alternative Digivolve Condition :16-26  (timing == None — AddSelfDigivolutionRequirementStaticEffect,
//      level:6)
//    * Shared OP / WD / WA             :28-100 (공유 로컬함수 — 3개 발화창이 재사용)
//    * On Play                         :103-116 (timing == OnEnterFieldAnyone + CanTriggerOnPlay)
//    * When Digivolving                :120-133 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving
//      게이트 — 미러는 방언 변환 필수, 아래 참조)
//    * When Attacking                  :137-150 (timing == OnAllyAttack + CanTriggerOnAttack)
//    * All Turns                       :154-333 (timing == WhenRemoveField — DigiXros 아닌 필드이탈 시
//      진화원 재배치+무상플레이)
//    * Digixros                        :336-345 (timing == None — DigiXrosEffectFromNames)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect, level:6) — AS-IS :24;
//      symbol_map row 10 OK, level 파라미터 포함 확립 idiom(AD1_011/P_223).
//    * E:SelectPermanentEffect Mode.PutLibraryBottom + UI SetBoolSelection/WaitForEndSelect/SelectedBoolValue
//      + P:IUnsuspendPermanents — 공유 OP/WD/WA 몸통 (AS-IS :50-98; BT1_111 established idiom for the bool
//      선택 UI, 미러 UserSelectionManager/SelectionElement<T> 실장).
//    * P:DigiXrosEffectFromNames — DigiXros (AS-IS :338-344; symbol_map row 382 OK). 사용자 사전-경고("G-Xros
//      재하우징 착지로 스테일 가능성")대로 표면 재확인 결과 실존 확인 — CardEffectFactory.cs:1450에 AS-IS와
//      완전 동일 시그니처(card, CostReduction, CanTargetCondition_ByPreSelecetedList, params names)로 이식돼
//      있음. STOP 불필요.
//    * E:SelectCardEffect Mode.Custom/Root.DigivolutionCards + P:AddDigivolutionCardsBottom + P:PlayPermanentCards
//      — All Turns 진화원 재배치+플레이 (AS-IS :199-330; BT17_026 idiom).
//
// ③ 배선 관례 근거 — 방언 변환:
//    * [When Digivolving] → AS-IS는 OnEnterFieldAnyone(CanTriggerWhenDigivolving 게이트)에 등록하지만, 미러는
//      전용 EffectTiming.WhenDigivolving 키로 등록해야 함(symbol_map_guide §2.7; DigivolveAction이
//      WhenDigivolving만 해소, 이중-키 등록 금지 — STOP 가드 있음). CanTriggerWhenDigivolving(hashtable, card)
//      게이트 본문은 그대로 유지.
//    * [On Play]/[When Attacking]은 AS-IS 그대로 OnEnterFieldAnyone+CanTriggerOnPlay / OnAllyAttack+
//      CanTriggerOnAttack.
//    * [All Turns]는 AS-IS 자체가 WhenRemoveField(비-DigiXros 필드이탈) 전용 창.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`/`yield return
//      StartCoroutine(X)`→`await X`, lone `yield return null`→`Task.CompletedTask` (BT8_092 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT17_026 idiom;
//      DigivolutionCards/DP/IsSuspended 접근에 가변 Permanent 필요).
//    * `new IUnsuspendPermanents(list, activateClass).Unsuspend()` — 시그니처 불변(§2.5).
//    * `selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass)` (AS-IS, ICardEffect 인자)
//      → 미러 시그니처는 `HeadlessEntityId?` 인자 — `activateClass.EffectSourceCard?.InstanceId`로 치환
//      (Permanent.cs:3896; BT17_026:316-317 idiom).
//    * `permanent.DigivolutionCards`(customRootCardList용) → `.ToList()` (§2.5, IReadOnlyList→List; BT5_086:210
//      idiom).
//    * `DigivolutionCards.Some(...)` — 미러에 동일 확장 메서드 존재(IEnumerableExtension.cs:63) 그대로.
//    * id-어댑터(§2.3 critical rule) — `HasMatchConditionOpponentsPermanent`(CardEffectCommons.cs:4772)와
//      `SelectPermanentEffect.SetUp`의 `canTargetCondition`은 `Func&lt;HeadlessEntityId,bool&gt;` 전용
//      (`HasMatchConditionOwnersPermanent`는 Permanent-술어 오버로드 유지, 어댑터 불필요) — IsWeakerEnemyDigimon/
//      CanSelectSaveTamerPermanentCondition은 AS-IS 본문 그대로 두고 PermanentOf/…ById 어댑터를 덧댐(BT17_026 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Red;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class AD1_006 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternative Digivolve Condition
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Blue Flare")
                        || targetPermanent.TopCard.EqualsTraits("Xros Heart");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(level: 6, permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Shared OP / WD / WA

        string SharedHashString = "AD1_006_OP_WD_WA";

        string SharedEffectName = "May bottom deck an opponent's Digimon with same or less DP as this. May unsuspend";

        string SharedEffectDescription(string tag) => $"[{tag}] [Once Per Turn] You may return 1 of your opponent's Digimon with as much or less DP as this Digimon to the bottom of the deck. Then, this Digimon may unsuspend.";

        // id 어댑터(symbol_map_guide §2.3 critical rule) — HasMatchConditionOpponentsPermanent /
        // SelectPermanentEffect.SetUp의 canTargetCondition은 Func<HeadlessEntityId,bool>만 받는다; AS-IS
        // Permanent-술어는 그대로 두고 어댑터를 덧댄다(술어 뭉갬 금지).
        Permanent? PermanentOfShared(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        bool SharedCanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleArea(card)
                && (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, IsWeakerEnemyDigimonById)
                    || ICardEffect.ResolvePermanentOfThisCard(card).IsSuspended);
        }

        bool IsWeakerEnemyDigimon(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                && permanent.HasDP
                && permanent.DP <= ICardEffect.ResolvePermanentOfThisCard(card).DP;
        }

        bool IsWeakerEnemyDigimonById(HeadlessEntityId id)
        {
            Permanent? permanent = PermanentOfShared(id);
            return permanent is not null && IsWeakerEnemyDigimon(permanent);
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, IsWeakerEnemyDigimonById))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: IsWeakerEnemyDigimonById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }

            Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);

            if (permanent.IsSuspended && permanent.CanUnsuspend)
            {
                string selectPlayerMessage = "Will you unsuspend this card?";
                string notSelectPlayerMessage = "The opponent is choosing if they will unsuspend.";

                List<SelectionElement<bool>> command_SelectCommands = new List<SelectionElement<bool>>()
                {
                    new SelectionElement<bool>(message: $"Yes", value: true, spriteIndex: 0),
                    new SelectionElement<bool>(message: $"No", value: false, spriteIndex: 1),
                };

                GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: command_SelectCommands, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

                await GManager.instance.userSelectionManager.WaitForEndSelect();

                bool unsuspend = GManager.instance.userSelectionManager.SelectedBoolValue;

                if (unsuspend)
                {
                    await new IUnsuspendPermanents(
                        new List<Permanent>() { permanent },
                        activateClass).Unsuspend();
                }
            }
        }

        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hash) => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("On Play"));
            activateClass.SetHashString(SharedHashString);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }
        }
        #endregion

        #region When Digivolving
        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:120)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — symbol_map_guide §2.7, BT17_026 idiom).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hash) => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("When Digivolving"));
            activateClass.SetHashString(SharedHashString);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, (hash) => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("When Attacking"));
            activateClass.SetHashString(SharedHashString);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place up to 4 [Xros Heart] or [Blue Flare] Digimon from this Digimon's digivolution cards under a tamer, then play 1 more for free", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[All Turns] When this Digimon would leave the battle area other than by DigiXros, from this Digimon's digivolution cards, you may place up to 4 [Xros Heart] or [Blue Flare] trait Digimon cards under 1 of your Tamers and play 1 such card without paying the cost.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card)
                    && !CardEffectCommons.IsLeavingForDigiXros(hashtable);
            }

            bool IsXrosHeartOrBlueFlare(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && (cardSource.EqualsTraits("Blue Flare") || cardSource.EqualsTraits("Xros Heart"));
            }

            bool CanPlayXrosHeartOrBlueFlare(CardSource cardSource)
            {
                return IsXrosHeartOrBlueFlare(cardSource)
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass);
            }

            bool CanSelectSaveTamerPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card)
                       && permanent.IsTamer;
            }

            Permanent? PermanentOfAllTurns(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectSaveTamerPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOfAllTurns(id);
                return permanent is not null && CanSelectSaveTamerPermanentCondition(permanent);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Some(IsXrosHeartOrBlueFlare);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool placedCards = false;

                #region Place up to 4 cards under a tamer
                if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, CanSelectSaveTamerPermanentCondition))
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    int maxCount = Math.Min(4, ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(IsXrosHeartOrBlueFlare));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: IsXrosHeartOrBlueFlare,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select digivolution cards to place under 1 of your Tamers.",
                        maxCount: maxCount,
                        canEndNotMax: true,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.DigivolutionCards,
                        customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage(
                        "Select digivolution cards to place under 1 of your Tamers.",
                        "The opponent is selecting digivolution cards to place under 1 of your Tamers.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectCardEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    Permanent? selectedPermanent = null;

                    if (selectedCards.Count > 0)
                    {
                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectSaveTamerPermanentById,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: true,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Tamer to place the chosen cards under.",
                            "The opponent is selecting 1 Tamer to place the chosen cards under.");

                        await selectPermanentEffect.Activate();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;

                            return Task.CompletedTask;
                        }

                        if (selectedPermanent != null)
                        {
                            placedCards = true;
                            await selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass.EffectSourceCard?.InstanceId);
                        }
                    }
                }
                #endregion

                #region Play 1 card
                if ((placedCards || !CardEffectCommons.HasMatchConditionOwnersPermanent(card, CanSelectSaveTamerPermanentCondition))
                    && ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Some(CanPlayXrosHeartOrBlueFlare))
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanPlayXrosHeartOrBlueFlare,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => !placedCards,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 digivolution card to play.",
                        maxCount: 1,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.DigivolutionCards,
                        customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage(
                        "Select 1 digivolution card to play.",
                        "The opponent is selecting 1 digivolution card to play.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectCardEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    if (selectedCards.Count > 0)
                    {
                        await CardEffectCommons.PlayPermanentCards(
                            cardSources: selectedCards,
                            activateClass: activateClass,
                            payCost: false,
                            isTapped: false,
                            root: SelectCardEffect.Root.DigivolutionCards,
                            activateETB: true);
                    }
                }
                #endregion
            }
        }
        #endregion

        #region Digixros
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.DigiXrosEffectFromNames(card, 2, null,
                "OmniShoutmon",
                "ZeigGreymon",
                "Ballistamon",
                "Dorulumon",
                "Starmons",
                "Sparrowmon"));
        }
        #endregion

        return cardEffects;
    }
}
