namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (GR-005) Derives whether a card currently HAS a self-static keyword (Blocker / Jamming / Piercing) by
/// querying the EffectRegistry at read time — the same pull pattern that makes continuous MODIFIERS work
/// (<see cref="ContinuousModifierGate"/>, <see cref="ContinuousDpGate"/>).
///
/// Why this exists: a ported <c>&lt;Blocker&gt;</c> (etc.) is registered as a keyword binding in the
/// EffectRegistry when the card enters play. But its consumers (BlockTiming, BattleResolver, SecurityResolver)
/// historically read a per-instance METADATA flag (<c>hasBlocker</c>/<c>hasPiercing</c>/<c>preventBattleDeletion</c>)
/// that is only written when a keyword effect RESOLVES at its timing — which never happens for a self-static
/// in the live flow. So the flag stayed false and the keyword was inert in real games (confirmed: a blocker
/// on the field opened 0 block windows across 223 attacks). Querying the registry binding directly closes
/// that gap, leaving the metadata-flag path intact for keywords GRANTED by other cards' effects.
/// </summary>
public static class ContinuousKeywordGate
{
    public const string Blocker = "Blocker";
    public const string Jamming = "Jamming";
    public const string Piercing = "Piercing";
    // (B-group preemptive seal) same presence-flag pattern as Blocker — consumers read hasReboot/hasRush/
    // hasBlitz/hasRetaliation, which a self-static binding never sets. No card ports these as self-statics
    // yet (latent), so wiring the gate now means a future self-static just works.
    public const string Reboot = "Reboot";
    public const string Rush = "Rush";
    public const string Blitz = "Blitz";
    public const string Retaliation = "Retaliation";
    // (C-group preemptive seal) also presence-flag pattern (hasAlliance/hasOverclock/hasProgress/
    // hasArmorPurge/hasDecode/hasPartition). Latent (no self-static factory/card yet). Names match
    // KeywordBaseBatch2Factory.KeywordName — note "Armor Purge" has a space.
    public const string Alliance = "Alliance";
    public const string Overclock = "Overclock";
    public const string Progress = "Progress";
    public const string ArmorPurge = "Armor Purge";
    public const string Decode = "Decode";
    public const string Partition = "Partition";
    public const string Vortex = "Vortex"; // GR-006: end-of-turn effect-driven attack (opponent Digimon).
    // (K1) AS-IS IVortexCanAttackPlayersEffect — NOT a Vortex grant: a marker that lets a permanent that IS
    // resolving its Vortex attack choose the PLAYER as the target (Vortex.cs CanActivateVortex:
    // `|| PermanentHasVortexCanAttackPlayers(...)`). Evaluated once when the Vortex offer opens (mirrors the
    // AS-IS canAttackPlayers snapshot at VortexProcess start).
    public const string VortexCanAttackPlayers = "VortexCanAttackPlayers";
    // (PRIM-W2 preemptive seal) same presence-flag pattern — behaviour consumers currently read the metadata
    // flags hasRaid (RaidAttackSwitch) / hasCollision (BlockTiming) / hasFortitude·hasBarrier·hasEvade
    // (DeletionReplacementGate). Wiring the gate names now means a self-static grant is queryable via
    // HasKeyword (same bar as Alliance/Overclock); migrating those consumers to also read HasKeyword is a
    // per-gate follow-up (behaviour, not this grant primitive).
    public const string Raid = "Raid";
    public const string Barrier = "Barrier";
    public const string Collision = "Collision";
    public const string Fortitude = "Fortitude";
    public const string Evade = "Evade";
    public const string Save = "Save"; // (PRIM-W2) deletion-replacement keyword (hasSave, DeletionReplacementGate).
    // (PRIM-W3) presence-flag keywords: Decoy/Fragment/Scapegoat are deletion-replacements
    // (hasDecoy/hasFragment/hasScapegoat, DeletionReplacementGate); Iceclad/Execute have their own gates.
    public const string Iceclad = "Iceclad";
    public const string Decoy = "Decoy";
    public const string Fragment = "Fragment";
    public const string Execute = "Execute";
    public const string Scapegoat = "Scapegoat";
    // (PRIM-W3) MindLink: a Tamer↔Digimon link (tamer treated as a Digimon for certain effects). Grant is
    // live via HasKeyword; the tamer-as-Digimon behavior consumer migrates separately (latent).
    public const string MindLink = "MindLink";
    // (PRIM-W4) Ascension: a post-deletion response (hasAscension, DeletionReplacementGate) — place the
    // deleted card into the security stack.
    public const string Ascension = "Ascension";
    // (PRIM-W4) TreatAsDigimon: this card is also treated as a Digimon. Grant live via HasKeyword; the
    // card-type-aware consumers migrate separately (latent, same bar as MindLink).
    public const string TreatAsDigimon = "TreatAsDigimon";

    /// <summary>True if an active self-static <paramref name="keyword"/> binding in the registry is sourced
    /// from (or targets) <paramref name="cardId"/>.</summary>
    public static bool HasKeyword(EngineContext context, HeadlessEntityId cardId, string keyword)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (HasKeyword(context.EffectRegistry, cardId, keyword))
        {
            return true;
        }

