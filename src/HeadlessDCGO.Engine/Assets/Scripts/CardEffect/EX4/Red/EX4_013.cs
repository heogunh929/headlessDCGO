// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 포팅 카드 — EX4_013 (Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX4/Red/EX4_013.cs (382 lines, no region markers, 4 timing 블록)
//    * Ignore Battle          :13-31  (timing None — DontBattleSecurityDigimonClass, security-effect, self 한정)
//    * [Security]             :33-115 (SecuritySkill — play self w/o battle+cost, then OnEndTurn hand-bounce)
//    * [On Play]              :117-246(OnEnterFieldAnyone — delete opp Digimon <=6000DP; else suspend 1 + can't-unsuspend)
//    * [When Attacking]       :248-377(OnAllyAttack — 동일 delete/suspend 몸통)
//
// ② 프리미티브 매핑:
//    * P:DontBattleSecurityDigimonClass — [Ignore Battle] 상시(AS-IS :15-20)
//    * P:PlayPermanentCards(root Security, activateETB) — [Security] self 무배틀 플레이(AS-IS :68-74)
//    * P:AddEffectToPlayer(UntilEachTurnEnd, OnEndTurn) — 턴 종료 시 손패 반환 예약(AS-IS :83)
//    * P:HandBounceClaass.Bounce() — 손패 반환(AS-IS :110)
//    * E:SelectPermanentEffect Mode.Custom(삭제 대상 선정)/Mode.Tap(suspend) — [On Play]/[When Attacking]
//    * P:DeletePeremanentAndProcessAccordingToResult(failureProcess) — 삭제→실패 시 suspend 분기(AS-IS :208/339)
//    * P:GainCantUnsuspendNextActivePhase — suspend 대상 다음 액티브페이즈 미회복(AS-IS :237/368)
//
// ③ 배선 관례 근거:
//    * [Security] → SecuritySkill + CanTriggerSecurityEffect + SetIsSecurityEffect(true)(AS-IS :48/38).
//    * [Ignore Battle] → None + CanUseIgnoreBattle + SetIsSecurityEffect/SetNotShowUI(AS-IS :18-19/24).
//    * [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay. [When Attacking] → OnAllyAttack + CanTriggerOnAttack.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`;
//      inner IEnumerator local funcs(ActivateCoroutine1/AfterSelectPermanentCoroutine/FailureProcess)→async Task;
//      `yield return null;` 소거.
//    * `card.Owner.MaxDP_DeleteEffect(6000, activateClass)` → `new Player(card.Context, card.Owner)
//      .MaxDP_DeleteEffect(6000, activateClass)` (미러 Player 접근 idiom — BT9_009).
//    * `CardEffectCommons.HasMatchConditionPermanent(cond)`(구식, card 없음) →
//      `HasMatchConditionPermanent(card, cond)`; `MatchConditionPermanentCount(cond)` →
//      `MatchConditionPermanentCount(card, cond)` (미러는 컨텍스트 획득 위해 card 파라미터 필수 — 스캔 범위
//      양측 배틀에어리어로 AS-IS 동일).
//    * SelectPermanentEffect.SetUp canTargetCondition 은 Func<HeadlessEntityId,bool> 만 받으므로 Permanent 술어
//      (CanSelectPermanentCondition/...1)를 id 어댑터(PermanentOf/CanSelectPermanentById/...ById1)로 감쌈
//      (술어 뭉갬 금지 — BT9_009/LM_054 idiom).
//    * `card.PermanentOfThisCard()`(가변 필요) → `ICardEffect.ResolvePermanentOfThisCard(card)` (LM_054 관례).
//    * `new HandBounceClaass(list, CardEffectCommons.CardEffectHashtable(activateClass1)).Bounce()` →
//      `CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(list, activateClass1, success:null,
//      failure:null)` (확립된 HandBounceClaass 미러 — BT9_031 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX4.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX4_013 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            DontBattleSecurityDigimonClass dontBattleSecurityDigimonClass = new DontBattleSecurityDigimonClass();
            dontBattleSecurityDigimonClass.SetUpICardEffect("Ignore Battle", CanUseCondition, card);
            dontBattleSecurityDigimonClass.SetUpDontBattleSecurityDigimonClass(CardSourceCondition: CardSourceCondition);
            dontBattleSecurityDigimonClass.SetIsSecurityEffect(true);
            dontBattleSecurityDigimonClass.SetNotShowUI(true);
            cardEffects.Add(dontBattleSecurityDigimonClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanUseIgnoreBattle(hashtable, card);
            }

            bool CardSourceCondition(CardSource cardSource)
            {
                return cardSource == card;
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play this card without battling and return this Digimon to hand at the end of the turn", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Play this card without battling and without paying the cost. At the end of the turn, return this Digimon to your hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnExecutingArea(card))
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: card, payCost: false, cardEffect: activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: card, payCost: false, cardEffect: activateClass))
                {
                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: new List<CardSource>() { card },
                        activateClass: activateClass,
                        payCost: false,
                        isTapped: false,
                        root: SelectCardEffect.Root.Security,
                        activateETB: true);

                    if (CardEffectCommons.IsExistOnBattleArea(card))
                    {
                        Permanent? selectedPermanent = null;

                        ActivateClass activateClass1 = new ActivateClass();
                        activateClass1.SetUpICardEffect("Return the Digimon to hand", CanUseCondition1, card);
                        activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, "");
                        CardEffectCommons.AddEffectToPlayer(effectDuration: EffectDuration.UntilEachTurnEnd, card: card, cardEffect: activateClass1, timing: EffectTiming.OnEndTurn);

                        bool CanUseCondition1(Hashtable hashtable)
                        {
                            return true;
                        }

                        bool CanActivateCondition1(Hashtable hashtable)
                        {
                            selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                            if (selectedPermanent.TopCard != null)
                            {
                                if (!selectedPermanent.CannotReturnToHand(activateClass1))
                                {
                                    if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass1))
                                    {
                                        return true;
                                    }
                                }
                            }

                            return false;
                        }

                        async Task ActivateCoroutine1(Hashtable _hashtable1)
                        {
                            await CardEffectCommons.BouncePeremanentAndProcessAccordingToResult(
                                targetPermanents: new List<Permanent>() { selectedPermanent! },
                                activateClass: activateClass1,
                                successProcess: null,
                                failureProcess: null);
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon with 6000 DP or less, or suspend 1 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Delete 1 of your opponent's Digimon with 6000 DP or less. If no Digimon was deleted by this effect, suspend 1 of your opponent's Digimon, and that Digimon doesn't unsuspend during your opponent's next unsuspend phase.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(6000, activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectPermanentCondition1(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
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

            bool CanSelectPermanentById1(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition1(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }

                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> deleteTargetPermanents = new List<Permanent>();

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete.");

                    await selectPermanentEffect.Activate();

                    async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                    {
                        foreach (Permanent permanent in permanents)
                        {
                            deleteTargetPermanents.Add(permanent);
                        }
                    }
                }

                await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(targetPermanents: deleteTargetPermanents, activateClass: activateClass, successProcess: null, failureProcess: FailureProcess);

                async Task FailureProcess()
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                    {
                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition1));

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectPermanentById1,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: null,
                            afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                            mode: SelectPermanentEffect.Mode.Tap,
                            cardEffect: activateClass);

                        await selectPermanentEffect.Activate();

                        async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                        {
                            foreach (Permanent selectedPermanent in permanents)
                            {
                                await CardEffectCommons.GainCantUnsuspendNextActivePhase(
                                    targetPermanent: selectedPermanent,
                                    activateClass: activateClass
                                );
                            }
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon with 6000 DP or less, or suspend 1 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Delete 1 of your opponent's Digimon with 6000 DP or less. If no Digimon was deleted by this effect, suspend 1 of your opponent's Digimon, and that Digimon doesn't unsuspend during your opponent's next unsuspend phase.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(6000, activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectPermanentCondition1(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
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

            bool CanSelectPermanentById1(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition1(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }

                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> deleteTargetPermanents = new List<Permanent>();

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.", "The opponent is selecting 1 Digimon to delete.");

                    await selectPermanentEffect.Activate();

                    async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                    {
                        foreach (Permanent permanent in permanents)
                        {
                            deleteTargetPermanents.Add(permanent);
                        }
                    }
                }

                await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(targetPermanents: deleteTargetPermanents, activateClass: activateClass, successProcess: null, failureProcess: FailureProcess);

                async Task FailureProcess()
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition1))
                    {
                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition1));

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectPermanentById1,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: null,
                            afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                            mode: SelectPermanentEffect.Mode.Tap,
                            cardEffect: activateClass);

                        await selectPermanentEffect.Activate();

                        async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                        {
                            foreach (Permanent selectedPermanent in permanents)
                            {
                                await CardEffectCommons.GainCantUnsuspendNextActivePhase(
                                    targetPermanent: selectedPermanent,
                                    activateClass: activateClass
                                );
                            }
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
