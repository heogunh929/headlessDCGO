namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (E-3 / RD-2) AS-IS <c>CardSource.CanNotPlayThisOption</c> (CardSource.cs:184-248): an Option cannot be
/// played while ANY active <c>ICanNotPlayCardEffect</c> forbids it. AS-IS scans THREE effect-list populations
/// — for each it takes <c>cardEffect is ICanNotPlayCardEffect &amp;&amp; cardEffect.CanUse(null) &amp;&amp;
/// ((ICanNotPlayCardEffect)cardEffect).CanNotPlay(this)</c> against the option being played (<c>this</c>):
/// <list type="number">
/// <item>every player's <c>player.EffectList(None)</c> — PLAYER-scope grants (e.g. EX1_072's duration-bound
/// "opponent can't use Option" registered via AddEffectToPlayer);</item>
/// <item>every field permanent's <c>EffectList(None)</c> — FIELD-scope statics (e.g. BT8_057 "[All Turns]
/// while all your Digimon are suspended on the opponent's turn, they can't use Option");</item>
/// <item>when the option is not itself a permanent (<c>PermanentOfThisCard() == null</c>) — the option's OWN
/// <c>EffectList(None)</c> (an option that forbids its own play).</item>
/// </list>
/// AS-IS then also returns true when <c>!MatchColorRequirement</c>; that half is the separately-wired
/// <see cref="OptionColorRequirement"/> gate (kept distinct so both surface their own reason), so THIS scan
/// covers only regions ①②③. Mirrors the live joint-scan substrate: producers embed
/// the AS-IS <c>CanNotPlay</c> = <c>CardCondition(option)</c> into a single
/// <c>Func&lt;CardSource /*option being played*/, bool&gt;</c>, gated by the effect's own CanUse
/// (<see cref="ContinuousScopeEvaluation.ConditionKey"/>). Field-scope bindings honour the AS-IS stack-position
/// membership (<see cref="ContinuousFieldMembership"/>); a player-scope binding carries
/// <see cref="PlayerScopeKey"/> and bypasses it (AS-IS region ① is a player bucket, not a field permanent).
/// The stub <c>CanNotPlayClass</c> had no such scan, so every option was playable regardless of these effects.
/// </summary>
public static class CanNotPlayOptionScan
{
    public const string Scope = "CanNotPlayOption";

    // (joint-migration) canonical joint predicate — the AS-IS ICanNotPlayCardEffect.CanNotPlay(cardSource) shape:
    // a single Func<CardSource /*option being played*/, bool> evaluated by SCANNING every player/field/self
    // CanNotPlay effect (mirrors CardSource.CanNotPlayThisOption). Producers embed the AS-IS CanNotPlayClass
    // CardCondition(option) into it.
    public const string JointPredicateKey = "joint.canNotPlayOption";

    // A binding registered at PLAYER scope (AS-IS region ① player.EffectList) — its granter card is NOT a field
    // permanent (e.g. an option played to trash that registered a duration-bound player effect), so it bypasses
    // the field stack-position membership check.
    public const string PlayerScopeKey = "canNotPlayOption.playerScope";