        // (PRIM-W2) a PLAYER-SCOPE keyword grant ("your Digimon gain <Blocker>") applies to any of the scoped
        // player's cards (optionally CardType-narrowed). Additive over the direct self/target check.
        if (cardId.IsEmpty || string.IsNullOrWhiteSpace(keyword)
            || !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        CardRecord? card = context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) ? def : null;
        foreach (EffectBinding binding in context.EffectRegistry.GetKeywordEffects(keyword))
        {
            IReadOnlyDictionary<string, object?> values = binding.Request.Context.Values;
            if (values.TryGetValue(Effects.PlayerScopeContinuousHelpers.PlayerScopeKey, out object? scoped) && scoped is true
                && ReadPlayerScopeId(values) == instance.OwnerId.Value
                && Effects.PlayerScopeContinuousHelpers.ConditionMatches(values, card)
                && KeywordConditionPasses(values)
                && ScopePredicatePasses(context, values, cardId, instance.OwnerId))
            {
                return true;
            }
        }

        return false;
    }

    private static int ReadPlayerScopeId(IReadOnlyDictionary<string, object?> values) =>
        values.TryGetValue(Effects.PlayerScopeContinuousHelpers.ScopePlayerIdKey, out object? raw) && raw is int id ? id : -1;

    // (FR-P1) honour a player-scope keyword grant's arbitrary per-permanent predicate (permanentCondition).
    private static bool ScopePredicatePasses(EngineContext context, IReadOnlyDictionary<string, object?> values, HeadlessEntityId cardId, HeadlessPlayerId owner)
    {
        if (!values.TryGetValue(Effects.PlayerScopeContinuousHelpers.ScopePredicateKey, out object? raw)
            || raw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> predicate)
        {
            return true;
        }

        return predicate(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, owner, owner));
    }

    private static bool KeywordConditionPasses(IReadOnlyDictionary<string, object?> values) =>
        !values.TryGetValue("continuous.condition", out object? raw) || raw is not Func<bool> condition || condition();

    /// <summary>(K4) The AS-IS single chokepoint <c>Permanent.IsDigimon</c> (Permanent.cs:3438): a card is a
    /// Digimon when its printed CardType says so OR an active <c>ITreatAsDigimonEffect</c> accepts it —
    /// live evaluation of the TreatAsDigimon keyword (self or player-scope, predicate-aware). Card-type
    /// consumers (attack/block/battle/raid/security/CanMove) call this instead of a raw CardType check so
    /// "also treated as a Digimon" reaches every judgement, mirroring the original's one getter.</summary>
    public static bool IsDigimon(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!cardId.IsEmpty &&
            context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) && instance is not null &&
            context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) && definition is not null &&
            string.Equals(definition.CardType, "Digimon", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasKeyword(context, cardId, TreatAsDigimon);
    }

    /// <summary>(D1 Decoy) Binding-values key under which a self keyword grant stores its per-card
    /// <c>permanentCondition</c> (adapted to <c>Func&lt;CardSource,bool&gt;</c>) — e.g. Decoy's
    /// protected-target predicate ("Decoy ([Bagra Army])"). AS-IS evaluates it live against the OTHER
    /// permanent being protected (Decoy.cs CanSelectPermanentCondition), not the keyword holder.</summary>
    public const string PermanentConditionKey = "keyword.permanentCondition";

    /// <summary>(D1 Decoy) True when SOME active <paramref name="keyword"/> grant held by
    /// <paramref name="holderId"/> accepts <paramref name="subjectId"/> under its stored
    /// <see cref="PermanentConditionKey"/> predicate (a grant without a predicate accepts everything).
    /// Mirrors the AS-IS live evaluation of <c>permanentCondition(protected permanent)</c>. Context-less
    /// callers (the sink's defer decision, a documented safe superset) treat a stored predicate as passing;
    /// the context-aware choice paths re-evaluate strictly.</summary>
    public static bool KeywordGrantAcceptsSubject(
        EffectRegistry registry,
        HeadlessEntityId holderId,
        string keyword,
        HeadlessEntityId subjectId,
        HeadlessPlayerId subjectOwner,
        EngineContext? context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (holderId.IsEmpty || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        foreach (EffectBinding binding in registry.GetKeywordEffects(keyword))
        {
            EffectContext effectContext = binding.Request.Context;
            if (effectContext.SourceEntityId != holderId && !effectContext.TargetEntityIds.Contains(holderId))
            {
                continue;
            }

            if (!effectContext.Values.TryGetValue(PermanentConditionKey, out object? raw)
                || raw is not Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> predicate
                || context is null
                || predicate(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, subjectId, subjectOwner, subjectOwner)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Registry-only overload for consumers that hold an <see cref="EffectRegistry"/> but not the
    /// full <see cref="EngineContext"/> (e.g. DeletionReplacementGate's context-less resolution methods).</summary>
    public static bool HasKeyword(EffectRegistry registry, HeadlessEntityId cardId, string keyword)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (cardId.IsEmpty || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        foreach (EffectBinding binding in registry.GetKeywordEffects(keyword))
        {
            EffectContext effectContext = binding.Request.Context;
            // (K1) a PLAYER-SCOPE grant's SourceEntityId is the GRANTING card, not a holder — it resolves
            // only through the context-aware scoped path (scope player + predicate). Matching it here made
            // the source card "have" the keyword while bypassing the scope predicate entirely.
            if (effectContext.Values.TryGetValue(Effects.PlayerScopeContinuousHelpers.PlayerScopeKey, out object? scoped) && scoped is true)
            {
                continue;
            }

            if (effectContext.SourceEntityId == cardId || effectContext.TargetEntityIds.Contains(cardId))
            {
                return true;
            }
        }

        return false;
    }
}
