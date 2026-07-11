namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (C-3) Digivolution-source trash protection (AS-IS <c>CardSource.CanNotTrashFromDigivolutionCards</c>,
/// CardSource.cs:2478-2520). A continuous <c>ICanNotTrashFromDigivolutionCardsEffect</c> registered on a field
/// permanent protects a MATCHING digivolution source from being trashed by an EFFECT. AS-IS scans every field /
/// player / self <c>EffectList(EffectTiming.None)</c> for such an effect and evaluates its joint predicate
/// <c>CanNotTrashFromDigivolutionCards(sourceBeingTrashed, causingEffect)</c>, gated by the effect's own
/// <c>CanUse(null)</c>. The headless port centralises the scan here; the effect-trash filter path
/// (<see cref="DigivolutionStackHelpers"/>) consults it, while the DELETION path (AS-IS
/// <c>Permanent.DiscardEvoRoots</c>, no keyword check) bypasses protection entirely (honorProtection: false).
///
/// The joint mirrors AS-IS <c>CanNotTrashFromDigivolutionCards(cardSource, cardEffect)</c> = CardCondition(source)
/// ∧ CardEffectCondition(effect) ∧ !source.IsFlipped, collapsed to
/// <c>Func&lt;CardSource sourceBeingTrashed, CardSource causingEffectSource, bool&gt;</c> (the AS-IS ICardEffect
/// second arg reduces to its source card, exactly as <see cref="ContinuousImmunityGate"/> collapses CanNotAffect).
/// The AS-IS legacy per-source stamp (<c>willBeRemoveSources</c>) DOES have real card producers — BT12_081:432 /
/// BT12_087 / P_224 / BT10_084 stamp cards they are about to play out of a stack so a concurrent trash skips
/// them; the headless <c>TrashProtectedKey</c> instance flag mirrors that stamp and is read alongside this scan
/// (design item C3-05: the AS-IS in-flight stamp inside the would-discard cut-in window of
/// <c>ITrashDigivolutionCards</c> itself is not yet surfaced). BT9_109 is the sole CONTINUOUS producer today.
/// </summary>
public static class TrashProtectionScan
{
    public const string Scope = "CanNotTrashFromDigivolution";

    // (joint-migration) canonical joint predicate — the AS-IS ICanNotTrashFromDigivolutionCardsEffect
    // .CanNotTrashFromDigivolutionCards(cardSource, cardEffect) shape: a single
    // Func<CardSource /*source being trashed*/, CardSource /*causing effect source*/, bool> evaluated by SCANNING
    // every field trash-protection effect (mirrors CardSource.CanNotTrashFromDigivolutionCards). Producers embed
    // the AS-IS CardCondition ∧ CardEffectCondition ∧ !IsFlipped conjunction into it.
    public const string JointPredicateKey = "joint.trashProtection";

    /// <summary>True when the digivolution source <paramref name="sourceBeingTrashedId"/> is protected from being
    /// trashed by the effect sourced from <paramref name="causingEffectSourceId"/> — any active
    /// trash-protection effect whose usable joint predicate matches (source, cause). Returns false when the
    /// registry / context is absent (registry-only unit mode, where no protection is registered), when the
    /// source cannot be resolved, or when there is no causing effect source (AS-IS <c>CardEffectCondition(null)</c>
    /// is false for every producer).</summary>
    public static bool IsProtected(
        IEffectQueryService? registry,
        ICardInstanceRepository repository,
        Bridge.EngineContext? context,
        HeadlessEntityId sourceBeingTrashedId,
        HeadlessEntityId causingEffectSourceId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (registry is null || context is null || sourceBeingTrashedId.IsEmpty || causingEffectSourceId.IsEmpty)
        {
            return false;
        }

        if (!repository.TryGetInstance(sourceBeingTrashedId, out CardInstanceRecord? source) || source is null ||
            !repository.TryGetInstance(causingEffectSourceId, out CardInstanceRecord? cause) || cause is null)
        {
            return false;
        }

        // (joint-migration) AS-IS CardSource.CanNotTrashFromDigivolutionCards: SCAN every field trash-protection
        // effect and evaluate the joint (source being trashed, causing effect source), gated by the effect's own
        // CanUse condition. Needs the EngineContext to build the CardSource views the joint predicate operates on.
        var trashedSource = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, sourceBeingTrashedId, source.OwnerId, source.OwnerId);
        var causeSource = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, causingEffectSourceId, cause.OwnerId, cause.OwnerId);
        foreach (EffectRequest request in registry.GetContinuousEffects(new EffectQueryContext(Scope)))
        {
            IReadOnlyDictionary<string, object?> values = request.Context.Values;
            if (!values.TryGetValue(JointPredicateKey, out object? jointRaw)
                || jointRaw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, Assets.Scripts.Script.CardEffectCommons.CardSource, bool> joint)
            {
                continue;
            }

            // (C-3 재상환 P1-C) AS-IS effect-list MEMBERSHIP (Permanent.EffectList_ForCard, Permanent.cs:1497-1546):
            // the field scan sees a card's effect only under the AS-IS stack-position rules — an INHERITED effect
            // only from a NON-TOP (tucked) source of a Digimon permanent, a non-inherited effect only from the TOP
            // card, and nothing at all from a flipped source or an off-field granter. Without this, a top-card
            // BT9_109 wrongly grants its (inherited-only) protection.
            if (!GranterMembershipHolds(repository, context, request))
            {
                continue;
            }

            // AS-IS cardEffect.CanUse(null): the protection effect's own condition gate (BT9_109 =
            // IsExistOnBattleArea(host)) — protection lapses the moment the granting host leaves the field.
            if (values.TryGetValue(Assets.Scripts.Script.CardEffectCommons.ContinuousSelfModifierEffect.ConditionKey, out object? condRaw)
                && condRaw is Func<bool> condition && !condition())
            {
                continue;
            }

            if (joint(trashedSource, causeSource))
            {
                return true;
            }
        }

        return false;
    }

    // AS-IS Permanent.EffectList(timing) = EffectList_ForCard(timing, TopCard) (Permanent.cs:1373-1376), whose
    // per-cardSource membership (Permanent.cs:1497-1546) is:
    //   ① a FLIPPED source contributes nothing (`if (!cardSource.IsFlipped)`);
    //   ② a NON-TOP source contributes only when the permanent IsDigimon;
    //   ③ an effect marked IsInheritedEffect is taken only from a NON-TOP source; the TOP card contributes only
    //     its non-inherited (and, when linked, linked) effects. No linked-effect producer exists for this scope,
    //     so the IsLinkedEffect branch has no headless mirror here (add it with its first witness).
    // The AS-IS scan population is player.GetFieldPermanents() (battle + breeding, Player.cs:665) — a granter
    // that is part of NO field permanent is never enumerated (the AS-IS "effects of itself" region applies to
    // the card BEING TRASHED, which as a stack source always has a permanent, so it never fires there). The
    // AS-IS PLAYER-effect region (player.EffectList, CardSource.cs:2500-2510) has no headless producer for this
    // scope; a future player-granted protection binding must carry a membership-bypass marker (add with its
    // first witness).
    private static bool GranterMembershipHolds(
        ICardInstanceRepository repository,
        Bridge.EngineContext context,
        EffectRequest request)
    {
        HeadlessEntityId granterId = request.Context.SourceEntityId;
        if (granterId.IsEmpty
            || !repository.TryGetInstance(granterId, out CardInstanceRecord? granter) || granter is null)
        {
            return false;
        }

        // ① AS-IS `!cardSource.IsFlipped` — a flipped granter's effects are skipped wholesale.
        if (granter.Metadata.TryGetValue("isFlipped", out object? flippedRaw) && flippedRaw is true)
        {
            return false;
        }

        bool isInherited =
            request.Context.Values.TryGetValue(Assets.Scripts.Script.CardEffectCommons.ContinuousSelfModifierEffect.InheritedEffectKey, out object? inheritedRaw)
            && inheritedRaw is true;

        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            foreach (ChoiceZone zone in FieldZones)
            {
                foreach (HeadlessEntityId top in zones.GetCards(player, zone))
                {
                    if (top == granterId)
                    {
                        // ③ TOP card: only NON-inherited effects reach the scan.
                        return !isInherited;
                    }

                    if (repository.TryGetInstance(top, out CardInstanceRecord? host) && host is not null
                        && ReadSourceIds(host.Metadata).Contains(granterId.Value, StringComparer.Ordinal))
                    {
                        // ②+③ tucked (non-top) source: only INHERITED effects, and only under a Digimon permanent.
                        return isInherited
                            && new Assets.Scripts.Script.CardEffectCommons.Permanent(context, top, player).IsDigimon;
                    }
                }
            }
        }

        return false;
    }

    // AS-IS GetFieldPermanents = battle area + breeding area frames (Player.cs:665-681).
    private static readonly ChoiceZone[] FieldZones = { ChoiceZone.BattleArea, ChoiceZone.BreedingArea };

    private static IReadOnlyList<string> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DigivolutionStackHelpers.SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
            ? ids.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
            : Array.Empty<string>();
}
