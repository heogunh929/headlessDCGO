// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [When Digivolving] mirrors
// EX8_074's "When Digivolving" region: ① suspend 1 Digimon (any owner, optional), then ② delete 1 of your
// opponent's Digimon whose DP <= 8000 + 3000 * (other suspended Digimon). The dynamic threshold is a closure
// in the delete target predicate — evaluated at the delete step, AFTER the suspend has applied (the mutation
// sink upserts immediately), so the count reflects the just-suspended Digimon. Used by tests/G9-009. Inert in
// actual play (no real card numbered "TfxWhenDigivolveDelete").
//
// F4 CUTOVER re-port (old-model ActivatedEffect+ActivatedSelectEffect -> new-model ActivateClass): SHARED
// INFRA (not an old-model-resolver-transaction probe like the other 8 retiring Tfx) — this fixture mirrors
// EX8_074's WhenDigivolving region, and EX8_074 (RD-R6-07 STOP) is a REAL card that will keep needing this
// exact suspend-then-dynamic-delete SHAPE validated. Established BT9_062 idiom: the two select steps are now
// driven inline via `GManager.instance.GetComponent<SelectPermanentEffect>().SetUp(...)` + `Activate()` (the
// same GManager component `ActivatedSelectEffect` wrapped internally), each as its own inline `ActivateClass`.
// The dynamic DP-threshold closure (DeletionMaxDp) is UNCHANGED — it re-evaluates
// MatchConditionPermanentCount fresh on every CanDelete/candidate-scan call, so it still reads the
// just-suspended state (SelectPermanentEffect's Mode.Tap batch mutates via the sink synchronously, and effect
// ① fully completes — including its Activate() await — before effect ②'s SetUp/Activate runs).
// The OptionSkill entry point (ReuseWhenDigivolvingEffect) is UNCHANGED: it is not an `ActivatedEffect`
// wrapper (it is its own dedicated marker kind class the resolver dispatches specially), so it is out of
// scope for this old-model -> new-model shell conversion.
//
// COMPANION CHANGE (documented, evidence for reviewers): tests/G9-009.WhenDigivolveDynamicDelete.Tests'
// `DeleteRequest` helper white-box-cast `((ActivatedSelectEffect)((ActivatedEffect)effects[1]).Body)` to reach
// into the delete step's ChoiceRequest for 3 of its 5 assertions — that cast necessarily breaks when
// effects[1] becomes an ActivateClass, so the helper was updated to call the SAME ChoiceRequest through the
// new-model surface (the mirror `SelectPermanentEffect` component, configured identically). No test
// assertion/expectation changed — same thresholds, same offered/not-offered outcomes.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class TfxWhenDigivolveDelete : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            // ① You may suspend 1 Digimon (any owner).
            bool CanSuspend(HeadlessEntityId id) => CardEffectCommons.IsBattleAreaDigimon(card, id);

            ActivateClass suspendActivateClass = new ActivateClass();
            suspendActivateClass.SetUpICardEffect("Suspend 1 Digimon.", CanUseConditionTrue, card);
            suspendActivateClass.SetUpActivateClass(null, ActivateSuspendCoroutine, -1, false, "Suspend 1 Digimon.");
            cardEffects.Add(suspendActivateClass);

            async Task ActivateSuspendCoroutine(Hashtable _hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSuspend,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Tap,
                    cardEffect: suspendActivateClass);

                await selectPermanentEffect.Activate();
            }

            // ② You may delete 1 of your opponent's Digimon with DP <= the dynamic threshold.
            int DeletionMaxDp() => CardEffectCommons.MaxDpDeleteThreshold(card,
                8000 + 3000 * CardEffectCommons.MatchConditionPermanentCount(card,
                    id => CardEffectCommons.IsBattleAreaDigimon(card, id)
                        && CardEffectCommons.IsSuspended(card, id)
                        && id != card.InstanceId));
            bool CanDelete(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.CurrentDp(card, id) <= DeletionMaxDp();

            ActivateClass deleteActivateClass = new ActivateClass();
            deleteActivateClass.SetUpICardEffect(
                "Delete 1 of your opponent's Digimon (DP threshold scales with suspended Digimon).",
                CanUseConditionTrue, card);
            deleteActivateClass.SetUpActivateClass(null, ActivateDeleteCoroutine, -1, false,
                "Delete 1 of your opponent's Digimon (DP threshold scales with suspended Digimon).");
            cardEffects.Add(deleteActivateClass);

            async Task ActivateDeleteCoroutine(Hashtable _hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanDelete,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: deleteActivateClass);

                await selectPermanentEffect.Activate();
            }

            // AS-IS canUse: null -> CanResolveUseHalf always true (ActivatedEffect.CanResolveUseHalf, :712-716).
            bool CanUseConditionTrue(Hashtable hashtable) => true;
        }

        // (이연③-b RETIRED) the OptionSkill branch that constructed `ReuseWhenDigivolvingEffect` was removed:
        // that mirror-invented "[All Turns] re-activate [When Digivolving]" marker is retired. The real card
        // EX8_074 delivers the re-activation through the AS-IS OnEnterFieldAnyone + play-window broadcast
        // (region #6), covered live by G9-012.LiveAllTurnsReactivation. This fixture keeps ONLY its
        // WhenDigivolving suspend+delete shape (shared infra for EX8_074), driven by G9-009 #1-#4.

        return cardEffects;
    }
}
