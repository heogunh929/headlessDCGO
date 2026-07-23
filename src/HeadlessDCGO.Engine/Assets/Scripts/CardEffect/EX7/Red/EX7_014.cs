// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3A 정본 카드 (수확 트랜치) — Volcanicdramon (EX7_014, Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX7/Red/EX7_014.cs (318 lines, 4 regions)
//    * [On Play] OnEnterFieldAnyone     :13-73  (상대 최저 DP 디지몬 1체 삭제)
//    * [When Attacking] OnAllyAttack     :76-136 (상대 최저 DP 디지몬 1체 삭제)
//    * [When Digivolving] OnEnterFieldAnyone :139-226 (상대 6000DP↓ 플레이/이동 불가 — CanNotMove + CanNotPutField)
//    * [All Turns] WhenRemoveField        :229-313 (once/turn: 이 디지몬이 자기 효과 외로 필드 이탈 시 [Machine/Sky Dragon] 플레이)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #29, 2축):
//    * P:CanNotMoveClass     — [When Digivolving] 몸통 (AS-IS :177; ICanNotMoveEffect)
//    * P:CanNotPutFieldClass — [When Digivolving] 몸통 (AS-IS :182; ICanNotPutFieldEffect)
//    * (+IsMinDP delete, SelectPermanentEffect Destroy, PlayPermanentCards, CanTriggerWhenRemoveField)
//
// ③ 배선 관례 근거: [On Play]→OnEnterFieldAnyone+CanTriggerOnPlay; [When Attacking]→OnAllyAttack+CanTriggerOnAttack;
//    [When Digivolving]→EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone+CanTriggerWhenDigivolving,
//    게이트 유지); [All Turns]→WhenRemoveField+SetHashString+by-own-effect 제외.
//
// 수확 명세 (예측 부분 BUSTED — coverage_exemplar_audit §6 "CanNotPutFieldClass MISSING"):
//    감사 예측은 CanNotPutFieldClass MISSING이었으나 — 미러에 **완전 클래스 존재**(CardEffects/CanNotPutFieldClass.cs).
//    ▸ CanNotMoveClass: 생산 + **집행 클린** — ICanNotMoveEffect 스캔이 이동-경로에서 LIVE이며 플레이어-레벨
//      UntilOwnerTurnEndEffects를 읽음(HeadlessLegalActionDispatcher.cs:172-195 + Permanent.cs:2971; Player.cs:362 집계).
//    ▸ CanNotPutFieldClass (RD-EXT3-03, harvest): 생산자는 정상 빌드되며 CanEnterField 스캔이 이 생산자를
//      보게 되지만(CardSource.cs:408-442, UntilOwnerTurnEndEffects 포함) — **PlayCard 리걸-액션 경로에서
//      CanEnterField가 호출되지 않음**(PlayCardAction.Validate가 CanEnterField/CanNotPutField/
//      CanPlayFromHandDuringMainPhase 미호출). 즉 제약이 펌프 플레이 경로에서 **집행되지 않음(inert)**.
//      집행-배선 갭 — witness가 이 무집행을 정직 고정(우회 green 금지).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask.
//    * `card.Owner.Enemy`(AS-IS Player) → `new Player(card.Context, card.Owner).Enemy` (BT2_023 idiom);
//      `IsMinDP(perm, card.Owner.Enemy)` → `IsMinDP(perm, Enemy.PlayerId)` (미러 오버로드 HeadlessPlayerId).
//    * SelectPermanentEffect canTargetCondition = 정본 Func<Permanent,bool> → Permanent 술어 직결(id 어댑터 없음).
//    * `PlaySE`/`DebuffSE` = UI/SFX 연출 — 스트립.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX7.Red;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX7_014 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        HeadlessPlayerId EnemyId() => new Player(card.Context, card.Owner).Enemy!.PlayerId;

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Delete 1 of your opponent's Digimon with the lowest DP.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsMinDP(permanent, EnemyId());
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Delete 1 of your opponent's Digimon with the lowest DP.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsMinDP(permanent, EnemyId());
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        #region When Digivolving
        // AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving → 미러 EffectTiming.WhenDigivolving.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Opponent can't play or move Digimon with 6000 DP or less", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Your opponent can't play or move Digimon with 6000 DP or less until the end of their turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                CanNotMoveClass canNotMoveClass = new CanNotMoveClass();
                canNotMoveClass.SetUpICardEffect("Can't move Digimon with 6000 DP or less", CanUseCondition1, card);
                canNotMoveClass.SetUpCanNotMoveClass(cardCondition: CardCondition, cardEffectCondition: MoveCardEffectCondition);
                new Player(card.Context, card.Owner).Enemy!.UntilOwnerTurnEndEffects.Add((_timing) => canNotMoveClass);

                CanNotPutFieldClass canNotPutFieldClass = new CanNotPutFieldClass();
                canNotPutFieldClass.SetUpICardEffect("Can't play Digimon with 6000 DP or less", CanUseCondition1, card);
                canNotPutFieldClass.SetUpCanNotPutFieldClass(cardCondition: CardCondition, cardEffectCondition: CardEffectCondition);
                new Player(card.Context, card.Owner).Enemy!.UntilOwnerTurnEndEffects.Add((_timing) => canNotPutFieldClass);

                // AS-IS :187 `PlaySE(DebuffSE)` — SFX 연출, 스트립.

                bool CanUseCondition1(Hashtable hashtable)
                {
                    return true;
                }

                bool CardCondition(CardSource cardSource)
                {
                    if (cardSource.Owner == EnemyId())
                    {
                        if (cardSource.IsDigimon)
                        {
                            if (cardSource.CardDP <= 6000)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                bool MoveCardEffectCondition(ICardEffect cardEffect)
                {
                    return true;
                }

                bool CardEffectCondition(ICardEffect cardEffect)
                {
                    if (cardEffect == null)
                        return true;
                    else
                        return cardEffect.EffectSourceCard.Owner == EnemyId();
                }

                await Task.CompletedTask;
            }
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 Digimon card with the [Machine Dragon]/[Sky Dragon] trait", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("PlayDigimon_EX7_014");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All Turns] [Once Per Turn] When this Digimon would leave battle area other than by one of your effects, you may play 1 Digimon card with the [Machine Dragon]/[Sky Dragon] trait from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card))
                    {
                        // AS-IS :249 `IsByEffect(hashtable, cardEffect => IsOwnerEffect(cardEffect, card))` — 미러
                        // IsOwnerEffect는 (CardSource? effectSourceCard, CardSource) 시그니처 → cardEffect.EffectSourceCard.
                        if (!CardEffectCommons.IsByEffect(hashtable, cardEffect => CardEffectCommons.IsOwnerEffect(cardEffect.EffectSourceCard, card)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectCardToPlayFromHand(CardSource cardSource)
            {
                return CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass) &&
                        cardSource.IsDigimon &&
                        (cardSource.ContainsTraits("Machine Dragon") || cardSource.ContainsTraits("Sky Dragon"));
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                    CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardToPlayFromHand);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectHandEffect selectCardEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectCardEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectCardToPlayFromHand,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
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
                    root: SelectCardEffect.Root.Hand,
                    activateETB: true);
            }
        }
        #endregion

        return cardEffects;
    }
}
