// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 카드 — Cendrillmon (BT22_040, Digimon / Yellow / Lv.5)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT22/Yellow/BT22_040.cs (199 lines, 4 regions)
//    * [Overclock]          :14-21  (OnEndTurn — OverclockSelfEffect trait "Puppet")
//    * [On Play]            :23-55  (OnEnterFieldAnyone + CanTriggerOnPlay — 1 [Familiar] Token)
//    * [When Digivolving]   :57-89  (OnEnterFieldAnyone + CanTriggerWhenDigivolving — 1 [Familiar] Token)
//    * [All Turns] OPT      :91-195 (OnDestroyedAnyone — 아군 삭제 시 자신의 [When Digivolving] 효과 1개 재발화)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #24):
//    * K:Overclock          — [Overclock] OverclockSelfEffect (AS-IS :18; CardEffectFactory KeyWord)
//    * P:PlayFamiliarToken  — [On Play]/[When Digivolving] 몸통 (AS-IS :51/:85; TokenSpecs["Familiar"])
//    * (+T:OnDestroyedAnyone, E:SelectCardEffect(SkillInfo 재발화), ActivateICardEffect
//       .Activate_Optional_Effect_Execute, CanTriggerOnPermanentDeleted)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언 rule 3; AS-IS OnEnterFieldAnyone +
//      CanTriggerWhenDigivolving 게이트). [On Play]는 OnEnterFieldAnyone + CanTriggerOnPlay 유지.
//    * [Overclock] → OnEndTurn(키워드 상시-턴종 훅).
//    * [All Turns] OPT → OnDestroyedAnyone + CanTriggerOnPermanentDeleted(IsOwnerDigimon).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask (BT8_092 idiom).
//    * AS-IS :46/:80 `card.Owner.fieldCardFrames.Count(빈 프레임)>=1` = 빈-프레임 용량 절반은 미러 frame-모델
//      부재로 소거(RD-P6C1-2, BT19_091 헤더 동일 근거); 토큰 커먼즈가 capacity 재검사. IsExistOnBattleAreaDigimon
//      절반은 1:1 유지.
//    * AS-IS :120 `permanent != card.PermanentOfThisCard()` → `permanent.InstanceId !=
//      card.PermanentOfThisCard().TopInstanceId` (PermanentView; BT2_073 idiom).
//    * AS-IS :125 `card.PermanentOfThisCard().EffectList(EffectTiming.OnEnterFieldAnyone)` — [When Digivolving]가
//      미러에서 WhenDigivolving 키로 이관됐으므로 질의 키를 동일 remap: `ResolvePermanentOfThisCard(card)
//      .EffectList(EffectTiming.WhenDigivolving)`(PermanentView→Permanent; 같은 효과가 사는 키를 조회 — 결과 동일).
//      Filter의 IsWhenDigivolving(EffectDiscription "[When Digivolving]" 시작 + CanTrigger)는 1:1 유지.
//    * UI 데코 SetNotShowCard/SetUpSkillInfos는 미러에도 존재(UI-only 저장) — AS-IS 1:1 호출 유지.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT22_040 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Overclock
        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.OverclockSelfEffect(trait: "Puppet", isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Familiar] Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] You may play 1 [Familiar] Token. (Digimon/Yellow/3000 DP/[On Deletion] 1 of your opponent's Digimon gets -3000 DP for the turn.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                // AS-IS :46 `&& card.Owner.fieldCardFrames.Count(빈 프레임)>=1` — 빈-프레임 용량 절반 소거(RD-P6C1-2).
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.PlayFamiliarToken(activateClass);
            }
        }
        #endregion

        #region When Digivolving
        // ③ 미러 방언: AS-IS OnEnterFieldAnyone(:57) + CanTriggerWhenDigivolving → WhenDigivolving 전용 키.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Familiar] Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] You may play 1 [Familiar] Token. (Digimon/Yellow/3000 DP/[On Deletion] 1 of your opponent's Digimon gets -3000 DP for the turn.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.PlayFamiliarToken(activateClass);
            }
        }
        #endregion

        #region All Turns - OPT
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Activate a [When Digivolving] effect", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("BT22_040_UseWD");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] [Once Per Turn] When any of your other Digimon are deleted, you may activate 1 of this Digimon's [When Digivolving] effects.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, IsOwnerDigimon);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool IsOwnerDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                       permanent.InstanceId != card.PermanentOfThisCard().TopInstanceId;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                // ③-remap: AS-IS EffectList(OnEnterFieldAnyone) — 미러는 [When Digivolving]를 WhenDigivolving 키로
                // 이관, 동일 효과가 사는 키를 조회.
                List<ICardEffect> candidateEffects = ICardEffect.ResolvePermanentOfThisCard(card).EffectList(EffectTiming.WhenDigivolving)
                    .Clone()
                    .Filter(cardEffect => cardEffect != null && cardEffect is ActivateICardEffect && !cardEffect.IsSecurityEffect && cardEffect.IsWhenDigivolving);

                if (candidateEffects.Count >= 1)
                {
                    ICardEffect selectedEffect = null;

                    if (candidateEffects.Count == 1)
                    {
                        selectedEffect = candidateEffects[0];
                    }
                    else
                    {
                        List<SkillInfo> skillInfos = candidateEffects
                            .Map(cardEffect => new SkillInfo(cardEffect, null, EffectTiming.None));

                        List<CardSource> cardSources = candidateEffects
                            .Map(cardEffect => cardEffect.EffectSourceCard);

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: (cardSource) => true,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 effect to activate.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: false,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: cardSources,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetNotShowCard();
                        selectCardEffect.SetUpSkillInfos(skillInfos);
                        selectCardEffect.SetUpAfterSelectIndexCoroutine(AfterSelectIndexCoroutine);

                        await selectCardEffect.Activate();

                        Task AfterSelectIndexCoroutine(List<int> selectedIndexes)
                        {
                            if (selectedIndexes.Count == 1)
                            {
                                selectedEffect = candidateEffects[selectedIndexes[0]];
                            }

                            return Task.CompletedTask;
                        }
                    }

                    if (selectedEffect != null)
                    {
                        Hashtable effectHashtable = CardEffectCommons.WhenDigivolvingCheckHashtableOfCard(selectedEffect.EffectSourceCard);

                        if (!selectedEffect.IsDisabled)
                        {
                            await ((ActivateICardEffect)selectedEffect).Activate_Optional_Effect_Execute(effectHashtable);
                        }
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
