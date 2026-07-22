// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Coverage-exemplar card — EX8_026 (Digimon / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX8/Blue/EX8_026.cs (5 regions)
//    * timing==None          : AddSelfDigivolutionRequirementStaticEffect ([DS] level 5 위 cost 3).
//    * timing==OnCounterTiming: BlastDigivolveEffect.
//    * [On Play] / [When Digivolving]: <De-Digivolve 1> + 코스트 7 이하 상대 디지몬 1체 덱 밑으로 반환.
//    * timing==None          : CanNotSuspendClass — 메모리≥1일 때 상대 디지몬 서스펜드 불가
//      (PRIMARY covered element: CanNotSuspendClass).
// ③ 배선(trigger-wiring): [When Digivolving]은 미러 방언 EffectTiming.WhenDigivolving 전용 키로 등록
//    (AS-IS는 OnEnterFieldAnyone+CanTriggerWhenDigivolving; DigivolveAction이 WhenDigivolving만 해소,
//     이중-키 등록 금지 — AD1_006/BT17_026 idiom). [On Play]는 OnEnterFieldAnyone+CanTriggerOnPlay 그대로.
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return StartCoroutine(X)`→`await X`.
//    * SelectPermanentEffect.canTargetCondition id-형 → AS-IS Permanent-술어에 id 어댑터(BT17_026 idiom).
//    * `new IDegeneration(perm, 1, activateClass).Degeneration()` → `new IDegeneration(perm, 1,
//      activateClass.EffectSourceCard?.InstanceId).Degeneration()` (미러 ctor).
//    * `card.Owner.MemoryForPlayer` → `new Player(card.Context, card.Owner).MemoryForPlayer`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_026 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("DS") && targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 5;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Blast Digivolve
        if (timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CardEffectFactory.BlastDigivolveEffect(card: card, condition: null));
        }
        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("<De-Digivolve 1>, Return 1 Digimon with 7 cost or less to bottom of deck ", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] [When Digivolving] <De-Digivolve 1> 1 of your opponent's Digimon. Then, return 1 of your opponent's Digimon with a play cost of 7 or less to the bottom of the deck.";
            }

            bool OpponentsDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool OpponentsDigimonToReturn(Permanent permanent)
            {
                return OpponentsDigimon(permanent) &&
                       permanent.TopCard.HasPlayCost && permanent.TopCard.GetCostItself <= 7;
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool OpponentsDigimonById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && OpponentsDigimon(permanent);
            }

            bool OpponentsDigimonToReturnById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && OpponentsDigimonToReturn(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionPermanent(card, OpponentsDigimon);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent? dedigivolveSelected = null;

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: OpponentsDigimonById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to De-Digivolve.", "The opponent is selecting 1 Digimon to De-Digivolve.");
                await selectPermanentEffect.Activate();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    dedigivolveSelected = permanent;
                    return Task.CompletedTask;
                }

                if (dedigivolveSelected != null)
                    await new IDegeneration(dedigivolveSelected, 1, activateClass.EffectSourceCard?.InstanceId).Degeneration();

                if (CardEffectCommons.HasMatchConditionPermanent(card, OpponentsDigimonToReturn))
                {
                    SelectPermanentEffect selectReturnEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectReturnEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: OpponentsDigimonToReturnById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                    cardEffect: activateClass);

                    selectReturnEffect.SetUpCustomMessage("Select 1 Digimon to return to bottom of deck.", "The opponent is selecting 1 Digimon to return to bottom of deck.");

                    await selectReturnEffect.Activate();
                }
            }
        }
        #endregion

        #region When Digivolving
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("<De-Digivolve 1>, Return 1 Digimon with 7 cost or less to bottom of deck ", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] <De-Digivolve 1> 1 of your opponent's Digimon. Then, return 1 of your opponent's Digimon with a play cost of 7 or less to the bottom of the deck.";
            }

            bool OpponentsDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool OpponentsDigimonToReturn(Permanent permanent)
            {
                return OpponentsDigimon(permanent) &&
                       permanent.TopCard.HasPlayCost && permanent.TopCard.GetCostItself <= 7;
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool OpponentsDigimonById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && OpponentsDigimon(permanent);
            }

            bool OpponentsDigimonToReturnById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && OpponentsDigimonToReturn(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card) &&
                       CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionPermanent(card, OpponentsDigimon);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent? dedigivolveSelected = null;

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: OpponentsDigimonById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to De-Digivolve.", "The opponent is selecting 1 Digimon to De-Digivolve.");
                await selectPermanentEffect.Activate();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    dedigivolveSelected = permanent;
                    return Task.CompletedTask;
                }

                if (dedigivolveSelected != null)
                    await new IDegeneration(dedigivolveSelected, 1, activateClass.EffectSourceCard?.InstanceId).Degeneration();

                if (CardEffectCommons.HasMatchConditionPermanent(card, OpponentsDigimonToReturn))
                {
                    SelectPermanentEffect selectReturnEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectReturnEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: OpponentsDigimonToReturnById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                    cardEffect: activateClass);

                    selectReturnEffect.SetUpCustomMessage("Select 1 Digimon to return to bottom of deck.", "The opponent is selecting 1 Digimon to return to bottom of deck.");

                    await selectReturnEffect.Activate();
                }
            }
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.None)
        {
            CanNotSuspendClass canNotSuspendClass = new CanNotSuspendClass();
            canNotSuspendClass.SetUpICardEffect("Opponents Digimon Can't Suspend", CanUseCondition, card);
            canNotSuspendClass.SetUpCanNotSuspendClass(PermanentCondition: CantSuspendCondition);
            cardEffects.Add(canNotSuspendClass);

            bool CantSuspendCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleArea(permanent, card))
                {
                    if (permanent.IsDigimon)
                    {
                        if (!permanent.TopCard.CanNotBeAffected(canNotSuspendClass))
                            return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    return new Player(card.Context, card.Owner).MemoryForPlayer >= 1;
                }

                return false;
            }
        }
        #endregion

        return cardEffects;
    }
}
