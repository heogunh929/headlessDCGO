namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// Headless mirror of the original <c>CardSource</c> — the handle a card-effect builder receives. Carries
/// the live instance id, the controlling / owning player, and the live <see cref="EngineContext"/> so
/// condition predicates can read turn / zone / stack state.
/// </summary>
public sealed class CardSource
{
    public CardSource(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller, HeadlessPlayerId? owner = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty)
        {
            throw new ArgumentException("Card source instance id must not be empty.", nameof(instanceId));
        }

        if (controller.IsEmpty)
        {
            throw new ArgumentException("Card source controller id must not be empty.", nameof(controller));
        }

        Context = context;
        InstanceId = instanceId;
        Controller = controller;
        Owner = owner ?? controller;
    }

    public EngineContext Context { get; }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId Controller { get; }

    public HeadlessPlayerId Owner { get; }

    /// <summary>Mirror of <c>CardSource.PermanentOfThisCard()</c>: the permanent (stack) this card is part
    /// of, whether it is the top card or a buried digivolution source. Empty if the card is not in a
    /// battle-area permanent.</summary>
    public PermanentView PermanentOfThisCard()
    {
        var zones = (IZoneStateReader)Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(Owner, ChoiceZone.BattleArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(Context.CardInstanceRepository, Context.CardRepository, top);
            if (top == InstanceId || stack.UnderCards.Any(under => under.InstanceId == InstanceId))
            {
                return new PermanentView(stack);
            }
        }

        return new PermanentView(DigivolutionStack.Empty);
    }

    // ===== (PRIM-W5-0) card-query view — the member surface card predicates read =====================
    // Backed by the definition CardRecord (colors/level/traits/type) + instance metadata. Enables 1:1
    // mirror of the original `cardSource.<X>` / `permanent.TopCard.<X>` predicates.

    private CardRecord? Definition =>
        Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? inst) && inst is not null
            && Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) ? def
            : (Context.CardRepository.TryGetCard(InstanceId, out CardRecord? self) ? self : null);

    private static IReadOnlyList<string> ReadStrings(IReadOnlyDictionary<string, object?>? meta, string key)
    {
        if (meta is null || !meta.TryGetValue(key, out object? raw) || raw is null) return Array.Empty<string>();
        return raw switch
        {
            IEnumerable<string> ss => ss.ToArray(),
            string s => s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => Array.Empty<string>(),
        };
    }

    /// <summary>(A3) The card's BASE colors (mirror of <c>BaseCardColors</c>, CardSource.cs:364-401):
    /// printed colors transformed by every active <see cref="CardEffects.ChangeBaseCardColorClass"/> effect,
    /// Distinct. AS-IS scans self + all field permanents — the registry's active bindings are that set.</summary>
    public IReadOnlyList<string> BaseCardColors
    {
        get
        {
            List<string> colors = ReadStrings(Definition?.Metadata, "colors").ToList();
            colors = FoldListTransforms(colors, CardEffects.ChangeBaseCardColorClass.ChangeBaseCardColorsKey);
            return colors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>(A3) The card's colors (mirror of <c>CardColors</c>, CardSource.cs:446-483): seeds from the
    /// fully-resolved <see cref="BaseCardColors"/> (base-change BEFORE change, AS-IS two-stage order), then
    /// every active <see cref="CardEffects.ChangeCardColorClass"/> effect transforms the list, Distinct.</summary>
    public IReadOnlyList<string> CardColors
    {
        get
        {
            List<string> colors = BaseCardColors.ToList();
            colors = FoldListTransforms(colors, CardEffects.ChangeCardColorClass.ChangeCardColorsKey);
            return colors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>(RD-2 latent-1 / AS-IS metadata key for a DUAL card's separate option colour requirement list —
    /// <c>CEntity_Base.OptionCardColorRequirements</c>, CEntity_Base.cs:41.)</summary>
    public const string OptionColorRequirementsKey = "optionColorRequirements";

    /// <summary>(RD-2 latent-1) The card's BASE dual colours (mirror of <c>BaseDualCardColors</c>,
    /// CardSource.cs:403-441): seeds from <see cref="OptionColorRequirementsKey"/> (a dual card's option-play
    /// colour requirement, distinct from its printed Digimon colours) then folds every active
    /// <see cref="CardEffects.ChangeBaseCardColorClass"/> effect — the same two-stage colour machinery as
    /// <see cref="BaseCardColors"/>, seeded differently.</summary>
    public IReadOnlyList<string> BaseDualCardColors
    {
        get
        {
            List<string> colors = ReadStrings(Definition?.Metadata, OptionColorRequirementsKey).ToList();
            colors = FoldListTransforms(colors, CardEffects.ChangeBaseCardColorClass.ChangeBaseCardColorsKey);
            return colors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>(RD-2 latent-1) The card's dual colours (mirror of <c>DualCardColors</c>, CardSource.cs:283-341):
    /// <see cref="BaseDualCardColors"/> then every active <see cref="CardEffects.ChangeCardColorClass"/> effect.
    /// AS-IS uses this (not <see cref="CardColors"/>) as the option colour requirement when the played card is a
    /// Digimon (a dual Digimon+Option card).</summary>
    public IReadOnlyList<string> DualCardColors
    {
        get
        {
            List<string> colors = BaseDualCardColors.ToList();
            colors = FoldListTransforms(colors, CardEffects.ChangeCardColorClass.ChangeCardColorsKey);
            return colors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>(A3) The card's traits (mirror of <c>CardTraits</c>, CardSource.cs:2581-2604): printed traits
    /// transformed by the card's OWN <see cref="CardEffects.ChangeTraitsClass"/> effects (AS-IS scans self
    /// only; no Distinct).</summary>
    public IReadOnlyList<string> CardTraits
    {
        get
        {
            List<string> traits = ReadStrings(Definition?.Metadata, "traits").ToList();
            foreach (Func<CardSource, List<string>, List<string>> transform in
                SelfTransforms<Func<CardSource, List<string>, List<string>>>(CardEffects.ChangeTraitsClass.ChangeTraitsKey))
            {
                traits = transform(this, traits);
            }

            return traits;
        }
    }

    // (A3) fold ALL active list-transform bindings for `key` over the accumulator (AS-IS field-wide scan).
    private List<string> FoldListTransforms(List<string> accumulator, string key)
    {
        foreach (EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(new EffectQueryContext(ContinuousRestrictionGate.Scope)))
        {
            if (effect.Context.Values.TryGetValue(key, out object? raw)
                && raw is Func<CardSource, List<string>, List<string>> transform
                && EffectConditionPasses(effect))
            {
                accumulator = transform(this, accumulator);
            }
        }

        return accumulator;
    }

    // (A3) the card's OWN transform bindings for `key` (AS-IS self EffectList scan).
    private IEnumerable<T> SelfTransforms<T>(string key)
    {
        foreach (EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
            new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: InstanceId)))
        {
            if (effect.Context.Values.TryGetValue(key, out object? raw) && raw is T transform && EffectConditionPasses(effect))
            {
                yield return transform;
            }
        }
    }

    // (A3) the AS-IS `cardEffect.CanUse(null)` gate — the binding's stored continuous condition.
    internal static bool EffectConditionPasses(EffectRequest effect) =>
        !effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? raw)
        || raw is not Func<bool> condition
        || condition();

    /// <summary>Continuous-binding key for an added card name (AS-IS ChangeCardNamesClass).</summary>
    public const string AddedCardNameKey = "addedCardName";

    /// <summary>The card's name(s) (mirror of AS-IS <c>CardSource.BaseCardNames</c>) — the printed name, first
    /// transformed by active <c>ChangeBaseCardName</c> effects (a REPLACE, like BaseCardColors; BT14_097 "original
    /// name is [Sukamon]"), then extended with any names ADDED by <c>ChangeCardNames</c> effects.</summary>
    public IReadOnlyList<string> CardNames
    {
        get
        {
            // (d-remediation) BASE names = printed name transformed by ChangeBaseCardName (REPLACE), mirroring the
            // BaseCardColors → CardColors two-stage order.
            var names = Definition is { } d ? new List<string> { d.Name } : new List<string>();
            names = FoldListTransforms(names, CardEffects.ChangeBaseCardNameClass.ChangeBaseCardNamesKey);

            foreach (EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
                new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: InstanceId)))
            {
                if (effect.Context.Values.TryGetValue(AddedCardNameKey, out object? raw) && raw is string added && !string.IsNullOrWhiteSpace(added))
                {
                    names.Add(added);
                }
            }

            return names;
        }
    }

    /// <summary>The card's PRINTED level, or -1. AS-IS <c>HasLevel</c> is printed-data based
    /// (CEntity_Base.cs:317) — level-change folds never alter it.</summary>
    private int PrintedLevel => Definition?.Metadata is { } m && m.TryGetValue("level", out object? raw) && raw is int lv ? lv : -1;

    /// <summary>(A3) The card's level (mirror of <c>Level =&gt; TreatedLevel</c>, CardSource.cs:941-975):
    /// printed level transformed by the card's OWN <see cref="CardEffects.ChangeCardLevelClass"/> effects
    /// (AS-IS scans self only). -1 mirrors the AS-IS no-level sentinel (1145140) — no gameplay code compares
    /// the sentinel; all consumers guard on <see cref="HasLevel"/> first.</summary>
    public int Level
    {
        get
        {
            int level = PrintedLevel;
            foreach (Func<CardSource, int, int> transform in
                SelfTransforms<Func<CardSource, int, int>>(CardEffects.ChangeCardLevelClass.GetLevelKey))
            {
                level = transform(this, level);
            }

            return level;
        }
    }

    /// <summary>The card's printed number (e.g. "BT10-012"), used as the SpecialPlayRecipe key.</summary>
    public string CardNumber => Definition?.CardNumber ?? string.Empty;

    // (C7) type judgements go through CardRecord.IsCardType — AS-IS CardKinds is a LIST, so a dual card
    // (e.g. Digimon/Option hybrid) reports true for BOTH kinds.
    public bool IsDigimon => Definition?.IsCardType("Digimon") == true;
    public bool IsTamer => Definition?.IsCardType("Tamer") == true;
    public bool IsOption => Definition?.IsCardType("Option") == true;
    /// <summary>(MIG2) AS-IS <c>CardSource.IsDigiEgg</c> (CardSource.cs:3466) — both fixture spellings.</summary>
    public bool IsDigiEgg => Definition?.IsCardType("DigiEgg") == true || Definition?.IsCardType("Digitama") == true;
    /// <summary>(MIG2 substrate) Whether a card DEFINITION is registered at all. AS-IS every CardSource carries
    /// a CEntity — a definition-less instance exists only in abstract test fixtures; rule predicates that would
    /// trash a card for its TYPE gate on this (fixture guard, same family as the D-2 defined-DP guard).</summary>
    public bool HasDefinition => Definition is not null;
    /// <summary>(MIG2) AS-IS <c>CardSource.IsFlipped</c> — the shared face-down instance flag.</summary>
    public bool IsFlipped => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? f) && f is not null
        && f.Metadata.TryGetValue("isFlipped", out object? flip) && flip is true;
    /// <summary>(joint-migration) public card-type check for scope synthesis in restriction producers.</summary>
    public bool IsCardType(string cardType) => Definition?.IsCardType(cardType) == true;
    public bool IsToken => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isToken", out object? t) && t is bool b && b;

    // (A3) printed-data based like AS-IS CEntity_Base.HasLevel — a level-change fold does not grant a level.
    public bool HasLevel => PrintedLevel >= 0;

    /// <summary>(Jogress by levels) AS-IS <c>Permanent</c> levels-for-Jogress fold (Permanent.cs:3560-3600): the
    /// levels THIS card counts as when used as a Jogress / DNA-Digivolution material against
    /// <paramref name="jogressCard"/> (the digivolving card). Its printed level PLUS every extra level its own
    /// <see cref="CardEffects.AddJogressLevelsClass"/> effects contribute (self-scoped, mirroring the AS-IS
    /// self-gated board scan). Level-based Jogress/DNA material predicates test membership in this set.</summary>
    public IReadOnlyList<int> JogressLevelsAgainst(CardSource jogressCard)
    {
        ArgumentNullException.ThrowIfNull(jogressCard);
        var levels = new List<int>();
        if (HasLevel)
        {
            levels.Add(Level);
        }

        foreach (Func<CardSource, IReadOnlyList<int>> getLevels in
            SelfTransforms<Func<CardSource, IReadOnlyList<int>>>(CardEffects.AddJogressLevelsClass.GetJogressLevelsKey))
        {
            levels.AddRange(getLevels(jogressCard));
        }

        return levels;
    }

    /// <summary>(W6-P) printed-data based like AS-IS <c>HasDP</c> — the card defines a DP at all.</summary>
    public bool HasDP => Definition?.Metadata.TryGetValue("dp", out object? dp) == true && dp is int;

    /// <summary>(W6 tail) AS-IS <c>HasPlayCost</c> — the card defines a play cost.</summary>
    public bool HasPlayCost => Definition?.PlayCost is not null;

    /// <summary>(W6 tail) The card's PRINTED play cost — behaviourally this mirrors AS-IS
    /// <c>BasePlayCostFromEntity</c> (CardSource.cs:757), NOT the AS-IS <c>GetCostItself</c> (CardSource.cs:769),
    /// which is modifier-FOLDED: <c>Max(0, GetChangedCostItselef(BasePlayCostFromEntity, Root.None,
    /// [PermanentOfThisCard()]))</c>. The name is kept for the existing call sites; a caller whose AS-IS analog
    /// reads the folded <c>GetCostItself</c> (Min/MaxCost comparisons, CostJustBeforeRemoveField) carries a
    /// documented reduction here (no per-card cost-change effects fold in); a caller whose AS-IS analog reads
    /// <c>BasePlayCostFromEntity</c> (e.g. BT22_035's "play cost 4 or less") is exact.</summary>
    public int GetCostItself => Definition?.PlayCost ?? 0;

    /// <summary>(C9) mirror of AS-IS <c>CardSource.IsLinked</c> (CardSource.cs:2947):
    /// <c>PermanentOfThisCard().LinkedCards.Contains(this)</c> — true while this card is a LINK card of a
    /// battle-area permanent (link cards are tracked separately from digivolution sources:
    /// <c>LinkHelpers.LinkedCardIdsKey</c>). Evaluated LIVE — breaking the link flips it false.</summary>
    public bool IsLinked
    {
        get
        {
            var zones = (IZoneStateReader)Context.ZoneMover;
            foreach (HeadlessEntityId hostId in zones.GetCards(Owner, ChoiceZone.BattleArea))
            {
                if (Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null
                    && LinkHelpers.ReadLinkedCardIds(host.Metadata).Contains(InstanceId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool IsLevel(int level) => Level == level;
    public bool HasCardColor(string color) => CardColors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
    public bool EqualsCardName(string name) => CardNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    public bool ContainsCardName(string fragment) => CardNames.Any(n => n.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    public bool EqualsTraits(string trait) => CardTraits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));
    public bool ContainsTraits(string fragment) => CardTraits.Any(t => t.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>(W6-L) Mirror of AS-IS <c>CardSource.linkCondition</c> (CardSource.cs:2727): the first
    /// usable <c>IAddLinkConditionEffect</c>'s condition for THIS card (dispatch-first, registry fallback —
    /// the AssemblyConditionOf pattern).</summary>
    public LinkCondition? LinkConditionOf()
    {
        if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? instance) && instance is not null &&
            Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) && definition is not null &&
            CardEffectDispatch.TryCreateForCard(definition, out CEntity_Effect? entity) && entity is not null)
        {
            foreach (ICardEffect effect in entity.CardEffects(EffectTiming.None, this))
            {
                if (effect is CardEffects.AddLinkConditionClass link && link.CanUse() &&
                    link.GetLinkCondition(this) is LinkCondition fromCard)
                {
                    return fromCard;
                }
            }
        }

        foreach (Headless.Effects.EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
            new Headless.Services.EffectQueryContext(Headless.Runtime.ContinuousRestrictionGate.Scope)))
        {
            if (effect.Context.SourceEntityId != InstanceId ||
                !effect.Context.Values.TryGetValue(CardEffects.AddLinkConditionClass.GetLinkConditionKey, out object? raw) ||
                raw is not Func<CardSource, LinkCondition?> getCondition)
            {
                continue;
            }

            if (effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? rawCond) &&
                rawCond is Func<bool> condition && !condition())
            {
                continue;
            }

            if (getCondition(this) is LinkCondition found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>(C-2 witness / BT22_035) 1:1 mirror of AS-IS <c>CardSource.CanLinkToTargetPermanent(target,
    /// PayCost:false, allowBreeding:false)</c> (CardSource.cs:3337) folded with <c>CanLink(false)</c>
    /// (CardSource.cs:3140): whether THIS card may be attached as a LINK card onto
    /// <paramref name="targetPermanent"/>. The host must be a real (non-token) battle-area — not breeding-area
    /// — permanent, and THIS card must carry a declared <see cref="LinkCondition"/> whose <c>digimonCondition</c>
    /// matches BOTH the host AND (CanLink) at least one owner battle-area Digimon (a battle-area host trivially
    /// satisfies that second clause). PayCost=false skips the memory check; the PayCost / allowBreeding variants
    /// land with their first witness (design item C2-02).</summary>
    public bool CanLinkToTargetPermanent(Permanent? targetPermanent, bool PayCost = false, bool allowBreeding = false)
    {
        if (targetPermanent is null)
        {
            return false;
        }

        // AS-IS: target.TopCard != null && !target.TopCard.IsToken.
        if (targetPermanent.TopCard.IsToken)
        {
            return false;
        }

        var zones = (IZoneStateReader)Context.ZoneMover;
        // AS-IS (CardSource.cs:3343): `allowBreeding || !target.Owner.GetBreedingAreaPermanents().Contains(target)`
        // — a breeding-area permanent is a valid link host ONLY when the caller allows breeding (the rule
        // predicate IsDigimonLackLinkCondition passes true; its trash-list re-filter passes false).
        if (!allowBreeding && zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BreedingArea).Contains(targetPermanent.InstanceId))
        {
            return false;
        }

        // AS-IS this.CanLink(false, allowBreeding) (CardSource.cs:3140): THIS card declares a link condition
        // matched by >= 1 owner permanent — GetBattleAreaDigimons normally, GetFieldPermanents (battle +
        // breeding, NO Digimon filter) when allowBreeding — the AS-IS branch asymmetry, preserved.
        LinkCondition? link = LinkConditionOf();
        if (link is null)
        {
            return false;
        }

        bool canLinkSomewhere = allowBreeding
            ? zones.GetCards(Owner, ChoiceZone.BattleArea).Concat(zones.GetCards(Owner, ChoiceZone.BreedingArea))
                .Any(id => link.digimonCondition(new Permanent(Context, id, Owner)))
            : zones.GetCards(Owner, ChoiceZone.BattleArea)
                .Any(id => CardEffectCommons.IsOwnerBattleAreaDigimon(this, id)
                    && link.digimonCondition(new Permanent(Context, id, Owner)));
        if (!canLinkSomewhere)
        {
            return false;
        }

        // AS-IS PayCost branch (GetChangedLinkCost vs Owner.MaxMemoryCost) = design item C2-02 (lands with its
        // first witness; every in-scope caller passes false, matching the AS-IS rule-process call sites).
        // AS-IS linkCondition.digimonCondition(target).
        return link.digimonCondition(targetPermanent);
    }

    /// <summary>(W6-F) Mirror of AS-IS <c>CardSource.appFusionCondition</c>: the first usable declared
    /// App-Fusion condition (dispatch-first, registry fallback).</summary>
    public AppFusionCondition? AppFusionConditionOf()
    {
        if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? instance) && instance is not null &&
            Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) && definition is not null &&
            CardEffectDispatch.TryCreateForCard(definition, out CEntity_Effect? entity) && entity is not null)
        {
            foreach (ICardEffect effect in entity.CardEffects(EffectTiming.None, this))
            {
                if (effect is CardEffects.AddAppFusionConditionClass appfusion && appfusion.CanUse() &&
                    appfusion.GetAppFusionCondition(this) is AppFusionCondition fromCard)
                {
                    return fromCard;
                }
            }
        }

        foreach (Headless.Effects.EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
            new Headless.Services.EffectQueryContext(Headless.Runtime.ContinuousRestrictionGate.Scope)))
        {
            if (effect.Context.SourceEntityId != InstanceId ||
                !effect.Context.Values.TryGetValue(CardEffects.AddAppFusionConditionClass.GetAppFusionConditionKey, out object? raw) ||
                raw is not Func<CardSource, AppFusionCondition?> getCondition)
            {
                continue;
            }

            if (effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? rawCond) &&
                rawCond is Func<bool> condition && !condition())
            {
                continue;
            }

            if (getCondition(this) is AppFusionCondition found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>(AD1-A) Mirror of AS-IS <c>CardSource.HasAssembly</c> (CardSource.cs:2575).</summary>
    public bool HasAssembly => AssemblyConditionOf() is not null;

    /// <summary>(AD1-A) Mirror of AS-IS <c>CardSource.assemblyCondition</c> (CardSource.cs:3043-3065): the
    /// first USABLE <c>IAddAssemblyConditionEffect</c>'s condition for THIS card. The AS-IS accessor reads
    /// the card's own EffectList — zone-independent (a HAND card has it, which is when Assembly matters) —
    /// so the primary source is the card's dispatched effect class; registered bindings are the fallback
    /// (test fixtures / registry-only setups).</summary>
    public AssemblyCondition? AssemblyConditionOf()
    {
        if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? instance) && instance is not null &&
            Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) && definition is not null &&
            CardEffectDispatch.TryCreateForCard(definition, out CEntity_Effect? entity) && entity is not null)
        {
            foreach (ICardEffect effect in entity.CardEffects(EffectTiming.None, this))
            {
                if (effect is CardEffects.AddAssemblyConditionClass assembly && assembly.CanUse() &&
                    assembly.GetAssemblyCondition(this) is AssemblyCondition fromCard)
                {
                    return fromCard;
                }
            }
        }

        foreach (Headless.Effects.EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
            new Headless.Services.EffectQueryContext(Headless.Runtime.ContinuousRestrictionGate.Scope)))
        {
            if (effect.Context.SourceEntityId != InstanceId ||
                !effect.Context.Values.TryGetValue(CardEffects.AddAssemblyConditionClass.GetAssemblyConditionKey, out object? raw) ||
                raw is not Func<CardSource, AssemblyCondition?> getCondition)
            {
                continue;
            }

            if (effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? rawCond) &&
                rawCond is Func<bool> condition && !condition())
            {
                continue;
            }

            if (getCondition(this) is AssemblyCondition found)
            {
                return found;
            }
        }

        return null;
    }
}

