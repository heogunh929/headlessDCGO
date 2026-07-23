// Source: DCGO/Assets/Scripts/CardEffect/BT21/Black/BT21_059.cs (1:1 mirror) — "Timemon".
//
// S6 롱테일 트랜치 — 감사 시절 ⚠STOP-예상(AppFusion 조건)이었으나 표면 실존 확인: 실제 활성 App Fusion 표면은
// AddAppfuseMethodByName(이름-기반, 미러 1:1 CardEffectFactory/AddAppfusionMethod.cs) 뿐 — AS-IS 원문의
// "#region App Fusion"(AddAppFusionConditionClass 직접 조립) 블록은 AS-IS 자체에서 전체가 `/* ... */`로
// 주석 처리된 죽은 코드(AddAppfuseMethodByName로 대체됨) → 포팅 대상에서 제외(1:1 원칙 위반 아님, AS-IS도
// 컴파일하지 않는 블록).
//   * [None] — AddSelfDigivolutionRequirementStaticEffect([Stnd.] 위 코스트2) + AddSelfLinkConditionStaticEffect
//     ([Appmon] 코스트2) + AddAppfuseMethodByName(Watchmon/Savemon/Calendamon) + BlockerSelfStaticEffect.
//   * [WhenLinked]"Your Turn" — 이 디지몬 자신이 링크될 때 <De-Digivolve 1> 상대 1체(once/turn).
//   * [OnDeclaration] — CardEffectFactory.LinkEffect(card) — <Link> 액션.
//   * [WhenLinked]"When Linking"(SetIsLinkedEffect(true)) — 링크 카드로서 자신이 링크될 때 <De-Digivolve 1>
//     상대 1체 — BT25_061/BT25_101/ST22_08/EX10_043 선례와 동형; linked-effect 소속 디스패치는 기존
//     design item C2-01(latent, ActivatedEffectResolver.cs 원장)이며 이 카드가 새로 발생시키는 갭이 아님.
//
// 치환(substrate translations only): IEnumerator→async Task, StartCoroutine(X)→await X;
// SelectPermanentEffect canTargetCondition는 Permanent-술어 직접 전달; `permanent == card.PermanentOfThisCard()` →
// `permanent.InstanceId == ICardEffect.ResolvePermanentOfThisCard(card).InstanceId`; `new IDegeneration(permanent,
// 1, activateClass)` → `new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId,
// cardEffect: activateClass)`(symbol_map_guide §2.5 canonical example).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT21.Black;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT21_059 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternative Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent) => targetPermanent.TopCard.EqualsTraits("Stnd.");

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Link Condition
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent) => targetPermanent.TopCard.EqualsTraits("Appmon");

            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 2, card: card));
        }
        #endregion

        #region App Fusion (Globemon & Charismon)
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.AddAppfuseMethodByName(new List<string>() { "Watchmon", "Savemon", "Calendamon" }, card));
        }
        #endregion

        #region Blocker
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Your Turn
        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("<De-Digivolve 1>", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() => "[Your Turn] [Once Per Turn] When this Digimon gets linked, <De-Digivolve 1> 1 of your opponent's Digimon.";

            bool PermanentCondition(Permanent permanent) =>
                permanent.InstanceId == ICardEffect.ResolvePermanentOfThisCard(card).InstanceId;

            bool CardCondition(CardSource source) => CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            bool IsOpponentsDigimon(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                CardEffectCommons.IsOwnerTurn(card) &&
                CardEffectCommons.CanTriggerWhenLinked(hashtable, PermanentCondition, CardCondition);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, IsOpponentsDigimon))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, IsOpponentsDigimon));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    async Task SelectDeDigivolvePermanent(Permanent permanent)
                    {
                        if (permanent != null)
                        {
                            await new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                        }
                    }

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOpponentsDigimon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectDeDigivolvePermanent,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        #region Link
        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }
        #endregion

        #region When Linking
        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("<De-Digivolve 1>", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription() => "[When Linking] <De-Digivolve 1> 1 of your opponent's Digimon.";

            bool IsOpponentsDigimon(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card) &&
                CardEffectCommons.HasMatchConditionOpponentsPermanent(card, IsOpponentsDigimon);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, IsOpponentsDigimon))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, IsOpponentsDigimon));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    async Task SelectDeDigivolvePermanent(Permanent permanent)
                    {
                        if (permanent != null)
                        {
                            await new IDegeneration(permanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                        }
                    }

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOpponentsDigimon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectDeDigivolvePermanent,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
