// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T1 정본 카드 — Warpmon (EX10_029, Digimon / Black / Lv.4)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX10/Black/EX10_029.cs (211 lines, 6 regions)
//    * [Security]                    :15-22  (SecuritySkill → PlaySelfDigimonAfterBattleSecurityEffect)
//    * Alt Digivolution Condition    :24-36  (None — Stnd. Appmon 위 2코스트 대체 진화 조건)
//    * Link Condition                :38-50  (None — Appmon 위 링크 코스트 2)
//    * Link (선언)                   :52-59  (OnDeclaration → CardEffectFactory.LinkEffect)
//    * <Blocker>                     :61-71  (None → BlockerSelfStaticEffect)
//    * [When Linking]                :73-207 (WhenLinked — 링크카드 1장 트래시→디지몬 1장 De-Digivolve 면역)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #7):
//    * K:Link                                  — Link 선언 팩토리 + AddSelfLinkConditionStaticEffect +
//                                                WhenLinked 창. ⚠ 기존-STOP 명기: CardEffectFactory.LinkEffect의
//                                                ActivateCoroutine은 RD-P6C2-7 STOP(AS-IS ILinkCard.LinkCard
//                                                미러 부재 — Factory/KeyWordEffects/Link.cs:79-86). 카드 등록은
//                                                클린; Link "발화" witness는 해당 STOP 상환 후 가능(수확물).
//    * P:ImmuneFromDeDigivolveClass            — [When Linking] 몸통 UntilOpponentTurnEndEffects 부여 (AS-IS :194-197)
//    * P:PlaySelfDigimonAfterBattleSecurityEffect — [Security] (AS-IS :19; CardEffectFactory.cs:988 미러)
//    * P:TrashLinkCardsAndProcessAccordingToResult — [When Linking] 몸통 (AS-IS :140-145)
//    * (+P:AddDigivolutionRequirementClass, P:AddLinkConditionClass, K:Blocker, T:WhenLinked,
//       E:SelectCardEffect Root.Custom(LinkedCards), E:SelectPermanentEffect)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Linking] → WhenLinked 등록(AllTimings 포함) + CanTriggerWhenLinking(hashtable, null, card)
//      triggerGate(AS-IS :85-88 그대로 — self-스코프 rule 1) + SetIsLinkedEffect(true).
//    * Link 선언 → OnDeclaration(AS-IS :54 그대로).
//    * [Security] → SecuritySkill 팩토리 등록(SecurityResolver가 해소).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `TopCard.HasStandardAppTraits` (AS-IS CardSource.cs:3919 = EqualsTraits("Stnd.")) /
//      `TopCard.HasAppmonTraits` (= EqualsTraits("Appmon")) — 파생-getter 몸통 1:1 인라인
//      (BT22_035 확립 idiom: "AS-IS HasAppmonTraits → EqualsTraits(\"Appmon\")").
//    * `card.PermanentOfThisCard()`(가변 필요) → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `selectedPermanent.TopCard` null-guard(AS-IS :186)는 미러 TopCard 비-null 표면상 battle-area 존재
//      검사로 유지(BT8_092 헤더의 동일 어댑테이션).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX10.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX10_029 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Security

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(card: card));
        }

        #endregion

        #region Alternative Digivolution Condition

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                // AS-IS :30 targetPermanent.TopCard.HasStandardAppTraits (CardSource.cs:3919) 몸통 인라인.
                return targetPermanent.TopCard.EqualsTraits("Stnd.");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Link Condition

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                // AS-IS :44 targetPermanent.TopCard.HasAppmonTraits 몸통 인라인(BT22_035 idiom).
                return targetPermanent.TopCard.EqualsTraits("Appmon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 2, card: card));
        }

        #endregion

        #region Link

        if (timing == EffectTiming.OnDeclaration)
        {
            // ⚠ 기존-STOP: 이 ActivateClass의 발화 몸통은 RD-P6C2-7 STOP(Link.cs:79-86 — ILinkCard 미러
            // 부재). 등록/선언-표면은 클린이며, 1:1 원칙상 팩토리 호출을 그대로 미러한다(우회·발명 금지).
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
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

        #region When Linked

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("De-digivolve protection", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription() => "[When Linking] By trashing 1 of this Digimon's link cards, <De-Digivolve> effects don't affect 1 of your Digimon until your opponent's turn ends.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       !ICardEffect.ResolvePermanentOfThisCard(card).HasNoLinkCards;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
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

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent thisCardPermament = ICardEffect.ResolvePermanentOfThisCard(card);

                CardSource? selectedCard = null;

                #region Select Link Card

                int maxCount = Math.Min(1, thisCardPermament.LinkedCards.Count);
                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                            canTargetCondition: _ => true,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 linked card to trash.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: thisCardPermament.LinkedCards,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCard = cardSource;
                    return Task.CompletedTask;
                }
                selectCardEffect.SetUpCustomMessage("Select 1 linked card to trash.", "The opponent is selecting 1 linked card to trash.");
                await selectCardEffect.Activate();

                #endregion

                if (selectedCard != null) await CardEffectCommons.TrashLinkCardsAndProcessAccordingToResult(
                    targetPermanent: thisCardPermament,
                    targetLinkCards: new List<CardSource>() { selectedCard },
                    activateClass: activateClass,
                    successProcess: SuccessProcess,
                    failureProcess: null);

                async Task SuccessProcess(List<CardSource> trashedLinkCards)
                {
                    if (trashedLinkCards.Any())
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                            Permanent? selectedPermanent = null;

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

                            Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                selectedPermanent = permanent;
                                return Task.CompletedTask;
                            }

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to gain De-digivolve immunity.", "The opponent is selecting 1 Digimon to gain De-digivolve immunity.");
                            await selectPermanentEffect.Activate();

                            if (selectedPermanent != null)
                            {
                                #region De-digivolve immunity

                                bool CanUseImmunityCondition(Hashtable hashtable1)
                                {
                                    return selectedPermanent.TopCard != null;
                                }

                                bool PermanentImmunityCondition(Permanent permanent)
                                {
                                    return permanent == selectedPermanent;
                                }

                                ImmuneFromDeDigivolveClass immuneFromDeDigivolveClass = new ImmuneFromDeDigivolveClass();
                                immuneFromDeDigivolveClass.SetUpICardEffect("Isn't affected by <De-Digivolve>", CanUseImmunityCondition, selectedPermanent.TopCard);
                                immuneFromDeDigivolveClass.SetUpImmuneFromDeDigivolveClass(PermanentCondition: PermanentImmunityCondition);
                                selectedPermanent.UntilOpponentTurnEndEffects.Add((_timing) => immuneFromDeDigivolveClass);

                                #endregion
                            }
                        }
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
