// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3A 정본 카드 (수확 트랜치) — ShineGreymon: Burst Mode (BT25_104, Digimon+Option 듀얼 / Red / Lv.7)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Red/BT25_104.cs (351 lines, 13 regions)
//    Digimon side:
//    * [None] Alt Digivolution      :16-26 (AddSelfDigivolutionRequirementStaticEffect: DATA SQUAD 위 Lv.6 코스트5)
//    * [None] Burst Digivolution    :28-79 (AddBurstDigivolutionConditionClass: Marcus Damon 바운스+ShineGreymon 위)
//    * [OnAllyAttack] Raid          :81-86 (RaidSelfEffect)
//    * [OnDetermineDoSecurityCheck] Piercing :88-93 (PierceSelfEffect)
//    * [None] Security Attack +1     :95-100 (ChangeSelfSAttackStaticEffect +1)
//    * [None] Blocker               :102-107 (BlockerSelfStaticEffect)
//    * [WhenPermanentWouldBeDeleted] Barrier :109-114 (BarrierSelfEffect)
//    * Shared WD/WA                 :116-140 (ActivateClassesForSharedEffects → ActivateMainOfOptionSide, once/turn)
//    * [Your Turn] Marcus=Digimon   :142-245 (OnMove/OnStartTurn/OnEnterField 백그라운드 그랜트 + None DP12000 + Rush)
//    Option side:
//    * [None] Ignore Colour Req     :251-261 (UseRequirements: DATA SQUAD)
//    * [OptionSkill] Main           :263-337 (상대 1체 -15000 + 손패 테이머 1장 무료 플레이)
//    * [None] Arts Digivolution     :339-344 (ArtsDigivolveEffect)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #1, 14축):
//    K:ArtsDigivolve, K:Raid, K:Rush, P:ActivateClassesForSharedEffects, P:ActivateMainOfOptionSide,
//    P:AddBurstDigivolutionConditionClass, P:ChangeBaseDPClass, P:RushClass, P:StartOfYourTurnClass,
//    P:TreatAsDigimonClass, P:UseRequirements, P:WhenDigivolvingClass, P:WhenMovingClass, X:BurstDigivolution
//    (+ Pierce/Blocker/Barrier/ChangeSAttack factory, SelectHandEffect PlayForFree, ChangeDigimonDP).
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * 정적 키워드(Blocker/Barrier/SAttack/Rush/DP/UseRequirements) → 각 KeyWord/factory 상시(EX11_074 관례).
//    * Shared WD/WA → ActivateClassesForSharedEffects(whenDigivolving+whenAttacking, maxCountPerTurn:1,
//      hashValue) 그대로(AS-IS :129-138) — 공유 hashValue 모델 1:1.
//    * [Your Turn] Marcus-그랜트 → OnMove/OnStartTurn/OnEnterFieldAnyone 각 background 클래스(AS-IS 그대로);
//      SetIsBackgroundProcess(true) 유지.
//
// 수확(적중/빗나감 — coverage_exemplar_audit §6):
//    * 예측 BUSTED: 공유 once-per-turn(P2-9 ActivateClassesForSharedEffects) STOP 예측은 순수 디스패처 확인으로
//      BUSTED; Burst 조건 등재/게이트도 클린(consumed at CardController.BurstDigivolutionConditionOf).
//    * 예측 적중(잔존 STOP): (a) [Arts Digivolution]는 ArtsDigivolveEffect의 지연 STOP(RD-P6C2-10;
//      CanPlayCardTargetFrame/PermanentFrame 미이관) — 팩토리 호출/등재는 클린(throw는 CanResolve/Resolution
//      클로저 내부로 지연), Arts 해소 시도만 STOP. (b) [Burst Digivolution] EXECUTION은 별개 엔진 STOP
//      (RD-P6C1-6; SelectBurstDigivolutionEffect/CardController burst-play) — 본 트랜치 witness 미구동.
//    * 좁은 미이관 표면(RD-EXT3-04): tamerCondition의 `permanent.CannotReturnToHand(null)`는 이제 Permanent-레벨
//      aggregate 스캔(Permanent.cs) 착지로 1:1 FLIP됨. 잔여 표면 = [Your Turn] DP arm의
//      `SetActivatedTime(Owner.TurnStartTime, card.ChangedLocationTime)` (두 타임스탬프 소스 멤버 미이관 — DP-set
//      fold 순서 tie-break용, DateTime.Now 기반이라 결정론 위반; 충돌 소비자 없어 order-0 default 유지). 아래 본문 주석 참조.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `permanent.TopCard.Owner`(AS-IS Player) → HeadlessPlayerId; `.GetBattleAreaPermanents()`는 HeadlessPlayerId
//      확장(Player.cs:768) 직접; `.GetFieldPermanents()`는 Player 인스턴스 전용 → `new Player(...).GetFieldPermanents()`.
//    * `card.Owner.UntilEachTurnEndEffects` → `new Player(card.Context, card.Owner).UntilEachTurnEndEffects`.
//    * SelectPermanentEffect canTargetCondition = id-형 → PermanentOf(id) 어댑터(BT19_091 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Red;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_104 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region Digimon Effects

        #region Alt Digivolution
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return permanent.TopCard.EqualsTraits("DATA SQUAD");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(PermanentCondition, 5, true, card, null, level: 6));
        }
        #endregion

        #region Burst Digivolution
        if (timing == EffectTiming.None)
        {
            AddBurstDigivolutionConditionClass addBurstDigivolutionConditionClass = new AddBurstDigivolutionConditionClass();
            addBurstDigivolutionConditionClass.SetUpICardEffect($"Burst Digivolution", CanUseCondition, card);
            addBurstDigivolutionConditionClass.SetUpAddBurstDigivolutionConditionClass(getBurstDigivolutionCondition: GetBurstDigivolution);
            addBurstDigivolutionConditionClass.SetNotShowUI(true);
            cardEffects.Add(addBurstDigivolutionConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            BurstDigivolutionCondition? GetBurstDigivolution(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    bool tamerCondition(Permanent permanent)
                    {
                        // AS-IS :48-53: all terms preserved 1:1 (RD-EXT3-04 FLIP). The former STOP on
                        // `!permanent.CannotReturnToHand(null)` is retired now that Permanent exposes the aggregate
                        // ICannotReturnToHand scan (Permanent.cs). Substrate: `permanent.TopCard.Owner`(Player) →
                        // HeadlessPlayerId; `.GetBattleAreaPermanents()` = the HeadlessPlayerId extension (Player.cs).
                        return permanent != null
                            && permanent.TopCard != null
                            && permanent.TopCard.Owner == card.Owner
                            && permanent.TopCard.Owner.GetBattleAreaPermanents().Contains(permanent)
                            && !permanent.CannotReturnToHand(null)
                            && permanent.TopCard.EqualsCardName("Marcus Damon");
                    }

                    bool digimonCondition(Permanent permanent)
                    {
                        return permanent != null
                            && permanent.TopCard != null
                            && permanent.TopCard.Owner == card.Owner
                            && new Player(card.Context, permanent.TopCard.Owner).GetFieldPermanents().Contains(permanent)
                            && !card.CanNotEvolve(permanent)
                            && permanent.TopCard.EqualsCardName("ShineGreymon");
                    }

                    BurstDigivolutionCondition burstDigivolutionCondition = new BurstDigivolutionCondition(
                        tamerCondition: tamerCondition,
                        selectTamerMessage: "1 [Marcus Damon]",
                        digimonCondition: digimonCondition,
                        selectDigimonMessage: "1 [ShineGreymon]",
                        cost: 0);

                    return burstDigivolutionCondition;
                }

                return null;
            }
        }
        #endregion

        #region Raid
        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.RaidSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Piercing
        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Security Attack +1
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Blocker
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Barrier
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(false, card, null));
        }
        #endregion

        #region Shared WD / WA

        string SharedHashString = "BT25_104_WD_WA";

        string SharedEffectName = "As an option effect, 1 Enemy Digimon gets -15k DP, play 1 tamer.";

        string SharedEffectDescription(string tag) => $"[{tag}] [Once Per Turn] Activate 1 [Main] effect on this card's Option side.";

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            await CardEffectCommons.ActivateMainOfOptionSide(card, activateClass);
        }

        CardEffectFactory.ActivateClassesForSharedEffects
            (ref cardEffects, timing, card,
                SharedEffectName,
                SharedActivateCoroutine,
                SharedEffectDescription,
                optional: false,
                maxCountPerTurn: 1,
                hashValue: SharedHashString,
                whenDigivolving: true,
                whenAttacking: true);

        #endregion

        #region Your Turn

        //All your Marcus Damon
        bool IsMarcusDamon(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card)
                && permanent.TopCard.EqualsCardName("Marcus Damon");
        }

        bool MakeDigimonCondition()
        {
            return CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card);
        }

        async Task MarcusActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            #region are also treated as Digimon
            TreatAsDigimonClass treatAsDigimonClass = CardEffectFactory.TreatAsDigimonStaticEffect(
                permanentCondition: IsMarcusDamon,
                isInheritedEffect: false,
                card: card,
                condition: MakeDigimonCondition);//Granted effect continues checking that Burst mode is on field and is idempotent, so does not have to be remove when it leaves play

            ICardEffect? GetEffect(EffectTiming timing)
            {
                if (timing == EffectTiming.None)
                {
                    return treatAsDigimonClass;
                }
                return null;
            }

            new Player(card.Context, card.Owner).UntilEachTurnEndEffects.Add(GetEffect);
            #endregion

            await Task.CompletedTask;
        }

        if (timing == EffectTiming.OnMove) // For the gigachad that evoes into it in raising
        {
            ActivateClass activateClass = CardEffectFactory.WhenMovingClass(
                card,
                "[Marcus Damon] are treated as Digimon",
                MarcusActivateCoroutine,
                "[Marcus Damon] are treated as Digimon",
                false
            );
            activateClass.SetIsBackgroundProcess(true);//Run in background at start of your turn to add effect to player
            cardEffects.Add(activateClass);
        }

        if (timing == EffectTiming.OnStartTurn)
        {
            ActivateClass activateClass = CardEffectFactory.StartOfYourTurnClass(
                card,
                "[Marcus Damon] are treated as Digimon",
                MarcusActivateCoroutine,
                "[Marcus Damon] are treated as Digimon",
                false
            );
            activateClass.SetIsBackgroundProcess(true);//Run in background at start of your turn to add effect to player
            cardEffects.Add(activateClass);
        }

        //To anyone copying this effect: If this were not a dual card it would also need a matching On Play effect
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = CardEffectFactory.WhenDigivolvingClass(
                card,
                "[Marcus Damon] are treated as Digimon",
                MarcusActivateCoroutine,
                "[Marcus Damon] are treated as Digimon",
                false
            );
            activateClass.SetIsBackgroundProcess(true);//Run in background when digivolve into this to add effect to player
            cardEffects.Add(activateClass);
        }

        if (timing == EffectTiming.None)
        {
            #region with 12000 DP
            ChangeBaseDPClass changeBaseDPClass = CardEffectFactory.ChangeBaseDPGlobalEffect(
                permanentCondition: IsMarcusDamon,
                changeValue: 12000,
                isInheritedEffect: false,
                card: card,
                condition: MakeDigimonCondition);
            // AS-IS :230 `changeBaseDPClass.SetActivatedTime(card.Owner.TurnStartTime, card.ChangedLocationTime)`
            // — `Player.TurnStartTime` and `CardSource.ChangedLocationTime` have no mirror (no per-turn/per-card
            // DateTime clock ported). ActivatedTime governs the DP-"set" fold order (Permanent.cs:314
            // OrderBy(ActivatedTime)); leaving it at the default DateTime.MinValue is the mirror's documented
            // order-0 analog (ModifierHelpers.cs:118). Narrow activation-order fidelity gap — design item
            // RD-EXT3-04 (the DP change itself applies; only its tie-break timestamp is unstamped).

            cardEffects.Add(changeBaseDPClass);
            #endregion

            #region and gain Rush
            RushClass rushClass = CardEffectFactory.RushStaticEffect(
                permanentCondition: IsMarcusDamon,
                isInheritedEffect: false,
                card: card,
                condition: MakeDigimonCondition);

            cardEffects.Add(rushClass);
            #endregion
        }
        #endregion

        #endregion

        #region Option Effects

        #region Ignore Colour Requirement
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.UseRequirements(card, CardCondition));

            bool CardCondition(CardSource cardSource)
            {
                return cardSource.EqualsTraits("DATA SQUAD");
            }
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(SharedEffectName, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
                => "[Main] 1 of your opponent's Digimon gets -15000 DP for the turn. Then, you may play 1 Tamer card from your hand without paying the cost.";

            bool CanUseCondition(Hashtable hashtable)
                => CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);

            bool CanSelectDPMinusPermanentCondition(Permanent permanent)
                => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanSelectDPMinusPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectDPMinusPermanentCondition(permanent);
            }

            bool CanSelectTamerCondition(CardSource cardSource)
            {
                return cardSource.IsTamer
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, false, activateClass);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, CanSelectDPMinusPermanentById))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectDPMinusPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP -15000.", "The opponent is selecting 1 Digimon that will get DP -15000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: -15000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                    }
                }

                if (CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectTamerCondition))
                {
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectTamerCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.PlayForFree,
                        cardEffect: activateClass);

                    await selectHandEffect.Activate();
                }
            }
        }
        #endregion

        #region Arts Digivolution
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ArtsDigivolveEffect(card));
        }
        #endregion

        #endregion

        return cardEffects;
    }
}
