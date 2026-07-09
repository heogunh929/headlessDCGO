namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (N-2 / D-A1·D-A2·D-A3) Bridges <see cref="ContinuousEffectEvaluator"/> DP output into battle DP
/// computation, mirroring the original <c>Permanent.GetDP</c> which rescans every field / public-security
/// / player continuous effect on each access. The static per-instance DP (printed DP combined with the
/// instance's own <c>dpModifiers</c>) is taken as the base, then continuous DP modifiers sourced from
/// OTHER cards are applied on top.
///
/// D-A3 — <c>ImmuneFromDPMinus</c>: this is modelled as a continuous DpReduction/Immune REPLACEMENT
/// (<see cref="ReplacementHelpers.ImmuneFromDpReduction"/>), not as a numeric modifier. When such a
/// replacement targets the card, DP-reducing continuous modifiers (negative <c>Dp</c> deltas) are
/// dropped before resolution, so reductions are prevented while positive buffs still apply.
///
/// Like <see cref="ContinuousRestrictionGate"/> and <see cref="BattleDeletionGate"/>, this queries the
/// continuous-role registry only, so it is a pure no-op (returns the base DP unchanged) until continuous
/// DP effects are registered by the Phase 4 card pool.
/// </summary>
public static class ContinuousDpGate
{
    /// <summary>Query scope used for continuous re-evaluation (shared with the other gates).</summary>
    public const string Scope = ContinuousRestrictionGate.Scope;

