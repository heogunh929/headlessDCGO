// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 트랜치2A — MagnaGarurumon (BT18_042, Digimon / Yellow / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT18/Yellow/BT18_042.cs (252 lines, 5 regions)
//    * Alternate Digivolution              :13-28  (timing == None, AddSelfDigivolutionRequirementStaticEffect;
//                                                    [Koji Minamoto] 위 + Hybrid 진화원 ≥5, cost 5, ignore=true)
//    * When Digivolving / EOT Shared 지역함수 :30-43 (CanSelectSourceCardConditionShared / CanActivateConditionShared —
//                                                    메서드 스코프 1회 선언, 두 region 공유)
//    * When Digivolving                    :45-118 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving 게이트)
//    * End of Opponent's Turn              :120-194 (timing == OnEndTurn, IsOpponentTurn 게이트)
//    * All Turns                           :196-248 (timing == OnAllyAttack or OnCounterTiming, Once Per Turn)
//
// ② 프리미티브 매핑 (감사 축 이름):
//    * P:AceOverflowClass / X:AceOverflow — When-Digivolving·EOT 공유 SelectCardCoroutine 몸통에서
//      선택된 진화원이 ACE면 오버플로 정산 (AS-IS :100, :176). 미러 AceOverflowClass.Overflow()가 내부에서
//      IsACE&&!IsFlipped&&OnBattleArea 게이트를 재현(CardController.cs:2112-2160) → IsACE 가드는 흡수됨(③ 참조).
//    * (+P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — Alt Digivolution 상시,
//       P:DestroyPermanentsClass — 동레벨 적 전멸, P:AddSecurityCard(바닥 보안), P:AddHandCards(보안 top→손),
//       P:IReduceSecurity(보안 -1), P:IUnsuspendPermanents(자기 언탭),
//       E:SelectCardEffect Mode.Custom/Root.Custom(진화원), T:WhenDigivolving, T:OnEndTurn,
//       T:OnAllyAttack/OnCounterTiming)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone :47 +
//      CanTriggerWhenDigivolving 게이트 — 게이트는 그대로, 키만 방언. 이중-키 등록 금지 — rule 3).
//    * [End of Opponent's Turn] → OnEndTurn 그대로(AS-IS :122); CanUseCondition의 IsOpponentTurn(card) 게이트 유지.
//    * [All Turns] → OnAllyAttack or OnCounterTiming 그대로(AS-IS :198); CanTriggerOnPermanentAttack(hashtable,
//      PermanentCondition) triggerGate 유지.
//    * Alt Digivolution → timing == None 그대로(상시 진화조건 등록).
//    * 공유 지역함수(CanSelectSourceCardConditionShared/CanActivateConditionShared)는 AS-IS와 동일하게 메서드
//      스코프에 1회 선언(구조 보존).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)` 및
//      `yield return StartCoroutine(X)`→`await X`. `IEnumerator SelectCardCoroutine`(내부 yield 보유)→
//      `async Task SelectCardCoroutine`(BT8_092 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (미러 accessor; ICardEffect.cs:537).
//    * `card.Owner.Enemy.GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons()`
//      (Player.Enemy=Player? Player.cs:43; 2인전 비-null → null-forgiving). `card.Owner.SecurityCards`/`[0]` →
//      `new Player(card.Context, card.Owner).SecurityCards`/`[0]` (Player.cs:129).
//    * `IReduceSecurity(player, ref ...nullSkillInfos, activateClass)` — AS-IS `ref nullSkillInfos`는 UI plumbing.
//      미러 ctor(CardController.cs:1560)는 `(EngineContext, HeadlessPlayerId, List<PendingSecurityTrigger>? refCollector,
//      ICardEffect?)` → `(card.Context, card.Owner, refCollector: null, activateClass)`. UI ref 파라미터 드롭.
//    * AS-IS `if (cardSource.IsACE)` 가드(:100, :176) — 미러 CardSource엔 IsACE 표면이 없음(§4 보고). 미러
//      AceOverflowClass.Overflow()가 내부 AceOverflowGate.OverflowFor(record)로 IsACE&&!IsFlipped&&OnBattleArea를
//      재현하고 비-ACE는 0=no-op(CardController.cs:2112-2160) → 가드는 흡수·Overflow 무조건 await(결과 등가·구조 정직).
//    * `DestroyPermanentsClass(..., CardEffectCommons.CardEffectHashtable(activateClass)).Destroy()` — 미러 ctor
//      1:1(CardController.cs:4152 `(List<Permanent>, Hashtable, bool notShowCards=false)`) + CardEffectHashtable
//      (HashtableSetting.cs:16). `.Filter`/`.Some`=미러 IEnumerableExtension(:51/:63) 그대로 유지.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT18.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT18_042 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsCardName("Koji Minamoto") &&
                       targetPermanent.DigivolutionCards.Count(cardSource => cardSource.ContainsTraits("Hybrid")) >= 5;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition, digivolutionCost: 5, ignoreDigivolutionRequirement: true,
                card: card, condition: null));
        }

        #endregion

        #region When Digivolving/ End of Opponent's Turn Shared

        bool CanSelectSourceCardConditionShared(CardSource cardSource)
        {
            return cardSource.IsDigimon;
        }

        bool CanActivateConditionShared(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                   ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Some(CanSelectSourceCardConditionShared);
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:47)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(
                "Place Digivolution card as your bottom security card to delete all opponent's same level Digimon",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("WDUnsuspend_BT18_042");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Digivolving] (Once Per Turn) By placing 1 Digimon card from this Digimon's digivolution cards as your bottom security card, delete all of your opponent's Digimon with the same level as the placed card.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                int chosenDigimonCardLevel = 0;

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: CanSelectSourceCardConditionShared,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 digivolution card to place as bottom security card.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Custom,
                    customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage(
                    "Select 1 digivolution card to place as bottom security card.",
                    "The opponent is selecting 1 digivolution card to place as bottom security card.");

                await selectCardEffect.Activate();

                async Task SelectCardCoroutine(CardSource cardSource)
                {
                    // AS-IS :100 `if (cardSource.IsACE) ... AceOverflowClass.Overflow()` — 미러 CardSource엔 IsACE
                    // 표면이 없고 Overflow()가 내부에서 IsACE&&!IsFlipped 게이트를 재현(비-ACE=0 no-op,
                    // CardController.cs:2112-2160) → 가드 흡수·무조건 await(결과 등가).
                    await new AceOverflowClass(new List<CardSource> { cardSource }).Overflow();
                    await CardObjectController.AddSecurityCard(cardSource, toTop: false);

                    chosenDigimonCardLevel = cardSource.Level;
                }

                if (chosenDigimonCardLevel > 0)
                {
                    List<Permanent> destroyTargetPermanents =
                        new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons()
                            .Filter(permanent => permanent.Level == chosenDigimonCardLevel);

                    await new DestroyPermanentsClass(
                        destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
                }
            }
        }

        #endregion

        #region End of Opponent's Turn

        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(
                "Place Digivolution card as your bottom security card to delete all opponent's same level Digimon",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("EOTUnsuspend_BT18_042");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[End of Opponent's Turn] (Once Per Turn) By placing 1 Digimon card from this Digimon's digivolution cards as your bottom security card, delete all of your opponent's Digimon with the same level as the placed card.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.IsOpponentTurn(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                int chosenDigimonCardLevel = 0;

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: CanSelectSourceCardConditionShared,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 digivolution card to place as bottom security card.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Custom,
                    customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage(
                    "Select 1 digivolution card to place as bottom security card.",
                    "The opponent is selecting 1 digivolution card to place as bottom security card.");

                await selectCardEffect.Activate();

                async Task SelectCardCoroutine(CardSource cardSource)
                {
                    // AS-IS :176 IsACE 가드 — When Digivolving region과 동일 흡수(CardController.cs:2112-2160).
                    await new AceOverflowClass(new List<CardSource> { cardSource }).Overflow();
                    await CardObjectController.AddSecurityCard(cardSource, toTop: false);

                    chosenDigimonCardLevel = cardSource.Level;
                }

                if (chosenDigimonCardLevel > 0)
                {
                    List<Permanent> destroyTargetPermanents =
                        new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons()
                            .Filter(permanent => permanent.Level == chosenDigimonCardLevel);

                    await new DestroyPermanentsClass(
                        destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
                }
            }
        }

        #endregion

        #region All Turns

        if (timing is EffectTiming.OnAllyAttack or EffectTiming.OnCounterTiming)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By adding the top card of your security stack to the hand, unsuspend this Digimon",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("ATUnsuspend_BT18_042");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[All Turns] (Once Per Turn) When a Digimon attacks, by adding the top card of your security stack to the hand, unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, PermanentCondition);
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       new Player(card.Context, card.Owner).SecurityCards.Count >= 1;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                CardSource topCard = new Player(card.Context, card.Owner).SecurityCards[0];

                await CardObjectController.AddHandCards(new List<CardSource>() { topCard }, false,
                    activateClass);

                await new IReduceSecurity(
                    card.Context,
                    card.Owner,
                    refCollector: null,
                    activateClass).ReduceSecurity();

                await new IUnsuspendPermanents(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass).Unsuspend();
            }
        }

        #endregion

        return cardEffects;
    }
}
