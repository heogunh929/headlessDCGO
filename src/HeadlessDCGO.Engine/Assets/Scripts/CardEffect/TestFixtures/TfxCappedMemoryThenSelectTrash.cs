// TEST FIXTURE (not a real card). TWO capped activated effects at the SAME timing: [0] a CAPPED non-interactive
// memory gain (+2 — an IMMEDIATELY-applied sink mutation) and [1] a CAPPED interactive hand-trash. Exercises the
// multi-effect suspend/replay of the resolution cycle (adversarial-review P0-3): effect [0] completes (memory
// applied immediately + its use staged), effect [1] suspends for the agent's selection — the resumed re-invocation
// must (a) NOT double-apply [0]'s memory (the CEntityUseCycle mutation replay journal skips the purely-immediate
// sink Apply), (b) NOT double-consume or cap-out [0]'s staged use against its own replay (CEntityUseCycle replay
// cursor), and (c) complete [1] normally.
// (uniform-사멸 flip) Re-written from the retired invented uniform `ActivatedEffect` + `MemoryBody`/`SelectTrashBody`
// to the literal AS-IS inline `new ActivateClass()` idiom: two effects on one card with SEPARATE [Once Per Turn]
// partitions = SetHashString("mem")/("trash") (AS-IS IsSameEffect HashString split, the ST16_11 pattern; unhashed
// same-card caps share one count). [0]'s memory rides the journaled context-bound sink Apply (the substrate's
// immediate-mutation replay guard this fixture pins — the same sink path the old MemoryBody used); [1] is the AS-IS
// SelectHandEffect(Mode.Discard) component flow. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxCappedMemoryThenSelectTrash : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            {
                ActivateClass activateClass = new ActivateClass();
                activateClass.SetUpICardEffect("Gain 2 memory", CanUseCondition, card);
                activateClass.SetUpActivateClass(null, MemoryCoroutine, 1, false, "[Once Per Turn] Gain 2 memory.");
                // Distinct cap partition per effect — this fixture models a SetHashString'd card (ST16_11 pattern):
                // its two [Once Per Turn] effects count separately (AS-IS IsSameEffect HashString partition).
                activateClass.SetHashString("mem");
                effects.Add(activateClass);

                async Task MemoryCoroutine(Hashtable hashtable)
                {
                    // The +2 memory is an IMMEDIATELY-applied mutation. It rides the context-bound (journaled)
                    // sink Apply so the deferred-choice REPLAY of this completed effect skips the re-application
                    // (CEntityUseCycle mutation journal — the substrate surface this fixture pins). Raw sink
                    // AddMemory amount is turn-player-relative; the fixture's owner is the turn player in its
                    // suites, so +2 == the AS-IS card idiom `card.Owner.AddMemory(2, activateClass)`.
                    EngineContext context = card.Context;
                    var sink = new MatchStateMutationSink(
                        context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
                        context.EffectRegistry, context.GameEventQueue, context: context);
                    sink.Apply(new EffectMutation(
                        MatchStateMutationSink.AddMemoryKind, card.InstanceId,
                        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = 2 }));
                    await sink.FlushAsync();
                }
            }

            {
                ActivateClass activateClass = new ActivateClass();
                activateClass.SetUpICardEffect("Trash 1 card in your hand", CanUseCondition, card);
                activateClass.SetUpActivateClass(null, TrashCoroutine, 1, false, "[Once Per Turn] Trash 1 card in your hand.");
                activateClass.SetHashString("trash");
                effects.Add(activateClass);

                async Task TrashCoroutine(Hashtable hashtable)
                {
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: static _ => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        isShowOpponent: false,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Discard,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("trash 1 hand card", "The opponent is selecting 1 card to trash.");

                    await selectHandEffect.Activate();
                }
            }
        }

        return effects;
    }
}
