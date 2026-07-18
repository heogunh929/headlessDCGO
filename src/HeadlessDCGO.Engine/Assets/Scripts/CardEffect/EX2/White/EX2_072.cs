// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — EX2_072 (Tamer / White)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX2/White/EX2_072.cs (343 lines, no region markers)
//    * Ignore Color Requirements :14-41  (timing None — "≥1 owner battle-area Tamer" gate, self 한정)
//    * [Main]                    :42-267 (OptionSkill — 덱탑 5장 리빌 → 비-white 디지몬 1장 무료 진화
//                                          시도(성공 시 나머지 4장 덱바닥) → 실패 시 디지몬 1장 손패, 나머지 덱바닥)
//    * [Security]                :269-339(SecuritySkill — 손패 Tamer 1장 무료 플레이)
//
// ② 프리미티브 매핑:
//    * P:IgnoreColorConditionClass, P:CanTriggerOptionMainEffect, P:CanTriggerSecurityEffect,
//      P:ReturnRevealedCardsToLibraryBottom, P:PlayPermanentCards
//    * RevealLibraryClass(card.Owner, 5).RevealLibrary()+.RevealedCards — 미러엔 이 구체 클래스가 없다
//      (symbol_map.csv: RevealLibraryClass → RevealDeckTopCardsAndSelect/SimplifiedRevealDeckTopCardsAndSelect
//      브리지로 계상). 그러나 EX2_072의 분기 구조(리빌 → 진화 시도 → 실패 시에만 별도 손패-추가 재-셀렉트,
//      두 패스가 서로 다른 실패조건에 걸림)는 그 브리지들의 "공유 풀에서 순차 소진" 모델과 맞지 않으므로,
//      브리지 내부가 실제로 쓰는 것과 동일한 1차 프리미티브(§3 인라인-조립 판례, BT2_044:84의 IZoneStateReader
//      리빌-idiom과 동형)로 "덱탑 N장 CardSource 목록"을 직접 재구성한다(물리 이동 없음 — AS-IS RevealLibrary도
//      순수 UI 리빌이며 개별 선택 카드만 SelectCardEffect가 실제로 옮김).
//
// ③ 배선 관례 근거:
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect. [Security] → SecuritySkill + CanTriggerSecurityEffect.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`/
//      `yield return StartCoroutine(X)` → `await X`.
//    * `card.Owner.GetBattleAreaPermanents()/GetBattleAreaDigimons()/LibraryCards/HandCards` →
//      `new Player(card.Context, card.Owner).*` 또는 확장(§2.2 예외: GetBattleAreaDigimons는 card.Owner 확장).
//    * `cardSource.CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClass)`(AS-IS :62,:142,:197)
//      — 점유-프레임 환원(RD-P6C1-2, symbol_map.csv#428): `permanent.TopCard.Owner == cardSource.Owner &&
//      cardSource.CanEvolve(permanent, false)`.
//    * `new RevealLibraryClass(card.Owner, 5).RevealLibrary()` + `.RevealedCards` → 덱탑 5장 직접 재구성
//      (`((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Take(5).Select(id
//      => new CardSource(card.Context, id, card.Owner, card.Owner))`, BT2_044:84 리빌-idiom 확장).
//    * `CardEffectCommons.ReturnRevealedCardsToLibraryBottom(libraryCards, activateClass)` → AS-IS-signature
//      그대로(2번째 인자는 sourceCard — card 전달).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX2.White;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX2_072 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            IgnoreColorConditionClass ignoreColorConditionClass = new IgnoreColorConditionClass();
            ignoreColorConditionClass.SetUpICardEffect("Ignore color requirements", CanUseCondition, card);
            ignoreColorConditionClass.SetUpIgnoreColorConditionClass(cardCondition: CardCondition);

            cardEffects.Add(ignoreColorConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).GetBattleAreaPermanents().Count((permanet) => permanet.IsTamer) >= 1)
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
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Reveal the top 5 cards of your deck. You may digivolve 1 of your Digimon into 1 non-white Digimon card among them without paying its memory cost. If you don't, add 1 Digimon card among them to your hand. Place the remaining cards at the bottom of your deck in any order.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (!cardSource.HasCardColor("White"))
                    {
                        foreach (Permanent permanent in card.Owner.GetBattleAreaDigimons())
                        {
                            // AS-IS :62 CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClass) —
                            // 점유-프레임 환원(RD-P6C1-2).
                            if (permanent.TopCard.Owner == cardSource.Owner && cardSource.CanEvolve(permanent, false))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanSelectCardCondition1(CardSource cardSource)
            {
                return cardSource.IsDigimon;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).LibraryCards.Count >= 1)
                {
                    bool digivolved = false;

                    List<CardSource> selectedCards = new List<CardSource>();

                    // AS-IS RevealLibraryClass(card.Owner, 5).RevealLibrary() — pure UI reveal marker, no
                    // physical move (BT2_044:84 리빌-idiom 확장): read the top 5 Library cards directly.
                    List<CardSource> revealedCards = ((IZoneStateReader)card.Context.ZoneMover)
                        .GetCards(card.Owner, ChoiceZone.Library)
                        .Take(5)
                        .Select(id => new CardSource(card.Context, id, card.Owner, card.Owner))
                        .ToList();

                    int maxCount = Math.Min(1, revealedCards.Count(CanSelectCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUseFaceDown();

                    if (maxCount >= 1)
                    {
                        selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to digivolve.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Custom,
                        customRootCardList: revealedCards,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                        await selectCardEffect.Activate();

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);
                            return Task.CompletedTask;
                        }
                    }

                    if (selectedCards.Count >= 1)
                    {
                        CardSource selectedCard = selectedCards[0];

                        if (selectedCard != null)
                        {
                            List<Permanent> candidatePermanents = new List<Permanent>();

                            foreach (Permanent permanent in card.Owner.GetBattleAreaDigimons())
                            {
                                if (permanent.TopCard.Owner == selectedCard.Owner && selectedCard.CanEvolve(permanent, false))
                                {
                                    candidatePermanents.Add(permanent);
                                }
                            }

                            bool CanSelectPermanentCondition(Permanent permanent)
                            {
                                if (permanent != null)
                                {
                                    if (candidatePermanents.Contains(permanent))
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            Permanent? PermanentOf(HeadlessEntityId id) =>
                                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                                    ? new Permanent(card.Context, id, rec.OwnerId)
                                    : null;

                            bool CanSelectPermanentById(HeadlessEntityId id)
                            {
                                Permanent? permanent = PermanentOf(id);
                                return permanent is not null && CanSelectPermanentCondition(permanent);
                            }

                            if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                            {
                                Permanent selectedPermanent = null;

                                maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                                selectPermanentEffect.SetUp(
                                    selectPlayer: card.Owner,
                                    canTargetCondition: CanSelectPermanentById,
                                    canTargetCondition_ByPreSelecetedList: null,
                                    canEndSelectCondition: null,
                                    maxCount: maxCount,
                                    canNoSelect: true,
                                    canEndNotMax: false,
                                    selectPermanentCoroutine: SelectPermanentCoroutine,
                                    afterSelectPermanentCoroutine: null,
                                    mode: SelectPermanentEffect.Mode.Custom,
                                    cardEffect: activateClass);

                                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will Digivolve.", "The opponent is selecting 1 Digimon that will Digivolve.");

                                await selectPermanentEffect.Activate();

                                Task SelectPermanentCoroutine(Permanent permanent)
                                {
                                    selectedPermanent = permanent;
                                    return Task.CompletedTask;
                                }

                                if (selectedPermanent != null)
                                {
                                    if (selectedCard != null)
                                    {
                                        // AS-IS :197 CanPlayCardTargetFrame(selectedPermanent.PermanentFrame, false,
                                        // activateClass) — 점유-프레임 환원(RD-P6C1-2).
                                        if (selectedPermanent.TopCard.Owner == selectedCard.Owner && selectedCard.CanEvolve(selectedPermanent, false))
                                        {
                                            digivolved = true;

                                            List<CardSource> libraryCards = new List<CardSource>();

                                            foreach (CardSource cardSource in revealedCards)
                                            {
                                                if (!selectedCards.Contains(cardSource))
                                                {
                                                    libraryCards.Add(cardSource);
                                                }
                                            }

                                            await CardEffectCommons.ReturnRevealedCardsToLibraryBottom(libraryCards, card);

                                            await new PlayCardClass(
                                                cardSources: new List<CardSource>() { selectedCard },
                                                hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                                                payCost: false,
                                                targetPermanent: selectedPermanent,
                                                isTapped: false,
                                                root: SelectCardEffect.Root.Library,
                                                activateETB: true).PlayCard();
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!digivolved)
                    {
                        List<CardSource> libraryCards = new List<CardSource>();

                        maxCount = Math.Min(1, revealedCards.Count(CanSelectCardCondition1));

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition1,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: AfterSelectCardCoroutine,
                            message: "Select 1 card to add to your hand.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.AddHand,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: revealedCards,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        await selectCardEffect.Activate();

                        Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                        {
                            foreach (CardSource cardSource in revealedCards)
                            {
                                if (!cardSources.Contains(cardSource))
                                {
                                    libraryCards.Add(cardSource);
                                }
                            }

                            return Task.CompletedTask;
                        }

                        await CardEffectCommons.ReturnRevealedCardsToLibraryBottom(libraryCards, card);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Play 1 Tamer from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] You may play 1 Tamer card from your hand without paying its memory cost.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsTamer)
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
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
                if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    int maxCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                    selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectHandEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    await CardEffectCommons.PlayPermanentCards(cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Hand, activateETB: true);
                }
            }
        }

        return cardEffects;
    }
}