    /// <summary>True when <paramref name="optionCardId"/> (owned by <paramref name="owner"/>) is currently
    /// forbidden from being played by an active <c>ICanNotPlayCardEffect</c> — AS-IS regions ①②③ of
    /// <c>CanNotPlayThisOption</c> (the color-requirement half is <see cref="OptionColorRequirement"/>).</summary>
    public static bool CanNotPlay(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId optionCardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (owner.IsEmpty || optionCardId.IsEmpty)
        {
            return false;
        }

        // AS-IS `this` — the option being played (the joint's argument).
        var option = new CardSource(context, optionCardId, owner, owner);
        ICardInstanceRepository repository = context.CardInstanceRepository;

        // (R3-W3c-4) AS-IS-literal LIVE scan (CardSource.CanNotPlayThisOption regions ①②③, CardSource.cs:184-238):
        // for each population, `cardEffect is ICanNotPlayCardEffect && cardEffect.CanUse(null) && CanNotPlay(option)`.
        // This is the interface-scan half — it sees every AS-IS CanNotPlayClass kind-class (no ToBinding): BT8_057
        // (region ②), EX1_072's player-bucket grant (region ①, via AddEffectToPlayer's Player.EffectList bucket),
        // and a self-forbidding option (region ③). (이연④-f) EVERY producer was flipped to CanNotPlayClass, so the
        // registry scan below (JointPredicateKey / ContinuousCanNotPlayOptionEffect.ToBinding) is now DEAD — no
        // producer writes it. It is retained (returns nothing) only pending the atomic CanNotPlayOptionScan
        // registry-read deletion; design item W3c-CANNOTPLAY-PLAYERBUCKET is RESOLVED.
        var gameContext = new GameContext(context);
        foreach (var player in gameContext.Players)
        {
            // region ①: every player's player.EffectList(None)
            foreach (var ce in player.EffectList(EffectTiming.None))
            {
                if (ce is ICanNotPlayCardEffect p1 && ce.CanUse(null) && p1.CanNotPlay(option))
                {
                    return true;
                }
            }

            // region ②: every field permanent's EffectList(None)
            foreach (var permanent in player.GetFieldPermanents())
            {
                foreach (var ce in permanent.EffectList(EffectTiming.None))
                {
                    if (ce is ICanNotPlayCardEffect p2 && ce.CanUse(null) && p2.CanNotPlay(option))
                    {
                        return true;
                    }
                }
            }
        }

        // region ③: the option's own [None] effects when it is not (the top of) a field permanent.
        if (option.PermanentOfThisCard().IsEmpty)
        {
            foreach (var ce in option.EffectList(EffectTiming.None))
            {
                if (ce is ICanNotPlayCardEffect p3 && ce.CanUse(null) && p3.CanNotPlay(option))
                {
                    return true;
                }
            }
        }

        // Regions ① (player-scope) + ② (field permanents): registered continuous CanNotPlay bindings.
        // (이연④-f) DEAD registry-half — no producer writes the JointPredicateKey binding any more
        // (ContinuousCanNotPlayOptionEffect deleted); this loop finds nothing. Retained pending atomic deletion.
        foreach (EffectRequest request in context.EffectRegistry.GetContinuousEffects(new EffectQueryContext(Scope)))
        {
            IReadOnlyDictionary<string, object?> values = request.Context.Values;
            if (!values.TryGetValue(JointPredicateKey, out object? jointRaw)
                || jointRaw is not Func<CardSource, bool> joint)
            {
                continue;
            }

            // Region ①: a player-scope grant bypasses the field stack-position membership (its granter card is
            // not a field permanent). Region ②: a field granter is exposed only under the AS-IS EffectList_ForCard
            // membership (top/tucked/flipped/inherited).
            bool playerScope = values.TryGetValue(PlayerScopeKey, out object? psRaw) && psRaw is true;
            if (!playerScope && !ContinuousFieldMembership.GranterMembershipHolds(repository, context, request))
            {
                continue;
            }

            // AS-IS cardEffect.CanUse(null): the CanNotPlay effect's own condition gate (BT8_057 = host on field
            // && all my Digimon suspended && opponent turn; EX1_072 = always true).
            if (values.TryGetValue(ContinuousScopeEvaluation.ConditionKey, out object? condRaw)
                && condRaw is Func<bool> condition && !condition())
            {
                continue;
            }

            if (joint(option))
            {
                return true;
            }
        }

        // Region ③: the option's OWN [None] CanNotPlay effects — AS-IS `if (PermanentOfThisCard() == null)`. An
        // option being played from hand is not a permanent, so its own continuous effects are dispatch-built
        // (unregistered, like the face-up-security source scan and OptionColorRequirement's self ignore-color
        // scan) and evaluated with their CanUse gate (CardSource.EffectConditionPasses).
        // (이연④-f) DEAD dispatch-half — the region-③ producer (TfxOptionForbidsSelf) now emits the AS-IS kind-class
        // CanNotPlayClass, served by the LIVE ICanNotPlayCardEffect scan above (option.EffectList(None)); no
        // JointPredicateKey is dispatch-built, so this loop finds nothing. Retained pending atomic deletion.
        if (!IsPermanentOfThisCard(context, owner, optionCardId))
        {
            foreach (EffectRequest request in CardEffectRegistrar.BuildContinuousRequests(context, optionCardId, owner, Scope))
            {
                IReadOnlyDictionary<string, object?> values = request.Context.Values;
                if (values.TryGetValue(JointPredicateKey, out object? jointRaw)
                    && jointRaw is Func<CardSource, bool> joint
                    && CardSource.EffectConditionPasses(request)
                    && joint(option))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // AS-IS CardSource.PermanentOfThisCard() != null iff this card is (the top of) a field permanent. An option
    // in hand / trash is not — mirrored by "is this instance in a field zone".
    private static bool IsPermanentOfThisCard(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId cardId)
    {
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            if (zones.GetCards(player, Choices.ChoiceZone.BattleArea).Contains(cardId)
                || zones.GetCards(player, Choices.ChoiceZone.BreedingArea).Contains(cardId))
            {
                return true;
            }
        }

        return false;
    }
}
