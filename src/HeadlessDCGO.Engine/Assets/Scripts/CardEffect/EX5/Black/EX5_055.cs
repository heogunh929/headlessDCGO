// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2B 정본 카드 — HeavyLeomon (EX5_055, Digimon / Black / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX5/Black/EX5_055.cs (346 lines, 5 regions)
//    * [None] alt-digivolve   :13-21  (AddSelfDigivolutionRequirementStaticEffect: [Leomon]Lv.5 위 코스트4)
//    * [None] Fortitude       :23-26  (FortitudeSelfEffect)
//    * [On Deletion]          :28-135 (OnDestroyedAnyone — <De-Digivolve 1> + 6000↓ 덱바운스)
//    * [When Digivolving]      :137-244(OnEnterFieldAnyone + CanTriggerWhenDigivolving — 동일 본체)
//    * [End of Attack]        :246-342(OnEndAttack — 4000↓ 덱바운스, 아니면 자기 unsuspend)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #26):
//    * K:Fortitude            — [None] FortitudeSelfEffect (AS-IS :25; CardEffectFactory KeyWord)
//    * P:DeckBouncePeremanentAndProcessAccordingToResult — [End of Attack] 몸통 (AS-IS :318-322)
//    * (+P:AddDigivolutionRequirementClass(=AddSelfDigivolutionRequirement) (AS-IS :20), IDegeneration(De-Digi),
//       IUnsuspendPermanents, E:SelectPermanentEffect Mode.Custom/PutLibraryBottom,
//       T:OnDestroyedAnyone/OnEndAttack, CanTriggerOnDeletion/CanActivateOnDeletion)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언 rule 3; AS-IS OnEnterFieldAnyone +
//      CanTriggerWhenDigivolving 게이트).
//    * [On Deletion] → OnDestroyedAnyone + CanTriggerOnDeletion/CanActivateOnDeletion(AS-IS :58-79).
//    * [End of Attack] self-스코프 → OnEndAttack + CanTriggerOnAttack(AS-IS :272-275, EoA 게이트 관례).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask (BT8_092 idiom).
//    * AS-IS :109/:218 `new IDegeneration(selectedPermanent, 1, activateClass)` → 미러 ctor
//      `(Permanent, count, causeEffectSourceId, cardEffect)` (EX8_051 idiom).
//    * AS-IS :337-339 `new IUnsuspendPermanents(list, activateClass)` → 미러 ctor `(list, cardEffect)` (ST2_11 idiom).
//    * AS-IS :318-322 `DeckBouncePeremanentAndProcessAccordingToResult(..., successProcess: SuccessProcess(),
//      failureProcess: null)` — 미러 bridge는 `Func<Task> successProcess` → 메서드-그룹 전달(SuccessProcess).
//    * SelectPermanentEffect.canTargetCondition는 id-형 — Permanent-술어에 PermanentOf(id) 어댑터 덧댐.
//    * `MatchConditionPermanentCount(cond)` → `MatchConditionPermanentCount(card, cond)` (미러 card-arg).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX5.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX5_055 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.ContainsCardName("Leomon") && targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 5;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 4, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Fortitude
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.FortitudeSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        // (BT17_026 관례) SelectPermanentEffect canTargetCondition는 id-형 — Permanent-술어에 id 어댑터를 덧댐.
        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region On Deletion
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("De-Digivolve 1 on 1 Digimon and return 1 Digimon to the bottom of deck", canUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Deletion] <De-Digivolve 1> 1 of your opponent's Digimon (Trash up to 1 card from the top of one of your opponent's Digimon. If it has no digivolution cards, or becomes a level 3 Digimon, you can't trash any more cards). Then, return 1 of their 6000 DP or lower Digimon to the bottom of the deck.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanSelectPermanentCondition1(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= 6000)
                    {
                        return true;
                    }
                }

                return false;
            }

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

            bool canUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanActivateOnDeletion(hashtable, card))
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
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to De-Digivolve.", "The opponent is selecting 1 Digimon to De-Digivolve.");
                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        await new IDegeneration(selectedPermanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                    }
                }

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
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        #region When Digivolving
        // ③ 미러 방언: AS-IS OnEnterFieldAnyone(:137) + CanTriggerWhenDigivolving → WhenDigivolving 전용 키.
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("De-Digivolve 1 on 1 Digimon and return 1 Digimon to the bottom of deck", canUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] <De-Digivolve 1> 1 of your opponent's Digimon (Trash up to 1 card from the top of one of your opponent's Digimon. If it has no digivolution cards, or becomes a level 3 Digimon, you can't trash any more cards). Then, return 1 of their 6000 DP or lower Digimon to the bottom of the deck.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanSelectPermanentCondition1(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= 6000)
                    {
                        return true;
                    }
                }

                return false;
            }

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

            bool canUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
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
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to De-Digivolve.", "The opponent is selecting 1 Digimon to De-Digivolve.");
                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        Permanent selectedPermanent = permanent;

                        await new IDegeneration(selectedPermanent, 1, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                    }
                }

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
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        #region End of Attack
        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 Digimon to the bottom of deck or unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("DeckBounceOrUnsuspend_EX5_055");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Attack] [Once Per Turn] Return 1 of your opponent's 4000 DP or lower Digimon to the bottom of the deck. If you didn't, unsuspend this Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DP <= 4000)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool returned = false;

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
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon to return to the bottom of deck.",
                        "The opponent is selecting 1 Digimon to return to the bottom of deck.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.DeckBouncePeremanentAndProcessAccordingToResult(
                            targetPermanents: new List<Permanent>() { permanent },
                            activateClass: activateClass,
                            successProcess: SuccessProcess,
                            failureProcess: null);

                        Task SuccessProcess()
                        {
                            returned = true;

                            return Task.CompletedTask;
                        }
                    }
                }

                if (!returned)
                {
                    // `card.PermanentOfThisCard()`(PermanentView) → `ICardEffect.ResolvePermanentOfThisCard(card)`
                    // (Permanent; BT19_091/ST16_14 idiom) — IUnsuspendPermanents는 List<Permanent>를 받음.
                    Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                    await new IUnsuspendPermanents(
                        new List<Permanent>() { selectedPermanent },
                        activateClass).Unsuspend();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
