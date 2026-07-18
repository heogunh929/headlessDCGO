// Source: DCGO/Assets/Scripts/CardEffect/BT25/Red/BT25_019.cs (1:1 mirror) — "UltimateBrachiomon".
//
// S6 롱테일 트랜치 — 감사 시절 ⚠STOP-예상(공유 OP/WD 디스패처)이었으나 표면 실존 확인(BT25_040/BT25_104
// EXEMPLAR-T3A 정본으로 CardEffectFactory.ActivateClassesForSharedEffects가 순수 디스패처임이 이미 실증됨).
//   * [None] — AddSelfDigivolutionRequirementStaticEffect(TS/Dinosaur 위 Lv.5 코스트4) + RebootSelfStaticEffect
//     + BlockerSelfStaticEffect — 전부 미러 1:1 키워드 팩토리.
//   * Shared OP/WD — ActivateClassesForSharedEffects(onPlay+whenDigivolving): 상대 최고DP 1체 삭제.
//   * [End of Your Turn] — 상대 메모리 5 이상이면 디지몬효과면역, 5 이하면 옵션효과면역
//     (thisPermanent.UntilOpponentTurnEndEffects.Add(...PermanentEffectFactory.DigimonEffectImmunity/
//     OptionEffectImmunity...) — 둘 다 AS-IS 상등 임계값이라 5일 때 둘 다 발동, AS-IS 원문 그대로 보존).
//
// 치환(substrate translations only): IEnumerator→async Task, StartCoroutine(X)→await X; id-adapter
// (SelectPermanentEffect.canTargetCondition = Func<HeadlessEntityId,bool>, BT25_040 idiom); `card.Owner.Enemy`
// → `new Player(card.Context, card.Owner).Enemy!`(non-null, 2인 게임); `card.PermanentOfThisCard()` →
// `ICardEffect.ResolvePermanentOfThisCard(card)`; `GManager.instance.GetComponent<Effects>().CreateBuffEffect(...)`
// = UI (stripped, 가드 프레디케이트 없음이라 보존할 것 없음).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Red;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_019 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region Static Effects

        #region Alt Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent) =>
                targetPermanent.TopCard.EqualsTraits("TS") || targetPermanent.TopCard.EqualsTraits("Dinosaur");

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(level: 5, permanentCondition: PermanentCondition, digivolutionCost: 4, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Reboot
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Blocker
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #endregion

        #region OP/WD Shared
        string SharedEffectName = "Delete 1 Digimon with highest DP.";
        string SharedEffectDescription(string tag) => $"[{tag}] Delete 1 of your opponent's highest DP Digimon.";

        bool CanSelectPermamentCondition(Permanent permanent) =>
            CardEffectCommons.IsMaxDP(permanent, CardEffectCommons.OpponentOf(card), null);

        bool CanSelectPermanentById(HeadlessEntityId id)
        {
            Permanent? permanent = PermanentOf(id);
            return permanent is not null && CanSelectPermamentCondition(permanent);
        }

        async Task SharedActivateCoroutine(Hashtable _hashtable, ActivateClass activateClass)
        {
            if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, CanSelectPermanentById))
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOpponentsPermanentCount(card, CanSelectPermamentCondition));

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 highest DP Digimon to delete.", "Opponent is select 1 highest DP digimon to delete.");
                await selectPermanentEffect.Activate();
            }
        }

        CardEffectFactory.ActivateClassesForSharedEffects
        (ref cardEffects, timing, card,
            SharedEffectName,
            SharedActivateCoroutine,
            SharedEffectDescription,
            optional: false,
            maxCountPerTurn: -1,
            onPlay: true,
            whenDigivolving: true);

        #endregion

        #region End of Your Turn
        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("If opponent is at 5 or more memory, this gains immune to digimon effects, If opponent is at 5 or less memory, this gains immune to option effects.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("BT25_019_EndOfYourTurn");
            cardEffects.Add(activateClass);

            string EffectDescription() => "[End of Your Turn] [Once Per Turn] If your opponent has 5 or more memory, their Digimon effects don't affect this Digimon until their turn ends. Then, if they have 5 or less, their Option effects don't affect this Digimon until their turn ends.";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card) && CardEffectCommons.IsOwnerTurn(card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);
                int opponentMemory = new Player(card.Context, card.Owner).Enemy!.MemoryForPlayer;

                if (opponentMemory >= 5)
                {
                    #region Give Digimon Effect Immunity
                    thisPermanent.UntilOpponentTurnEndEffects.Add((_timing) => PermanentEffectFactory.DigimonEffectImmunity(thisPermanent));
                    // AS-IS `GManager.instance.GetComponent<Effects>().CreateBuffEffect(thisPermanent)` = UI (stripped).
                    #endregion
                }
                if (opponentMemory <= 5)
                {
                    #region Give Option Effect Immunity
                    thisPermanent.UntilOpponentTurnEndEffects.Add((_timing) => PermanentEffectFactory.OptionEffectImmunity(thisPermanent));
                    // AS-IS `GManager.instance.GetComponent<Effects>().CreateBuffEffect(thisPermanent)` = UI (stripped).
                    #endregion
                }

                await Task.CompletedTask;
            }
        }
        #endregion

        return cardEffects;
    }
}
