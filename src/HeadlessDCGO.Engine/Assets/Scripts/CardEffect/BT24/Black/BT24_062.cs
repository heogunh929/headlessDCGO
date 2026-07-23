// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3B 정본 카드 — MasterBlimpmon (BT24_062, Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT24/Black/BT24_062.cs (228 lines, 7 regions)
//    * Alternate Digivolution :15-30  (None — AddSelfDigivolutionRequirementStaticEffect, Lv.4 [TS]/Machine/Cyborg 위 코스트 3)
//    * Blocker                :32-40  (None — BlockerSelfStaticEffect)
//    * Armor Purge            :42-47  (WhenPermanentWouldBeDeleted — ArmorPurgeEffect)
//    * Assembly               :49-89  (None — AddAssemblyConditionClass, [Blimpmon]/TS Tamer 1재료 -2)
//    * Shared EOA/EOOT OPT    :91-162 (진화원에서 5코스트↓ [Machine]/[Cyborg]/[TS] 무료 플레이)
//    * End of Attack          :164-181(OnEndAttack + CanTriggerOnAttack, Once Per Turn)
//    * End of Opponent's Turn :183-200(OnEndTurn + IsOpponentTurn, Once Per Turn)
//    * Your Turn - ESS        :202-224(None — CanNotSwitchAttackTargetClass, inherited)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #10, 4축):
//    * K:ArmorPurge — Armor Purge 팔(AS-IS :45). ★수확 예상 정정: 감사 §2′은 🟡stop-only(BT24_018)로 표기했으나
//      미러 팩토리 ArmorPurgeEffect(ArmorPurge.cs:19)+ArmorPurgeProcess 실존 — 포팅 성공. 예상 빗나감 → 원장 정정.
//    * P:AddAssemblyConditionClass — Assembly 등록(AS-IS :52). None-타이밍 등록은 데이터-홀더로 착지
//      (AD1_025 판례와 동형). **RD-EXT3-02 RESOLVED (해소 — G-AppF, SelectAssemblyClass 전이식·throw 부재):
//      이 조건을 소비하는 인터랙티브 Assembly 플레이가 실행된다. 구 "SelectAssemblyClass 정적 헬퍼 경계 throw →
//      잠복 STOP" 주석은 stale이라 정정(BT21_030 자기-정정 판례). 등록·소비 팔 전부 실착지 —
//      witness EXEMPLAR-T3B BT24_062 W2(Assembly 소비 실행).**
//    * P:CanNotSwitchAttackTargetClass — ESS 팔(AS-IS :206). 미러 CardEffects/CanNotSwitchAttackTargetClass 실존.
//    * X:Assembly — Assembly 특수플레이(위 RD-EXT3-02 RESOLVED와 동일 요지).
//    * (+P:BlockerSelfStaticEffect·P:AddSelfDigivolutionRequirementStaticEffect·P:PlayPermanentCards(진화원 root),
//       S:SelectCardEffect(Root.Custom 진화원), T:OnEndAttack/OnEndTurn/WhenPermanentWouldBeDeleted)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * End of Attack → OnEndAttack + CanTriggerOnAttack(hashtable, card)(AS-IS :176-177). SetHashString
//      "BT24_062_EOT_EOOT" 공유(Once Per Turn 공유 계정, AS-IS :171/190 동일 해시).
//    * End of Opponent's Turn → OnEndTurn + IsOpponentTurn(card)(AS-IS :195-196). (AS-IS는 OnEndTurn을 상대턴
//      게이트로 제한 — 미러 동일 키 유지, IsOpponentTurn 술어로 상대턴만 발화.)
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`/`yield return ContinuousController.instance.
//      StartCoroutine(X)`→`await X`, lone `yield return null`→`Task.CompletedTask`.
//    * `TopCard.IsLevel4` → `TopCard.IsLevel(4)`(미러 IsLevel(int) idiom; IsLevel4 프로퍼티 부재).
//    * `TopCard.HasTSTraits`/`cardSource.HasTSTraits` → 미러 CardSource.HasTSTraits(EXEMPLAR-T3B 추가,
//      AS-IS CardSource.cs:3739 앵커 `EqualsTraits("TS")` 1:1).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`(LM_054 판례).
//    * `card.Owner`(AS-IS Player) → HeadlessPlayerId.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT24.Black;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT24_062 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.IsLevel(4)
                    && (targetPermanent.TopCard.HasTSTraits
                        || targetPermanent.TopCard.EqualsTraits("Machine")
                        || targetPermanent.TopCard.EqualsTraits("Cyborg"));
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(PermanentCondition, 3, false, card, null));
        }

        #endregion

        #region Blocker

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        #endregion

        #region Armor Purge

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.ArmorPurgeEffect(card));
        }

        #endregion

        #region Assembly

        if (timing == EffectTiming.None)
        {
            AddAssemblyConditionClass addAssemblyConditionClass = new AddAssemblyConditionClass();
            addAssemblyConditionClass.SetUpICardEffect($"Assembly", CanUseCondition, card);
            addAssemblyConditionClass.SetUpAddAssemblyConditionClass(getAssemblyCondition: GetAssembly);
            addAssemblyConditionClass.SetNotShowUI(true);
            cardEffects.Add(addAssemblyConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            AssemblyCondition GetAssembly(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    AssemblyConditionElement element = new AssemblyConditionElement(CanSelectCardCondition);

                    bool CanSelectCardCondition(CardSource cardSource)
                    {
                        return cardSource.EqualsCardName("Blimpmon")
                            || (cardSource.IsTamer
                                && cardSource.HasTSTraits);
                    }

                    AssemblyCondition assemblyCondition = new AssemblyCondition(
                        element: element,
                        CanTargetCondition_ByPreSelecetedList: null,
                        selectMessage: "[Blimpmon]/Tamer Card w/ [TS] trait",
                        elementCount: 1,
                        reduceCost: 2);

                    return assemblyCondition;
                }

                return null;
            }
        }

        #endregion

        #region Shared EOA / EOOT OPT

        string SharedEffectName = "Play a 5- Cost [Machine]/[Cyborg]/[TS] from digivolution cards.";

        string SharedEffectDescription(string tag) => $"[{tag}] [Once Per Turn] You may play 1 play cost 5 or lower card with the [Machine], [Cyborg] or [TS] trait from this Digimon's digivolution cards without paying the cost.";

        string SharedEffectHash = "BT24_062_EOT_EOOT";

        bool SharedCanActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsExistOnBattleArea(card)
                && ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Any(cardSource => CanSelectCardCondition(cardSource));
        }

        bool CanSelectCardCondition(CardSource cardSource)
        {
            return cardSource.HasPlayCost
                && cardSource.GetCostItself <= 5
                && (cardSource.HasTSTraits
                    || cardSource.EqualsTraits("Machine")
                    || cardSource.EqualsTraits("Cyborg"));
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

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
                        message: "Select 1 card to play.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Custom,
                        customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                        canLookReverseCard: false,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

            selectCardEffect.SetUpCustomMessage("Select 1 digivolution card to play.", "The opponent is selecting 1 digivolution card to play.");
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
                root: SelectCardEffect.Root.DigivolutionCards,
                activateETB: true);
        }

        #endregion

        #region End of Attack

        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(hash => SharedCanActivateCondition(hash, activateClass), hash => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("End of Attack"));
            activateClass.SetHashString(SharedEffectHash);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }
        }

        #endregion

        #region End of Opponent's Turn

        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(hash => SharedCanActivateCondition(hash, activateClass), hash => SharedActivateCoroutine(hash, activateClass), 1, true, SharedEffectDescription("End of Opponent's Turn"));
            activateClass.SetHashString(SharedEffectHash);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOpponentTurn(card);
            }
        }

        #endregion

        #region Your Turn - ESS

        if (timing == EffectTiming.None)
        {
            CanNotSwitchAttackTargetClass canNotSwitchAttackTargetClass = new CanNotSwitchAttackTargetClass();
            canNotSwitchAttackTargetClass.SetUpICardEffect("This Digimon's attack target can't be switched.", CanUseCondition, card);
            canNotSwitchAttackTargetClass.SetUpCanNotSwitchAttackTargetClass(PermanentCondition: PermanentCondition);
            canNotSwitchAttackTargetClass.SetIsInheritedEffect(true);
            cardEffects.Add(canNotSwitchAttackTargetClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.IsOwnerTurn(card);
            }

            bool PermanentCondition(Permanent permanent)
            {
                return permanent != null && permanent.TopCard != null && permanent == ICardEffect.ResolvePermanentOfThisCard(card);
            }
        }

        #endregion

        return cardEffects;
    }
}