    /// <summary>
    /// Returns <paramref name="baseDp"/> adjusted by the continuous DP modifiers targeting
    /// <paramref name="cardId"/>, honouring DP-reduction immunity. With no registered continuous effects
    /// this returns the base unchanged.
    /// </summary>
    public static int ResolveDp(EngineContext context, HeadlessEntityId cardId, int baseDp)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return baseDp;
        }

        // F-5: fold in player-scope continuous effects (e.g. "your Digimon get +1000 DP") alongside the
        // card-targeted ones.
        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId);

        // (d-remediation) AS-IS Permanent.HasDP==false (IDontHaveDPEffect): a "has no DP" restriction overrides the
        // base DP and EVERY modifier — Permanent.DP returns the -1 no-DP sentinel outright.
        foreach (var effect in ContinuousScopeEvaluation.ApplicableEffects(context, Scope, cardId))
        {
            if (effect.Context.Values.TryGetValue(Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.DontHaveDpKey, out object? raw) && raw is true)
            {
                return Assets.Scripts.Script.CardEffectCommons.RestrictionHelpers.NoDpValue;
            }
        }

        IReadOnlyList<NumericModifier> modifiers = result.Modifiers;

        // (P1-DP-3) AS-IS gates EVERY ChangeDP/ChangeBaseDP candidate with !TopCard.CanNotBeAffected(cardEffect)
        // (Permanent.cs:229/257/534/567/595): a card immune to an opponent's effect does not receive that effect's
        // DP change. Drop any modifier whose SOURCE effect this card is immune to (a self-sourced modifier is never
        // an opponent effect, so BlocksOpponentEffect returns false and it is kept). This is the general immunity,
        // distinct from the DP-minus-specific ImmuneFromDPMinus filter below.
        modifiers = modifiers
            .Where(modifier => modifier.SourceEntityId is not { IsEmpty: false } source
                || !ContinuousImmunityGate.BlocksOpponentEffect(
                    context.EffectRegistry, context.CardInstanceRepository, cardId, source, context))
            .ToArray();

        // D-A3: DP-reduction immunity. AS-IS ImmuneFromDPMinus(permanent, cardEffect) is evaluated PER causing
        // effect — a card "immune to your OPPONENT's DP-minus" still takes its own controller's reduction. Collect
        // the immunity grants applicable to this card and drop a DP-reducing modifier only when some immunity's
        // causing-effect predicate matches that modifier's source (a null predicate = immune to ALL reductions,
        // the pre-existing blanket behaviour).
        IReadOnlyList<Func<CardSource, bool>?> immunities = CollectDpMinusImmunities(context, cardId);
        if (immunities.Count > 0)
        {
            modifiers = modifiers
                .Where(modifier => !(IsDpReduction(modifier) && DpMinusImmunityApplies(context, immunities, modifier)))
                .ToArray();
        }

        // (P1-DP-5) AS-IS folds the host's accumulated LinkedDP (from attached link cards) into DP between the
        // isUpDown and NotIsUpDown groups (`DP += LinkedDP`, Permanent.cs:639). Inject it as a synthetic Dp Add
        // whose ModifierOrder places it there; self-sourced positive add, not immunity/DP-minus filtered.
        int linkedDp = context.CardInstanceRepository.TryGetInstance(cardId, out var linkHost) && linkHost is not null
            ? LinkHelpers.ReadLinkedDp(linkHost.Metadata)
            : 0;
        if (linkedDp != 0)
        {
            modifiers = new List<NumericModifier>(modifiers)
            {
                NumericModifier.Add(ModifierHelpers.LinkedDpModifierId, NumericModifierMetric.Dp, linkedDp),
            };
        }

        // (M-5) Fold BASE-DP modifiers (AS-IS ChangeBaseDP / "origin DP is X") into the base first, THEN apply
        // current-DP modifiers on top — base-DP changes were previously registered but consumed by nothing.
        // (P0-DP-1) AS-IS clamps BaseDP to >=0 AFTER folding base-DP effects (Permanent.BaseDP getter:
        // `if (BaseDP < 0) BaseDP = 0`) and clamps the final DP to >=0 (Permanent.DP getter: `if (DP < 0) DP = 0`).
        // Both clamps are load-bearing: the intermediate BaseDP floor changes the value a later current-DP buff
        // adds onto (base 1000, -3000 base-minus, +2000 buff => AS-IS clamp(-2000)=0, +2000 = 2000; not -2000+2000=0).
        // The DontHaveDp (-1 NoDpValue) case returned earlier, so a 0-floor here never clobbers the no-DP sentinel.
        int effectiveBase = Math.Max(0, ModifierHelpers.Evaluate(
            new NumericModifierRequest(NumericModifierMetric.BaseDp, baseDp, modifiers, cardId)).FinalValue);

        return Math.Max(0, ModifierHelpers.ResolveDp(effectiveBase, modifiers, cardId).FinalValue);
    }

    // (P1-DP-4) AS-IS applies ImmuneFromDPMinus to BOTH current-DP and BASE-DP reductions (Permanent.cs:221-227
    // gates IChangeBaseDPEffect.IsMinusDP with ImmuneFromDPMinus, same as the current-DP path). A negative Add on
    // either metric is a "DP minus" in the headless model.
    private static bool IsDpReduction(NumericModifier modifier) =>
        (modifier.Metric == NumericModifierMetric.Dp || modifier.Metric == NumericModifierMetric.BaseDp) &&
        modifier.Mode == NumericModifierMode.Add &&
        modifier.Value < 0;

    /// <summary>The DP-minus immunity grants applicable to <paramref name="cardId"/>. Each entry is the grant's
    /// causing-effect predicate (AS-IS <c>cardEffectCondition</c>) over the reducing effect's source card, or
    /// <c>null</c> for an unconditional ("immune to all DP-minus") grant. Reads the same scoped continuous
    /// bindings the restriction sink uses, so self- and player-scope immunities are both honoured.</summary>
    private static IReadOnlyList<Func<CardSource, bool>?> CollectDpMinusImmunities(EngineContext context, HeadlessEntityId cardId)
    {
        var immunities = new List<Func<CardSource, bool>?>();
        foreach (EffectRequest effect in ContinuousScopeEvaluation.ApplicableEffects(context, Scope, cardId))
        {
            IReadOnlyDictionary<string, object?> values = effect.Context.Values;
            if (!(values.TryGetValue(ReplacementHelpers.ImmuneFromDpMinusKey, out object? raw) && raw is bool flag && flag))
            {
                continue;
            }

            immunities.Add(
                values.TryGetValue(RestrictionHelpers.CausingEffectPredicateKey, out object? predRaw)
                    && predRaw is Func<CardSource, bool> predicate
                        ? predicate
                        : null);
        }

        return immunities;
    }

    /// <summary>True when some immunity covers <paramref name="modifier"/>: a null-predicate grant covers every
    /// reduction; a predicate grant covers it only when the modifier's source card matches (a modifier with no
    /// distinct source — e.g. the card's own instance modifier — is never matched by a predicate grant).</summary>
    private static bool DpMinusImmunityApplies(EngineContext context, IReadOnlyList<Func<CardSource, bool>?> immunities, NumericModifier modifier)
    {
        CardSource? source = null;
        foreach (Func<CardSource, bool>? predicate in immunities)
        {
            if (predicate is null)
            {
                return true;
            }

            if (modifier.SourceEntityId is { IsEmpty: false } sourceId)
            {
                source ??= BuildSource(context, sourceId);
                if (predicate(source))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static CardSource BuildSource(EngineContext context, HeadlessEntityId sourceId)
    {
        HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? inst) && inst is not null
            ? inst.OwnerId
            : default;
        return new CardSource(context, sourceId, owner, owner);
    }
}
