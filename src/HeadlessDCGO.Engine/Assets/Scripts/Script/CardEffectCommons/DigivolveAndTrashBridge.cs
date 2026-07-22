// (EFFECT-MODEL REBUILD / bridge W3) AS-IS-signature `Task` overloads for the digivolve / stack-trash /
// option-side / draw-discard mutation helpers whose AS-IS home is the monolith
// `DCGO/Assets/Scripts/Script/CardEffectCommons.cs` (sibling-partial convention, see
// ProcessAccordingToResultBridge.cs header):
//   - DigivolveIntoHandOrTrashCard                     (AS-IS :756, 342 card calls)
//   - DigivolveIntoExcecutingAreaCard                  (AS-IS :1106, 1 call — AS-IS spelling kept)
//   - TrashDigivolutionCardsAndProcessAccordingToResult (AS-IS :541, 9 calls — the same-named substrate
//     method is an INCOMPATIBLE top/bottom-count shape; built on DigivolutionStackHelpers.TrashSpecificSourcesAsync)
//   - TrashDigivolutionCardsFromTopOrBottom            (AS-IS :675, 121 calls — restores the dropped
//     `cardCondition` filter wrapper-side)
//   - ActivateMainOfOptionSide                         (AS-IS :733, 1 call — threads `afterMainEffect`)
//   - DrawAndDiscardCards                              (AS-IS :1408, 3 calls — restores the 4 dropped params)
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    #region Target permanent Digivolves into Digimon card from hand or trash (AS-IS CardEffectCommons.cs:756)

    /// <summary>(BRIDGE W3) AS-IS <c>DigivolveIntoHandOrTrashCard</c> — AS-IS-signature overload; the substrate
    /// overload (Script/CardEffectCommons.cs, "verbatim verified" DigivolveIntoZoneCoreAsync) is already
    /// param-for-param (cost tuples, requirement-ignore, optionality, success/failed branches); only
    /// <c>ICardEffect</c>→source-card and the AS-IS inert-coroutine references→<c>Func&lt;Task&gt;</c>
    /// factories (W2 convention) need bridging.</summary>
    public static async Task DigivolveIntoHandOrTrashCard(
        Permanent targetPermanent,
        Func<CardSource, bool> cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool> reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool> fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        bool isHand,
        ICardEffect activateClass,
        Func<Task> successProcess,
        bool ignoreSelection = false,
        IgnoreRequirement ignoreRequirements = IgnoreRequirement.None,
        Func<Task> failedProcess = null,
        bool isOptional = true)
    {
        // AS-IS guards (:772-775) — activateClass/EffectSourceCard null → silent no-op.
        if (activateClass?.EffectSourceCard == null)
        {
            return;
        }

        await DigivolveIntoHandOrTrashCard(
            targetPermanent, cardCondition, payCost, reduceCostTuple, fixedCostTuple,
            ignoreDigivolutionRequirementFixedCost, isHand, activateClass.EffectSourceCard,
            successProcess, ignoreSelection, ignoreRequirements, failedProcess, isOptional).ConfigureAwait(false);
    }

    #endregion

    #region Target permanent Digivolves into Digimon card execution area (AS-IS CardEffectCommons.cs:1106)

    /// <summary>(BRIDGE W3) AS-IS <c>DigivolveIntoExcecutingAreaCard</c> — AS-IS-signature overload; same
    /// clean delegation as <see cref="DigivolveIntoHandOrTrashCard"/> (the substrate already carries the
    /// single-candidate no-pick behaviour of the Execution-zone variant).</summary>
    public static async Task DigivolveIntoExcecutingAreaCard(
        Permanent targetPermanent,
        Func<CardSource, bool> cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool> reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool> fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        ICardEffect activateClass,
        Func<Task> successProcess,
        bool ignoreSelection = false,
        IgnoreRequirement ignoreRequirements = IgnoreRequirement.None)
    {
        if (activateClass?.EffectSourceCard == null)
        {
            return;
        }

        await DigivolveIntoExcecutingAreaCard(
            targetPermanent, cardCondition, payCost, reduceCostTuple, fixedCostTuple,
            ignoreDigivolutionRequirementFixedCost, activateClass.EffectSourceCard,
            successProcess, ignoreSelection, ignoreRequirements).ConfigureAwait(false);
    }

    #endregion

    #region Trash target digivolution cards, and the effect determines the result (AS-IS CardEffectCommons.cs:541)

    /// <summary>(BRIDGE W3) AS-IS <c>TrashDigivolutionCardsAndProcessAccordingToResult</c> — the arbitrary
    /// pre-selected <c>List&lt;CardSource&gt;</c> shape. The SAME-NAMED substrate method
    /// (Script/CardEffectCommons.cs) is a DIFFERENT, top/bottom-count shape (bridge-map ⚠️⚠️ "name collision,
    /// not a valid delegation target"); the true substrate for this shape is
    /// <c>DigivolutionStackHelpers.TrashSpecificSourcesAsync</c> (explicitly documented as the AS-IS
    /// <c>ITrashDigivolutionCards(permanent, selectedCards, …)</c> mirror — the same primitive
    /// <c>SelectTrashDigivolutionCards</c> already rides). The host-level gates AS-IS applies through
    /// ITrashDigivolutionCards (top card CanNotBeAffected + ImmuneFromStackTrashing) are applied via the same
    /// <c>IsHostStackTrashGated</c> the substrate's own direct-call mirrors use; per-card
    /// CanNotTrashFromDigivolutionCards protection is honoured inside TrashSpecificSourcesAsync. Success = any
    /// requested card actually trashed (AS-IS <c>Some(IsTrashed)</c>); the success payload (AS-IS
    /// <c>TrashedCards</c>) is reconstructed RD-W2-2-style from real state evidence — a requested card counts
    /// as trashed iff it was a source before, is no longer one after, and now sits in its owner's trash.</summary>
    public static async Task TrashDigivolutionCardsAndProcessAccordingToResult(
        Permanent targetPermanent, List<CardSource> targetDigivolutionCards, ICardEffect activateClass,
        Func<List<CardSource>, Task> successProcess, Func<Task> failureProcess)
    {
        CardSource sourceCard = activateClass?.EffectSourceCard;
        List<CardSource> targets = targetDigivolutionCards ?? new List<CardSource>();
        var trashedCards = new List<CardSource>();

        if (sourceCard != null && targetPermanent != null && !targetPermanent.InstanceId.IsEmpty &&
            targets.Count > 0 && !IsHostStackTrashGated(targetPermanent.InstanceId, sourceCard))
        {
            EngineContext context = sourceCard.Context;
            HashSet<HeadlessEntityId> beforeSources = targetPermanent.DigivolutionCards
                .Select(cs => cs.InstanceId).ToHashSet();

            int trashed = await Headless.Runtime.DigivolutionStackHelpers.TrashSpecificSourcesAsync(
                context.CardInstanceRepository, context.ZoneMover,
                targetPermanent.InstanceId,
                targets.Where(cs => cs != null).Select(cs => cs.InstanceId).ToList(),
                gameEventQueue: context.GameEventQueue,
                context: context,
                causingEffectSourceId: sourceCard.InstanceId).ConfigureAwait(false);

            if (trashed > 0)
            {
                HashSet<HeadlessEntityId> afterSources = targetPermanent.DigivolutionCards
                    .Select(cs => cs.InstanceId).ToHashSet();
                var zones = (IZoneStateReader)context.ZoneMover;
                trashedCards = targets
                    .Where(cs => cs != null
                        && beforeSources.Contains(cs.InstanceId)
                        && !afterSources.Contains(cs.InstanceId)
                        && zones.GetCards(cs.Owner, ChoiceZone.Trash).Contains(cs.InstanceId))
                    .ToList();
            }
        }

        if (trashedCards.Count > 0)
        {
            if (successProcess != null)
            {
                await successProcess(trashedCards).ConfigureAwait(false);
            }
        }
        else if (failureProcess != null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    #endregion

    #region Trash digivolution cards from top or bottom (AS-IS CardEffectCommons.cs:675)

    /// <summary>(BRIDGE W3) AS-IS <c>TrashDigivolutionCardsFromTopOrBottom</c> WITH the optional
    /// <paramref name="cardCondition"/> filter the substrate overload lacks (121 calls; ST24_06/10/11/12 pass a
    /// real filter — bridge-map "second-highest-priority gap"). Mirrors the AS-IS body: walk the digivolution
    /// cards from the top (or bottom), collect up to <paramref name="trashCount"/> cards passing
    /// <paramref name="cardCondition"/> (protected cards still occupy collection slots, exactly as AS-IS —
    /// protection is filtered afterwards inside the trash primitive), then trash that SPECIFIC list via
    /// <c>DigivolutionStackHelpers.TrashSpecificSourcesAsync</c>. Host gates (top card CanNotBeAffected /
    /// ImmuneFromStackTrashing — AS-IS :679/:681 + ITrashDigivolutionCards re-gate) via
    /// <c>IsHostStackTrashGated</c>; the AS-IS "no trashable source at all" pre-gate (:680) is
    /// result-equivalent to the primitive's per-card protection trashing nothing. ORDER NOTE: the mirror
    /// <c>Permanent.DigivolutionCards</c> lists sources BOTTOM→TOP (DigivolutionStack.UnderCards), the AS-IS
    /// list is TOP→BOTTOM — hence the reversal for <paramref name="isFromTop"/>.</summary>
    public static async Task TrashDigivolutionCardsFromTopOrBottom(
        Permanent targetPermanent, int trashCount, bool isFromTop, ICardEffect activateClass,
        Func<CardSource, bool> cardCondition = null)
    {
        // AS-IS guards (:677-683).
        CardSource sourceCard = activateClass?.EffectSourceCard;
        if (sourceCard == null || targetPermanent == null || targetPermanent.InstanceId.IsEmpty ||
            targetPermanent.TopCard == null || trashCount <= 0 ||
            IsHostStackTrashGated(targetPermanent.InstanceId, sourceCard))
        {
            return;
        }

        IReadOnlyList<CardSource> sources = targetPermanent.DigivolutionCards;   // bottom→top.
        IEnumerable<CardSource> walk = isFromTop ? sources.Reverse() : sources;

        var trashTargets = new List<HeadlessEntityId>();
        foreach (CardSource trashTargetCard in walk)
        {
            if (trashTargets.Count >= trashCount)
            {
                break;
            }

            if (cardCondition == null || cardCondition(trashTargetCard))
            {
                trashTargets.Add(trashTargetCard.InstanceId);
            }
        }

        if (trashTargets.Count == 0)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        await Headless.Runtime.DigivolutionStackHelpers.TrashSpecificSourcesAsync(
            context.CardInstanceRepository, context.ZoneMover,
            targetPermanent.InstanceId, trashTargets,
            gameEventQueue: context.GameEventQueue,
            context: context,
            causingEffectSourceId: sourceCard.InstanceId).ConfigureAwait(false);
    }

    #endregion

    #region Activate Main of Dual Cards Option Side (AS-IS CardEffectCommons.cs:733)

    /// <summary>(BRIDGE W3) AS-IS <c>ActivateMainOfOptionSide</c> — AS-IS-signature overload; the substrate
    /// resolves ONLY the [Main]-tagged OptionSkill effect (the AS-IS <c>OptionMainEffect(card)</c> filter),
    /// then the AS-IS <paramref name="afterMainEffect"/> follow-up runs with <paramref name="activateClass"/>
    /// (AS-IS: forwarded verbatim, never inspected by this method itself). AS-IS also stamps the resolved
    /// [Main] instance with <c>SetIsDigimonEffect(asEffectOfThisDigimon)</c>/<c>SetIsTamerEffect(false)</c>;
    /// the substrate resolver constructs the effect instance itself and exposes no hook to stamp it, so a
    /// <c>true</c> flag cannot be threaded — STOP (design item RD-W3-5) rather than a silent drop. The default
    /// (<c>false</c>) path's residual deviation (no explicit false-override on a factory-flagged instance) is
    /// recorded in the same design item; the single AS-IS card caller (BT25_104) uses the defaults.</summary>
    public static async Task ActivateMainOfOptionSide(
        CardSource card, ICardEffect activateClass, Func<ICardEffect, Task> afterMainEffect = null,
        bool asEffectOfThisDigimon = false)
    {
        if (asEffectOfThisDigimon)
        {
            throw new NotSupportedException(
                "ActivateMainOfOptionSide(asEffectOfThisDigimon: true) cannot stamp IsDigimonEffect on the " +
                "resolver-constructed [Main] instance — design item RD-W3-5 (STOP, strong model).");
        }

        if (card == null)
        {
            return;
        }

        await ActivateMainOfOptionSide(card, activateClass?.EffectSourceCard!).ConfigureAwait(false);

        if (afterMainEffect != null)
        {
            await afterMainEffect(activateClass).ConfigureAwait(false);
        }
    }

    #endregion

    #region Draw and discard (AS-IS CardEffectCommons.cs:1408)

    /// <summary>(BRIDGE W3) AS-IS <c>DrawAndDiscardCards</c> — AS-IS-signature overload restoring the four
    /// parameters the substrate overload drops: <paramref name="canTargetCondition_ByPreSelecetedList"/> /
    /// <paramref name="canEndSelectCondition"/> (the AS-IS SelectHandEffect advanced select semantics — same
    /// panel rules as the reveal bridges, see RevealLibrary.cs header) and
    /// <paramref name="afterSelectPermanentCoroutine"/> (runs with the actually-discarded cards after the
    /// discard resolves, mirroring SelectHandEffect's <c>_afterSelectCardCoroutine(_targetCards)</c> — invoked
    /// only when the select phase ran, i.e. ≥1 discardable candidate existed). <paramref name="card"/> is dead
    /// in the AS-IS body (never read) and <paramref name="isShowOpponent"/> is UI-only (opponent hand-reveal
    /// overlay + log gating) — both kept for signature fidelity, discarded. When none of the advanced params
    /// are supplied this delegates to the verified substrate overload unchanged.</summary>
    public static async Task DrawAndDiscardCards(
        (Player drawPlayer, Player trashPlayer) player,
        int drawAmount,
        int trashAmount,
        CardSource card,
        ICardEffect activateClass,
        Func<CardSource, bool> canTrashTargetCondition = null,
        Func<List<CardSource>, CardSource, bool> canTargetCondition_ByPreSelecetedList = null,
        Func<List<CardSource>, bool> canEndSelectCondition = null,
        bool canNoSelect = false,
        bool canEndNotMax = false,
        bool isShowOpponent = true,
        Func<List<CardSource>, Task> afterSelectPermanentCoroutine = null)
    {
        _ = card;             // dead in the AS-IS body.
        _ = isShowOpponent;   // UI-only.
        CardSource sourceCard = activateClass?.EffectSourceCard;
        if (sourceCard == null || player.drawPlayer == null || player.trashPlayer == null)
        {
            return;
        }

        if (canTargetCondition_ByPreSelecetedList == null && canEndSelectCondition == null &&
            afterSelectPermanentCoroutine == null)
        {
            await DrawAndDiscardCards(
                (player.drawPlayer.PlayerId, player.trashPlayer.PlayerId), drawAmount, trashAmount,
                sourceCard, canTrashTargetCondition, canNoSelect, canEndNotMax).ConfigureAwait(false);
            return;
        }

        EngineContext context = sourceCard.Context;

        // Draw half — the same DrawCards mutation the substrate overload stages (flushed before the discard
        // pool is read, AS-IS order: DrawClass completes first).
        if (drawAmount > 0)
        {
            var drawSink = NewSink(context);
            drawSink.Apply(new EffectMutation(
                MatchStateMutationSink.DrawCardsKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.PlayerIdKey] = player.drawPlayer.PlayerId.Value,
                    [MatchStateMutationSink.CountKey] = drawAmount,
                }));
            await drawSink.FlushAsync().ConfigureAwait(false);
        }

        // Discard half — AS-IS SelectHandEffect(Mode.Discard) semantics with the advanced params honoured.
        var zones = (IZoneStateReader)context.ZoneMover;
        HeadlessPlayerId trashPlayerId = player.trashPlayer.PlayerId;
        Func<CardSource, bool> canTarget = canTrashTargetCondition ?? (_ => true);
        List<CardSource> handPool = zones.GetCards(trashPlayerId, ChoiceZone.Hand)
            .Select(id => new CardSource(context, id, trashPlayerId, trashPlayerId))
            .ToList();
        int maxCount = Math.Min(trashAmount, handPool.Count(canTarget));
        if (maxCount < 1)
        {
            return;   // AS-IS: no discardable candidate -> the select phase (and its callback) never runs.
        }

        List<CardSource> selected = await SelectCardsFromRevealPoolAsync(
            context, trashPlayerId, handPool, canTarget,
            canTargetCondition_ByPreSelecetedList, canEndSelectCondition,
            maxCount, canNoSelect, canEndNotMax,
            $"Discard {maxCount} card(s).", ChoiceZone.Hand).ConfigureAwait(false);

        if (selected.Count > 0)
        {
            var discardSink = NewSink(context);
            foreach (CardSource cs in selected)
            {
                discardSink.Apply(new EffectMutation(
                    MatchStateMutationSink.TrashCardKind, sourceCard.InstanceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value,
                    }));
            }

            await discardSink.FlushAsync().ConfigureAwait(false);
        }

        if (afterSelectPermanentCoroutine != null)
        {
            await afterSelectPermanentCoroutine(selected).ConfigureAwait(false);
        }
    }

    #endregion
}
