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

// (Phase 1) Card-porting recipe foundation.
//
// The original DCGO authors each card as `public class <Id> : CEntity_Effect` overriding
// `CardEffects(EffectTiming timing, CardSource card)` which returns the `ICardEffect`s active for that
// timing (see DCGO/Assets/Scripts/CardEffect/<set>/<color>/<id>.cs). To keep ported card files a 1:1
// mirror of that source (AS-IS structure-mirror rule), this file provides the headless equivalents of
// the Unity authoring surface — `CEntity_Effect`, `CardSource`, `EffectTiming`, `ICardEffect`, a
// `CardEffectFactory` whose method names match the original, and `CardEffectCommons` condition predicates
// — so a ported card body reads identically to the original and compiles against the headless engine.
//
// Each `ICardEffect` lowers to an `EffectBinding` that the existing continuous / keyword gates already
// consume (no new resolution plumbing). The original evaluates conditions against global singletons; the
// headless threads the live `EngineContext` through `CardSource` so a `condition` lambda evaluates against
// real turn / zone / digivolution state at read time. `CardEffectRegistrar` materialises a card's
// bindings into the EffectRegistry when it enters play.

/// <summary>
/// Headless mirror of the original (large) <c>EffectTiming</c> enum. Only the timings used by ported
/// cards are listed; grow this as cards require new ones. <see cref="None"/> is the original's marker for
/// always-on continuous / static effects (registered once while the card is in play).
/// </summary>
public enum EffectTiming
{
    None = 0,
    OnEnterFieldAnyone,
    OnDetermineDoSecurityCheck,
    OnUseAttack,
    WhenDigivolving,
    OnDestroyedAnyone,
    OnAllyAttack,
    OnBlockAnyone,
    OnEndTurn,
    OnStartTurn,

    // Player-activated abilities (NOT auto-registered on enter-play; activation flow is Wave 3).
    OptionSkill,
    SecuritySkill,

    // (EX8_074 Stage 1) "When this card would be played" — the original BeforePayCost timing. Engine-level
    // string trigger `TriggerTimings.BeforePayCost` already fires in PlayCardAction; this enum value lets a
    // ported card return BeforePayCost effects. The interactive pre-payment cost-reduction WINDOW that
    // consumes them is a later stage (PlayCardAction's cost is currently locked at action-generation time).
    BeforePayCost,
    // (PRIM-W4 WhenMovingClass) mirrors the original EffectTiming.OnMove — fires when a Digimon is promoted
    // out of the breeding area (CV-A4). ToTriggerName -> "OnMove" matches the engine's TriggerTimings.OnMove
    // emit. Appended at the end to keep existing enum ordinals stable.
    OnMove,

    // (PRIM-P0-timing) High-volume card-facing timings from ALL_CARD_PRIMITIVE_BACKLOG P0. Each enum name
    // is string-equal to an emitted TriggerTimings value (ToTriggerName -> ToString()); appended at the end
    // to keep existing ordinals stable.
    //   OnStartMainPhase — main-phase entry (emit exists: MetadataActionProcessor OnStartMainPhase). 222 cards.
    //   OnEndBattle      — after battle resolved/deletions applied (emit exists: BattleResolver). 84 cards.
    //   OnDeclaration    — attack declared; new emit added alongside OnAttack/OnAllyAttack. 298 cards.
    OnStartMainPhase,
    OnEndBattle,
    OnDeclaration,

    // (PRIM-P0-timing batch 2) Timings ALREADY emitted by the engine (verified emit sites) that only lacked
    // a card-facing enum member. Each name is string-equal to its emitted TriggerTimings value. Pure enum
    // additions against existing emits (same low-risk shape as OnEndBattle) — collection/resolution reuse the
    // generic path. "...Anyone" board timings are self-scoped here (cross-card broadcast is a per-card
    // follow-up via TriggerTimings.BroadcastTimings, as with the existing OnBlockAnyone).
    //   OnTappedAnyone 139 · OnCounterTiming 111 · WhenLinked 64 · OnAddDigivolutionCards 50 · OnUseOption 30
    //   OnUnTappedAnyone 29 · OnDiscardSecurity 14 · OnLinkCardDiscarded 7 · AfterPayCost 7 · WhenTopCardTrashed 3
    //   OnFaceUpSecurityIncreased 1
    OnTappedAnyone,
    OnUnTappedAnyone,
    OnCounterTiming,
    WhenLinked,
    OnLinkCardDiscarded,
    OnAddDigivolutionCards,
    OnUseOption,
    OnDiscardSecurity,
    AfterPayCost,
    WhenTopCardTrashed,
    OnFaceUpSecurityIncreased,

    // (PRIM-P0-timing batch 3a) Timings already DERIVED from CardMoved zone transitions (or the SecurityCheck
    // event) by TriggerTimingMap.Derive — already available, no emit needed, only a card-facing enum member.
    // Same low-risk shape as batch 2; the derivation is existing engine behavior exercised by the suite.
    //   WhenRemoveField 164 · OnLoseSecurity 73 · OnDiscardHand 34 · OnAddHand 21 · OnDiscardLibrary 20
    //   OnAddSecurity 14 · WhenReturntoHandAnyone 9 · WhenReturntoLibraryAnyone 9 · OnSecurityCheck 9
    //   OnReturnCardsToHandFromTrash 2 · OnPermamemtReturnedToHand 2 (sic) · OnRemovedField 2 ·
    //   OnLeaveFieldAnyone 1 · OnReturnCardsToLibraryFromTrash 1
    WhenRemoveField,
    OnLoseSecurity,
    OnDiscardHand,
    OnAddHand,
    OnDiscardLibrary,
    OnAddSecurity,
    WhenReturntoHandAnyone,
    WhenReturntoLibraryAnyone,
    OnSecurityCheck,
    OnReturnCardsToHandFromTrash,
    OnPermamemtReturnedToHand,
    OnRemovedField,
    OnLeaveFieldAnyone,
    OnReturnCardsToLibraryFromTrash,

    // (PRIM-P0-timing batch 3b) OnEndAttack (80 cards): end of a single attack. Already collected by
    // EndAttackTriggerHook (keys on "OnEndAttack") at AttackPipeline.AdvanceEndAttackAsync — enum-only add.
    OnEndAttack,

    // (PRIM-P0-timing batch 3b) new emit sites added:
    //   OnDigivolutionCardDiscarded 53 — source (under) card trashed by an effect (DigivolutionStackHelpers).
    //   OnAttackTargetChanged 31 — attack defender switched by raid/block (RaidAttackSwitch/BlockTiming).
    // Both are broadcast (see TriggerTimings.BroadcastTimings) to mirror the AS-IS global StackSkillInfos.
    OnDigivolutionCardDiscarded,
    OnAttackTargetChanged,

    // (PRIM-P0-timing batch 4) The would-be-deleted replacement/prevention window (206 cards). A card
    // registered here surfaces as a PRE option in the existing DeletionReplacementTiming synchronous window;
    // activating it prevents/replaces the deletion. See docs/porting/when_permanent_would_be_deleted_design.md.
    WhenPermanentWouldBeDeleted,
}

/// <summary>The headless <see cref="EffectTiming"/> mirror values are named after the engine trigger
/// strings (the "...Anyone" forms used by <c>TriggerTimings</c> / <c>GetEffectsForTiming</c>), so the
/// engine timing string is just the enum name.</summary>
public static class EffectTimings
{
    public static string ToTriggerName(EffectTiming timing) => timing.ToString();
}

/// <summary>A read-only view of the permanent (digivolution stack) a card belongs to — the headless
/// stand-in for the original <c>Permanent</c> accessed via <c>CardSource.PermanentOfThisCard()</c>.</summary>
public sealed class PermanentView
{
    public PermanentView(DigivolutionStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        Stack = stack;
    }

    public DigivolutionStack Stack { get; }

    /// <summary>The under-cards (digivolution sources) of the permanent — mirrors
    /// <c>Permanent.DigivolutionCards</c>. <c>.Count</c> is the source count.</summary>
    public IReadOnlyList<StackedCard> DigivolutionCards => Stack.UnderCards;

    public bool IsEmpty => Stack.IsEmpty;

    /// <summary>The top card's instance id (the battling Digimon) — mirrors <c>Permanent.TopCard</c>.</summary>
    public HeadlessEntityId TopInstanceId => Stack.Cards.Count > 0 ? Stack.Cards[^1].InstanceId : default;
}

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

    /// <summary>The card's name(s) (mirror of <c>CardNames</c>) — the printed name plus any names granted by
    /// active continuous effects (ChangeCardNames).</summary>
    public IReadOnlyList<string> CardNames
    {
        get
        {
            var names = new List<string>();
            if (Definition is { } d)
            {
                names.Add(d.Name);
            }

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
    public bool IsToken => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isToken", out object? t) && t is bool b && b;

    // (A3) printed-data based like AS-IS CEntity_Base.HasLevel — a level-change fold does not grant a level.
    public bool HasLevel => PrintedLevel >= 0;

    /// <summary>(W6-P) printed-data based like AS-IS <c>HasDP</c> — the card defines a DP at all.</summary>
    public bool HasDP => Definition?.Metadata.TryGetValue("dp", out object? dp) == true && dp is int;

    /// <summary>(W6 tail) AS-IS <c>HasPlayCost</c> — the card defines a play cost.</summary>
    public bool HasPlayCost => Definition?.PlayCost is not null;

    /// <summary>(W6 tail) AS-IS <c>GetCostItself</c> — the card's own play cost (printed; per-card cost
    /// modifiers fold in the play pipeline, not here — the Min/MaxCost comparisons use the printed value).</summary>
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

/// <summary>(AD1-A) 1:1 mirror of AS-IS <c>AssemblyConditionElement</c> (CardSource.cs:4339-4358): one
/// material slot of an Assembly condition — an arbitrary card predicate, a required count, and an optional
/// gate against the already-selected materials.</summary>
public sealed class AssemblyConditionElement
{
    public AssemblyConditionElement(
        Func<CardSource, bool> cardCondition,
        bool skipAllIfNoSelect = true,
        string? selectMessage = null,
        int elementCount = 0,
        Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList = null)
    {
        CardCondition = cardCondition ?? throw new ArgumentNullException(nameof(cardCondition));
        this.skipAllIfNoSelect = skipAllIfNoSelect;
        this.selectMessage = selectMessage ?? string.Empty;
        ElementCount = elementCount;
        this.CanTargetCondition_ByPreSelecetedList = CanTargetCondition_ByPreSelecetedList;
    }

    public Func<CardSource, bool> CardCondition { get; }
    public bool skipAllIfNoSelect { get; }
    public int ElementCount { get; }
    public Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList { get; }
    public string selectMessage { get; }
}

/// <summary>(AD1-A) 1:1 mirror of AS-IS <c>AssemblyCondition</c> (CardSource.cs:4313-4337): the material
/// element list plus ONE flat <c>reduceCost</c>, applied only when the FULL set is assembled. Materials come
/// from the OWNER'S TRASH and end up UNDER the played permanent as digivolution cards.</summary>
public sealed class AssemblyCondition
{
    /// <summary>Old single-condition form ("1 condition × N times").</summary>
    public AssemblyCondition(
        AssemblyConditionElement element,
        Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList,
        string? selectMessage, int elementCount, int reduceCost)
    {
        ArgumentNullException.ThrowIfNull(element);
        elements = new List<AssemblyConditionElement>
        {
            new(element.CardCondition, element.skipAllIfNoSelect, selectMessage ?? element.selectMessage,
                elementCount, CanTargetCondition_ByPreSelecetedList ?? element.CanTargetCondition_ByPreSelecetedList),
        };
        this.elementCount = elementCount;
        this.reduceCost = reduceCost;
    }

    /// <summary>The A×B×C… DigiXros-like form (each element carries its own count).</summary>
    public AssemblyCondition(List<AssemblyConditionElement> elements, int reduceCost)
    {
        this.elements = elements ?? throw new ArgumentNullException(nameof(elements));
        elementCount = elements.Sum(element => element.ElementCount);
        this.reduceCost = reduceCost;
    }

    public List<AssemblyConditionElement> elements { get; }
    public int elementCount { get; }
    public int reduceCost { get; }
}

/// <summary>(W6-L) 1:1 mirror of AS-IS <c>LinkCondition</c> (CardSource.cs:4286): "this card may LINK onto
/// an owner battle-area Digimon matching <c>digimonCondition</c>, paying <c>cost</c> memory". LinkDP is NOT
/// declared here — it is per-card data (definition metadata <c>linkDP</c>, folded by LinkHelpers).</summary>
public sealed class LinkCondition
{
    public LinkCondition(Func<Permanent, bool> digimonCondition, int cost)
    {
        this.digimonCondition = digimonCondition ?? throw new ArgumentNullException(nameof(digimonCondition));
        this.cost = cost;
    }

    public Func<Permanent, bool> digimonCondition { get; }
    public int cost { get; }
}

/// <summary>(W6-F) 1:1 mirror of AS-IS <c>AppFusionCondition</c> (CardSource.cs:4298): "may App-Fuse onto an
/// owner Digimon whose TOP matches one material and one of whose LINK cards matches a DIFFERENT material,
/// paying <c>cost</c>". Executed as an EVOLUTION (the chosen link card joins the fused sources).</summary>
public sealed class AppFusionCondition
{
    public AppFusionCondition(Func<Permanent, CardSource, bool> linkedCondition, Func<Permanent, bool> digimonCondition, int cost)
    {
        this.linkedCondition = linkedCondition ?? throw new ArgumentNullException(nameof(linkedCondition));
        this.digimonCondition = digimonCondition ?? throw new ArgumentNullException(nameof(digimonCondition));
        this.cost = cost;
    }

    public Func<Permanent, CardSource, bool> linkedCondition { get; }
    public Func<Permanent, bool> digimonCondition { get; }
    public int cost { get; }
}

/// <summary>
/// Headless mirror of the original <c>ICardEffect</c>. A ported card returns these; the registrar lowers
/// each to an <see cref="EffectBinding"/> using the supplied unique effect id.
/// </summary>
public interface ICardEffect
{
    EffectBinding ToBinding(string effectId);
}

/// <summary>Marker for effects resolved via the activation / choice flow (Option / Security skills,
/// select-and-act, triggered-with-choice) rather than auto-registered continuous/trigger bindings.
/// <see cref="CardEffectRegistrar"/> skips these on enter-play; they are resolved imperatively until the
/// interactive activation path is wired.</summary>
public interface IActivatedCardEffect : ICardEffect
{
}

/// <summary>Headless mirror of the original card-effect base class <c>CEntity_Effect</c>.</summary>
public abstract class CEntity_Effect
{
    /// <summary>Returns the effects active for <paramref name="timing"/> (mirrors the original override).</summary>
    public abstract IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card);
}

/// <summary>
/// A continuous numeric self-modifier (DP / security attack / cost). Lowers to a continuous-role binding
/// targeting the source card, carrying the delta under the matching <see cref="ModifierHelpers"/> key plus
/// optional inherited / condition markers, so <see cref="ContinuousDpGate"/> /
/// <see cref="ContinuousModifierGate"/> fold it in automatically (with inherited / condition gating
/// applied by <see cref="ContinuousScopeEvaluation"/>).
/// </summary>
public sealed class ContinuousSelfModifierEffect : ICardEffect
{
    /// <summary>Marks a continuous binding as an inherited (digivolution-source) effect: it applies to the
    /// TOP card of the stack the source is buried in, never to the source as a stand-alone permanent.</summary>
    public const string InheritedEffectKey = "continuous.isInherited";

    /// <summary>Carries the card-authored <c>condition</c> predicate (a <c>Func&lt;bool&gt;</c>) evaluated
    /// at read time by <see cref="ContinuousScopeEvaluation"/>.</summary>
    public const string ConditionKey = "continuous.condition";

    /// <summary>Carries a card-authored dynamic delta (<c>Func&lt;int&gt;</c>, e.g. "+X where X = sources / 2")
    /// evaluated at read time; the resolved int is written under <see cref="DynamicMetricKey"/>'s metric.</summary>
    public const string DynamicValueKey = "continuous.dynamicValue";

    /// <summary>The metric delta key a resolved <see cref="DynamicValueKey"/> should be written under.</summary>
    public const string DynamicMetricKey = "continuous.dynamicMetric";

    public ContinuousSelfModifierEffect(CardSource card, string deltaKey, int changeValue, bool isInheritedEffect, Func<bool>? condition, Func<int>? dynamicValue = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        DynamicValue = dynamicValue;
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public Func<int>? DynamicValue { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (DynamicValue is not null)
        {
            // Resolved to a concrete int under DeltaKey at read time by ContinuousScopeEvaluation.
            values[DynamicValueKey] = DynamicValue;
            values[DynamicMetricKey] = DeltaKey;
        }
        else
        {
            values[DeltaKey] = ChangeValue;
        }

        if (IsInheritedEffect)
        {
            values[InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId },
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null,
            EffectQueryRole.Continuous,
            new[] { ContinuousModifierGate.Scope },
            effect: null,
            duration: null);
    }
}

/// <summary>(PRIM-W1) A continuous SELF restriction (cannot digivolve / attack / block / suspend / …) — the
/// restriction analogue of <see cref="ContinuousSelfModifierEffect"/>. Registers a <c>Restriction</c>-role
/// binding under <see cref="ContinuousRestrictionGate.Scope"/> carrying the given restriction flag, targeting
/// this card; the various actions (DigivolveAction / AttackPermanentAction / BlockTiming / …) already consult
/// <see cref="ContinuousRestrictionGate"/>. Condition / inherited-effect are honoured (same
/// <c>ContinuousScopeEvaluation</c> as the modifier gate). Reused across the CanNot* self-static primitives.</summary>
public sealed class ContinuousSelfRestrictionEffect : ICardEffect
{
    public ContinuousSelfRestrictionEffect(CardSource card, string restrictionKey, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(restrictionKey);
        Card = card;
        RestrictionKey = restrictionKey;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        CausingEffectPredicate = causingEffectPredicate;
    }

    public CardSource Card { get; }

    public string RestrictionKey { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    /// <summary>(FR2/M-2) AS-IS cardEffectCondition — the restriction only blocks effects whose causing effect's
    /// SOURCE card matches this. Null = blocks any effect.</summary>
    public Func<CardSource, bool>? CausingEffectPredicate { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [RestrictionHelpers.RestrictionTargetEntityIdKey] = Card.InstanceId.Value,
            [RestrictionHelpers.RestrictionSourceEntityIdKey] = Card.InstanceId.Value,
            [RestrictionKey] = true,
        };
        if (CausingEffectPredicate is not null)
        {
            values[RestrictionHelpers.CausingEffectPredicateKey] = CausingEffectPredicate;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        // Role Continuous (not Restriction): ContinuousRestrictionGate.Evaluate reads restrictions off the
        // CONTINUOUS-role effects (ContinuousScopeEvaluation.EvaluateForCard -> GetContinuousEffects ->
        // RestrictionHelpers.ReadRestrictions on their values), the same seam ContinuousSelfModifierEffect
        // rides. This also gets condition / inherited honouring for free.
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W1) A continuous PLAYER-SCOPE restriction — the restriction analogue of the player-scope
/// buff. Registers a <c>Restriction</c> flag over a player's cards (optionally narrowed by CardType) under
/// <see cref="ContinuousRestrictionGate.Scope"/>, collected by <c>ContinuousScopeEvaluation</c>'s player-scope
/// path. Covers the structured "your opponent's Digimon cannot digivolve" style; arbitrary per-permanent
/// predicates (the original's <c>Func&lt;Permanent,bool&gt;</c>) beyond CardType/meta scoping are per-card.</summary>
public sealed class ContinuousPlayerScopeRestrictionEffect : ICardEffect
{
    private readonly HeadlessPlayerId _scopePlayerId;

    public ContinuousPlayerScopeRestrictionEffect(CardSource card, HeadlessPlayerId scopePlayerId, string restrictionKey, string? scopeCardType, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? scopePredicate = null, Func<CardSource, bool>? causingEffectPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(restrictionKey);
        Card = card;
        _scopePlayerId = scopePlayerId;
        RestrictionKey = restrictionKey;
        ScopeCardType = scopeCardType;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        ScopePredicate = scopePredicate;
        CausingEffectPredicate = causingEffectPredicate;
    }

    public CardSource Card { get; }

    public string RestrictionKey { get; }

    public string? ScopeCardType { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public Func<CardSource, bool>? ScopePredicate { get; }

    public Func<CardSource, bool>? CausingEffectPredicate { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = _scopePlayerId.Value,
            [RestrictionKey] = true,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (ScopePredicate is not null)
        {
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = ScopePredicate;
        }

        if (CausingEffectPredicate is not null)
        {
            values[RestrictionHelpers.CausingEffectPredicateKey] = CausingEffectPredicate;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(W6-X) inert mirror of AS-IS <c>AddDetailClass</c> — tooltip text only (no consumer beyond
/// the AS-IS UI); the binding carries the string for observability.</summary>
public sealed class DisplayDetailEffect : ICardEffect
{
    private readonly CardSource _card;
    private readonly string _detail;
    private readonly Func<bool>? _condition;

    public DisplayDetailEffect(CardSource card, string detail, Func<bool>? condition)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _detail = detail;
        _condition = condition;
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["display.detail"] = _detail };
        if (_condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _condition;
        }

        var context = new EffectContext(
            _card.Controller, _card.Owner, _card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { _card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), _card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, queryScopes: null, effect: null, duration: null);
    }
}

/// <summary>(W6 tail) the AS-IS <c>StartOfMainAttack</c> activate body: at the owner's main-phase start,
/// open a MANDATORY attack offer for the granted Digimon (AS-IS SetCanNotSelectNotAttack — cannot decline;
/// player or any Digimon).</summary>
public sealed class StartOfMainAttackEffect : Headless.Effects.IHeadlessCardEffect
{
    private readonly EngineContext _context;
    private readonly HeadlessEntityId _attackerId;

    public StartOfMainAttackEffect(EngineContext context, HeadlessEntityId attackerId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _attackerId = attackerId;
    }

    public Headless.Effects.CardEffectDefinition Definition => new(
        new HeadlessEntityId($"start-of-main-attack:{_attackerId.Value}"), _attackerId,
        "[Start of Your Main Phase] Attack with this Digimon.", Headless.Effects.TriggerTimings.OnStartMainPhase,
        isOptional: false);

    public Headless.Effects.CardEffectCanResolveResult CanResolve(Headless.Effects.CardEffectResolveContext context)
    {
        bool onField = _context.ZoneMover is IZoneStateReader zones &&
            _context.CardInstanceRepository.TryGetInstance(_attackerId, out CardInstanceRecord? rec) && rec is not null &&
            zones.GetCards(rec.OwnerId, ChoiceZone.BattleArea).Contains(_attackerId);
        return onField
            ? Headless.Effects.CardEffectCanResolveResult.Success()
            : Headless.Effects.CardEffectCanResolveResult.Failure("The granted Digimon is no longer on the battle area.");
    }

    public ValueTask<Headless.Effects.EffectResult> ResolveAsync(
        Headless.Effects.CardEffectResolveContext context,
        Headless.Effects.IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        Headless.Runtime.EffectDrivenAttack.RequestChoice(
            _context, _attackerId,
            new Headless.Runtime.EffectAttackOptions(WithoutTap: false, AllowPlayerTarget: true, AllowDigimonTarget: true, TargetUnsuspended: true));
        return ValueTask.FromResult(Headless.Effects.EffectResult.Success("Attack offer opened."));
    }
}

/// <summary>(W6-A2) Mirror of the AS-IS Arts-Digivolve resolution (an <c>OptionResolutionClass</c>): from
/// the executing area, pick an owner Digimon this card can legally digivolve onto (normal requirement,
/// cost unpaid) and stack this card on top (WhenDigivolving fires; effects auto-register).</summary>
public sealed class ArtsDigivolveSelfEffect : IActivatedCardEffect
{
    public ArtsDigivolveSelfEffect(CardSource card)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
    }

    public CardSource Card { get; }

    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        List<ChoiceCandidate> candidates = zones.GetCards(Card.Owner, ChoiceZone.BattleArea)
            .Where(id => Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, Card.InstanceId, id, out _, out _))
            // AS-IS CanPlayCardTargetFrame includes the CanNotEvolve gate — same restriction as the
            // normal digivolve path.
            .Where(id => !Headless.Runtime.ContinuousRestrictionGate.EvaluateDigivolve(context, id).IsRestricted)
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        if (candidates.Count == 0)
        {
            return;   // AS-IS CanResolveCondition: no qualifying Digimon -> no resolution.
        }

        var request = new ChoiceRequest(
            ChoiceType.Card, Card.Owner, "Arts Digivolve: choose a Digimon to digivolve onto.",
            minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId targetId = result.SelectedIds[0];
        ChoiceZone fromZone = zones.GetCards(Card.Owner, ChoiceZone.Execution).Contains(Card.InstanceId)
            ? ChoiceZone.Execution
            : ChoiceZone.Hand;

        // The AS-IS PlayCardClass(payCost:false, root:Execution, target) evolution placement, in order:
        // target off its spot -> this card onto it -> the target stack folds under -> WhenDigivolving.
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(Card.Owner, targetId, ChoiceZone.BattleArea, ChoiceZone.None), cancellationToken).ConfigureAwait(false);
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(Card.Owner, Card.InstanceId, fromZone, ChoiceZone.BattleArea), cancellationToken).ConfigureAwait(false);
        Headless.Runtime.DigivolveAction.AttachTargetAsSource(context.CardInstanceRepository, Card.InstanceId, targetId);
        TriggerEventEmitter.Emit(context.GameEventQueue, Headless.Effects.TriggerTimings.WhenDigivolving, actor: Card.Owner, subject: Card.InstanceId);
        CardEffectRegistrar.RegisterCard(context, Card.InstanceId, Card.Owner);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException("Arts Digivolve is resolved via the activation flow, not registered.");
}

/// <summary>(PRIM-P0-flow B.O.3) The cost treatment of a select-and-digivolve (AS-IS payCost / reduceCost /
/// fixedCost knobs).</summary>
public enum DigivolveCost
{
    Free,     // payCost:false
    Normal,   // the resolved evolution cost (ContinuousModifierGate-folded)
    Reduced,  // Normal minus a fixed amount (floored at 0)
    Fixed,    // a fixed literal cost
}

/// <summary>(PRIM-P0-flow B.O.3) The headless mirror of the AS-IS <c>DigivolveIntoHandOrTrashCard</c> (309
/// cards): select 1 of the owner's Digimon (<paramref name="targetPredicate"/>), select a source card in
/// <c>sourceZone</c> (Hand / Trash, matching <c>sourcePredicate</c>) that can legally digivolve onto it, pay the
/// cost per <see cref="DigivolveCost"/>, and place the source onto the target as a digivolution (stack fold +
/// WhenDigivolving). A generalization of <see cref="ArtsDigivolveSelfEffect"/> adding source-card selection and
/// cost. v1 ENFORCES digivolution requirements (the TryGetEvolutionCost gate); requirement-bypass is a follow-up.
/// See docs/porting/select_and_digivolve_design.md.</summary>
public sealed class SelectAndDigivolveEffect : IActivatedCardEffect
{
    private readonly ChoiceZone _sourceZone;
    private readonly Func<HeadlessEntityId, bool> _sourcePredicate;
    private readonly Func<HeadlessEntityId, bool> _targetPredicate;
    private readonly DigivolveCost _cost;
    private readonly int _costAmount;

    public SelectAndDigivolveEffect(CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> sourcePredicate,
        Func<HeadlessEntityId, bool> targetPredicate, DigivolveCost cost, int costAmount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sourcePredicate);
        ArgumentNullException.ThrowIfNull(targetPredicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _sourceZone = sourceZone;
        _sourcePredicate = sourcePredicate;
        _targetPredicate = targetPredicate;
        _cost = cost;
        _costAmount = costAmount;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        // 1. Select the target Digimon (own battle area, predicate, not digivolve-restricted).
        var targetCandidates = zones.GetCards(Card.Owner, ChoiceZone.BattleArea)
            .Where(_targetPredicate)
            .Where(id => !Headless.Runtime.ContinuousRestrictionGate.EvaluateDigivolve(context, id).IsRestricted)
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        if (targetCandidates.Count == 0)
        {
            return;
        }

        ChoiceResult targetResult = await context.ChoiceProvider.ChooseAsync(
            new ChoiceRequest(ChoiceType.Card, Card.Owner, Description, minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, targetCandidates),
            cancellationToken).ConfigureAwait(false);
        if (targetResult.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId targetId = targetResult.SelectedIds[0];

        // 2. Select the source card in the zone that can legally digivolve onto the target (TryGetEvolutionCost
        // is the AS-IS CanPlayCardTargetFrame legality + requirement gate).
        var sourceCandidates = zones.GetCards(Card.Owner, _sourceZone)
            .Where(_sourcePredicate)
            .Where(id => Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, id, targetId, out _, out _))
            .Select(id => new ChoiceCandidate(id, id.Value, _sourceZone, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        if (sourceCandidates.Count == 0)
        {
            return;
        }

        ChoiceResult sourceResult = await context.ChoiceProvider.ChooseAsync(
            new ChoiceRequest(ChoiceType.Card, Card.Owner, Description, minCount: 1, maxCount: 1, canSkip: false, _sourceZone, sourceCandidates),
            cancellationToken).ConfigureAwait(false);
        if (sourceResult.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId sourceId = sourceResult.SelectedIds[0];

        // 3. Resolve the cost.
        int normalCost = Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, sourceId, targetId, out int resolved, out _) ? resolved : 0;
        int payCost = _cost switch
        {
            DigivolveCost.Free => 0,
            DigivolveCost.Fixed => Math.Max(0, _costAmount),
            DigivolveCost.Reduced => Math.Max(0, normalCost - _costAmount),
            _ => normalCost,
        };

        // 4. Pay (skip if the owner cannot afford — AS-IS CanPlayCardTargetFrame would not have offered it).
        if (payCost > 0)
        {
            if (!context.MemoryController.CanPay(payCost))
            {
                return;
            }

            context.MemoryController.Pay(payCost);
        }

        // 5. Place the source onto the target as a digivolution (same order as ArtsDigivolveSelfEffect).
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(Card.Owner, targetId, ChoiceZone.BattleArea, ChoiceZone.None), cancellationToken).ConfigureAwait(false);
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(Card.Owner, sourceId, _sourceZone, ChoiceZone.BattleArea), cancellationToken).ConfigureAwait(false);
        Headless.Runtime.DigivolveAction.AttachTargetAsSource(context.CardInstanceRepository, sourceId, targetId);
        TriggerEventEmitter.Emit(context.GameEventQueue, Headless.Effects.TriggerTimings.WhenDigivolving, actor: Card.Owner, subject: sourceId);
        CardEffectRegistrar.RegisterCard(context, sourceId, Card.Owner);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-and-digivolve is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W2) Mirror of the original <c>&lt;Link&gt;</c> activation (<c>CardEffectFactory.LinkEffect</c>):
/// attach THIS card as a link card to a chosen own battle-area Digimon, paying the link cost. Drives the
/// host choice through the activation <c>ChoiceProvider</c> and attaches via
/// <see cref="Runtime.LinkHelpers.AddLinkCardAsync"/> (which emits the WhenLinked window / trims the host's
/// link max). Bounded to the self-play synchronous flow; the link CONDITION (which hosts are valid) is a
/// per-card predicate.</summary>
public sealed class LinkSelfEffect : IActivatedCardEffect
{
    public LinkSelfEffect(CardSource card, int linkCost, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        LinkCost = linkCost;
        Description = description;
    }

    public CardSource Card { get; }

    public int LinkCost { get; }

    public string Description { get; }

    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        // (W6-L) AS-IS LinkEffect's CanSelectPermanentCondition: owner battle-area Digimon, not this
        // card's own permanent, AND the declared linkCondition.digimonCondition (Link.cs:18).
        LinkCondition? linkCondition = Card.LinkConditionOf();
        List<ChoiceCandidate> candidates = zones.GetCards(Card.Owner, ChoiceZone.BattleArea)
            .Where(id => id != Card.InstanceId && CardEffectCommons.IsOwnerBattleAreaDigimon(Card, id))
            .Where(id => linkCondition is null || linkCondition.digimonCondition(new Permanent(context, id, Card.Owner)))
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        if (candidates.Count == 0)
        {
            return; // no valid host.
        }

        var request = new ChoiceRequest(
            ChoiceType.Card, Card.Owner, Description, minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.BattleArea, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        // (M-4) fold continuous linkCostDelta reductions (GrantedReduceLinkCost) into the paid cost.
        int effectiveLinkCost = LinkHelpers.ResolveLinkCost(context, Card.InstanceId, LinkCost);
        if (effectiveLinkCost > 0)
        {
            context.MemoryController.Pay(effectiveLinkCost);
        }

        ChoiceZone from = zones.GetCards(Card.Owner, ChoiceZone.Hand).Contains(Card.InstanceId) ? ChoiceZone.Hand : ChoiceZone.BattleArea;
        await LinkHelpers.AddLinkCardAsync(
            context.CardInstanceRepository, context.ZoneMover, result.SelectedIds[0], Card.InstanceId, from, context.GameEventQueue, cancellationToken, context)
            .ConfigureAwait(false);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Link effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W1-6/9) A continuous "added digivolution requirement" on self — grants this card an
/// ADDITIONAL "Color@Level" from-condition (AS-IS AddDigivolutionRequirementStaticEffect /
/// AddDigivolutionRequirementClass). Registered under <see cref="ContinuousRestrictionGate.Scope"/> carrying
/// <see cref="DigivolveAction.AddedEvolutionConditionKey"/>; DigivolveAction consults it when the printed
/// condition fails. Condition / inherited honoured. (Per-path cost is composed via
/// <c>ChangeDigivolutionCostStaticEffect</c> or handled per-card.)</summary>
public sealed class AddedDigivolutionRequirementEffect : ICardEffect
{
    public AddedDigivolutionRequirementEffect(CardSource card, string fromCondition, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromCondition);
        Card = card;
        FromCondition = fromCondition;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }

    public string FromCondition { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DigivolveAction.AddedEvolutionConditionKey] = FromCondition,
        };
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) Predicate-based added digivolution source (AS-IS
/// <c>AddSelfDigivolutionRequirementStaticEffect</c>): "you can also digivolve this card from any Digimon
/// matching <see cref="Predicate"/> (for <see cref="DigivolutionCost"/> memory)". Registers the predicate on a
/// continuous binding that <c>DigivolveAction</c> evaluates by building the under-card as a <see cref="Permanent"/>.
/// Cost/ignore-requirement are retained for fidelity; the primary behavior is enabling the digivolve.</summary>
public sealed class AddedDigivolutionRequirementPredicateEffect : ICardEffect
{
    public AddedDigivolutionRequirementPredicateEffect(CardSource card, Func<Permanent, bool> predicate, int digivolutionCost, bool ignoreDigivolutionRequirement, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? targetCardCondition = null, Func<int>? costEquation = null, int level = -1, int minLevel = -1, int maxLevel = -1)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        Predicate = predicate;
        DigivolutionCost = digivolutionCost;
        IgnoreDigivolutionRequirement = ignoreDigivolutionRequirement;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        TargetCardCondition = targetCardCondition;
        CostEquation = costEquation;
        Level = level;
        MinLevel = minLevel;
        MaxLevel = maxLevel;
    }

    /// <summary>(A2) AS-IS level / minLevel / maxLevel — a HARD level gate on the digivolving-FROM permanent,
    /// separate from (and evaluated before) <see cref="Predicate"/>. Exact <see cref="Level"/> wins; the
    /// min/max range applies only when Level &lt; 0. -1 = unset.</summary>
    public int Level { get; }
    public int MinLevel { get; }
    public int MaxLevel { get; }

    public CardSource Card { get; }
    public Func<Permanent, bool> Predicate { get; }
    public int DigivolutionCost { get; }

    /// <summary>(FR2/M-3) AS-IS costEquation — a DYNAMIC digivolution cost for this added path, evaluated at read
    /// time (<c>costEquation() ?? digivolutionCost</c>). Null = the fixed <see cref="DigivolutionCost"/>.</summary>
    public Func<int>? CostEquation { get; }

    public bool IgnoreDigivolutionRequirement { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    /// <summary>(FR2/M-1) AS-IS cardCondition — WHICH cards receive this added digivolution requirement. Null =
    /// self only (default <c>cs => cs == card</c>). Non-null = any owner's card matching it (player-scope +
    /// predicate), e.g. "your UlforceVeedramon cards in hand".</summary>
    public Func<CardSource, bool>? TargetCardCondition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DigivolveAction.AddedEvolutionPredicateKey] = Predicate,
            [DigivolveAction.AddedEvolutionCostKey] = DigivolutionCost,
        };
        if (CostEquation is not null)
        {
            values[DigivolveAction.AddedEvolutionCostEquationKey] = CostEquation;
        }

        if (Level >= 0)
        {
            values[DigivolveAction.AddedEvolutionLevelKey] = Level;
        }

        if (MinLevel >= 0)
        {
            values[DigivolveAction.AddedEvolutionMinLevelKey] = MinLevel;
        }

        if (MaxLevel >= 0)
        {
            values[DigivolveAction.AddedEvolutionMaxLevelKey] = MaxLevel;
        }

        if (TargetCardCondition is not null)
        {
            // Player-scope so the requirement reaches every owner's card the predicate selects (not just self).
            values[PlayerScopeContinuousHelpers.PlayerScopeKey] = true;
            values[PlayerScopeContinuousHelpers.ScopePlayerIdKey] = Card.Owner.Value;
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = TargetCardCondition;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>A self keyword grant (Blocker / Jamming / Reboot / Piercing) reusing the existing
/// <see cref="KeywordBaseBatch1Effect"/> resolution + gate wiring.</summary>
public sealed class SelfKeywordEffect : ICardEffect
{
    public SelfKeywordEffect(CardSource card, KeywordBaseBatch1Kind kind, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        Kind = kind;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }

    public KeywordBaseBatch1Kind Kind { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        // The keyword factory derives its own deterministic effect id from (kind, source); effectId is
        // accepted for signature uniformity with ICardEffect but not needed here.
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId });
        KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(
            Kind,
            Card.InstanceId,
            targetEntityId: Card.InstanceId,
            isInherited: IsInheritedEffect,
            isLinked: false);
        return KeywordBaseBatch1Factory.ToBinding(effect, Card.Controller, context);
    }
}

/// <summary>
/// Self-static keyword grant for the <see cref="KeywordBaseBatch2Kind"/> family (Vortex / Alliance /
/// Overclock / …). Structural twin of <see cref="SelfKeywordEffect"/> (which covers Batch1) — the original
/// <c>CardEffectFactory</c> exposes a per-keyword <c>&lt;Keyword&gt;SelfEffect</c> for each of these, so the
/// headless mirror provides the same entry points lowering to a Batch2 binding. The "this Digimon is on the
/// battle area" guard the original <c>SelfEffect</c> wraps around <paramref name="condition"/> is enforced
/// here by the binding lifecycle (registered on enter-play, unregistered on leave) + the read-time
/// <see cref="ContinuousKeywordGate"/> query, matching how the existing Batch1 self-statics behave.
/// </summary>
public sealed class SelfKeywordBatch2Effect : ICardEffect
{
    public SelfKeywordBatch2Effect(CardSource card, KeywordBaseBatch2Kind kind, bool isInheritedEffect, Func<bool>? condition, IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        Kind = kind;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        ExtraValues = extraValues;
    }

    public CardSource Card { get; }

    public KeywordBaseBatch2Kind Kind { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    /// <summary>(A4) additional binding values carried on the grant (e.g. Partition's stored
    /// <c>PartitionCondition</c> list) — consumed live by the keyword's behaviour gate.</summary>
    public IReadOnlyDictionary<string, object?>? ExtraValues { get; }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId },
            values: ExtraValues);
        KeywordBaseBatch2Effect effect = KeywordBaseBatch2Factory.Create(
            Kind,
            Card.InstanceId,
            targetEntityId: Card.InstanceId,
            isInherited: IsInheritedEffect,
            isLinked: false);
        return KeywordBaseBatch2Factory.ToBinding(effect, Card.Controller, context);
    }
}

/// <summary>(PRIM-W2) A self-static keyword grant BY NAME — for keywords outside the Batch1/Batch2 enums
/// (Raid / Barrier / Collision / Fortitude / Evade) whose behaviour gates read a metadata flag. Registers a
/// keyword binding (keywords = [name], target self) so <see cref="ContinuousKeywordGate.HasKeyword"/> reports
/// it live; the same bar as the Batch2 self-statics. Condition / inherited carried on the binding values.</summary>
public sealed class SelfKeywordByNameEffect : ICardEffect
{
    public SelfKeywordByNameEffect(CardSource card, string keywordName, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? permanentCondition = null, IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(keywordName);
        Card = card;
        KeywordName = keywordName;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        PermanentCondition = permanentCondition;
        ExtraValues = extraValues;
    }

    /// <summary>(C1) additional binding values carried on the grant (e.g. Fragment's trashValue).</summary>
    public IReadOnlyDictionary<string, object?>? ExtraValues { get; }

    public CardSource Card { get; }

    public string KeywordName { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    /// <summary>(D1) The keyword's per-card <c>permanentCondition</c>, evaluated against the OTHER
    /// permanent the keyword acts on (AS-IS Decoy: the protected Digimon), not the holder. Stored on the
    /// binding under <see cref="ContinuousKeywordGate.PermanentConditionKey"/> and read live by the
    /// consuming gate (<see cref="ContinuousKeywordGate.KeywordGrantAcceptsSubject"/>).</summary>
    public Func<CardSource, bool>? PermanentCondition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        if (PermanentCondition is not null)
        {
            values[ContinuousKeywordGate.PermanentConditionKey] = PermanentCondition;
        }

        if (ExtraValues is not null)
        {
            foreach (KeyValuePair<string, object?> pair in ExtraValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: new[] { KeywordName }, EffectQueryRole.Continuous, queryScopes: null, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W2) A continuous PLAYER-SCOPE keyword grant — grants a keyword to a player's cards
/// (optionally narrowed by CardType), e.g. "your Digimon gain &lt;Blocker&gt;". Registers a keyword binding
/// (keywords = [name]) carrying the player-scope markers; <see cref="ContinuousKeywordGate.HasKeyword"/>
/// (context overload) resolves it for any of the scoped player's cards.</summary>
public sealed class ContinuousPlayerScopeKeywordEffect : ICardEffect
{
    private readonly HeadlessPlayerId _scopePlayerId;

    public ContinuousPlayerScopeKeywordEffect(CardSource card, HeadlessPlayerId scopePlayerId, string keywordName, string? scopeCardType, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? scopePredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(keywordName);
        Card = card;
        _scopePlayerId = scopePlayerId;
        KeywordName = keywordName;
        ScopeCardType = scopeCardType;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        ScopePredicate = scopePredicate;
    }

    public CardSource Card { get; }

    public string KeywordName { get; }

    public string? ScopeCardType { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public Func<CardSource, bool>? ScopePredicate { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = _scopePlayerId.Value,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (ScopePredicate is not null)
        {
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = ScopePredicate;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: new[] { KeywordName }, EffectQueryRole.Continuous, queryScopes: null, effect: null, duration: null);
    }
}

/// <summary>(PRIM-P0 AddSkill) The headless mirror of AS-IS AddSkillClass whose getEffects splices a TRIGGERED
/// activated effect onto a live-matched set. Wraps a nested triggered effect's binding with the TriggerGrant +
/// player-scope markers so it is registered under the nested effect's timing but fires for ANY event whose actor
/// is the scoped player; the collector injects the triggering card as the subject so the nested effect (built to
/// read TriggerEntityId and apply its per-card predicate) resolves against that card. See
/// docs/porting/play_option_and_delayed_player_effect_design.md.</summary>
public sealed class PlayerScopeTriggerGrantEffect : ICardEffect
{
    public PlayerScopeTriggerGrantEffect(CardSource card, HeadlessPlayerId scopePlayer, ICardEffect nestedEffect, bool scopeAnyPlayer = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(nestedEffect);
        Card = card;
        ScopePlayer = scopePlayer;
        NestedEffect = nestedEffect;
        ScopeAnyPlayer = scopeAnyPlayer;
    }

    public CardSource Card { get; }

    public HeadlessPlayerId ScopePlayer { get; }

    public ICardEffect NestedEffect { get; }

    public bool ScopeAnyPlayer { get; }

    public EffectBinding ToBinding(string effectId)
    {
        EffectBinding inner = NestedEffect.ToBinding(effectId);
        Headless.Effects.EffectContext ctx = inner.Request.Context;
        var values = new Dictionary<string, object?>(ctx.Values, StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.TriggerGrantKey] = true,
            [Headless.Effects.PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
        };
        if (ScopeAnyPlayer)
        {
            values[Headless.Effects.PlayerScopeContinuousHelpers.ScopeAnyPlayerKey] = true;
        }
        else
        {
            values[Headless.Effects.PlayerScopeContinuousHelpers.ScopePlayerIdKey] = ScopePlayer.Value;
        }

        var newCtx = new Headless.Effects.EffectContext(ctx.SourcePlayerId, ctx.OwnerPlayerId, ctx.SourceEntityId, ctx.TriggerEntityId, ctx.TargetEntityIds, values);
        return new EffectBinding(
            new EffectRequest(inner.Request.EffectId, inner.Request.ControllerId, inner.Request.Timing, newCtx),
            inner.Keywords, inner.QueryRoles, inner.QueryScopes, inner.Effect, inner.Duration);
    }
}

/// <summary>Minimal headless mirror of the original <c>Permanent</c> — used only for the signature of
/// card <c>permanentCondition</c> predicates. Player-scope effects scope to the owner's cards directly, so
/// the predicate body is not invoked by the headless evaluation (it exists for 1:1 source fidelity).</summary>
/// <summary>(PRIM-W5-0) A battle-area permanent view — the member surface card predicates read off
/// <c>permanent.*</c>. Backed by the engine: <see cref="TopCard"/> reuses <see cref="CardSource"/> for the
/// card-view members, DP folds continuous modifiers, and digivolution sources come from the stack.</summary>
public sealed class Permanent
{
    private readonly EngineContext _context;

    public Permanent(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId ownerId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        InstanceId = instanceId;
        OwnerId = ownerId;
    }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId OwnerId { get; }

    /// <summary>The top (battling) card of this permanent as a <see cref="CardSource"/>.</summary>
    public CardSource TopCard => new(_context, InstanceId, OwnerId);

    /// <summary>Effective DP (base + continuous modifiers), or 0.</summary>
    public int DP => ContinuousDpGate.ResolveDp(_context, InstanceId, BaseDp());

    /// <summary>(A3) Mirror of <c>Permanent.Level</c> (Permanent.cs:48-102): seeds from the top card's
    /// (already card-level-folded) level, then EVERY active <see cref="CardEffects.ChangePermanentLevelClass"/>
    /// effect transforms it (AS-IS scans all field permanents' + players' effects — the registry's active
    /// bindings are that set).</summary>
    public int Level
    {
        get
        {
            int level = TopCard.Level;
            foreach (Headless.Effects.EffectRequest effect in _context.EffectRegistry.GetContinuousEffects(
                new Headless.Services.EffectQueryContext(Headless.Runtime.ContinuousRestrictionGate.Scope)))
            {
                if (effect.Context.Values.TryGetValue(CardEffects.ChangePermanentLevelClass.GetPermanentLevelKey, out object? raw)
                    && raw is Func<Permanent, int, int> transform
                    && CardSource.EffectConditionPasses(effect))
                {
                    level = transform(this, level);
                }
            }

            return level;
        }
    }

    public bool HasNoDigivolutionCards => DigivolutionCards.Count == 0;
    public bool IsDigimon => TopCard.IsDigimon;
    public bool IsTamer => TopCard.IsTamer;
    public bool IsToken => TopCard.IsToken;

    public bool IsSuspended =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool b && b;

    /// <summary>The digivolution (under-)cards of this permanent (mirror of <c>DigivolutionCards</c>).</summary>
    public IReadOnlyList<CardSource> DigivolutionCards
    {
        get
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(_context.CardInstanceRepository, _context.CardRepository, InstanceId);
            return stack.UnderCards.Select(u => new CardSource(_context, u.InstanceId, OwnerId)).ToArray();
        }
    }

    private int BaseDp() =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("dp", out object? raw) && raw is int dp ? dp : 0;

    /// <summary>(W6-P) mirror of AS-IS <c>Permanent.BaseDP</c> — the unmodified DP (IsMinDP/IsMaxDP read it).</summary>
    public int BaseDP => BaseDp();
}

/// <summary>
/// A continuous player-scope numeric modifier ("your Digimon get +X DP"). Lowers to a continuous-role
/// binding carrying the player-scope markers (<see cref="PlayerScopeContinuousHelpers"/>) so it reaches
/// every applicable card the owner controls via <see cref="ContinuousScopeEvaluation"/>.
/// </summary>
public sealed class PlayerScopeModifierEffect : ICardEffect
{
    public PlayerScopeModifierEffect(CardSource card, string deltaKey, int changeValue, string? scopeCardType, Func<bool>? condition, string? scopeZone = null, Func<CardSource, bool>? scopePredicate = null, bool scopeAnyPlayer = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        ScopeCardType = scopeCardType;
        Condition = condition;
        ScopeZone = scopeZone;
        ScopePredicate = scopePredicate;
        ScopeAnyPlayer = scopeAnyPlayer;
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public string? ScopeCardType { get; }

    public Func<bool>? Condition { get; }

    public string? ScopeZone { get; }

    public Func<CardSource, bool>? ScopePredicate { get; }

    public bool ScopeAnyPlayer { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = Card.Owner.Value,
            [DeltaKey] = ChangeValue,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (!string.IsNullOrWhiteSpace(ScopeZone))
        {
            values[PlayerScopeContinuousHelpers.ScopeZoneKey] = ScopeZone;
        }

        if (ScopePredicate is not null)
        {
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = ScopePredicate;
        }

        if (ScopeAnyPlayer)
        {
            values[PlayerScopeContinuousHelpers.ScopeAnyPlayerKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>(),
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null,
            EffectQueryRole.Continuous,
            new[] { ContinuousModifierGate.Scope },
            effect: null,
            duration: null);
    }
}

/// <summary>
/// A triggered effect that gains / loses memory when its timing fires (the common ActivateClass form
/// "[When ...] gain/lose N memory", e.g. ST1_06 / ST1_09). Carries the effect body itself so the existing
/// scheduler / resolver pipeline (TriggerEventEmitter -> AutoProcessingTriggerCollector -> EffectScheduler
/// -> CardEffectSchedulerResolver) resolves it into an AddMemory mutation on the
/// <see cref="MatchStateMutationSink"/>. The original coroutine becomes an emitted mutation (1:1 relaxed
/// for trigger plumbing).
/// </summary>
public sealed class TriggeredMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _condition;
    private readonly Func<CardEffectResolveContext, bool>? _triggerGate;

    public TriggeredMemoryEffect(
        CardSource card, EffectTiming timing, int amount, bool isInheritedEffect, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        IsInheritedEffect = isInheritedEffect;
        _condition = condition;
        _triggerGate = triggerGate;
        string trigger = EffectTimings.ToTriggerName(timing);
        var effectId = new HeadlessEntityId($"{card.InstanceId.Value}:mem:{trigger}:{amount}");
        // Gaining memory defaults to an optional "you may" prompt; a card whose trigger is mandatory passes
        // isOptional: false explicitly (e.g. ST3_04 "gain 1 memory").
        Definition = new CardEffectDefinition(effectId, card.InstanceId, description, trigger, isOptional: isOptional ?? (amount > 0), maxCountPerTurn: maxCountPerTurn, hash: hash);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public bool IsInheritedEffect { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        if (_triggerGate is not null && !_triggerGate(context))
        {
            return CardEffectCanResolveResult.Failure("Trigger event condition not met.");
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        CardEffectCanResolveResult check = CanResolve(context);
        if (!check.CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure(check.Message ?? "Cannot resolve.", check.Values));
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount };
        mutations.Apply(new EffectMutation(MatchStateMutationSink.AddMemoryKind, Definition.SourceEntityId, values));
        return ValueTask.FromResult(EffectResult.Success($"Memory {(Amount >= 0 ? "+" : string.Empty)}{Amount}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null,
            EffectQueryRole.None,
            Array.Empty<string>(),
            effect: this,
            duration: null);
    }
}

/// <summary>(G8-004) "[Security] activate this card's [Main] effect" — a security skill that re-runs the
/// card's Option [Main] activated effects. Resolved by <see cref="ActivatedEffectResolver"/>; not
/// auto-registered (security timing is excluded from <see cref="CardEffectRegistrar.AllTimings"/>).</summary>
public sealed class ReuseMainOptionEffect : IActivatedCardEffect
{
    public ReuseMainOptionEffect(string description)
    {
        Description = description;
    }

    public string Description { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reuse-main security effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (EX8-2 brick) Re-activates THIS card's <see cref="EffectTiming.WhenDigivolving"/> effects through the
/// choice flow — the headless analog of the original "[All Turns] you may activate 1 of this Digimon's
/// [When Digivolving] effects" (EX8_074 region "All Turns"). Structural twin of
/// <see cref="ReuseMainOptionEffect"/> (which re-runs [Main]/OptionSkill): when resolved,
/// <see cref="ActivatedEffectResolver"/> recursively resolves <c>CardEffects(WhenDigivolving)</c> on the same
/// sink / choice provider. The once-per-turn, "when any Digimon is played" TRIGGER that OFFERS this effect
/// is the remaining EX8-2 integration (see docs/audit/ex8_074_remaining_goals.md §EX8-2).
/// </summary>
public sealed class ReuseWhenDigivolvingEffect : IActivatedCardEffect
{
    public ReuseWhenDigivolvingEffect(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
    }

    public string Description { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reuse-when-digivolving effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>Placeholder for an original effect whose subsystem is not yet ported. Returned so a ported
/// card body compiles 1:1; never registered (its timing is excluded from
/// <see cref="CardEffectRegistrar.AllTimings"/>). If ever lowered, it fails loudly.</summary>
public sealed class DeferredCardEffect : IActivatedCardEffect
{
    public DeferredCardEffect(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Card effect not yet ported: {Reason}");
}

/// <summary>(PRIM-P0-flow) An activated "choose one of the following modes" menu (AS-IS UserSelectionManager
/// SetBool/IntSelection). Each available mode is a labeled branch (an existing <see cref="ICardEffect"/>); the
/// selected branch is dispatched recursively by the ActivatedEffectResolver. Modes whose availability predicate
/// returns false are OMITTED from the menu, mirroring the AS-IS conditional <c>selectionElements.Add</c>. The
/// menu is mandatory (pick exactly one of the offered modes). See docs/porting/mode_choice_primitive_design.md.</summary>
public sealed class ModeChoiceEffect : IActivatedCardEffect
{
    /// <summary>One mode: a menu label, an optional availability predicate (null = always available), and the
    /// branch effect run when this mode is chosen.</summary>
    public readonly record struct Mode(string Label, Func<bool>? IsAvailable, ICardEffect Branch);

    private const string ModeToken = "mode";
    private readonly IReadOnlyList<Mode> _modes;

    public ModeChoiceEffect(CardSource card, string description, IReadOnlyList<Mode> modes)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(modes);
        Card = card;
        Description = description;
        _modes = modes;
    }

    public CardSource Card { get; }

    public string Description { get; }

    /// <summary>Modes whose availability predicate passes (null predicate = always available).</summary>
    public IReadOnlyList<Mode> AvailableModes() => _modes.Where(m => m.IsAvailable?.Invoke() ?? true).ToList();

    /// <summary>The mandatory labeled-menu ChoiceRequest — one candidate per available mode (synthetic id
    /// <c>"{inst}#mode#{index}"</c> + the mode's label).</summary>
    public ChoiceRequest BuildRequest(IReadOnlyList<Mode> available)
    {
        var candidates = new List<ChoiceCandidate>(available.Count);
        for (int index = 0; index < available.Count; index++)
        {
            candidates.Add(new ChoiceCandidate(
                new HeadlessEntityId($"{Card.InstanceId.Value}#{ModeToken}#{index}"),
                available[index].Label, ChoiceZone.BattleArea, IsSelectable: true, ownerId: Card.Owner));
        }

        return new ChoiceRequest(
            ChoiceType.ModeChoice, Card.Owner, Description,
            minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, candidates);
    }

    /// <summary>The branch effect for the chosen candidate id (parses the index off <c>"{inst}#mode#{index}"</c>).</summary>
    public ICardEffect BranchFor(IReadOnlyList<Mode> available, HeadlessEntityId selectedId)
    {
        string[] parts = selectedId.Value.Split('#');
        return int.TryParse(parts.Length > 2 ? parts[2] : null, out int index) && index >= 0 && index < available.Count
            ? available[index].Branch
            : available[0].Branch;
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Mode-choice effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated targeted effect (an Option [Main] / [Security] skill that selects permanents and acts on
/// them, e.g. "delete up to 2 of your opponent's Digimon"). Wraps the <see cref="SelectPermanentEffect"/>
/// helper: <see cref="BuildRequest"/> enumerates candidates into a <c>ChoiceRequest</c> and
/// <see cref="Apply"/> applies the Mode's mutation to the chosen targets.
///
/// NOTE: the interactive activation path (Option/Security action -> resolve this effect with a live choice
/// provider) is NOT yet wired (IHeadlessCardEffect.ResolveAsync has no choice provider). These effects are
/// therefore resolved imperatively (build request -> answer -> apply), exactly as the
/// SelectPermanentEffect tests do, until that integration lands. They are not auto-registered (their
/// OptionSkill / SecuritySkill timing is excluded from <see cref="CardEffectRegistrar.AllTimings"/>).
/// </summary>
public sealed class ActivatedSelectEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();

    public ActivatedSelectEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canTarget,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        SelectPermanentEffect.Mode mode,
        string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect, canEndNotMax, mode, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string Description { get; }

    /// <summary>Enumerate the candidates into a Permanent ChoiceRequest the driver/agent answers.</summary>
    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    /// <summary>Apply the Mode's mutation to the chosen targets.</summary>
    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected) =>
        _select.Apply(sink, selected);

    // Activated effects are not auto-registered; lowering one to a binding is a wiring error.
    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated select effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-P0 B.O.4) A non-interactive one-shot before-pay cost reduction: when this card is being
/// played/digivolved and <see cref="_condition"/> holds, register a one-shot <c>playCostDelta = -amount</c>
/// self modifier tagged <see cref="EffectDuration.UntilCalculateFixedCost"/> (cleared once the cost is locked).
/// The headless mirror of the AS-IS <c>BeforePayCost</c> ActivateClass that does
/// <c>card.Owner.UntilCalculateFixedCostEffect.Add(_ =&gt; changeCostClass)</c> (e.g. BT18_057). Non-interactive
/// counterpart of <see cref="SuspendCostReductionEffect"/>. Reduces THIS play's own cost. See
/// docs/porting/cost_modification_design.md.</summary>
public sealed class BeforePayCostReductionEffect : IActivatedCardEffect
{
    private readonly Func<int> _amount;
    private readonly Func<bool>? _condition;

    public BeforePayCostReductionEffect(CardSource card, Func<int> amount, Func<bool>? condition, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _amount = amount;
        _condition = condition;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    /// <summary>Register the one-shot reduction if the condition holds and the amount is positive.</summary>
    public void Apply()
    {
        if (_condition is not null && !_condition())
        {
            return;
        }

        int amount = _amount();
        if (amount <= 0)
        {
            return;
        }

        // Register BOTH the play-cost (PlayCost metric — play & option) and digivolution-cost (DigivolutionCost
        // metric) deltas. A given action pays exactly one of these costs, so only the relevant metric's resolve
        // applies the reduction — registering both lets one effect cover play / option / digivolve uniformly.
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModifierHelpers.PlayCostDeltaKey] = -amount,
            [ModifierHelpers.DigivolutionCostDeltaKey] = -amount,
        };
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId }, values: values);
        Card.Context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:beforePayCostReduction"), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope },
            effect: null, duration: EffectDuration.UntilCalculateFixedCost));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Before-pay cost reduction is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (EX8_074 Stage 3 brick) An activated "suspend N of your Digimon to reduce THIS card's play cost by M"
/// effect — the headless composite of the original <c>SuspendPermanentsClass.Tap()</c> +
/// <c>ChangeCostClass</c> added to <c>Player.UntilCalculateFixedCostEffect</c>. Selecting EXACTLY
/// <see cref="SuspendCount"/> own Digimon suspends them (<see cref="SelectPermanentEffect.Mode.Tap"/> →
/// <c>SuspendKind</c>) and registers a one-shot self play-cost reduction binding
/// (<see cref="EffectDuration.UntilCalculateFixedCost"/> — cleared by PlayCardAction's
/// <c>ExpireFixedCostCalc</c> once the play's cost is locked, mirroring the original's one-shot lifetime).
/// Selecting fewer (declined / insufficient) applies nothing — the original adds the ChangeCostClass only
/// inside the "permanents.Count == 2" branch. Resolved via the choice flow (<see cref="ActivatedEffectResolver"/>),
/// not auto-registered. This brick is engine-side only; wiring it into the BeforePayCost pre-payment window
/// of PlayCardAction is a later stage.
/// </summary>
public sealed class SuspendCostReductionEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();

    private readonly Func<HeadlessEntityId, bool> _canSuspendTarget;

    public SuspendCostReductionEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canSuspendTarget,
        int suspendCount,
        int costReduction,
        string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canSuspendTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (suspendCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(suspendCount), "Suspend count must be positive.");
        }

        if (costReduction <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costReduction), "Cost reduction must be positive.");
        }

        Card = card;
        SuspendCount = suspendCount;
        CostReduction = costReduction;
        Description = description;
        _canSuspendTarget = canSuspendTarget;
        // Configure the suspend selection (Mode.Tap); canNoSelect is recomputed per BuildRequest from the
        // owner's affordability (see Configure). The ctor setup also keeps Apply safe if called without a
        // prior BuildRequest (mode must be Tap).
        Configure(canNoSelect: true);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public int SuspendCount { get; }

    public int CostReduction { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        // (#1↔#2 coupling) The original sets canNoSelect:false when the player cannot otherwise afford the
        // card (PayingCost > MaxMemoryCost) — the suspend is FORCED when the reduction is the only way to
        // pay; optional (canNoSelect:true) when the full cost is affordable without it.
        Configure(canNoSelect: CanAffordFullCost());
        return _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);
    }

    private void Configure(bool canNoSelect) =>
        _select.SetUp(Card.Owner, _canSuspendTarget, maxCount: SuspendCount, canNoSelect, canEndNotMax: false, SelectPermanentEffect.Mode.Tap, Card.InstanceId);

    /// <summary>Whether the owner can pay this card's FULL play cost (without this reduction, which is only
    /// registered in <see cref="Apply"/>). When false, the suspend is mandatory.</summary>
    private bool CanAffordFullCost()
    {
        if (!Card.Context.CardInstanceRepository.TryGetInstance(Card.InstanceId, out CardInstanceRecord? instance) || instance is null
            || !Card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) || def is null
            || !PlayCostHelpers.TryResolveCost(def, instance, out int baseCost, out _))
        {
            return true; // unknown cost → don't force the suspend
        }

        int fullCost = ContinuousModifierGate.ResolvePlayCost(Card.Context, Card.InstanceId, baseCost);
        return Card.Context.MemoryController.CanPay(fullCost);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        List<HeadlessEntityId> ids = selected?.ToList() ?? new List<HeadlessEntityId>();
        if (ids.Count != SuspendCount)
        {
            // Declined or short — mirror the original: the ChangeCostClass is only added when exactly N
            // Digimon were suspended. No suspend, no reduction.
            return;
        }

        _select.Apply(sink, ids);
        Card.Context.EffectRegistry.Register(BuildReductionBinding());
    }

    /// <summary>The one-shot self play-cost reduction the suspend pays for — a <c>playCostDelta = -M</c>
    /// continuous self modifier scoped/keyed exactly like <see cref="ContinuousSelfModifierEffect"/>, but
    /// tagged <see cref="EffectDuration.UntilCalculateFixedCost"/> so it lasts only until this play's cost is
    /// locked in (mirrors <c>Player.UntilCalculateFixedCostEffect.Add(_ => changeCostClass)</c>).</summary>
    private EffectBinding BuildReductionBinding()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModifierHelpers.PlayCostDeltaKey] = -CostReduction,
        };
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId },
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:beforePayCostReduction"), Card.Controller, "Continuous", context),
            keywords: null,
            EffectQueryRole.Continuous,
            new[] { ContinuousModifierGate.Scope },
            effect: null,
            duration: EffectDuration.UntilCalculateFixedCost);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Suspend-cost-reduction effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated effect that SELECTS targets and grants each a continuous numeric modifier for a
/// <see cref="EffectDuration"/> (e.g. ST1_13 [Main] "1 of your Digimon gets +3000 DP for the turn").
/// <see cref="ApplyBuff"/> registers a duration-tagged continuous binding per chosen target, so the
/// existing gate folds it in and <see cref="EffectDurationExpiry"/> removes it on expiry.
/// </summary>
public sealed class ActivatedTargetBuffEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();

    public ActivatedTargetBuffEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string deltaKey, int changeValue, EffectDuration duration, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        Duration = duration;
        Description = description;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Custom, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public EffectDuration Duration { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    /// <summary>Register a duration-tagged continuous modifier on each chosen target.</summary>
    public void ApplyBuff(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        int index = 0;
        foreach (HeadlessEntityId target in selected)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [DeltaKey] = ChangeValue };
            var context = new EffectContext(
                Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { target }, values: values);
            var binding = new EffectBinding(
                new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:buff:{target.Value}:{DeltaKey}:{index++}"), Card.Controller, "Continuous", context),
                keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }, effect: null, duration: Duration);
            Card.Context.EffectRegistry.Register(binding);
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated buff effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated PLAYER-SCOPE timed buff ("all your Digimon gain +X for a duration", e.g. ST1_13 [Security]
/// "all your Digimon gain Security Attack +1 until your next turn end"). <see cref="ApplyBuff"/> registers
/// one duration-tagged player-scope continuous binding.
/// </summary>
public sealed class ActivatedPlayerScopeBuffEffect : IActivatedCardEffect
{
    private readonly HeadlessPlayerId _scopePlayerId;

    public ActivatedPlayerScopeBuffEffect(CardSource card, string deltaKey, int changeValue, EffectDuration duration, string scopeCardType, string description, string? scopeZone = null, HeadlessPlayerId? scopePlayerId = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        Duration = duration;
        ScopeCardType = scopeCardType;
        ScopeZone = scopeZone;
        Description = description;
        _scopePlayerId = scopePlayerId ?? card.Owner;
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public EffectDuration Duration { get; }

    public string? ScopeCardType { get; }

    public string? ScopeZone { get; }

    public string Description { get; }

    public void ApplyBuff()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = _scopePlayerId.Value,
            [DeltaKey] = ChangeValue,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (!string.IsNullOrWhiteSpace(ScopeZone))
        {
            values[PlayerScopeContinuousHelpers.ScopeZoneKey] = ScopeZone;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        var binding = new EffectBinding(
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:pscopebuff:{DeltaKey}:{ScopeZone ?? "battle"}:{_scopePlayerId.Value}"), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }, effect: null, duration: Duration);
        Card.Context.EffectRegistry.Register(binding);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated player-scope buff is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// A triggered "[When ...] unsuspend this Digimon" effect (the common ActivateClass IUnsuspendPermanents
/// form, e.g. ST2_11). Auto-registered under its trigger timing; on resolution emits an Unsuspend mutation
/// on the source card. (The original's [Once Per Turn] gate maps to the once-flag subsystem; the headless
/// emission is unconditional for now — a 1:1 relaxation, like the threshold relaxations in ST1.)
/// </summary>
public sealed class TriggeredUnsuspendSelfEffect : ICardEffect, IHeadlessCardEffect
{
    public TriggeredUnsuspendSelfEffect(CardSource card, EffectTiming timing, string description, int? maxCountPerTurn = null, string? hash = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:unsuspendself:{trigger}"), card.InstanceId, description, trigger,
            isOptional: true, maxCountPerTurn: maxCountPerTurn, hash: hash);
    }

    public CardSource Card { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();
        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.UnsuspendKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
        return ValueTask.FromResult(EffectResult.Success("Unsuspend this Digimon."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>(PRIM-W2) A triggered "set your memory to <see cref="TargetMemory"/> if it is
/// &lt;= <see cref="Threshold"/>" effect — the Tamer memory-setter family (AS-IS SetMemoryTo3TamerEffect:
/// "[Start of Your Turn] If you have 2 or less memory, set your memory to 3."). Auto-registered under its
/// timing (OnStartTurn); resolves only on the owner's turn (mirrors IsOwnerTurn) and only when the current
/// memory is at or below the threshold, emitting a SetMemory mutation.</summary>
public sealed class TriggeredSetMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    public TriggeredSetMemoryEffect(CardSource card, EffectTiming timing, int targetMemory, int threshold, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        TargetMemory = targetMemory;
        Threshold = threshold;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:setmemory:{trigger}"), card.InstanceId, description, trigger, isOptional: false);
    }

    public CardSource Card { get; }

    public int TargetMemory { get; }

    public int Threshold { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        // AS-IS IsOwnerTurn + "2 or less memory" gate.
        if (Card.Context.TurnController.Current.TurnPlayerId != Card.Owner
            || Card.Context.MemoryController.Current.Current > Threshold)
        {
            return ValueTask.FromResult(EffectResult.Success("Set-memory condition not met; no change."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.SetMemoryKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = TargetMemory }));
        return ValueTask.FromResult(EffectResult.Success($"Set memory to {TargetMemory}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>(PRIM-W3) A triggered "gain <see cref="Amount"/> memory (if <see cref="ExtraCondition"/> holds)"
/// effect — the Tamer memory-gain family (AS-IS Gain1MemoryTamerOpponentDigimonEffect etc.). Auto-registered
/// under its timing; resolves only on the owner's turn (and when the extra condition passes), emitting an
/// AddMemory mutation.</summary>
public sealed class TriggeredGainMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _extraCondition;

    public TriggeredGainMemoryEffect(CardSource card, EffectTiming timing, int amount, string description, Func<bool>? extraCondition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        _extraCondition = extraCondition;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:gainmemory:{trigger}"), card.InstanceId, description, trigger, isOptional: false);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        if (Card.Context.TurnController.Current.TurnPlayerId != Card.Owner || (_extraCondition is not null && !_extraCondition()))
        {
            return ValueTask.FromResult(EffectResult.Success("Gain-memory condition not met; no change."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
        return ValueTask.FromResult(EffectResult.Success($"Gain {Amount} memory."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>
/// An activated "select up to <paramref name="maxCount"/> opponent Digimon and trash
/// <paramref name="trashCount"/> of each host's digivolution cards" effect (e.g. ST2_03 / ST2_06 / ST2_09).
/// Resolved imperatively (BuildRequest → answer → Apply); Apply emits a TrashDigivolutionCards mutation
/// (host = selected target) for each chosen host.
/// </summary>
public sealed class ActivatedSelectTrashDigivolutionEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();
    private readonly int _trashCount;
    private readonly bool _fromBottom;

    public ActivatedSelectTrashDigivolutionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int trashCount, bool fromBottom, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _trashCount = trashCount;
        _fromBottom = fromBottom;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Custom, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        foreach (HeadlessEntityId host in selected)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashDigivolutionCardsKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
                    [MatchStateMutationSink.CountKey] = _trashCount,
                    [MatchStateMutationSink.FromBottomKey] = _fromBottom,
                }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated trash-digivolution effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM special-play) AS-IS <c>IDigiBurst</c>: a <c>[Digi-Burst N]</c> effect — trash N of THIS card's
/// OWN digivolution sources as a cost, then resolve <see cref="InnerEffect"/>. Gated on the permanent holding at
/// least <see cref="Count"/> digivolution cards (AS-IS <c>CanDigiBurst</c>). Resolved via the activation flow.</summary>
public sealed class DigiBurstActivatedEffect : IActivatedCardEffect
{
    public DigiBurstActivatedEffect(CardSource card, int count, ICardEffect innerEffect, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(innerEffect);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Count = count < 1 ? 1 : count;
        InnerEffect = innerEffect;
        Description = description;
    }

    public CardSource Card { get; }

    public int Count { get; }

    public ICardEffect InnerEffect { get; }

    public string Description { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Digi-Burst effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "gain/lose N memory" effect for player-activated skills (Option [Main] / [Security], e.g.
/// ST2_13). Resolved imperatively; <see cref="Apply"/> emits an AddMemory mutation.
/// </summary>
public sealed class ActivatedMemoryEffect : IActivatedCardEffect
{
    public ActivatedMemoryEffect(CardSource card, int amount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        Description = description;
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated memory effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A1) Mirror of the original <c>DrawClass</c> (DCGO/Assets/Scripts/Script/CardController.cs):
/// "draw <see cref="DrawCount"/> cards" from the top of the controller's library to their hand. The AS-IS
/// <c>Draw()</c> guards drawCount &gt; 0 and an empty library (no-op), and draws min(count, available); those
/// guards live in <c>ZoneMover.DrawAsync</c>, which this stages via the sink's <c>DrawCards</c> mutation so
/// it flushes once with the rest of the activation (re-run safe under the deferred-choice cycle — a later
/// effect suspending will NOT double-draw, since nothing flushes until resolution completes).
/// </summary>
public sealed class DrawEffect : IActivatedCardEffect
{
    public DrawEffect(CardSource card, int drawCount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        DrawCount = drawCount;
        Description = description;
    }

    public CardSource Card { get; }

    public int DrawCount { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        // AS-IS DrawClass.Draw(): `if (_drawCount <= 0) yield break;` — emit nothing for a non-positive count.
        if (DrawCount <= 0)
        {
            return;
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DrawCardsKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = Card.Owner,
                [MatchStateMutationSink.CountKey] = DrawCount,
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Draw effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A2) Mirror of the original <c>SimplifiedSelectCardConditionClass</c>
/// (DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs): a single "select up to
/// <see cref="MaxCount"/> revealed cards matching <see cref="CanTargetCondition"/>, sending the chosen ones
/// to <see cref="SelectedTo"/>" condition, used inside <see cref="SimplifiedRevealAndSelectEffect"/>. The
/// AS-IS <c>mode</c> (<c>SelectCardEffect.Mode</c>) is represented by a <see cref="RevealDestination"/> — the
/// dominant BT usage is <c>Mode.AddHand</c> → <see cref="RevealDestination.Hand"/> (tutor). The AS-IS
/// <c>selectCardCoroutine</c> (Mode.Custom) per-card action is a per-card follow-up (not modeled here).
/// </summary>
public sealed class SimplifiedSelectCardConditionClass
{
    public SimplifiedSelectCardConditionClass(
        Func<HeadlessEntityId, bool> canTargetCondition,
        string message,
        RevealDestination selectedTo,
        int maxCount)
    {
        ArgumentNullException.ThrowIfNull(canTargetCondition);
        CanTargetCondition = canTargetCondition;
        Message = message ?? string.Empty;
        SelectedTo = selectedTo;
        MaxCount = maxCount;
    }

    public Func<HeadlessEntityId, bool> CanTargetCondition { get; }

    public string Message { get; }

    public RevealDestination SelectedTo { get; }

    public int MaxCount { get; }
}

/// <summary>(PRIM-W2) Mirror of the original <c>SelectCardConditionClass</c>
/// (CardEffectCommons/RevealLibrary.cs) — the fuller reveal-select descriptor
/// (<see cref="SimplifiedSelectCardConditionClass"/> is the simplified twin, which the original maps onto
/// this). Consumed by the same reveal-select mechanism (<see cref="SimplifiedRevealAndSelectEffect"/>) via
/// <see cref="ToSimplified"/>. The advanced predicates (by-pre-selected-list / can-end-select) and the
/// <c>Mode.Custom</c> per-card select action are accepted for 1:1 source fidelity but resolved per-card.</summary>
public sealed class SelectCardConditionClass
{
    public SelectCardConditionClass(
        Func<HeadlessEntityId, bool> canTargetCondition,
        Func<IReadOnlyList<HeadlessEntityId>, HeadlessEntityId, bool>? canTargetConditionByPreSelectedList,
        Func<IReadOnlyList<HeadlessEntityId>, bool>? canEndSelectCondition,
        bool canNoSelect,
        string message,
        int maxCount,
        bool canEndNotMax,
        RevealDestination selectedTo)
    {
        ArgumentNullException.ThrowIfNull(canTargetCondition);
        CanTargetCondition = canTargetCondition;
        CanTargetConditionByPreSelectedList = canTargetConditionByPreSelectedList;
        CanEndSelectCondition = canEndSelectCondition;
        CanNoSelect = canNoSelect;
        Message = message ?? string.Empty;
        MaxCount = maxCount;
        CanEndNotMax = canEndNotMax;
        SelectedTo = selectedTo;
    }

    public Func<HeadlessEntityId, bool> CanTargetCondition { get; }

    public Func<IReadOnlyList<HeadlessEntityId>, HeadlessEntityId, bool>? CanTargetConditionByPreSelectedList { get; }

    public Func<IReadOnlyList<HeadlessEntityId>, bool>? CanEndSelectCondition { get; }

    public bool CanNoSelect { get; }

    public string Message { get; }

    public int MaxCount { get; }

    public bool CanEndNotMax { get; }

    public RevealDestination SelectedTo { get; }

    /// <summary>The core (condition/message/destination/maxCount) as the simplified twin the reveal-select
    /// mechanism consumes.</summary>
    public SimplifiedSelectCardConditionClass ToSimplified() =>
        new(CanTargetCondition, Message, SelectedTo, MaxCount);
}

/// <summary>
/// (BT-PRE-A2) Mirror of the original <c>CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect</c>: reveal
/// the top <see cref="RevealCount"/> library cards, run each <see cref="SimplifiedSelectCardConditionClass"/>
/// in turn (select condition-matching revealed cards → that condition's destination), then send every
/// still-unselected revealed card to <see cref="RemainingTo"/>. Choices flow through the activation
/// <c>ChoiceProvider</c> (re-run safe: choose-then-stage, all moves staged on the sink and flushed once).
/// </summary>
public sealed class SimplifiedRevealAndSelectEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<SimplifiedSelectCardConditionClass> _conditions;

    public SimplifiedRevealAndSelectEffect(
        CardSource card,
        int revealCount,
        IReadOnlyList<SimplifiedSelectCardConditionClass> conditions,
        RevealDestination remainingTo,
        string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        RevealCount = revealCount;
        _conditions = conditions;
        RemainingTo = remainingTo;
        Description = description;
    }

    public CardSource Card { get; }

    public int RevealCount { get; }

    public RevealDestination RemainingTo { get; }

    public string Description { get; }

    /// <summary>Reveal + per-condition select + destination routing. Driven by <see cref="ActivatedEffectResolver"/>
    /// (which has the live ChoiceProvider); all moves are staged on <paramref name="sink"/> for one flush.</summary>
    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        HeadlessPlayerId player = Card.Owner;
        List<HeadlessEntityId> revealed = zones.GetCards(player, ChoiceZone.Library)
            .Take(Math.Max(0, RevealCount)).ToList();
        if (revealed.Count == 0)
        {
            return; // AS-IS: nothing to reveal -> no-op.
        }

        var picked = new HashSet<HeadlessEntityId>();
        foreach (SimplifiedSelectCardConditionClass condition in _conditions)
        {
            List<ChoiceCandidate> candidates = revealed
                .Where(id => !picked.Contains(id) && condition.CanTargetCondition(id))
                .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Library, IsSelectable: true, ownerId: player))
                .ToList();
            if (candidates.Count == 0)
            {
                continue; // no match for this condition -> skip it (AS-IS surfaces an empty/auto selection).
            }

            int max = Math.Min(condition.MaxCount, candidates.Count);
            var request = new ChoiceRequest(
                ChoiceType.Card, player, string.IsNullOrEmpty(condition.Message) ? Description : condition.Message,
                minCount: 0, maxCount: Math.Max(1, max), canSkip: true, ChoiceZone.Library, candidates);

            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.IsSkipped)
            {
                continue;
            }

            foreach (HeadlessEntityId id in result.SelectedIds)
            {
                if (picked.Add(id))
                {
                    StageMove(sink, id, condition.SelectedTo);
                }
            }
        }

        foreach (HeadlessEntityId id in revealed)
        {
            if (!picked.Contains(id))
            {
                StageMove(sink, id, RemainingTo);
            }
        }
    }

    private void StageMove(MatchStateMutationSink sink, HeadlessEntityId cardId, RevealDestination destination)
    {
        string kind = destination switch
        {
            RevealDestination.Hand => MatchStateMutationSink.ReturnToHandKind,
            RevealDestination.DeckTop => MatchStateMutationSink.ReturnToDeckTopKind,
            RevealDestination.DeckBottom => MatchStateMutationSink.ReturnToDeckBottomKind,
            RevealDestination.Trash => MatchStateMutationSink.TrashCardKind,
            _ => MatchStateMutationSink.ReturnToDeckBottomKind,
        };
        sink.Apply(new EffectMutation(
            kind, Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reveal-and-select effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (P4) Mirror of the FULL AS-IS <c>CardEffectCommons.RevealDeckTopCardsAndSelect</c> with a
/// <c>SelectCardConditionClass[]</c> (multi-condition, BT10-096/BT10-097/ST17-11 shape,
/// RevealLibrary.cs:229-465): reveal the top N cards, then run each pass SEQUENTIALLY over the shared
/// revealed pool — per-pass maxCount = Min(pass.MaxCount, matching in the CURRENT pool), chosen cards
/// leave the pool, a pass with no matching card is skipped, per-pass destination
/// (<see cref="RevealDestination.Custom"/> = no move, recorded on <see cref="CustomSelections"/> for the
/// card script's follow-up). <c>canNoAction</c> (with ≥2 passes) opens the AS-IS whole-effect opt-out
/// first; <c>mutualConditions</c> mirrors the exact relaxation rule (RevealLibrary.cs:302-308). Remaining
/// cards go to <see cref="RemainingTo"/>; ≥2 deck-bound remainders open the AS-IS ordering pick (bottom =
/// pick order; top = first pick topmost). Driven by the activation <c>ChoiceProvider</c>; all moves staged
/// on the shared sink.
/// </summary>
public sealed class RevealMultiSelectEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> _passes;
    private readonly bool _canNoAction;
    private readonly bool _isOpponentDeck;
    private readonly bool _mutualConditions;
    private readonly List<HeadlessEntityId> _customSelections = new();

    public RevealMultiSelectEffect(
        CardSource card,
        int revealCount,
        IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> passes,
        RevealDestination remainingTo,
        string description,
        bool canNoAction = false,
        bool isOpponentDeck = false,
        bool mutualConditions = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(passes);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        RevealCount = revealCount;
        _passes = passes;
        RemainingTo = remainingTo;
        Description = description;
        _canNoAction = canNoAction;
        _isOpponentDeck = isOpponentDeck;
        _mutualConditions = mutualConditions;
    }

    public CardSource Card { get; }

    public int RevealCount { get; }

    public RevealDestination RemainingTo { get; }

    public string Description { get; }

    /// <summary>The cards picked by <see cref="RevealDestination.Custom"/> passes — the card script's
    /// follow-up (e.g. "play it, ignoring its play cost") consumes them after resolution.</summary>
    public IReadOnlyList<HeadlessEntityId> CustomSelections => _customSelections;

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _customSelections.Clear();
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        HeadlessPlayerId chooser = Card.Owner;
        HeadlessPlayerId revealPlayer = _isOpponentDeck ? Opponent(context, chooser) : chooser;
        if (revealPlayer.IsEmpty)
        {
            return;
        }

        List<HeadlessEntityId> pool = zones.GetCards(revealPlayer, ChoiceZone.Library)
            .Take(Math.Max(0, RevealCount)).ToList();
        if (pool.Count == 0)
        {
            return; // AS-IS: nothing to reveal -> no-op.
        }

        bool doAction = true;
        if (_canNoAction && _passes.Count >= 2)
        {
            // AS-IS whole-effect opt-out (RevealLibrary.cs:264-283): with >=2 conditions and canNoAction,
            // the player first decides whether to select at all; declining sends everything to the
            // remaining-cards place.
            var optOut = new ChoiceRequest(
                ChoiceType.Card, chooser, $"{Description} — select?",
                minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.Library,
                new[] { new ChoiceCandidate(new HeadlessEntityId($"reveal-optout:{Card.InstanceId.Value}"), "Select", ChoiceZone.Library, IsSelectable: true, ownerId: chooser) });
            ChoiceResult optResult = await context.ChoiceProvider.ChooseAsync(optOut, cancellationToken).ConfigureAwait(false);
            doAction = !optResult.IsSkipped && optResult.SelectedIds.Count > 0;
        }

        var chosen = new List<HeadlessEntityId>();
        if (doAction)
        {
            for (int passIndex = 0; passIndex < _passes.Count; passIndex++)
            {
                var pass = _passes[passIndex];
                int matching = pool.Count(pass.Condition);
                if (matching == 0)
                {
                    continue;   // AS-IS: the loop continues to the next condition.
                }

                bool canNoSelect = pass.CanNoSelect;
                // AS-IS mutualConditions (RevealLibrary.cs:302-308): a later pass becomes optional when
                // exactly one card was chosen so far, it also satisfies THIS pass, and pass[0] has no
                // candidates left.
                if (!canNoSelect && _mutualConditions && passIndex > 0 &&
                    chosen.Count == 1 && pass.Condition(chosen[0]) &&
                    !pool.Any(_passes[0].Condition))
                {
                    canNoSelect = true;
                }

                int maxCount = Math.Min(pass.MaxCount, matching);
                int minCount = canNoSelect ? 0 : (pass.CanEndNotMax ? Math.Min(1, maxCount) : maxCount);
                ChoiceCandidate[] candidates = pool
                    .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Library, IsSelectable: pass.Condition(id), ownerId: revealPlayer))
                    .ToArray();
                var request = new ChoiceRequest(
                    ChoiceType.Card, chooser, pass.Message,
                    minCount, maxCount, canSkip: canNoSelect, ChoiceZone.Library, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                if (result.IsSkipped)
                {
                    continue;
                }

                foreach (HeadlessEntityId id in result.SelectedIds)
                {
                    if (!pool.Remove(id))
                    {
                        continue;
                    }

                    chosen.Add(id);
                    if (pass.Destination == RevealDestination.Custom)
                    {
                        _customSelections.Add(id);   // no move — the card script's follow-up handles it.
                    }
                    else
                    {
                        StageMove(sink, id, pass.Destination);
                    }
                }
            }
        }

        await HandleRemainingAsync(sink, context, chooser, pool, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleRemainingAsync(
        MatchStateMutationSink sink, EngineContext context, HeadlessPlayerId chooser,
        List<HeadlessEntityId> remaining, CancellationToken cancellationToken)
    {
        if (remaining.Count == 0)
        {
            return;
        }

        RevealDestination destination = RemainingTo;
        if (destination == RevealDestination.DeckTopOrBottom)
        {
            // AS-IS ReturnRevealedCardsToLibraryTopOrBottom: the controller picks Top vs Bottom first.
            var placeRequest = new ChoiceRequest(
                ChoiceType.Card, chooser, "Place the remaining cards on the top or the bottom of the deck.",
                minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.Library,
                new[]
                {
                    new ChoiceCandidate(new HeadlessEntityId($"reveal-place-top:{Card.InstanceId.Value}"), "Top", ChoiceZone.Library, IsSelectable: true, ownerId: chooser),
                    new ChoiceCandidate(new HeadlessEntityId($"reveal-place-bottom:{Card.InstanceId.Value}"), "Bottom", ChoiceZone.Library, IsSelectable: true, ownerId: chooser),
                });
            ChoiceResult placeResult = await context.ChoiceProvider.ChooseAsync(placeRequest, cancellationToken).ConfigureAwait(false);
            destination = placeResult.SelectedIds.Count > 0 &&
                placeResult.SelectedIds[0].Value.StartsWith("reveal-place-top", StringComparison.Ordinal)
                ? RevealDestination.DeckTop
                : RevealDestination.DeckBottom;
        }

        IReadOnlyList<HeadlessEntityId> ordered = remaining;
        if (remaining.Count >= 2 && destination is RevealDestination.DeckTop or RevealDestination.DeckBottom)
        {
            // AS-IS ReturnRevealedCardsToLibraryBottom/Top: the controller specifies the order — a full
            // sequential pick; pick order = placement order ("lower numbers on top").
            var orderRequest = new ChoiceRequest(
                ChoiceType.Card, chooser, "Specify the order of the remaining cards.",
                minCount: remaining.Count, maxCount: remaining.Count, canSkip: false, ChoiceZone.Library,
                remaining.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Library, IsSelectable: true, ownerId: chooser)).ToArray());
            ChoiceResult orderResult = await context.ChoiceProvider.ChooseAsync(orderRequest, cancellationToken).ConfigureAwait(false);
            if (orderResult.SelectedIds.Count == remaining.Count)
            {
                ordered = orderResult.SelectedIds;
            }
        }

        if (destination == RevealDestination.DeckTop)
        {
            // Top: the FIRST pick ends up topmost — stage in reverse pick order (each top-insert stacks).
            ordered = ordered.Reverse().ToArray();
        }

        foreach (HeadlessEntityId id in ordered)
        {
            StageMove(sink, id, destination);
        }
    }

    private static HeadlessPlayerId Opponent(EngineContext context, HeadlessPlayerId player)
    {
        foreach (HeadlessPlayerId candidate in context.TurnController.Current.PlayerOrder)
        {
            if (!candidate.IsEmpty && candidate != player)
            {
                return candidate;
            }
        }

        return default;
    }

    private void StageMove(MatchStateMutationSink sink, HeadlessEntityId cardId, RevealDestination destination)
    {
        string kind = destination switch
        {
            RevealDestination.Hand => MatchStateMutationSink.ReturnToHandKind,
            RevealDestination.DeckTop => MatchStateMutationSink.ReturnToDeckTopKind,
            RevealDestination.DeckBottom => MatchStateMutationSink.ReturnToDeckBottomKind,
            RevealDestination.Trash => MatchStateMutationSink.TrashCardKind,
            _ => MatchStateMutationSink.ReturnToDeckBottomKind,
        };
        sink.Apply(new EffectMutation(
            kind, Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reveal-and-select effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A3) Mirror of the original <c>DestroyPermanentsClass</c>
/// (DCGO/Assets/Scripts/Script/CardController.cs): DIRECTLY delete a pre-computed list of permanents (no
/// selection — the card already filtered them, e.g. "all enemy Digimon with the same name"). Each target is
/// staged as a <c>Delete</c> sink mutation whose source is this card, so the sink's CENTRALISED gates apply:
/// opponent-effect immunity (<see cref="HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate"/> — AS-IS <c>CanNotBeAffected</c>) and
/// deletion-prevention (<c>cannotBeDeleted</c> / continuous prevent) / the optional would-be-deleted window.
/// The AS-IS filter is NOT re-implemented here (EX8_074 lesson). NOTE: the AS-IS <c>CanBeDestroyedBySkill</c>
/// (skill-destroy immunity) is not modeled engine-wide (<c>CanNotBeDestroyedBySkillClass</c> is an unported
/// skeleton — no card sets it), so it is a documented engine gap, not re-implemented in this predicate.
/// </summary>
public sealed class DestroyPermanentsEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<HeadlessEntityId> _targets;

    public DestroyPermanentsEffect(CardSource card, IReadOnlyList<HeadlessEntityId> targets, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _targets = targets;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (HeadlessEntityId target in _targets)
        {
            if (target.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DeleteKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Destroy-permanents effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W2) Mirror of the original <c>DeckBottomBounceClass</c> (CardController.cs): return a
/// pre-computed list of permanents to the bottom of their owners' decks. Each target is staged as a
/// <c>ReturnToDeckBottom</c> sink mutation; the sink's centralised immunity gate filters (source = this
/// card), mirroring <see cref="DestroyPermanentsEffect"/> for the delete case.</summary>
public sealed class DeckBottomBounceEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<HeadlessEntityId> _targets;

    public DeckBottomBounceEffect(CardSource card, IReadOnlyList<HeadlessEntityId> targets, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _targets = targets;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (HeadlessEntityId target in _targets)
        {
            if (target.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToDeckBottomKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Deck-bottom-bounce effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3) Mirror of AS-IS <c>ReturnToLibraryBottomDigivolutionCardsClass</c> — returns the host's
/// own digivolution (under-)cards to the bottom of the deck. Emits the engine's existing
/// <see cref="MatchStateMutationSink.ReturnDigivolutionCardsKind"/> (toDeck) on the host.</summary>
public sealed class ReturnSelfDigivolutionCardsToDeckEffect : IActivatedCardEffect
{
    private readonly int _count;

    public ReturnSelfDigivolutionCardsToDeckEffect(CardSource card, int count, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _count = count;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnDigivolutionCardsKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value,
                [MatchStateMutationSink.CountKey] = _count,
                [MatchStateMutationSink.ToDeckKey] = true,
                [MatchStateMutationSink.FromBottomKey] = true,
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Return-digivolution-to-deck effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3) Mirror of AS-IS <c>ReplaceBottomSecurityWithFaceUpOption(Main)Effect</c> — "add your
/// bottom security card to the hand, then place this card face up as the bottom security card." Emits
/// ReturnToHand on the current bottom security card, then AddToSecurity (face up, bottom) for the host.</summary>
public sealed class ReplaceBottomSecurityWithFaceUpEffect : IActivatedCardEffect
{
    private readonly bool _top;

    public ReplaceBottomSecurityWithFaceUpEffect(CardSource card, string description, bool top = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _top = top;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (Card.Context.ZoneMover is IZoneStateReader reader)
        {
            IReadOnlyList<HeadlessEntityId> security = reader.GetCards(Card.Owner, ChoiceZone.Security);
            if (security.Count > 0)
            {
                // Top security = index 0; bottom = last of the ordered stack.
                HeadlessEntityId target = _top ? security[0] : security[^1];
                sink.Apply(new EffectMutation(
                    MatchStateMutationSink.ReturnToHandKind,
                    Card.InstanceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
            }
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value,
            [MatchStateMutationSink.FaceUpKey] = true,
        };
        if (!_top)
        {
            values[MatchStateMutationSink.ToBottomKey] = true;
        }

        sink.Apply(new EffectMutation(MatchStateMutationSink.AddToSecurityKind, Card.InstanceId, values));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Replace-bottom-security effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W4) Mirror of AS-IS <c>RevealLibraryClass</c> — reveals the top N cards of the owner's
/// deck. The full-information headless model has no hidden state to expose, so this carries no mutation; it
/// exists so a card that reveals-then-acts can declare the reveal step (the follow-up act is authored per
/// card). The reveal count is retained for logging / any card-facing consumer.</summary>
public sealed class InformationalRevealEffect : IActivatedCardEffect
{
    public InformationalRevealEffect(CardSource card, int revealCount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        RevealCount = revealCount;
        Description = description;
    }

    public CardSource Card { get; }

    public int RevealCount { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        // No state change: a reveal exposes cards to the opponent, which the full-information model already has.
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Informational reveal effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3, C-24) Mirror of AS-IS <c>TrainingEffect</c> — an activated [Breeding] effect: suspend
/// self (cost) and place the top card of the owner's deck at the bottom of self's digivolution stack. Wraps
/// the engine's <see cref="DigivolutionStackHelpers.TrainAsync"/> primitive via the Train mutation.</summary>
public sealed class TrainingActivatedEffect : IActivatedCardEffect
{
    public TrainingActivatedEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.TrainKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Training effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3, C-23) Mirror of AS-IS <c>MaterialSaveEffect</c> — re-parents <c>count</c> of this
/// Digimon's digivolution cards to another of the owner's Digimon (<paramref name="destinationId"/>, selected
/// at porting time). Wraps the engine's <see cref="DigivolutionStackHelpers.MoveSourcesBottom"/> primitive
/// via the MaterialSave mutation.</summary>
public sealed class MaterialSaveActivatedEffect : IActivatedCardEffect
{
    private readonly HeadlessEntityId _destinationId;
    private readonly int _count;

    public MaterialSaveActivatedEffect(CardSource card, HeadlessEntityId destinationId, int count, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _destinationId = destinationId;
        _count = count;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (_destinationId.IsEmpty)
        {
            return;
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.MaterialSaveKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.ToEntityIdKey] = _destinationId.Value,
                [MatchStateMutationSink.CountKey] = _count,
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Material-save effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A4) Mirror of the original <c>HatchDigiEggClass</c>
/// (DCGO/Assets/Scripts/Script/CardController.cs): if the controller <c>CanHatch</c> (an empty breeding area
/// and an available digi-egg), move the top digi-egg from the digitama library into the breeding area. The
/// AS-IS <c>CanHatch</c> guard is mirrored explicitly here — the raw <c>ZoneMover.HatchDigitamaAsync</c> only
/// checks for an available egg, NOT the empty-breeding-area rule (that lives on the legal-action dispatcher),
/// so the effect re-checks it (also keeping this re-run safe: a second pass finds the breeding area occupied
/// and no-ops).
/// </summary>
public sealed class HatchDigiEggEffect : IActivatedCardEffect
{
    public HatchDigiEggEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        HeadlessPlayerId player = Card.Owner;
        // AS-IS CanHatch: an empty breeding area AND an available digi-egg.
        if (zones.GetCards(player, ChoiceZone.BreedingArea).Count > 0
            || zones.GetCards(player, ChoiceZone.DigitamaLibrary).Count == 0)
        {
            return;
        }

        await context.ZoneMover.HatchDigitamaAsync(player, cancellationToken).ConfigureAwait(false);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Hatch effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A5) Mirror of the original <c>PlayCardClass</c>
/// (DCGO/Assets/Scripts/Script/CardController.cs) for the SIMPLE cost-free play the BT sets actually use
/// (BT1_078: <c>payCost: false, root: Library</c>): play <see cref="TargetCardId"/> from <see cref="FromZone"/>
/// onto the battle area at no cost. Staged as a <c>PlayCard</c> sink mutation (same seam as
/// <see cref="PlayThisCardToBattleEffect"/>, generalised to an arbitrary target). The original's
/// jogress / burst / app-fusion / targetPermanent / isTapped / <c>payCost:true</c> branches are NOT modeled
/// here (out of BT-PRE scope — no such mechanism is invented until a card needs it).
/// </summary>
public sealed class PlayCardEffect : IActivatedCardEffect
{
    public PlayCardEffect(CardSource card, HeadlessEntityId targetCardId, ChoiceZone fromZone, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        TargetCardId = targetCardId;
        FromZone = fromZone;
        Description = description;
    }

    public CardSource Card { get; }

    public HeadlessEntityId TargetCardId { get; }

    public ChoiceZone FromZone { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (TargetCardId.IsEmpty)
        {
            return;
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = TargetCardId.Value,
                [MatchStateMutationSink.FromZoneKey] = FromZone.ToString(),
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-card effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W5) A material condition for a Blast-DNA digivolution (AS-IS <c>BlastDNACondition</c>) —
/// the material card names that fuse. Card-facing shim so ported cards compile.</summary>
/// <summary>(S2) A continuous effect-immunity registered under <see cref="HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate"/>
/// (AS-IS <c>CanNotAffectedClass</c>). Carries the per-card <c>SkillCondition</c> (over the causing effect's
/// source) so the immunity gate evaluates it 1:1. Null skill → opponent-only fallback.</summary>
public sealed class ContinuousImmunityEffect : ICardEffect
{
    public ContinuousImmunityEffect(CardSource card, Func<CardSource, bool>? skillCondition, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? targetPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        SkillCondition = skillCondition;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        TargetPredicate = targetPredicate;
    }

    public CardSource Card { get; }
    public Func<CardSource, bool>? SkillCondition { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    /// <summary>(C2) AS-IS <c>CanNotAffectedClass.CardCondition</c> (the factory's permanentCondition) —
    /// WHICH permanents this immunity protects, evaluated live against the protected target. Non-null →
    /// the grant is registered field-wide (no target) and only reaches predicate-matching cards.</summary>
    public Func<CardSource, bool>? TargetPredicate { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (SkillCondition is not null)
        {
            values[HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate.SkillPredicateKey] = SkillCondition;
        }
        else
        {
            values[HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate.ImmunityFromOpponentOnlyKey] = true;
        }

        if (TargetPredicate is not null)
        {
            values[HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate.TargetPredicateKey] = TargetPredicate;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null,
            // (C2) predicate-scoped grants protect the predicate-matching set, not (implicitly) the holder.
            targetEntityIds: TargetPredicate is null ? new[] { Card.InstanceId } : Array.Empty<HeadlessEntityId>(),
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(FR-P3) A defender-conditional "cannot attack" restriction (AS-IS
/// <c>CanNotAttackTargetDefendingPermanentClass</c> with a <c>defenderCondition</c>): the attacker may not
/// attack defenders matching <see cref="DefenderPredicate"/>, but MAY attack others. Registers a self
/// CannotAttack binding carrying the defender predicate, which ContinuousRestrictionGate.EvaluateAttack
/// evaluates against the chosen defender.</summary>
public sealed class CanNotAttackDefenderConditionEffect : ICardEffect
{
    public CanNotAttackDefenderConditionEffect(CardSource card, Func<CardSource, bool> defenderPredicate, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(defenderPredicate);
        Card = card;
        DefenderPredicate = defenderPredicate;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }
    public Func<CardSource, bool> DefenderPredicate { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [RestrictionHelpers.RestrictionTargetEntityIdKey] = Card.InstanceId.Value,
            [RestrictionHelpers.RestrictionSourceEntityIdKey] = Card.InstanceId.Value,
            [RestrictionHelpers.CannotAttackKey] = true,
            [RestrictionHelpers.DefenderPredicateKey] = DefenderPredicate,
        };
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) A material condition for a Blast-DNA digivolution (AS-IS <c>BlastDNACondition</c>).
/// <see cref="Matches"/> preserves the original's per-material predicate 1:1; use <see cref="ByName"/> for the
/// name-equality subset.</summary>
public sealed record BlastDNACondition(Func<CardSource, bool> Matches, string Label)
{
    public static BlastDNACondition ByName(string name) => new(cs => cs.EqualsCardName(name), name);
}

/// <summary>(PRIM-W5) A no-op effect returned by the special-play factories. The real work (registering the
/// card's SpecialPlayRecipe) happens in the factory; this marker just occupies the card's effect list and is
/// never consumed (role None).</summary>
public sealed class SpecialPlayRecipeMarkerEffect : ICardEffect
{
    public SpecialPlayRecipeMarkerEffect(CardSource card) => Card = card;

    public CardSource Card { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var context = new EffectContext(Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "None", context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) Grants this card an additional card name (AS-IS <c>ChangeCardNamesClass</c>). Registers
/// a continuous binding carrying <see cref="CardSource.AddedCardNameKey"/>, which <see cref="CardSource.CardNames"/>
/// folds in — so name-based predicates (EqualsCardName / ContainsCardName) see it.</summary>
public sealed class ChangeCardNamesEffect : ICardEffect
{
    public ChangeCardNamesEffect(CardSource card, string addedName, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(addedName);
        Card = card;
        AddedName = addedName;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }
    public string AddedName { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CardSource.AddedCardNameKey] = AddedName,
        };
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) Return this card to the owner's hand (AS-IS <c>AddThisCardToHand</c>).</summary>
public sealed class ReturnThisCardToHandEffect : IActivatedCardEffect
{
    public ReturnThisCardToHandEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Return-to-hand effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.DigivolveIntoHandOrTrashCard(..)</c>
/// coroutine: select up to <paramref name="maxCount"/> battle-area Digimon matching <c>canTarget</c> and
/// de-digivolve each by <c>count</c> (remove its top digivolution cards). Wraps the engine's
/// <see cref="DeDigivolveHelpers"/> primitive via the DeDigivolve mutation.</summary>
public sealed class ActivatedSelectAndDeDigivolveEffect : IActivatedCardEffect
{
    private readonly Func<HeadlessEntityId, bool> _canTarget;
    private readonly int _maxCount;
    private readonly int _count;
    private readonly bool _canEndNotMax;

    public ActivatedSelectAndDeDigivolveEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int count, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _canTarget = canTarget;
        _maxCount = maxCount;
        _count = count;
        _canEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    private IEnumerable<HeadlessEntityId> Candidates()
    {
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in Card.Context.TurnController.Current.PlayerOrder)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (_canTarget(id))
                {
                    yield return id;
                }
            }
        }
    }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = Candidates()
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.BattleArea, isSelectable: true, Card.Owner))
            .ToList();
        int max = Math.Min(_maxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: _canEndNotMax ? 0 : max, maxCount: max, canSkip: _canEndNotMax, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        foreach (HeadlessEntityId id in selected)
        {
            if (id.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DeDigivolveKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = id.Value,
                    [MatchStateMutationSink.CountKey] = _count,
                }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-and-de-digivolve effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.PlayPermanentCards(..., root)</c>
/// coroutine: select up to <paramref name="maxCount"/> of the owner's cards in <paramref name="fromZone"/>
/// (Trash / Hand) matching <paramref name="canTarget"/>, then play each onto the battle area (cost-free).</summary>
public sealed class ActivatedSelectAndPlayEffect : IActivatedCardEffect
{
    private readonly ChoiceZone _fromZone;
    private readonly Func<HeadlessEntityId, bool> _canTarget;
    private readonly int _maxCount;
    private readonly bool _canEndNotMax;

    public ActivatedSelectAndPlayEffect(CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _fromZone = fromZone;
        _canTarget = canTarget;
        _maxCount = maxCount;
        _canEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    private IEnumerable<HeadlessEntityId> Candidates() =>
        ((IZoneStateReader)Card.Context.ZoneMover).GetCards(Card.Owner, _fromZone).Where(_canTarget);

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = Candidates()
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, _fromZone, isSelectable: true, Card.Owner))
            .ToList();
        int max = Math.Min(_maxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: _canEndNotMax ? 0 : max, maxCount: max, canSkip: _canEndNotMax, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        foreach (HeadlessEntityId id in selected)
        {
            if (id.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.PlayCardKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = id.Value,
                    [MatchStateMutationSink.FromZoneKey] = _fromZone.ToString(),
                }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-and-play effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-P0 B.O.5) The headless mirror of AS-IS <c>CardEffectCommons.PlayOptionCards</c>: select up to
/// <paramref name="maxCount"/> of the owner's Option cards in <c>sourceZone</c> (matching
/// <paramref name="optionPredicate"/>) and PLAY each as a nested effect — trash it, open OnUseOption, and resolve
/// its [Main] (OptionSkill) effects through the SAME activation sink/choice cycle. v1 plays cost-free (the 34-card
/// bulk). See docs/porting/play_option_and_delayed_player_effect_design.md.</summary>
public sealed class PlayOptionCardEffect : IActivatedCardEffect
{
    public PlayOptionCardEffect(CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> optionPredicate,
        int maxCount, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(optionPredicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        SourceZone = sourceZone;
        OptionPredicate = optionPredicate;
        MaxCount = maxCount;
        CanEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public ChoiceZone SourceZone { get; }

    public Func<HeadlessEntityId, bool> OptionPredicate { get; }

    public int MaxCount { get; }

    public bool CanEndNotMax { get; }

    public string Description { get; }

    /// <summary>The zone-card select for the Option(s) to play (from <see cref="SourceZone"/>).</summary>
    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = ((IZoneStateReader)Card.Context.ZoneMover).GetCards(Card.Owner, SourceZone)
            .Where(OptionPredicate)
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, SourceZone, isSelectable: true, Card.Owner))
            .ToList();
        int max = Math.Min(MaxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: CanEndNotMax ? 0 : max, maxCount: max, canSkip: CanEndNotMax, candidates);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-option effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-P0-flow B.O.3) An activated "select up to <paramref name="maxCount"/> of the owner's cards in
/// <paramref name="fromZone"/> (Trash / Library / Security …) matching a predicate, then apply a single-target
/// mutation to each" — the zone-card select-follow-up wrapper (AS-IS SelectCardEffect Mode AddHand / Discard).
/// The mutation kind picks the follow-up (ReturnToHand = add-to-hand, TrashCard = trash-from-zone); the sink
/// moves each target from its current zone, so no from-zone payload is needed.</summary>
public sealed class ActivatedSelectFromZoneEffect : IActivatedCardEffect
{
    private readonly ChoiceZone _fromZone;
    private readonly Func<HeadlessEntityId, bool> _canTarget;
    private readonly int _maxCount;
    private readonly bool _canEndNotMax;
    private readonly string _mutationKind;

    public ActivatedSelectFromZoneEffect(CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget,
        int maxCount, bool canEndNotMax, string mutationKind, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(mutationKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _fromZone = fromZone;
        _canTarget = canTarget;
        _maxCount = maxCount;
        _canEndNotMax = canEndNotMax;
        _mutationKind = mutationKind;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    private IEnumerable<HeadlessEntityId> Candidates() =>
        ((IZoneStateReader)Card.Context.ZoneMover).GetCards(Card.Owner, _fromZone).Where(_canTarget);

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = Candidates()
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, _fromZone, isSelectable: true, Card.Owner))
            .ToList();
        int max = Math.Min(_maxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: _canEndNotMax ? 0 : max, maxCount: max, canSkip: _canEndNotMax, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        foreach (HeadlessEntityId id in selected)
        {
            if (id.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                _mutationKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-from-zone effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "select up to <paramref name="maxCount"/> Digimon and make each unable to attack and/or
/// block for a <see cref="EffectDuration"/>" effect (e.g. ST2_14). <see cref="ApplyRestriction"/> registers
/// one duration-tagged restriction binding per chosen target, queried by <c>RestrictionHelpers</c> via the
/// continuous-restriction scope, so <see cref="EffectDurationExpiry"/> removes it on expiry.
/// </summary>
public sealed class ActivatedTargetRestrictionEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();
    private readonly EffectDuration _duration;
    private readonly bool _cannotAttack;
    private readonly bool _cannotBlock;

    public ActivatedTargetRestrictionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, EffectDuration duration, bool cannotAttack, bool cannotBlock, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _duration = duration;
        _cannotAttack = cannotAttack;
        _cannotBlock = cannotBlock;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Custom, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    public void ApplyRestriction(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        int index = 0;
        foreach (HeadlessEntityId target in selected)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [RestrictionHelpers.RestrictionTargetEntityIdKey] = target.Value,
                [RestrictionHelpers.RestrictionSourceEntityIdKey] = Card.InstanceId.Value,
            };
            if (_cannotAttack)
            {
                values[RestrictionHelpers.CannotAttackKey] = true;
            }

            if (_cannotBlock)
            {
                values[RestrictionHelpers.CannotBlockKey] = true;
            }

            var context = new EffectContext(
                Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { target }, values: values);
            var binding = new EffectBinding(
                new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:restrict:{target.Value}:{index++}"), Card.Controller, "Continuous", context),
                keywords: null, EffectQueryRole.Restriction, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: _duration);
            Card.Context.EffectRegistry.Register(binding);
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated restriction effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// A triggered "[When ...] this Digimon gets +X DP for a <see cref="EffectDuration"/>" effect (e.g. ST3_01
/// "when an opponent's Digimon is deleted by 0 DP, this Digimon gets +1000 DP for the turn"). On resolution
/// it registers one duration-tagged self DP-modifier binding, folded in by the continuous gate and removed
/// by <see cref="EffectDurationExpiry"/>. Auto-registered under its trigger timing. (The original's
/// [Once Per Turn] / 0-DP-delete gates map to the once-flag / trigger subsystems — relaxed here, like ST2_11.)
/// </summary>
public sealed class TriggeredSelfDpBuffEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly int _changeValue;
    private readonly EffectDuration _duration;
    private readonly Func<bool>? _condition;
    private readonly Func<CardEffectResolveContext, bool>? _triggerGate;

    public TriggeredSelfDpBuffEffect(
        CardSource card, EffectTiming timing, int changeValue, EffectDuration duration, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _changeValue = changeValue;
        _duration = duration;
        _condition = condition;
        _triggerGate = triggerGate;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:selfdpbuff:{trigger}"), card.InstanceId, description, trigger,
            isOptional: false, maxCountPerTurn: maxCountPerTurn, hash: hash);
    }

    public CardSource Card { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        if (_triggerGate is not null && !_triggerGate(context))
        {
            return CardEffectCanResolveResult.Failure("Trigger event condition not met.");
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanResolve(context).CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure("Cannot resolve."));
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ModifierHelpers.DpDeltaKey] = _changeValue };
        var bindingContext = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        // Unique per application (the triggering subject) so repeated firings across turns don't collide;
        // the duration expiry removes each at turn end.
        string applied = context.Request.Context.TriggerEntityId?.Value ?? "self";
        var binding = new EffectBinding(
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:selfdpbuff:applied:{_changeValue}:{applied}"), Card.Controller, "Continuous", bindingContext),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }, effect: null, duration: _duration);
        Card.Context.EffectRegistry.Register(binding);
        return ValueTask.FromResult(EffectResult.Success($"This Digimon gets {(_changeValue >= 0 ? "+" : string.Empty)}{_changeValue} DP."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>
/// A triggered "[When ...] &lt;Recovery +N (Deck)&gt;" effect (e.g. ST3_09): on resolution emits a Recover
/// mutation moving the top <paramref name="amount"/> deck card(s) onto the owner's security stack.
/// </summary>
public sealed class RecoverTriggerEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly int _amount;
    private readonly Func<bool>? _condition;

    public RecoverTriggerEffect(CardSource card, EffectTiming timing, int amount, Func<bool>? condition, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _amount = amount;
        _condition = condition;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:recover:{trigger}"), card.InstanceId, description, trigger, isOptional: true);
    }

    public CardSource Card { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanResolve(context).CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure("Cannot resolve."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.RecoverKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = Card.Owner.Value,
                [MatchStateMutationSink.CountKey] = _amount,
            }));
        return ValueTask.FromResult(EffectResult.Success($"Recovery +{_amount}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>
/// An activated "add this card to its owner's hand" effect (Option/Security self-bounce, e.g. ST3_13 /
/// ST3_14 [Security]). <see cref="Apply"/> emits a ReturnToHand mutation on the source card.
/// </summary>
public sealed class AddThisCardToHandEffect : IActivatedCardEffect
{
    public AddThisCardToHandEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Add-to-hand effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "play THIS card onto the battle area (without paying its cost)" effect — the headless
/// realization of a Tamer's <c>PlaySelfTamerSecurityEffect</c> security skill (e.g. ST1_12 / ST2_12 /
/// ST3_12 [Security] "Play this Tamer"). The security loop reveals the card (to the trash) before resolving
/// its SecuritySkill, so <see cref="Apply"/> plays it from whatever zone it currently sits in to the battle
/// area via a PlayCard mutation, which also auto-registers its effects (G6-001 / G8-002).
/// </summary>
public sealed class PlayThisCardToBattleEffect : IActivatedCardEffect
{
    public PlayThisCardToBattleEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ChoiceZone from = CurrentZone() ?? ChoiceZone.Trash;
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value,
                [MatchStateMutationSink.FromZoneKey] = from.ToString(),
            }));
    }

    private ChoiceZone? CurrentZone()
    {
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        foreach (ChoiceZone zone in new[] { ChoiceZone.Security, ChoiceZone.Trash, ChoiceZone.Hand, ChoiceZone.BattleArea })
        {
            if (zones.GetCards(Card.Owner, zone).Contains(Card.InstanceId))
            {
                return zone;
            }
        }

        return null;
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-this-card effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "choose a Digimon digivolution card under one of your Digimon and play it as another Digimon
/// without paying its cost" effect (e.g. ST2_15 [Main], the play-from-under flow). Candidates are the
/// Digimon under-cards of the owner's battle-area Digimon; <see cref="Apply"/> emits a
/// PlayDigivolutionAsDigimon mutation that moves the chosen under-card out of its host onto the battle area
/// (cost-free) and auto-registers it.
/// </summary>
public sealed class ActivatedPlayFromUnderEffect : IActivatedCardEffect
{
    private readonly string _cardType;
    private readonly string? _cardName;

    /// <summary>(K5) <paramref name="cardType"/> selects which under-cards are candidates ("Digimon" for the
    /// ST2_15-style play-from-under; "Tamer" for the MindLink play-back). <paramref name="cardName"/>
    /// optionally narrows to a specific card name (AS-IS PlayMindLinkTamerFromDigivolutionCards).</summary>
    public ActivatedPlayFromUnderEffect(CardSource card, string description, string cardType = "Digimon", string? cardName = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardType);
        Card = card;
        Description = description;
        _cardType = cardType;
        _cardName = cardName;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = new List<ChoiceCandidate>();
        foreach ((HeadlessEntityId under, HeadlessEntityId _) in OwnerDigimonUnderCards())
        {
            candidates.Add(EffectChoiceHelpers.Candidate(under, under.Value, ChoiceZone.BattleArea, isSelectable: true, Card.Owner));
        }

        int max = Math.Min(1, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: max, maxCount: max, canSkip: false, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        var selectedSet = new HashSet<string>(selected.Select(s => s.Value), StringComparer.Ordinal);
        foreach ((HeadlessEntityId under, HeadlessEntityId host) in OwnerDigimonUnderCards())
        {
            if (!selectedSet.Contains(under.Value))
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.PlayDigivolutionAsDigimonKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = under.Value,
                    [MatchStateMutationSink.HostEntityIdKey] = host.Value,
                }));
        }
    }

    private IEnumerable<(HeadlessEntityId Under, HeadlessEntityId Host)> OwnerDigimonUnderCards()
    {
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(Card.Owner, ChoiceZone.BattleArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(Card.Context.CardInstanceRepository, Card.Context.CardRepository, top);
            foreach (StackedCard under in stack.UnderCards)
            {
                if (IsCandidateCard(under.InstanceId))
                {
                    yield return (under.InstanceId, top);
                }
            }
        }
    }

    private bool IsCandidateCard(HeadlessEntityId id)
    {
        return Card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            && Card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
            && string.Equals(def.CardType, _cardType, StringComparison.OrdinalIgnoreCase)
            && (_cardName is null || string.Equals(def.Name, _cardName, StringComparison.OrdinalIgnoreCase));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-from-under effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// Headless mirror of the original <c>CardEffectFactory</c>. Method names match the original so ported
/// card bodies read 1:1. Each returns an <see cref="ICardEffect"/> the registrar lowers to a binding.
/// </summary>
public static partial class CardEffectFactory
{
    /// <summary>Original: <c>ChangeSelfSAttackStaticEffect</c> — continuous ±security attack on self.</summary>
    public static ICardEffect ChangeSelfSAttackStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>Original: <c>ChangeSelfSAttackStaticEffect&lt;Func&lt;int&gt;&gt;</c> — continuous ±security
    /// attack on self with a dynamic (read-time) value.</summary>
    public static ICardEffect ChangeSelfSAttackStaticEffect(Func<int> changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(changeValue);
        return new ContinuousSelfModifierEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue: 0, isInheritedEffect, condition, dynamicValue: changeValue);
    }

    /// <summary>Original: <c>ChangeSelfDPStaticEffect</c> — continuous ±DP on self.</summary>
    public static ICardEffect ChangeSelfDPStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>(PRIM-W1-3) Original: <c>ChangeDigivolutionCostStaticEffect</c> — continuous ±digivolution
    /// cost on self (delta). Registers a <see cref="DigivolutionCostHelpers.DigivolutionCostDeltaKey"/> modifier
    /// under the continuous-modifier scope, which <c>ContinuousModifierGate.ResolveDigivolutionCost</c> folds
    /// into this card's evolution cost (D-8; "cannot be reduced" replacement honoured). <paramref name="changeValue"/>
    /// is signed (negative = reduction). The original's <c>setFixedCost</c> (SET rather than ±) and per-target
    /// permanent/root conditions are out of this delta primitive's scope (per-card follow-up).</summary>
    public static ICardEffect ChangeDigivolutionCostStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, DigivolutionCostHelpers.DigivolutionCostDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>(PRIM-W1-3) Dynamic (<c>Func&lt;int&gt;</c>) variant of <see cref="ChangeDigivolutionCostStaticEffect(int,bool,CardSource,Func{bool})"/>.</summary>
    public static ICardEffect ChangeDigivolutionCostStaticEffect(Func<int> changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, DigivolutionCostHelpers.DigivolutionCostDeltaKey, changeValue: 0, isInheritedEffect, condition, dynamicValue: changeValue);

    /// <summary>(PRIM-W1-5) Original: <c>CanNotDigivolveStaticSelfEffect</c> — a continuous "this card cannot
    /// be digivolved (as the digivolution source)" restriction on self. Registers a
    /// <see cref="RestrictionHelpers.CannotDigivolveKey"/> restriction that <c>DigivolveAction</c> already
    /// consults (<c>ContinuousRestrictionGate.EvaluateDigivolve</c> on the target under-card).</summary>
    public static ICardEffect CanNotDigivolveStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotDigivolveKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W1-8) Original: <c>CanNotDigivolveStaticEffect</c> — a continuous "the scoped player's
    /// Digimon (optionally of <paramref name="scopeCardType"/>) cannot digivolve" restriction. Covers the
    /// structured scope (e.g. "your opponent's Digimon cannot digivolve" — <paramref name="scopePlayerId"/> =
    /// the opponent); the original's arbitrary per-permanent predicate beyond CardType is a per-card concern.</summary>
    public static ICardEffect CanNotDigivolveStaticEffect(HeadlessPlayerId scopePlayerId, string? scopeCardType, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayerId, RestrictionHelpers.CannotDigivolveKey, scopeCardType, isInheritedEffect, condition);

    /// <summary>(PRIM-W1-6/9) Original: <c>AddDigivolutionRequirementStaticEffect</c> — grant this card an
    /// ADDITIONAL digivolution path "from <paramref name="fromColor"/> Lv<paramref name="fromLevel"/>". When
    /// the printed condition fails but this added condition matches the target, DigivolveAction allows the
    /// digivolve. (Per-path cost via <see cref="ChangeDigivolutionCostStaticEffect(int,bool,CardSource,Func{bool})"/>
    /// or per-card; arbitrary per-permanent predicates beyond Color@Level are per-card.)</summary>
    public static ICardEffect AddDigivolutionRequirementStaticEffect(string fromColor, int fromLevel, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new AddedDigivolutionRequirementEffect(card, $"{fromColor}@{fromLevel}", isInheritedEffect, condition);

    /// <summary>(PRIM-W5) <c>AddSelfDigivolutionRequirementStaticEffect</c> — adds an alternative digivolution
    /// source for THIS card: it may digivolve from any under-card matching <paramref name="permanentCondition"/>
    /// (for <paramref name="digivolutionCost"/> memory). DigivolveAction evaluates the predicate against the
    /// target. Extra AS-IS args (effectName/cardCondition/costEquation/level ranges) accepted for fidelity;
    /// the original <c>CardColor cardColor</c> param is omitted (no headless CardColor — express color via
    /// the predicate).</summary>
    public static ICardEffect AddSelfDigivolutionRequirementStaticEffect(
        Func<Permanent, bool> permanentCondition, int digivolutionCost, bool ignoreDigivolutionRequirement,
        CardSource card, Func<bool>? condition, string? effectName = null, Func<CardSource, bool>? cardCondition = null,
        Func<int>? costEquation = null, int level = -1, int minLevel = -1, int maxLevel = -1) =>
        // (FR2/M-1) cardCondition = which cards receive the added requirement; null → self only (AS-IS default
        // cs => cs == card), non-null → any owner's card matching it (e.g. ST8_04 UlforceVeedramon in hand).
        // (A2) level/minLevel/maxLevel = the AS-IS hard level gate on the digivolving-FROM permanent
        // (GetEvoCost) — previously accepted and dropped, which silently widened ~111 cards' alt-digivolve.
        new AddedDigivolutionRequirementPredicateEffect(card, permanentCondition, digivolutionCost, ignoreDigivolutionRequirement, isInheritedEffect: false, condition, targetCardCondition: cardCondition, costEquation: costEquation, level: level, minLevel: minLevel, maxLevel: maxLevel);

    /// <summary>(PRIM-W5) <c>DrawCardsEffect</c> — the declarative form of the AS-IS
    /// <c>new DrawClass(owner, count, ...).Draw()</c> coroutine: the owner draws <paramref name="count"/>
    /// cards. Use this in place of the original draw coroutine.</summary>
    public static IActivatedCardEffect DrawCardsEffect(CardSource card, int count) =>
        new DrawEffect(card, count, $"Draw {count} card(s).");

    /// <summary>Original: <c>PierceSelfEffect</c> — grants Piercing to self.</summary>
    public static ICardEffect PierceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Piercing, isInheritedEffect, condition);

    /// <summary>Original: <c>BlockerSelfStaticEffect</c> — grants Blocker to self.</summary>
    public static ICardEffect BlockerSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Blocker, isInheritedEffect, condition);

    /// <summary>Original: <c>JammingSelfStaticEffect</c> — grants Jamming to self.</summary>
    public static ICardEffect JammingSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Jamming, isInheritedEffect, condition);

    /// <summary>Original: <c>RebootSelfStaticEffect</c> — grants Reboot to self (Batch1).</summary>
    public static ICardEffect RebootSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Reboot, isInheritedEffect, condition);

    /// <summary>Original: <c>AllianceSelfEffect</c> — grants Alliance to self (Batch2). The original wraps
    /// <paramref name="condition"/> with <c>IsExistOnBattleAreaDigimon(card)</c>; here that battle-area guard
    /// is the binding lifecycle (see <see cref="SelfKeywordBatch2Effect"/>).</summary>
    public static ICardEffect AllianceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Alliance, isInheritedEffect, condition);

    /// <summary>Original: <c>OverclockSelfEffect</c> — grants Overclock to self (Batch2).</summary>
    public static ICardEffect OverclockSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Overclock, isInheritedEffect, condition);

    /// <summary>Original: <c>VortexSelfEffect(isInheritedEffect, card, condition, rootCardEffect = null)</c> —
    /// grants Vortex to self (Batch2). <paramref name="rootCardEffect"/> is accepted for 1:1 source-signature
    /// fidelity (the original threads it to the underlying <c>VortexEffect</c>); the headless grant layer
    /// derives its binding from the card source, so it is not otherwise needed.</summary>
    public static ICardEffect VortexSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, ICardEffect? rootCardEffect = null) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Vortex, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>RushSelfStaticEffect</c> — grants Rush to self (Batch2).</summary>
    public static ICardEffect RushSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Rush, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>RetaliationSelfEffect(isInheritedEffect, card, condition, isLinkedEffect = false)</c>
    /// — grants Retaliation to self (Batch2). <paramref name="isLinkedEffect"/> is accepted for source-signature
    /// fidelity; the headless grant derives from the card source.</summary>
    public static ICardEffect RetaliationSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Retaliation, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(PRIM-W2) Original: <c>RaidSelfEffect</c> — grants Raid (attack-switch) to self.
    /// <paramref name="rootCardEffect"/>/<paramref name="isLinkedEffect"/> accepted for source fidelity.</summary>
    public static ICardEffect RaidSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, ICardEffect? rootCardEffect = null, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Raid, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(PRIM-W2) Original: <c>BarrierSelfEffect</c> — grants Barrier (deletion-replacement) to self.</summary>
    public static ICardEffect BarrierSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Barrier, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>CollisionSelfStaticEffect</c> — grants Collision (forced-block) to self.</summary>
    public static ICardEffect CollisionSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Collision, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(PRIM-W2) Original: <c>FortitudeSelfEffect</c> — grants Fortitude (post-deletion replay) to self.</summary>
    public static ICardEffect FortitudeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Fortitude, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>EvadeSelfEffect</c> — grants Evade (deletion-replacement) to self.</summary>
    public static ICardEffect EvadeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Evade, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>SaveEffect(card)</c> — grants Save (deletion-replacement: place under a
    /// Tamer instead of trashing) to self.</summary>
    public static ICardEffect SaveEffect(CardSource card) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Save, isInheritedEffect: false, condition: null);

    // --- (PRIM-W3) keyword self-static grants -----------------------------------------------------------
    /// <summary>(PRIM-W3) <c>BlitzSelfEffect</c> — grants Blitz to self (Batch2).</summary>
    public static ICardEffect BlitzSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Blitz, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>DecodeSelfEffect</c> — grants Decode to self (Batch2).</summary>
    public static ICardEffect DecodeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Decode, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ProgressSelfStaticEffect</c> — grants Progress to self (Batch2).</summary>
    public static ICardEffect ProgressSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Progress, isInheritedEffect, condition);

    /// <summary>(PRIM-W3/A4) <c>PartitionSelfEffect</c> — grants Partition to self (Batch2).
    /// <paramref name="cardSourceConditions"/> = the AS-IS two-entry <c>PartitionCondition</c> list defining
    /// colour group 1 ([0]) and group 2 ([1]) — stored on the grant and consumed by the per-group candidate
    /// filter (previously accepted and dropped, which let any two sources be played).</summary>
    public static ICardEffect PartitionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, IReadOnlyList<PartitionCondition>? cardSourceConditions = null) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Partition, isInheritedEffect, condition,
            cardSourceConditions is null
                ? null
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [PartitionCondition.PartitionConditionsKey] = cardSourceConditions,
                });

    /// <summary>(PRIM-W3) <c>IcecladSelfStaticEffect</c> — grants Iceclad to self.</summary>
    public static ICardEffect IcecladSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Iceclad, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>DecoySelfEffect</c> — grants Decoy (deletion-replacement) to self.
    /// (D1) <paramref name="permanentCondition"/> narrows the PROTECTED target (AS-IS Decoy.cs
    /// <c>CanSelectPermanentCondition</c>: evaluated live against the other permanent being spared, e.g.
    /// "Decoy ([Bagra Army])") — stored on the grant and read by <c>DeletionReplacementGate</c>.</summary>
    public static ICardEffect DecoySelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<Permanent, bool>? permanentCondition = null, string? effectName = null, string? effectDescription = null) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Decoy, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W3/C1) <c>FragmentSelfEffect</c> — grants Fragment (deletion-replacement) to self.
    /// <paramref name="trashValue"/> is the AS-IS Fragment &lt;X&gt; count (sources trashed to survive) —
    /// stored on the grant and read by <c>DeletionReplacementGate.FragmentCostOf</c> (previously dropped,
    /// collapsing every Fragment to X=1).</summary>
    public static ICardEffect FragmentSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, int trashValue = 0, string? effectName = null, string? effectDescription = null) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Fragment, isInheritedEffect, condition,
            extraValues: trashValue > 0
                ? new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.FragmentTrashValueKey] = trashValue }
                : null);

    /// <summary>(PRIM-W3) <c>ExecuteSelfEffect</c> — grants Execute to self.</summary>
    public static ICardEffect ExecuteSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Execute, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ScapegoatSelfEffect</c> — grants Scapegoat (deletion-replacement) to self.</summary>
    public static ICardEffect ScapegoatSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, string? effectDescription = null, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Scapegoat, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(C9) AS-IS linked-effect gate (Permanent.cs:1532 / ICardEffect.cs:403): an effect flagged
    /// <c>isLinkedEffect</c> is ACTIVE only while its source card is a LINK card of a battle-area permanent
    /// (<c>cardSource.IsLinked</c>), evaluated LIVE on every read — breaking the link stops the effect (the
    /// original has no removal event, only the two live guards). Wrapping the stored condition gives that
    /// gate to every consumer with no per-gate changes.</summary>
    internal static Func<bool>? LinkedGate(CardSource card, bool isLinkedEffect, Func<bool>? condition) =>
        !isLinkedEffect
            ? condition
            : () => card.IsLinked && (condition?.Invoke() ?? true);

    /// <summary>(FR-P2) Adapts a ported card's <c>Func&lt;Permanent,bool&gt; permanentCondition</c> into the
    /// player-scope predicate (evaluated against each candidate 1:1). Null → no predicate (whole scope).</summary>
    internal static Func<CardSource, bool>? ScopePred(Func<Permanent, bool>? permanentCondition) =>
        permanentCondition is null ? null : cs => permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner));

    /// <summary>(PRIM-W3/FR-P2) <c>RushStaticEffect(permanentCondition, ...)</c> — grants Rush to the owner's
    /// Digimon matching <paramref name="permanentCondition"/> (evaluated 1:1; null = all owner's Digimon).</summary>
    public static ICardEffect RushStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Rush, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W3) <c>RebootStaticEffect(permanentCondition, ...)</c> — grants Reboot to the owner's
    /// Digimon (player-scope). <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> per-card.</summary>
    public static ICardEffect RebootStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Reboot, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

    /// <summary>(PRIM-W3) <c>CanNotAttackStaticEffect(...)</c> — the scoped player's Digimon cannot attack
    /// (player-scope CannotAttack restriction consulted by AttackPermanentAction). Per-permanent predicate is
    /// per-card.</summary>
    public static ICardEffect CanNotAttackStaticEffect(HeadlessPlayerId scopePlayerId, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayerId, RestrictionHelpers.CannotAttackKey, scopeCardType: null, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>Gain1MemoryTamerOpponentDigimonEffect(card)</c> — "[Start of Your Turn] if your
    /// opponent has a Digimon, gain 1 memory." (main-phase timing mapped to OnStartTurn).</summary>
    public static ICardEffect Gain1MemoryTamerOpponentDigimonEffect(CardSource card) =>
        new TriggeredGainMemoryEffect(card, EffectTiming.OnStartTurn, amount: 1,
            "[Start of Your Turn] If your opponent has a Digimon, gain 1 memory.",
            extraCondition: () => CardEffectCommons.MatchConditionPermanentCount(card, id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)) > 0);

    /// <summary>(PRIM-W3) <c>Gain2MemoryOptionDelayEffect(card)</c> — a delayed "gain 2 memory" (resolves at
    /// the next start of the owner's turn). The Option-delay timing is mapped to OnStartTurn.</summary>
    public static ICardEffect Gain2MemoryOptionDelayEffect(CardSource card) =>
        new TriggeredGainMemoryEffect(card, EffectTiming.OnStartTurn, amount: 2, "Gain 2 memory (delayed to the start of your turn).");

    /// <summary>(PRIM-W3) <c>CanNotBeBlockedStaticSelfEffect</c> — this Digimon cannot be blocked (unblockable);
    /// consulted by BlockTiming when enumerating blocker candidates.</summary>
    public static ICardEffect CanNotBeBlockedStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBeBlockedKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>CantUnsuspendStaticEffect</c> — this Digimon does not unsuspend; consulted by the
    /// Unsuspend step.</summary>
    public static ICardEffect CantUnsuspendStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotUnsuspendKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>CanNotBeDestroyedBySkillStaticEffect</c> — this Digimon cannot be deleted by
    /// effects/skills (battle deletion still applies); consulted by the effect-sourced delete path.</summary>
    public static ICardEffect CanNotBeDestroyedBySkillStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBeDeletedBySkillKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ChangeSAttackStaticEffect</c> — continuous ±security attack on the owner's Digimon
    /// (player-scope SA modifier consulted by ContinuousModifierGate.ResolveSecurityAttack). Mirrors the SA
    /// analogue of <see cref="ChangeDPStaticEffect"/>; <paramref name="permanentCondition"/> per-card.</summary>
    public static ICardEffect ChangeSAttackStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.SAttackDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W3) <c>ReturnToLibraryBottomDigivolutionCardsClass</c> — returns the host's own
    /// digivolution (under-)cards to the bottom of the deck (activated).</summary>
    public static IActivatedCardEffect ReturnToLibraryBottomDigivolutionCardsClass(CardSource card, int count) =>
        new ReturnSelfDigivolutionCardsToDeckEffect(card, count, "Return this Digimon's digivolution cards to the bottom of the deck.");

    /// <summary>(PRIM-W3) <c>ReplaceBottomSecurityWithFaceUpOptionEffect</c> — Option [Main]: add the bottom
    /// security card to hand, then place this card face up as the bottom security card.</summary>
    public static IActivatedCardEffect ReplaceBottomSecurityWithFaceUpOptionEffect(CardSource card) =>
        new ReplaceBottomSecurityWithFaceUpEffect(card, "[Main] Add your bottom security card to the hand. Then, place this card face up as the bottom security card.");

    /// <summary>(PRIM-W3) <c>ReplaceBottomSecurityWithFaceUpOptionMainEffect</c> — Main-phase variant of
    /// <see cref="ReplaceBottomSecurityWithFaceUpOptionEffect"/>.</summary>
    public static IActivatedCardEffect ReplaceBottomSecurityWithFaceUpOptionMainEffect(CardSource card) =>
        new ReplaceBottomSecurityWithFaceUpEffect(card, "[Main] Add your bottom security card to the hand. Then, place this card face up as the bottom security card.");

    /// <summary>(PRIM-W3, C-24) <c>TrainingEffect</c> — activated [Breeding]: suspend self, place the top deck
    /// card at the bottom of self's digivolution stack.</summary>
    public static IActivatedCardEffect TrainingEffect(CardSource card) =>
        new TrainingActivatedEffect(card, "[Breeding] Suspend this Digimon: place the top card of your deck under it as its bottom digivolution card.");

    /// <summary>(PRIM-W3, C-23) <c>MaterialSaveEffect</c> — move <paramref name="count"/> of this Digimon's
    /// digivolution cards under another of your Digimon (<paramref name="destinationId"/>, chosen at port time).</summary>
    public static IActivatedCardEffect MaterialSaveEffect(CardSource card, HeadlessEntityId destinationId, int count) =>
        new MaterialSaveActivatedEffect(card, destinationId, count, "Place this Digimon's digivolution cards under another of your Digimon.");

    /// <summary>(PRIM-W3) <c>ChangeSelfLinkMaxStaticEffect</c> — continuous ±link-maximum on self. Registers a
    /// LinkedMaxDelta continuous modifier (queryable via ContinuousModifierGate). Grant is live; the link
    /// enforcement consumer migrates to consult it separately (preemptive seal).</summary>
    public static ICardEffect ChangeSelfLinkMaxStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.LinkedMaxDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>GrantedReduceLinkCostClass</c> — continuous link-cost reduction. Registers a
    /// LinkCostDelta continuous modifier (queryable via ContinuousModifierGate); the link-cost payment consumer
    /// migrates to consult it separately (preemptive seal). Per-card conditions accepted for fidelity.</summary>
    public static ICardEffect GrantedReduceLinkCostClass(CardSource card, int reducedCost, bool isInheritedEffect = false, Func<bool>? condition = null) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.LinkCostDeltaKey, -Math.Abs(reducedCost), isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>MindLink</c> — grants the MindLink keyword (Tamer↔Digimon link). Grant is live via
    /// HasKeyword; the tamer-as-Digimon behavior consumer migrates separately (preemptive seal).</summary>
    public static ICardEffect MindLinkSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.MindLink, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>UseRequirements</c> (AS-IS <c>IgnoreColorConditionClass</c>) — lets this card
    /// digivolve ignoring the COLOR part of the printed requirement (level still enforced). Registers a
    /// continuous ignore-color flag consulted by DigivolveAction. <paramref name="cardCondition"/> per-card.</summary>
    public static ICardEffect UseRequirements(CardSource card, Func<CardSource, bool>? cardCondition = null, bool isInheritedEffect = false, Func<bool>? condition = null)
    {
        // (FR2/M-1) AS-IS UseRequirements' CanUseCondition: the ignore-color is ACTIVE only while the owner
        // controls a battle-area OR breeding-area Digimon/Tamer whose top card matches cardCondition. Fold that
        // into the effect's condition so it is not granted unconditionally.
        Func<bool>? gate = cardCondition is null
            ? condition
            : () => (condition is null || condition())
                && OwnerControlsMatchingDigimon(card, p => (p.IsDigimon || p.IsTamer) && cardCondition(p.TopCard), ChoiceZone.BattleArea, ChoiceZone.BreedingArea);
        return new ContinuousSelfRestrictionEffect(card, DigivolveAction.IgnoreColorRequirementKey, isInheritedEffect, gate);
    }

    // ===== PRIM-W4 (low-frequency tail) =================================================================

    /// <summary>(PRIM-W4) <c>CanNotBlockStaticSelfEffect</c> — this Digimon cannot block (self CannotBlock
    /// restriction consulted by ContinuousRestrictionGate.EvaluateBlock).</summary>
    public static ICardEffect CanNotBlockStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBlockKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>CanNotBlockStaticEffect</c> — the scoped player's Digimon cannot block
    /// (player-scope CannotBlock restriction).</summary>
    public static ICardEffect CanNotBlockStaticEffect(HeadlessPlayerId scopePlayerId, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayerId, RestrictionHelpers.CannotBlockKey, scopeCardType: null, isInheritedEffect, condition);

    /// <summary>(PRIM-W4/FR2) <c>CanNotBeDestroyedStaticEffect</c> — registers a continuous Delete/Prevent
    /// replacement (battle + effect deletion), honoured by BattleDeletionGate and the effect-delete path.
    /// null <paramref name="permanentCondition"/> = the self form ("THIS Digimon cannot be deleted");
    /// non-null = the SET form ("your &lt;X&gt; Digimon cannot be deleted") — a player-scope prevent with the
    /// predicate evaluated 1:1 per permanent. (An earlier revision of this doc said SET was unbuilt — stale.)</summary>
    public static ICardEffect CanNotBeDestroyedStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, ReplacementHelpers.PreventDeletionKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, ReplacementHelpers.PreventDeletionKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>ImmuneFromDPMinusStaticEffect</c> — this Digimon is immune to DP-reducing effects
    /// (D-A3). Registers a continuous DpReduction/Immune replacement honoured by ContinuousDpGate.</summary>
    public static ICardEffect ImmuneFromDPMinusStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, ReplacementHelpers.ImmuneFromDpMinusKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, ReplacementHelpers.ImmuneFromDpMinusKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 B.O.6) <c>CannotReduceCostClass</c> — the play/digivolution cost of this card (or, with
    /// <paramref name="permanentCondition"/>, the owner's matching cards) cannot be reduced. Registers a
    /// continuous CostReduction/Immune replacement honoured by ContinuousModifierGate.Resolve{Play,Digivolution}Cost.</summary>
    public static ICardEffect CanNotReduceCostStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, ReplacementHelpers.ImmuneFromCostReductionKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, ReplacementHelpers.ImmuneFromCostReductionKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 B.O.6) <c>CannotAddSecurityClass</c> — <paramref name="scopePlayer"/> cannot add cards to
    /// their security (AS-IS Player.CanAddSecurity). <paramref name="causingEffectPredicate"/> mirrors the AS-IS
    /// <c>CardEffectCondition</c> — the restriction fires only when the CAUSING effect matches (e.g.
    /// IsOpponentEffect); null = block every add. Consulted at the AddToSecurity / Recover mutation chokes.</summary>
    public static ICardEffect CanNotAddSecurityStaticEffect(HeadlessPlayerId scopePlayer, bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayer, RestrictionHelpers.CannotAddSecurityKey, scopeCardType: null, isInheritedEffect, condition, causingEffectPredicate: causingEffectPredicate);

    /// <summary>(PRIM-P0 B.O.6) <c>CannotAddMemoryClass</c> — <paramref name="scopePlayer"/> cannot gain memory
    /// (AS-IS Player.CanAddMemory). <paramref name="causingEffectPredicate"/> mirrors the AS-IS
    /// <c>CardEffectCondition</c> (null = block every gain). Consulted at the AddMemory mutation choke.</summary>
    public static ICardEffect CanNotAddMemoryStaticEffect(HeadlessPlayerId scopePlayer, bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayer, RestrictionHelpers.CannotAddMemoryKey, scopeCardType: null, isInheritedEffect, condition, causingEffectPredicate: causingEffectPredicate);

    /// <summary>(PRIM-W4) <c>AllianceStaticEffect</c> — grants Alliance to the owner's Digimon (player-scope
    /// keyword). <paramref name="permanentCondition"/> per-card.</summary>
    public static ICardEffect AllianceStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Alliance, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    // (PRIM-P0 AddSkillClass) player-scope keyword grants — "your Digimon (matching permanentCondition) gain
    // <keyword>". These are the headless port target for AS-IS AddSkillClass whose getEffects grants a
    // <keyword>SelfEffect to a live-matched set: the player-scope binding re-evaluates the set per query, so a
    // Digimon that enters AFTER the grant still gains the keyword (proven in PRIM-P0.AddSkillLiveSet.Tests).
    /// <summary>(PRIM-P0 AddSkill) grants Piercing to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect PiercingStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Piercing, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Blitz to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect BlitzStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Blitz, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Retaliation to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect RetaliationStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Retaliation, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Scapegoat (deletion-replacement) to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect ScapegoatStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Scapegoat, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Decoy (deletion-replacement) to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect DecoyStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Decoy, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Barrier to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect BarrierStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Barrier, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) AS-IS AddSkillClass whose getEffects splices a TRIGGERED activated effect onto
    /// a live-matched set: <paramref name="nestedTriggeredEffect"/> (built to read the triggering card via
    /// TriggerEntityId and apply the per-card predicate + a nested activated resolution) fires for any event whose
    /// actor is <paramref name="scopePlayer"/> (the live set). The nested effect's ToBinding sets the granted
    /// timing.</summary>
    public static ICardEffect GrantTriggeredEffectToScopedSet(CardSource card, HeadlessPlayerId scopePlayer, ICardEffect nestedTriggeredEffect, bool scopeAnyPlayer = false) =>
        new PlayerScopeTriggerGrantEffect(card, scopePlayer, nestedTriggeredEffect, scopeAnyPlayer);

    /// <summary>(PRIM-W4) <c>JammingStaticEffect</c> — grants Jamming to the owner's Digimon (player-scope
    /// keyword). <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> per-card.</summary>
    public static ICardEffect JammingStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Jamming, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>AscensionSelfEffect</c> — grants the Ascension keyword (post-deletion → security).
    /// Grant live via HasKeyword; DeletionReplacementGate's hasAscension consumer migrates separately.</summary>
    public static ICardEffect AscensionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Ascension, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(PRIM-W4) <c>ChangeBaseDPGlobalEffect</c> — continuous ±base-DP on the owner's Digimon
    /// (player-scope BaseDp modifier consulted by ContinuousDpGate). <paramref name="permanentCondition"/>
    /// per-card; the opponent-side "global" reach is a per-card scope concern.</summary>
    public static ICardEffect ChangeBaseDPGlobalEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        // (M-5) AS-IS "Global" = BOTH players' Digimon matching permanentCondition (no owner scope). scopeAnyPlayer
        // + the predicate select the set across both players (the owner-only scope missed the opponent's).
        new PlayerScopeModifierEffect(card, ModifierHelpers.BaseDpDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition), scopeAnyPlayer: true);

    /// <summary>(PRIM-W4) <c>InvertSAttackStaticEffect</c> — continuous invert-security-attack on self
    /// (consumed by ContinuousModifierGate.ResolveSecurityAttack).</summary>
    public static ICardEffect InvertSAttackStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        permanentCondition is null
            ? new ContinuousSelfModifierEffect(card, ModifierHelpers.InvertSecurityAttackDeltaKey, changeValue, isInheritedEffect, condition)
            : new PlayerScopeModifierEffect(card, ModifierHelpers.InvertSecurityAttackDeltaKey, changeValue, scopeCardType: null, condition, scopeZone: null, scopePredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CollisionStaticEffect</c> — grants Collision to the owner's Digimon (player-scope
    /// keyword). Grant live via HasKeyword; BlockTiming's hasCollision consumer migrates separately.</summary>
    public static ICardEffect CollisionStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Collision, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4/K1) <c>VortexCanAttackPlayersStaticEffect</c> — the AS-IS
    /// <c>IVortexCanAttackPlayersEffect</c>: a marker letting a permanent that is resolving its Vortex
    /// attack target the PLAYER (it does NOT grant Vortex itself — the previous Vortex-keyword lowering was
    /// a flatten and wrongly opened the end-of-turn window for non-Vortex allies).
    /// <paramref name="attackerCondition"/> = AS-IS AttackerCondition, evaluated against the attacker.</summary>
    public static ICardEffect VortexCanAttackPlayersStaticEffect(Func<Permanent, bool>? attackerCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.VortexCanAttackPlayers, scopeCardType: null, isInheritedEffect, condition, ScopePred(attackerCondition));

    /// <summary>(PRIM-W4) <c>ChangeLinkMaxStaticEffect</c> — continuous ±link-maximum on the owner's Digimon
    /// (player-scope LinkedMaxDelta modifier, queryable). Link enforcement consumer migrates separately
    /// (preemptive seal, same as ChangeSelfLinkMax).</summary>
    public static ICardEffect ChangeLinkMaxStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.LinkedMaxDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W4/K4) <c>TreatAsDigimonStaticEffect</c> — "also treated as a Digimon". AS-IS
    /// <c>TreatAsDigimonClass.IsDigimon(permanent)</c> evaluates <paramref name="permanentCondition"/>
    /// against the permanent BEING JUDGED (any of the owner's, e.g. a Tamer), so the grant is player-scope
    /// with the predicate — the previous self-only lowering dropped the predicate. Consumed by the central
    /// <see cref="ContinuousKeywordGate.IsDigimon(EngineContext, HeadlessEntityId)"/> chokepoint.</summary>
    public static ICardEffect TreatAsDigimonStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.TreatAsDigimon, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>Gain1MemoryTamerOwnerDigimonConditionalEffect</c> — "[Start of Your Turn] if you
    /// have a matching Digimon, gain 1 memory." The per-permanent predicate is captured in
    /// <paramref name="condition"/> at porting time.</summary>
    public static ICardEffect Gain1MemoryTamerOwnerDigimonConditionalEffect(string effectDescription, Func<Permanent, bool>? permanentCondition, Func<bool>? condition, CardSource card)
    {
        // (FR2) The memory gain is CONDITIONAL on the owner controlling a Digimon matching permanentCondition
        // (AS-IS). Fold that predicate into the trigger gate so it is not gained unconditionally.
        Func<bool>? gate = permanentCondition is null
            ? condition
            : () => (condition is null || condition()) && OwnerControlsMatchingDigimon(card, permanentCondition);
        return new TriggeredGainMemoryEffect(card, EffectTiming.OnStartTurn, amount: 1,
            string.IsNullOrWhiteSpace(effectDescription) ? "[Start of Your Turn] Gain 1 memory." : effectDescription, extraCondition: gate);
    }

    /// <summary>(FR2) Whether <paramref name="card"/>'s owner controls at least one permanent in
    /// <paramref name="searchZones"/> (default: battle area) satisfying <paramref name="predicate"/> (evaluated
    /// as a <see cref="Permanent"/> 1:1).</summary>
    internal static bool OwnerControlsMatchingDigimon(CardSource card, Func<Permanent, bool> predicate, params ChoiceZone[] searchZones)
    {
        if (card.Context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        ChoiceZone[] zonesToSearch = searchZones is { Length: > 0 } ? searchZones : new[] { ChoiceZone.BattleArea };
        foreach (ChoiceZone zone in zonesToSearch)
        {
            foreach (HeadlessEntityId id in zones.GetCards(card.Owner, zone))
            {
                if (predicate(new Permanent(card.Context, id, card.Owner)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>(PRIM-W4) <c>EoTLose3Memory</c> — "[End of Your Turn] lose 3 memory."</summary>
    public static ICardEffect EoTLose3Memory(CardSource card) =>
        new TriggeredGainMemoryEffect(card, EffectTiming.OnEndTurn, amount: -3, "[End of Your Turn] Lose 3 memory.");

    /// <summary>(PRIM-W4) <c>CantSuspendStaticEffect</c> — this Digimon cannot be suspended (self CannotSuspend
    /// restriction consulted by the Suspend sink path). <paramref name="permanentCondition"/> per-card.</summary>
    public static ICardEffect CantSuspendStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotSuspendKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotSuspendKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CannotReturnToHandStaticEffect</c> — this Digimon cannot be returned to hand
    /// (self restriction consulted by the ReturnToHand sink path).</summary>
    public static ICardEffect CannotReturnToHandStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotReturnToHandKey, isInheritedEffect, condition, cardEffectCondition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotReturnToHandKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), cardEffectCondition);

    /// <summary>(PRIM-W4) <c>CannotReturnToDeckStaticEffect</c> — this Digimon cannot be returned to the deck
    /// (self restriction consulted by the ReturnToDeck sink paths).</summary>
    public static ICardEffect CannotReturnToDeckStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotReturnToDeckKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotReturnToDeckKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CanNotBeDestroyedByBattleStaticEffect</c> — this Digimon cannot be deleted in
    /// battle (effect deletion still applies). Registers a battle-only immunity flag read by
    /// BattleDeletionGate. Per-card predicates accepted for fidelity.</summary>
    public static ICardEffect CanNotBeDestroyedByBattleStaticEffect(Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition, Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, bool isLinkedEffect = false) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, BattleDeletionGate.PreventBattleDeletionKey, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition))
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, BattleDeletionGate.PreventBattleDeletionKey, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CanNotBeTrashedBySkillStaticEffect</c> / <c>ImmuneStackTrashingClass</c> — this
    /// Digimon's digivolution cards cannot be trashed by effects. Registers a stack-trash immunity flag read
    /// by the source-trash sink path.</summary>
    public static ICardEffect CanNotBeTrashedBySkillStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, MatchStateMutationSink.ImmuneStackTrashingKey, isInheritedEffect, condition, cardEffectCondition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, MatchStateMutationSink.ImmuneStackTrashingKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), cardEffectCondition);

    /// <summary>(PRIM-W4) <c>ImmuneStackTrashingClass</c> — alias of <see cref="CanNotBeTrashedBySkillStaticEffect"/>.</summary>
    public static ICardEffect ImmuneStackTrashingClass(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, MatchStateMutationSink.ImmuneStackTrashingKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>ReplaceTopSecurityWithFaceUpOptionMainEffect</c> — Option [Main]: add the TOP
    /// security card to hand, then place this card face up as the top security card.</summary>
    public static IActivatedCardEffect ReplaceTopSecurityWithFaceUpOptionMainEffect(CardSource card) =>
        new ReplaceBottomSecurityWithFaceUpEffect(card, "[Main] Add your top security card to the hand. Then, place this card face up as the top security card.", top: true);

    /// <summary>(PRIM-W4/K5) <c>PlayMindLinkTamerFromDigivolutionCards</c> — plays the Mind-Linked TAMER
    /// under-card matching <paramref name="cardName"/> from a Digimon's digivolution stack onto the field
    /// (cost-free). Candidates are TAMER under-cards (the previous Digimon-only reuse could never surface
    /// the tamer), narrowed to the AS-IS card name.</summary>
    public static IActivatedCardEffect PlayMindLinkTamerFromDigivolutionCards(CardSource card, string cardName, string effectDescription) =>
        new ActivatedPlayFromUnderEffect(
            card,
            string.IsNullOrWhiteSpace(effectDescription) ? $"Play {cardName} from under a Digimon." : effectDescription,
            cardType: "Tamer",
            cardName: string.IsNullOrWhiteSpace(cardName) ? null : cardName);

    /// <summary>(PRIM-W4) <c>CanNotBeAttackedSelfStaticEffect</c> — this Digimon cannot be attacked (self
    /// CannotBeAttacked restriction consulted on the defender by AttackPermanentAction).</summary>
    public static ICardEffect CanNotBeAttackedSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBeAttackedKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>RevealLibraryClass</c> — reveals the top <paramref name="revealCount"/> cards of
    /// the owner's deck. In the full-information headless model a pure reveal has no hidden-state change, so
    /// this is an informational primitive; any follow-up act on the revealed cards is authored per-card.</summary>
    public static IActivatedCardEffect RevealLibraryClass(CardSource card, int revealCount) =>
        new InformationalRevealEffect(card, revealCount, $"Reveal the top {revealCount} card(s) of your deck.");

    /// <summary>(PRIM-W2) Original: <c>PlaceSelfDelayOptionSecurityEffect(card)</c> — "[Security] place this
    /// card in the battle area" (a Delay Option triggered from security). Reuses the play-this-card-to-battle
    /// mechanism (<see cref="PlayThisCardToBattleEffect"/>, cost-free move to the battle area); the Delay
    /// option's later trigger is a per-card effect on the placed card.</summary>
    public static ICardEffect PlaceSelfDelayOptionSecurityEffect(CardSource card) =>
        new PlayThisCardToBattleEffect(card, "[Security] Place this card in the battle area.");

    /// <summary>(PRIM-W2) Original: <c>PlaySelfDigimonAfterBattleSecurityEffect(card, deleteDigimon)</c> —
    /// "[Security] play this Digimon" (from security to the battle area). Reuses the play-this-card-to-battle
    /// mechanism (<see cref="PlayThisCardToBattleEffect"/>). The "at end of battle" timing and the temporary
    /// (<c>deleteDigimon</c>) lifetime are per-card refinements on the placed Digimon.</summary>
    public static ICardEffect PlaySelfDigimonAfterBattleSecurityEffect(CardSource card) =>
        new PlayThisCardToBattleEffect(card, "[Security] Play this Digimon (from security).");

    /// <summary>(PRIM-W2) Original: <c>LinkEffect(card, condition)</c> — the &lt;Link&gt; activation: attach
    /// this card to a chosen own Digimon, paying the link cost (read from the card's <c>linkCost</c> data).</summary>
    /// <summary>(W6-X) 1:1 mirror of AS-IS <c>AddDetailClass(canUseCondition, permanentCondition, detail,
    /// triggerEffect, card)</c> (CardEffectFactory.cs:1523) — DISPLAY-ONLY (PermanentDetail.cs tooltip text;
    /// the <c>triggerEffect</c> bool has ZERO consumers in the AS-IS codebase). Registers an inert binding
    /// carrying the detail string for observability; no game behavior.</summary>
    public static ICardEffect AddDetailClass(
        Func<bool>? canUseCondition, Func<Permanent, bool>? permanentCondition, string detail, bool triggerEffect, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _ = permanentCondition;
        _ = triggerEffect;
        return new DisplayDetailEffect(card, detail ?? string.Empty, canUseCondition);
    }

    /// <summary>(W6-A2) 1:1 mirror of AS-IS <c>ArtsDigivolveEffect(card)</c>
    /// (CardEffectFactory/KeyWordEffects/ArtsDigivolve.cs): while this Option resolves (executing area),
    /// digivolve it COST-FREE onto an owner Digimon that satisfies the normal digivolution requirement
    /// (<c>CanPlayCardTargetFrame</c> — the port's evolution-cost gate, cost unpaid).</summary>
    public static IActivatedCardEffect ArtsDigivolveEffect(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new ArtsDigivolveSelfEffect(card);
    }

    /// <summary>(W6-F) 1:1 mirror of AS-IS <c>AddAppfuseMethodByCondition(cardConditions, card, cost,
    /// effectName)</c> (CardEffectFactory/AddAppfusionMethod.cs:16): declares the App-Fusion condition —
    /// the host's TOP card matches condition i and one of its LINK cards matches a DIFFERENT condition j
    /// (i != j), each material used once. Cost defaults 0.</summary>
    public static ICardEffect AddAppfuseMethodByCondition(
        IReadOnlyList<Func<CardSource, bool>> cardConditions, CardSource card, int cost = 0, string effectName = "App Fusion")
    {
        ArgumentNullException.ThrowIfNull(cardConditions);
        ArgumentNullException.ThrowIfNull(card);

        bool DigimonCondition(Permanent permanent)
        {
            if (!CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
            {
                return false;
            }

            for (int i = 0; i < cardConditions.Count; i++)
            {
                if (!cardConditions[i](permanent.TopCard))
                {
                    continue;
                }

                for (int j = 0; j < cardConditions.Count; j++)
                {
                    if (i != j && LinkedViews(permanent).Any(linked => cardConditions[j](linked)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool LinkedCondition(Permanent permanent, CardSource linkedCard)
        {
            for (int i = 0; i < cardConditions.Count; i++)
            {
                if (!cardConditions[i](permanent.TopCard))
                {
                    continue;
                }

                for (int j = 0; j < cardConditions.Count; j++)
                {
                    if (i != j && cardConditions[j](linkedCard))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        var effect = new CardEffects.AddAppFusionConditionClass();
        effect.SetUpICardEffect(effectName, null, card);
        effect.SetUpAddAppFusionConditionClass(cardSource =>
            cardSource.InstanceId == card.InstanceId
                ? new AppFusionCondition(LinkedCondition, DigimonCondition, cost)
                : null);
        effect.SetNotShowUI(true);
        return effect;

        static IEnumerable<CardSource> LinkedViews(Permanent permanent)
        {
            EngineContext context = permanent.TopCard.Context;
            if (!context.CardInstanceRepository.TryGetInstance(permanent.InstanceId, out CardInstanceRecord? host) || host is null)
            {
                yield break;
            }

            foreach (HeadlessEntityId linkId in Headless.Runtime.LinkHelpers.ReadLinkedCardIds(host.Metadata))
            {
                yield return new CardSource(context, linkId, permanent.OwnerId, permanent.OwnerId);
            }
        }
    }

    /// <summary>(W6-F) 1:1 mirror of AS-IS <c>AddAppfuseMethodByName(cardNames, card, cost, effectName)</c>
    /// (AddAppfusionMethod.cs): the by-NAME sugar over <see cref="AddAppfuseMethodByCondition"/>.</summary>
    public static ICardEffect AddAppfuseMethodByName(
        IReadOnlyList<string> cardNames, CardSource card, int cost = 0, string effectName = "App Fusion") =>
        AddAppfuseMethodByCondition(
            cardNames.Select<string, Func<CardSource, bool>>(name => cs => cs.EqualsCardName(name)).ToList(),
            card, cost, effectName);

    /// <summary>(W6-L) 1:1 mirror of AS-IS <c>AddSelfLinkConditionStaticEffect(permanentCondition, linkCost,
    /// card, condition, cardCondition, effectName)</c> (CardEffectFactory/AddLinkRequirement.cs:11): the
    /// timing-None declaration "this card may LINK onto an owner Digimon matching
    /// <paramref name="permanentCondition"/>, paying <paramref name="linkCost"/>". The separate
    /// <c>LinkEffect(card)</c> (OnDeclaration) is the play action that consumes it.</summary>
    public static ICardEffect AddSelfLinkConditionStaticEffect(
        Func<Permanent, bool> permanentCondition, int linkCost, CardSource card,
        Func<bool>? condition = null, Func<CardSource, bool>? cardCondition = null, string? effectName = null)
    {
        ArgumentNullException.ThrowIfNull(permanentCondition);
        ArgumentNullException.ThrowIfNull(card);
        Func<CardSource, bool> cardGate = cardCondition ?? (cardSource => cardSource.InstanceId == card.InstanceId);
        var effect = new CardEffects.AddLinkConditionClass();
        effect.SetUpICardEffect(effectName ?? "Link", condition, card);
        effect.SetUpAddLinkConditionClass(cardSource =>
            cardSource.InstanceId == card.InstanceId && cardGate(cardSource)
                ? new LinkCondition(permanentCondition, linkCost)
                : null);
        return effect;
    }

    public static ICardEffect LinkEffect(CardSource card, Func<bool>? condition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        // (W6-L) the declared LinkCondition (AddSelfLinkConditionStaticEffect) is authoritative — AS-IS
        // LinkEffect reads card.linkCondition.cost; the definition-metadata linkCost is the data fallback.
        int linkCost = card.LinkConditionOf()?.cost
            ?? (card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? inst) && inst is not null
                && card.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
                && def.Metadata.TryGetValue("linkCost", out object? raw) && raw is int cost ? cost : 0);
        return new LinkSelfEffect(card, linkCost, $"Link (Cost: {linkCost}).");
    }

    /// <summary>(PRIM-W2) Original: <c>BlockerStaticEffect(permanentCondition, isInheritedEffect, card,
    /// condition, isLinkedEffect)</c> — grants Blocker to a set of permanents. Modeled as a PLAYER-SCOPE
    /// Blocker grant on the owner's Digimon (the common "your Digimon gain &lt;Blocker&gt;" form);
    /// <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> accepted for source fidelity,
    /// per-permanent narrowing beyond the owner scope is a per-card concern.</summary>
    public static ICardEffect BlockerStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Blocker, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

    /// <summary>(PRIM-W2) Original: <c>SetMemoryTo3TamerEffect(card)</c> — "[Start of Your Turn] If you have
    /// 2 or less memory, set your memory to 3." (Tamer memory-setter). Triggered on OnStartTurn.</summary>
    public static ICardEffect SetMemoryTo3TamerEffect(CardSource card) =>
        new TriggeredSetMemoryEffect(card, EffectTiming.OnStartTurn, targetMemory: 3, threshold: 2,
            "[Start of Your Turn] If you have 2 or less memory, set your memory to 3.");

    /// <summary>(PRIM-W2) Original: <c>ArmorPurgeEffect(card)</c> — grants ArmorPurge to self (Batch2).</summary>
    public static ICardEffect ArmorPurgeEffect(CardSource card) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.ArmorPurge, isInheritedEffect: false, condition: null);

    /// <summary>(PRIM-W2) Original: <c>CanNotAttackSelfStaticEffect(defenderCondition, isInheritedEffect, card,
    /// condition, effectName)</c> — "this Digimon cannot attack" (self). Registers a CannotAttack restriction
    /// (reusable <see cref="ContinuousSelfRestrictionEffect"/>) that AttackPermanentAction consults via
    /// ContinuousRestrictionGate.EvaluateAttack. <paramref name="defenderCondition"/>/<paramref name="effectName"/>
    /// are accepted for source fidelity; per-defender narrowing is a per-card concern.</summary>
    public static ICardEffect CanNotAttackSelfStaticEffect(Func<Permanent, bool>? defenderCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        defenderCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotAttackKey, isInheritedEffect, condition)
            : new CanNotAttackDefenderConditionEffect(card, cs => defenderCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)), isInheritedEffect, condition);

    /// <summary>Original: <c>ChangeDPStaticEffect</c> — continuous ±DP on a set of permanents. Here scoped
    /// to the owner's Digimon (the common "your Digimon get +X DP" form); <paramref name="permanentCondition"/>
    /// is accepted for source fidelity but the owner-scope handles targeting.</summary>
    public static ICardEffect ChangeDPStaticEffect(
        Func<Permanent, bool> permanentCondition,
        int changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool>? condition,
        Func<string>? effectName = null) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition));

    /// <summary>A triggered "[When ...] gain/lose N memory" effect (the common ActivateClass memory form).
    /// <paramref name="timing"/> is the branch timing the card declared it under.</summary>
    public static ICardEffect AddMemoryTriggerEffect(
        EffectTiming timing, int amount, bool isInheritedEffect, CardSource card, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null) =>
        new TriggeredMemoryEffect(card, timing, amount, isInheritedEffect, condition, description, triggerGate, maxCountPerTurn, hash, isOptional);

    /// <summary>Original: <c>PlaySelfTamerSecurityEffect</c> — a Tamer's [Security] "play this Tamer". Plays
    /// the revealed Tamer onto the battle area (cost-free), auto-registering its effects (G10-003).</summary>
    public static ICardEffect PlaySelfTamerSecurityEffect(CardSource card) =>
        new PlayThisCardToBattleEffect(card, "[Security] Play this Tamer.");

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching permanents and delete them"
    /// effect (Option [Main] delete skill, e.g. ST1_16 / ST1_15).</summary>
    public static ICardEffect SelectAndDestroyEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canTarget,
        int maxCount,
        bool canEndNotMax,
        string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Destroy, description);

    /// <summary>(PRIM-P0-flow) An activated "choose one of the following modes" menu (AS-IS UserSelectionManager
    /// SetBool/IntSelection). Each mode is a labeled branch effect; a mode with an availability predicate that
    /// returns false is omitted. The selected branch resolves through the same activation flow / sink.</summary>
    public static ICardEffect SelectModeEffect(CardSource card, string description, params ModeChoiceEffect.Mode[] modes) =>
        new ModeChoiceEffect(card, description, modes);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>new SuspendPermanentsClass(perms, ..).Tap()</c>
    /// coroutine: select up to <paramref name="maxCount"/> matching permanents and suspend them.</summary>
    public static ICardEffect SelectAndSuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Tap, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS unsuspend coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and unsuspend them.</summary>
    public static ICardEffect SelectAndUnsuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.UnTap, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS bounce coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and return them to hand.</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Bounce, description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> matching permanents and return
    /// them to the owner's deck (top or bottom). AS-IS SelectPermanentEffect.Mode PutLibraryTop/Bottom.</summary>
    public static ICardEffect SelectAndReturnToDeckEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax,
            toTop ? SelectPermanentEffect.Mode.PutLibraryTop : SelectPermanentEffect.Mode.PutLibraryBottom, description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> matching permanents and place
    /// them into the owner's security (top or bottom). AS-IS SelectPermanentEffect.Mode PutSecurityTop/Bottom.</summary>
    public static ICardEffect SelectAndPutSecurityEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax,
            toTop ? SelectPermanentEffect.Mode.PutSecurityTop : SelectPermanentEffect.Mode.PutSecurityBottom, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.PlayPermanentCards(.., root)</c>
    /// coroutine: select up to <paramref name="maxCount"/> of the owner's cards in <paramref name="fromZone"/>
    /// (Trash / Hand) matching <paramref name="canTarget"/> and play each onto the battle area (cost-free).
    /// The AS-IS <c>SelectCardEffect.Root</c> maps to <paramref name="fromZone"/>.</summary>
    public static ICardEffect SelectAndPlayFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectAndPlayEffect(card, fromZone, canTarget, maxCount, canEndNotMax, description);

    /// <summary>(PRIM-P0 B.O.5) AS-IS <c>CardEffectCommons.PlayOptionCards</c>: select up to
    /// <paramref name="maxCount"/> of the owner's Option cards in <paramref name="sourceZone"/> and play each as a
    /// nested effect (trash → OnUseOption → resolve its [Main]). Cost-free (v1).</summary>
    public static ICardEffect PlayOptionCardEffect(
        CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> optionPredicate, int maxCount, bool canEndNotMax, string description) =>
        new PlayOptionCardEffect(card, sourceZone, optionPredicate, maxCount, canEndNotMax, description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> of the owner's cards in
    /// <paramref name="fromZone"/> (Trash / Library / Security …) matching <paramref name="canTarget"/> and add
    /// each to the owner's hand. AS-IS SelectCardEffect.Mode AddHand.</summary>
    public static ICardEffect SelectAndAddToHandFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.ReturnToHandKind, description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> of the owner's cards in
    /// <paramref name="fromZone"/> matching <paramref name="canTarget"/> and trash each. AS-IS
    /// SelectCardEffect.Mode Discard.</summary>
    public static ICardEffect SelectAndTrashFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.TrashCardKind, description);

    /// <summary>(PRIM-P0-flow B.O.3) AS-IS <c>DigivolveIntoHandOrTrashCard</c>: select 1 of the owner's Digimon
    /// (<paramref name="targetPredicate"/>) and a source card in <paramref name="sourceZone"/> (Hand / Trash,
    /// <paramref name="sourcePredicate"/>) that can digivolve onto it, pay the cost, and digivolve. v1 enforces
    /// requirements.</summary>
    public static ICardEffect SelectAndDigivolveEffect(
        CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> sourcePredicate,
        Func<HeadlessEntityId, bool> targetPredicate, DigivolveCost cost, int costAmount, string description) =>
        new SelectAndDigivolveEffect(card, sourceZone, sourcePredicate, targetPredicate, cost, costAmount, description);

    /// <summary>(PRIM-P0 B.O.4) A one-shot before-pay reduction of THIS card's own play/digivolve cost by
    /// <paramref name="amount"/> when <paramref name="condition"/> holds (AS-IS BeforePayCost ActivateClass →
    /// UntilCalculateFixedCostEffect.Add). Non-interactive.</summary>
    public static ICardEffect BeforePayCostReductionEffect(CardSource card, int amount, Func<bool>? condition, string description) =>
        new BeforePayCostReductionEffect(card, () => amount, condition, description);

    /// <summary>(PRIM-P0 B.O.4) As above with a dynamic reduction amount (e.g. -1 per matching card).</summary>
    public static ICardEffect BeforePayCostReductionEffect(CardSource card, Func<int> amount, Func<bool>? condition, string description) =>
        new BeforePayCostReductionEffect(card, amount, condition, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.AddThisCardToHand(..)</c> — return
    /// this card to the owner's hand.</summary>
    public static IActivatedCardEffect AddThisCardToHandEffect(CardSource card) =>
        new ReturnThisCardToHandEffect(card, "Return this card to the hand.");

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.DigivolveIntoHandOrTrashCard(..)</c>:
    /// select up to <paramref name="maxCount"/> battle-area Digimon matching <paramref name="canTarget"/> and
    /// de-digivolve each by <paramref name="count"/> (remove its top digivolution card[s]).</summary>
    public static ICardEffect SelectAndDeDigivolveEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int count, bool canEndNotMax, string description) =>
        new ActivatedSelectAndDeDigivolveEffect(card, canTarget, maxCount, count, canEndNotMax, description);

    /// <summary>(PRIM-W5) Mirror of the AS-IS <c>CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect</c>:
    /// reveal the top <paramref name="revealCount"/> cards of the owner's deck, select per
    /// <paramref name="conditions"/>, route the rest to <paramref name="remainingTo"/>.</summary>
    public static IActivatedCardEffect SimplifiedRevealDeckTopCardsAndSelect(
        CardSource card, int revealCount, IReadOnlyList<SimplifiedSelectCardConditionClass> conditions,
        RevealDestination remainingTo, string description) =>
        new SimplifiedRevealAndSelectEffect(card, revealCount, conditions, remainingTo, description);

    /// <summary>(P4) Mirror of the FULL AS-IS <c>CardEffectCommons.RevealDeckTopCardsAndSelect</c> with a
    /// <c>SelectCardConditionClass[]</c> (multi-condition sequential passes over the shared revealed pool,
    /// BT10-096/BT10-097/ST17-11 shape). Each original condition maps to one
    /// <see cref="HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass"/> (predicate 1:1, maxCount,
    /// Mode → destination — original <c>Mode.Custom</c> = <see cref="RevealDestination.Custom"/>, read the
    /// picks back from <see cref="RevealMultiSelectEffect.CustomSelections"/>).</summary>
    public static IActivatedCardEffect RevealDeckTopCardsAndSelect(
        CardSource card, int revealCount, IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> selectCardConditions,
        RevealDestination remainingCardsPlace, string description,
        bool canNoAction = false, bool isOpponentDeck = false, bool mutualConditions = false) =>
        new RevealMultiSelectEffect(card, revealCount, selectCardConditions, remainingCardsPlace, description, canNoAction, isOpponentDeck, mutualConditions);

    /// <summary>(PRIM-W5/S2) <c>CanNotAffectedStaticEffect</c> — AS-IS <c>CanNotAffectedClass</c>:
    /// <c>CanNotAffect(target, effect) = CardCondition(target) &amp;&amp; SkillCondition(effect)</c>. Registers a
    /// continuous immunity under <see cref="HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate"/> (consumed by the sink's effect path),
    /// carrying <paramref name="skillCondition"/> — the per-card predicate over the CAUSING effect's source that
    /// decides WHICH effects the card is immune to (e.g. <c>src =&gt; src.Owner != card.Owner &amp;&amp; src.IsDigimon</c>
    /// for "opponent's Digimon effects only"). <b>skillCondition must be provided to mirror the original</b>; null
    /// falls back to opponent-only.</summary>
    public static ICardEffect CanNotAffectedStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? skillCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        // (C2) permanentCondition = AS-IS CardCondition (WHICH permanents are protected) — evaluated live
        // against the protected target (previously accepted and dropped; null keeps the self-only grant).
        new ContinuousImmunityEffect(card, skillCondition, isInheritedEffect, condition, targetPredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W5) <c>ChangeCardNamesClass</c> — grants this card an additional name
    /// (<paramref name="addedName"/>), folded into <c>CardSource.CardNames</c>.</summary>
    public static ICardEffect ChangeCardNamesStaticEffect(string addedName, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ChangeCardNamesEffect(card, addedName, isInheritedEffect, condition);

    // ===== (PRIM-W5) special plays — DigiXros / Blast / Blast-DNA =====================================
    // The card DECLARES its recipe (SpecialPlayRecipeRegistry, keyed by card number); SpecialPlayAction then
    // offers/executes the fusion or free digivolve. These factories register the recipe and return a no-op
    // marker for the card's effect list.

    /// <summary>(PRIM-W5) <c>DigiXrosEffectFromNames</c> — declares this card's DigiXros recipe: the named
    /// materials (hand/field) that fuse under it. <paramref name="costReduction"/> / per-card target predicate
    /// accepted for fidelity; material consumption + cost are engine-handled at play time.</summary>
    public static ICardEffect DigiXrosEffectFromNames(CardSource card, int costReduction, object? canTargetCondition = null, params string[] names)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DigiXros, NameMaterials(names), MemoryCost: 0));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) DigiXros with ARBITRARY per-material predicates — the faithful form of the AS-IS
    /// <c>AddDigiXrosConditionClass</c> whose <c>getDigiXrosCondition</c> returns
    /// <c>DigiXrosConditionElement(CanSelectCardCondition, label)</c> per material. Each
    /// <paramref name="materials"/> slot carries the original's <c>CanSelectCardCondition</c> predicate 1:1.</summary>
    public static ICardEffect DigiXrosEffect(CardSource card, int costReduction, params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DigiXros, materials, MemoryCost: 0));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM special-play) DigiXros whose material slots may ALSO be satisfied by cards from the TRASH
    /// (up to <paramref name="maxTrashCount"/>) and/or a Tamer's digivolution sources (up to
    /// <paramref name="maxUnderTamerCount"/>) — AS-IS <c>AddMaxTrashCountDigiXrosClass</c> /
    /// <c>maxTamerDigivolutionCardsCount</c>, whose <c>getMaxTrashCount</c> Func is threaded 1:1 (evaluated per
    /// play). Pass null (or a Func returning 0) for a source not allowed.</summary>
    public static ICardEffect DigiXrosWithExtraMaterialsEffect(
        CardSource card, int costReduction,
        Func<CardSource, int>? maxTrashCount, Func<CardSource, int>? maxUnderTamerCount,
        params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(
            SpecialPlayKind.DigiXros, materials, MemoryCost: 0, Condition: null,
            MaxTrashCount: maxTrashCount, MaxUnderTamerCount: maxUnderTamerCount));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>A material slot matched by card name (the name-equality subset of a DigiXros condition).</summary>
    public static SpecialPlayMaterial MaterialByName(string name) =>
        new(cs => cs.EqualsCardName(name), name);

    private static IReadOnlyList<SpecialPlayMaterial> NameMaterials(IEnumerable<string> names) =>
        names.Select(MaterialByName).ToArray();

    /// <summary>(PRIM-W5) <c>BlastDigivolveEffect</c> — declares this card as Blast-capable: it may digivolve
    /// onto a single matching battle-area Digimon for free (SpecialPlayKind.Blast, via FreeDigivolveHelpers).</summary>
    public static ICardEffect BlastDigivolveEffect(CardSource card, Func<bool>? condition)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.Blast, Array.Empty<SpecialPlayMaterial>(), MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) <c>BlastDNADigivolveEffect</c> — declares this card's Blast-DNA recipe: the material
    /// names (from <paramref name="blastDNAConditions"/>) fuse as sources, played for free (DnaDigivolve).</summary>
    public static ICardEffect BlastDNADigivolveEffect(CardSource card, IReadOnlyList<BlastDNACondition> blastDNAConditions, Func<bool>? condition)
    {
        var materials = (blastDNAConditions ?? Array.Empty<BlastDNACondition>())
            .Select(c => new SpecialPlayMaterial(c.Matches, c.Label)).ToArray();
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, materials, MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) <c>AddJogressConditionClass</c> equivalent — declares this card's Jogress (DNA
    /// digivolve) recipe: the two material names that fuse under it (SpecialPlayKind.DnaDigivolve). Translate
    /// the AS-IS <c>GetJogress</c> callback's material names into <paramref name="names"/>.</summary>
    public static ICardEffect JogressEffectFromNames(CardSource card, Func<bool>? condition, params string[] names)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, NameMaterials(names), MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) Jogress with ARBITRARY per-material predicates (faithful form of
    /// <c>AddJogressConditionClass</c>'s <c>GetJogress</c>).</summary>
    public static ICardEffect JogressEffect(CardSource card, Func<bool>? condition, params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, materials, MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(AD1-J) 1:1 mirror of the AS-IS <c>GetJogressConditionClass(permanentCondition1, description1,
    /// permanentCondition2, description2, card, cost, canUseCondition)</c> (CardEffectFactory.cs:752): the
    /// PREDICATE form of DNA digivolution — each material slot is an arbitrary Permanent predicate, evaluated
    /// against the owner's battle-area Digimon (the AS-IS <c>AddJogressConditionClass</c> wraps each with
    /// <c>IsPermanentExistsOnOwnerBattleAreaDigimon</c>). Lowers to <see cref="JogressEffect"/>.
    /// <paramref name="cost"/> is accepted for source-signature fidelity and IGNORED — the original drops it
    /// before <c>GetJogressConditions</c> (whose cost defaults to 0), so the predicate form is always cost 0.</summary>
    public static ICardEffect GetJogressConditionClass(
        Func<Permanent, bool> permanentCondition1, string description1,
        Func<Permanent, bool> permanentCondition2, string description2,
        CardSource card, int cost = 0, Func<bool>? canUseCondition = null)
    {
        ArgumentNullException.ThrowIfNull(permanentCondition1);
        ArgumentNullException.ThrowIfNull(permanentCondition2);
        ArgumentNullException.ThrowIfNull(card);
        _ = cost; // AS-IS quirk: the parameter is not forwarded (always cost 0).
        return JogressEffect(
            card, canUseCondition,
            AsMaterial(permanentCondition1, description1),
            AsMaterial(permanentCondition2, description2));

        SpecialPlayMaterial AsMaterial(Func<Permanent, bool> permanentCondition, string description) =>
            new(cs => cs.IsDigimon && cs.Owner == card.Owner
                    && permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)),
                description);
    }

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching Digimon and give each
    /// +<paramref name="changeValue"/> DP for <paramref name="duration"/>" effect (e.g. ST1_13 [Main]).</summary>
    public static ICardEffect SelectAndBuffDpEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.DpDeltaKey, changeValue, duration, description);

    /// <summary>An activated "all your Digimon gain +<paramref name="changeValue"/> Security Attack for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST1_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description);

    /// <summary>An activated "all your Security Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect, scoped to the owner's Security-zone Digimon
    /// (e.g. ST1_14).</summary>
    public static ICardEffect PlayerScopeBuffSecurityDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopeZone: "Security");

    /// <summary>An activated "select up to <paramref name="maxCount"/> opponent Digimon and trash
    /// <paramref name="trashCount"/> of each host's digivolution cards from the bottom/top" effect
    /// (e.g. ST2_03 / ST2_06 / ST2_09).</summary>
    public static ICardEffect SelectAndTrashDigivolutionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int trashCount, bool fromBottom, string description) =>
        new ActivatedSelectTrashDigivolutionEffect(card, canTarget, maxCount, trashCount, fromBottom, description);

    /// <summary>(PRIM special-play) AS-IS <c>IDigiBurst</c> — <c>[Digi-Burst N] &lt;effect&gt;</c>: trash N of this
    /// card's own digivolution sources as a cost, then resolve <paramref name="innerEffect"/>. Offered only when
    /// the permanent holds &gt;= N sources. Wrap the card's Digi-Burst body as the inner effect.</summary>
    public static ICardEffect DigiBurstEffect(CardSource card, int count, ICardEffect innerEffect, string description) =>
        new DigiBurstActivatedEffect(card, count, innerEffect, description);

    /// <summary>A triggered "[When ...] unsuspend this Digimon" effect (e.g. ST2_11). Pass
    /// <paramref name="maxCountPerTurn"/> = 1 (+ <paramref name="hash"/> for the original SetHashString) to
    /// mirror a [Once Per Turn] limit — enforced by the live trigger loop via <c>OnceFlagController</c>.</summary>
    public static ICardEffect UnsuspendSelfTriggerEffect(EffectTiming timing, CardSource card, string description, int? maxCountPerTurn = null, string? hash = null) =>
        new TriggeredUnsuspendSelfEffect(card, timing, description, maxCountPerTurn, hash);

    /// <summary>An activated "gain/lose <paramref name="amount"/> memory" skill (Option [Main] / [Security],
    /// e.g. ST2_13).</summary>
    public static ICardEffect GainMemoryActivatedEffect(CardSource card, int amount, string description) =>
        new ActivatedMemoryEffect(card, amount, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and return each to its owner's
    /// hand" effect (Option [Main] bounce, e.g. ST2_16).</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Bounce, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and make each unable to attack
    /// and/or block for <paramref name="duration"/>" effect (e.g. ST2_14).</summary>
    public static ICardEffect SelectAndRestrictEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, EffectDuration duration, bool cannotAttack, bool cannotBlock, string description) =>
        new ActivatedTargetRestrictionEffect(card, canTarget, maxCount, duration, cannotAttack, cannotBlock, description);

    /// <summary>A triggered "[When ...] this Digimon gets +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" effect (e.g. ST3_01).</summary>
    public static ICardEffect SelfDpBuffTriggerEffect(
        EffectTiming timing, int changeValue, EffectDuration duration, CardSource card, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null) =>
        new TriggeredSelfDpBuffEffect(card, timing, changeValue, duration, condition, description, triggerGate, maxCountPerTurn, hash);

    /// <summary>A triggered "[When ...] &lt;Recovery +<paramref name="amount"/> (Deck)&gt;" effect (e.g. ST3_09).</summary>
    public static ICardEffect RecoveryTriggerEffect(EffectTiming timing, int amount, CardSource card, Func<bool>? condition, string description) =>
        new RecoverTriggerEffect(card, timing, amount, condition, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and give each
    /// +<paramref name="changeValue"/> Security Attack for <paramref name="duration"/>" effect (e.g. ST3_15 [Main]).</summary>
    public static ICardEffect SelectAndBuffSAttackEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, description);

    /// <summary>An activated "all your Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST3_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description);

    /// <summary>An activated "all of your opponent's Digimon get +<paramref name="changeValue"/> Security
    /// Attack for <paramref name="duration"/>" player-scope effect, scoped to <paramref name="opponentId"/>
    /// (e.g. ST3_15 [Security] "all opponent Digimon gain Security Attack -1").</summary>
    public static ICardEffect OpponentScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, HeadlessPlayerId opponentId, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopePlayerId: opponentId);

    /// <summary>Original: <c>ChangeSecurityDigimonCardDPStaticEffect</c> — continuous ±DP on the owner's
    /// Security-zone Digimon matching <paramref name="cardCondition"/> (evaluated 1:1). The condition decides the
    /// affected set INCLUDING the player — e.g. ST3_12 "your Security Digimon get +2000 DP" targets the owner,
    /// while BT9_084/LM_040 "your opponent's Security Digimon get -DP" target the enemy — so scope is any-player
    /// and the predicate (not a hardcoded owner scope) selects.</summary>
    public static ICardEffect ChangeSecurityDigimonCardDPStaticEffect(
        Func<CardSource, bool> cardCondition,
        int changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool>? condition,
        string? effectName = null) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopeZone: "Security", scopePredicate: cardCondition, scopeAnyPlayer: true);
}

/// <summary>
/// Headless mirror of the original <c>CardEffectCommons</c> condition predicates used inside card
/// <c>condition</c> lambdas. Each reads live state from the <see cref="CardSource"/>'s engine context.
/// </summary>
public static class CardEffectCommons
{
    /// <summary>(AD1-G) 1:1 mirror of AS-IS <c>CardEffectCommons.GainCanNotBeDeletedByBattle</c>
    /// (GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11-54): grant the TARGET permanent a
    /// timed battle-deletion immunity. Registers a duration-tagged, card-TARGETED restriction binding
    /// (consumed by <see cref="BattleDeletionGate"/>): the flag + the caller's 4-arg battle predicate stored
    /// verbatim + a LIVE condition (target still on the battle area — the AS-IS <c>CanUseCondition</c>).
    /// The AS-IS grant-time <c>CanNotBeAffected</c> guard is mirrored: an immune target refuses the grant.
    /// Synchronous (all ported Gain-commons are; the AS-IS coroutine only drove UI). Returns true when the
    /// grant registered.</summary>
    public static bool GainCanNotBeDeletedByBattle(
        Permanent targetPermanent,
        Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition,
        EffectDuration effectDuration,
        CardSource sourceCard,
        string effectName)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null)
        {
            return false;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId targetId = targetPermanent.InstanceId;
        var zones = (IZoneStateReader)context.ZoneMover;
        if (targetId.IsEmpty || !zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BattleArea).Contains(targetId))
        {
            return false;   // AS-IS: IsPermanentExistsOnBattleArea guard.
        }

        // AS-IS grant-time + live CanUse guard: !target.CanNotBeAffected(activateClass).
        if (ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, sourceCard.InstanceId, context))
        {
            return false;
        }

        HeadlessPlayerId targetOwner = targetPermanent.OwnerId;
        HeadlessEntityId grantSourceId = sourceCard.InstanceId;
        Func<bool> liveCondition = () =>
            ((IZoneStateReader)context.ZoneMover).GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId)
            // AS-IS CanUseCondition re-checks !CanNotBeAffected LIVE.
            && !ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, grantSourceId, context);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BattleDeletionGate.PreventBattleDeletionKey] = true,
            [ContinuousSelfModifierEffect.ConditionKey] = liveCondition,
        };
        if (canNotBeDestroyedByBattleCondition is not null)
        {
            values[BattleDeletionGate.BattleConditionKey] = canNotBeDestroyedByBattleCondition;
        }

        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: new[] { targetId }, values: values);
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:gainCanNotBeDeletedByBattle:{targetId.Value}:{effectName}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope },
            effect: null, duration: effectDuration));
        return true;
    }

    // ===== (W6-S) "...AndProcessAccordingToResult" commons — 1:1 mirrors of CardEffectCommons.cs:437-644 =====
    // AS-IS shape: run the action via its I-class, then branch on whether it ACTUALLY happened (success =
    // real occurrence, not the attempt). The Delete form runs the FULL deletion pipeline — a target's
    // would-be-deleted replacement may respond across a game-loop pause, so the continuation parks on the
    // DeletionOutcomeWatcher context service (W6-S; the P6 parking generalised). The non-delete siblings
    // settle synchronously in the port. Original spelling ("Peremanent") kept for name parity.

    /// <summary>AS-IS <c>DeletePeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:463-483).
    /// Success = at least one target ACTUALLY left the field (AS-IS <c>DestroyedPermanents</c> membership);
    /// <paramref name="successProcess"/> receives the destroyed permanents. Deferred targets (a would-be-
    /// deleted window opened) park the continuation until every target settles.</summary>
    public static async Task DeletePeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents,
        CardSource sourceCard,
        Func<IReadOnlyList<Permanent>, Task>? successProcess,
        Func<Task>? failureProcess,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;

        var targets = targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty).Select(p => p.InstanceId).ToList();
        if (targets.Count == 0)
        {
            if (failureProcess is not null)
            {
                await failureProcess().ConfigureAwait(false);
            }

            return;
        }

        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
            context.EffectRegistry, context.GameEventQueue, context: context);
        foreach (HeadlessEntityId target in targets)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DeleteKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        DeletionOutcomeWatcher watcher = GetOutcomeWatcher(context);
        watcher.Register(targets, async (destroyed, spared) =>
        {
            if (destroyed.Count > 0)
            {
                if (successProcess is not null)
                {
                    IReadOnlyList<Permanent> views = destroyed
                        .Select(id => new Permanent(context, id, OwnerOfInstance(context, id)))
                        .ToArray();
                    await successProcess(views).ConfigureAwait(false);
                }
            }
            else if (failureProcess is not null)
            {
                await failureProcess().ConfigureAwait(false);
            }
        });

        // Settle immediately when nothing deferred (the common case: no replacement windows opened).
        await watcher.SettleAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>SuspendPeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:437):
    /// suspend the targets, then branch on whether any ACTUALLY became suspended.</summary>
    public static async Task SuspendPeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents,
        CardSource sourceCard,
        Func<IReadOnlyList<Permanent>, Task>? successProcess,
        Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;

        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
            context.EffectRegistry, context.GameEventQueue, context: context);
        foreach (Permanent target in targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty))
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.SuspendKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        IReadOnlyList<Permanent> suspended = targetPermanents
            .Where(p => p is not null && !p.InstanceId.IsEmpty && IsSuspended(p.TopCard, p.InstanceId))
            .ToArray();
        if (suspended.Count > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(suspended).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>BouncePeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:489):
    /// return the targets to hand, then branch on whether any ACTUALLY left the field.</summary>
    public static async Task BouncePeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents,
        CardSource sourceCard,
        Func<Task>? successProcess,
        Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;

        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
            context.EffectRegistry, context.GameEventQueue, context: context);
        foreach (Permanent target in targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty))
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToHandKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        var zones = (IZoneStateReader)context.ZoneMover;
        bool bounced = targetPermanents.Any(p => p is not null && !p.InstanceId.IsEmpty
            && !zones.GetCards(p.OwnerId, ChoiceZone.BattleArea).Contains(p.InstanceId));
        if (bounced)
        {
            if (successProcess is not null)
            {
                await successProcess().ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>DeckBouncePeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:515):
    /// return the targets to the deck bottom; success = any actually left the field.</summary>
    public static async Task DeckBouncePeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard,
        Func<Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var sink = NewSink(context);
        foreach (Permanent target in targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty))
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToDeckBottomKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);
        var zones = (IZoneStateReader)context.ZoneMover;
        bool bounced = targetPermanents.Any(p => p is not null && !p.InstanceId.IsEmpty
            && !zones.GetCards(p.OwnerId, ChoiceZone.BattleArea).Contains(p.InstanceId));
        await Branch(bounced, successProcess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>TrashDigivolutionCardsAndProcessAccordingToResult</c> (CardEffectCommons.cs:541):
    /// trash <paramref name="trashCount"/> digivolution sources; success = any actually trashed.</summary>
    public static async Task TrashDigivolutionCardsAndProcessAccordingToResult(
        Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard,
        Func<int, Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        int trashed = targetPermanent is null || targetPermanent.InstanceId.IsEmpty
            ? 0
            : await Headless.Runtime.DigivolutionStackHelpers.TrashSourcesAsync(
                sourceCard.Context.CardInstanceRepository, sourceCard.Context.ZoneMover,
                targetPermanent.InstanceId, trashCount, fromBottom: !isFromTop,
                gameEventQueue: sourceCard.Context.GameEventQueue).ConfigureAwait(false);
        if (trashed > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(trashed).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>TrashDigivolutionCardsFromTopOrBottom</c> (GiveEffect …, 121 card files): the plain
    /// trash (no success branch).</summary>
    public static Task<int> TrashDigivolutionCardsFromTopOrBottom(
        Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard) =>
        targetPermanent is null || targetPermanent.InstanceId.IsEmpty
            ? Task.FromResult(0)
            : Headless.Runtime.DigivolutionStackHelpers.TrashSourcesAsync(
                sourceCard.Context.CardInstanceRepository, sourceCard.Context.ZoneMover,
                targetPermanent.InstanceId, trashCount, fromBottom: !isFromTop,
                gameEventQueue: sourceCard.Context.GameEventQueue);

    /// <summary>AS-IS <c>TrashLinkCardsAndProcessAccordingToResult</c> (CardEffectCommons.cs:567): trash the
    /// given link cards off their host; success = any actually trashed.</summary>
    public static async Task TrashLinkCardsAndProcessAccordingToResult(
        Permanent? hostPermanent, IReadOnlyList<HeadlessEntityId> linkCardIds, CardSource sourceCard,
        Func<int, Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(linkCardIds);
        ArgumentNullException.ThrowIfNull(sourceCard);
        int trashed = 0;
        if (hostPermanent is not null && !hostPermanent.InstanceId.IsEmpty)
        {
            foreach (HeadlessEntityId linkCard in linkCardIds)
            {
                if (await Headless.Runtime.LinkHelpers.RemoveLinkCardAsync(
                        sourceCard.Context.CardInstanceRepository, sourceCard.Context.ZoneMover,
                        hostPermanent.InstanceId, linkCard).ConfigureAwait(false))
                {
                    trashed++;
                }
            }
        }

        if (trashed > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(trashed).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>TrashSecurityAndProcessAccordingToResult</c> (CardEffectCommons.cs:593): trash
    /// <paramref name="trashAmount"/> of <paramref name="player"/>'s security (top/bottom); success = any
    /// actually trashed.</summary>
    public static async Task TrashSecurityAndProcessAccordingToResult(
        HeadlessPlayerId player, int trashAmount, bool fromTop, CardSource sourceCard,
        Func<int, Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        int before = zones.GetCards(player, ChoiceZone.Security).Count;
        var sink = NewSink(context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.TrashSecurityKind, sourceCard.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = player.Value,
                [MatchStateMutationSink.CountKey] = trashAmount,
                [MatchStateMutationSink.FromTopKey] = fromTop,
            }));
        await sink.FlushAsync().ConfigureAwait(false);
        int trashed = before - zones.GetCards(player, ChoiceZone.Security).Count;
        if (trashed > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(trashed).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>TrashHandAndProcessAccordingToResult</c> (CardEffectCommons.cs:619): discard a
    /// specific hand card; success = it actually reached the trash.</summary>
    public static async Task TrashHandAndProcessAccordingToResult(
        CardSource? handCard, CardSource sourceCard,
        Func<Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        bool discarded = false;
        if (handCard is not null && !handCard.InstanceId.IsEmpty &&
            zones.GetCards(handCard.Owner, ChoiceZone.Hand).Contains(handCard.InstanceId))
        {
            var sink = NewSink(context);
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashCardKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = handCard.InstanceId.Value }));
            await sink.FlushAsync().ConfigureAwait(false);
            discarded = zones.GetCards(handCard.Owner, ChoiceZone.Trash).Contains(handCard.InstanceId);
        }

        await Branch(discarded, successProcess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>PlacePermanentInSecurityAndProcessAccordingToResult</c> (CardEffectCommons.cs:644):
    /// put the target permanent's TOP CARD into its owner's security (top/bottom); success = actually
    /// placed. (The AS-IS CanAddSecurity guard folds here when the CannotAddSecurity restriction lands —
    /// fidelity_debt K2 latent.)</summary>
    public static async Task PlacePermanentInSecurityAndProcessAccordingToResult(
        Permanent? targetPermanent, bool toTop, CardSource sourceCard,
        Func<CardSource, Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        bool placed = false;
        CardSource? placedTop = null;
        if (targetPermanent is not null && !targetPermanent.InstanceId.IsEmpty &&
            zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BattleArea).Contains(targetPermanent.InstanceId))
        {
            HeadlessEntityId topId = targetPermanent.InstanceId;
            await context.ZoneMover.AddToSecurityAsync(targetPermanent.OwnerId, topId, faceUp: false, toTop: toTop).ConfigureAwait(false);
            placed = zones.GetCards(targetPermanent.OwnerId, ChoiceZone.Security).Contains(topId);
            placedTop = new CardSource(context, topId, targetPermanent.OwnerId, targetPermanent.OwnerId);
        }

        if (placed && placedTop is not null)
        {
            if (successProcess is not null)
            {
                await successProcess(placedTop).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>(W6-D) AS-IS <c>PlaceDelayOptionCards</c> (CardEffectCommons.cs:113-134): play the [Delay]
    /// Option COST-FREE as a face-up permanent on the owner's battle area (gated by
    /// <see cref="CanPlayAsNewPermanent"/> with <c>isPlayOption:true</c>), then tag
    /// <c>IsPlayedOptionPermanent</c> — the tag alone exempts it from the "Option with no DP → trash" rule
    /// (P7 models that exemption). The [Delay] ability itself is an ordinary OnDeclaration activated skill
    /// gated by <see cref="CanDeclareOptionDelayEffect"/>; its resolution typically self-deletes via
    /// <see cref="DeletePeremanentAndProcessAccordingToResult"/>. Returns true when placed.</summary>
    public static async Task<bool> PlaceDelayOptionCards(CardSource card, ICardEffect? cardEffect = null, ChoiceZone root = ChoiceZone.Execution)
    {
        ArgumentNullException.ThrowIfNull(card);
        _ = cardEffect;
        if (!CanPlayAsNewPermanent(card, payCost: false, null, isPlayOption: true))
        {
            return false;
        }

        EngineContext context = card.Context;
        var sink = NewSink(context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind, card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value,
                [MatchStateMutationSink.FromZoneKey] = root,
            }));
        await sink.FlushAsync().ConfigureAwait(false);

        var zones = (IZoneStateReader)context.ZoneMover;
        if (!zones.GetCards(card.Owner, ChoiceZone.BattleArea).Contains(card.InstanceId))
        {
            return false;
        }

        if (context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) && record is not null)
        {
            context.CardInstanceRepository.Upsert(record with
            {
                Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [Headless.Runtime.GameFlowProcessor.IsPlayedOptionPermanentKey] = true,
                }
            });
        }

        return true;
    }

    private static MatchStateMutationSink NewSink(EngineContext context) =>
        new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
            context.EffectRegistry, context.GameEventQueue, context: context);

    private static async Task Branch(bool success, Func<Task>? successProcess, Func<Task>? failureProcess)
    {
        if (success)
        {
            if (successProcess is not null)
            {
                await successProcess().ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    private static DeletionOutcomeWatcher GetOutcomeWatcher(EngineContext context)
    {
        if (context.TryGetService(out DeletionOutcomeWatcher? watcher) && watcher is not null)
        {
            return watcher;
        }

        var created = new DeletionOutcomeWatcher();
        context.RegisterService(created);
        return created;
    }

    private static HeadlessPlayerId OwnerOfInstance(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : default;

    // ===== (W6-T) trigger-gate commons batch — 1:1 mirrors of CanUseEffects/*.cs =====
    // The AS-IS gates read the driving Hashtable; the port mirror reads the enriched resolve context
    // (subject = TriggerEntityId; the event's primitive metadata under "event.<key>" —
    // GameFlowProcessor.EnrichWithEventSubject, W6-T). Verbatim AS-IS bodies verified
    // (primitive_w6_design.md W6-T). Translation: `CanActivateCondition(Hashtable h)` bodies become
    // `triggerGate: ctx => CardEffectCommons.CanTriggerX(ctx, card, ...) && ...` with the same names.

    /// <summary>AS-IS <c>CanTriggerOnPlay</c> (CanUseEffects/PermanentEnterField/OnPlay.cs:11): the entered
    /// permanent CONTAINS this card and the entry was a PLAY (not a digivolve).
    /// <paramref name="rootCondition"/> = the AS-IS Root filter over the source zone (headless: the
    /// event's from-zone).</summary>
    public static bool CanTriggerOnPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null) =>
        !EventIsDigivolve(ctx) && SubjectPermanentContains(ctx, card) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerWhenDigivolving</c> (.../WhenDigivolving.cs:10).</summary>
    public static bool CanTriggerWhenDigivolving(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null) =>
        EventIsDigivolve(ctx) && SubjectPermanentContains(ctx, card) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerOnPermanentPlay</c> (.../OnPlay.cs:18) — arbitrary predicate over the
    /// ENTERED permanent.</summary>
    public static bool CanTriggerOnPermanentPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<ChoiceZone, bool>? rootCondition = null) =>
        !EventIsDigivolve(ctx) && SubjectPermanentPasses(ctx, card, permanentCondition) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentDigivolving</c> (.../WhenDigivolving.cs:17).</summary>
    public static bool CanTriggerWhenPermanentDigivolving(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<ChoiceZone, bool>? rootCondition = null) =>
        EventIsDigivolve(ctx) && SubjectPermanentPasses(ctx, card, permanentCondition) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerOnAttack</c> (CanUseEffects/OnAttack.cs:10): the ATTACKING permanent
    /// contains this card.</summary>
    public static bool CanTriggerOnAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOnPermanentAttack</c> (.../OnAttack.cs:17).</summary>
    public static bool CanTriggerOnPermanentAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        SubjectPermanentPasses(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerOnEndAttack</c> (.../OnEndAttack.cs:10) — delegates to the attack gate.</summary>
    public static bool CanTriggerOnEndAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerOnAttack(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOptionMainEffect</c> (CanUseEffects/OptionEffect.cs:10): the resolving
    /// card IS this card.</summary>
    public static bool CanTriggerOptionMainEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && subject == card.InstanceId;

    /// <summary>AS-IS <c>CanTriggerSecurityEffect</c> (CanUseEffects/SecurityEffect.cs:10) — delegates to
    /// the option-main gate.</summary>
    public static bool CanTriggerSecurityEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerOptionMainEffect(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOnDeletion</c> (CanUseEffects/OnDeletion.cs:13): the deleted permanent
    /// contained this card.</summary>
    public static bool CanTriggerOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanActivateOnDeletion</c> (CanUseEffects/OnDeletion.cs:113): a token activates
    /// unconditionally; otherwise the permanent this card belonged to just before leaving the field is the
    /// deletion subject AND its top card is in the trash (a true deletion — a bounce fails this).</summary>
    public static bool CanActivateOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (card.IsToken)
        {
            return true;
        }

        if (!SubjectPermanentContains(ctx, card) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject)
        {
            return false;
        }

        // AS-IS: return IsExistOnTrash(TopCard) — the deleted permanent's top card actually reached the trash.
        HeadlessPlayerId owner = card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null
            ? dead.OwnerId
            : default;
        return !owner.IsEmpty
            && ((IZoneStateReader)card.Context.ZoneMover).GetCards(owner, ChoiceZone.Trash).Contains(subject);
    }

    /// <summary>AS-IS <c>CanTriggerWhenLoseSecurity</c> (CanUseEffects/WhenLoseSecurity.cs:10): the
    /// security-losing PLAYER (headless: the moved security card's owner = the event subject's owner)
    /// passes <paramref name="playerCondition"/>.</summary>
    public static bool CanTriggerWhenLoseSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<HeadlessPlayerId, bool>? playerCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty ||
            !card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        return playerCondition is null || playerCondition(record.OwnerId);
    }

    /// <summary>AS-IS <c>CanTriggerWhenRemoveField</c> (CanUseEffects/WhenRemoveField.cs:11).</summary>
    public static bool CanTriggerWhenRemoveField(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentRemoveField</c> (.../WhenRemoveField.cs:19).</summary>
    public static bool CanTriggerWhenPermanentRemoveField(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        SubjectPermanentPasses(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentSuspends</c> (CanUseEffects/OnSuspend.cs:17).</summary>
    public static bool CanTriggerWhenPermanentSuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        SubjectPermanentPasses(ctx, card, permanentCondition, requireOnBattleArea: true);

    /// <summary>AS-IS <c>IsByEffect</c> (CanUseEffects/OnDeletion.cs:89): the deletion was caused by an
    /// EFFECT (the AS-IS hashtable carried a CardEffect) — headless the dead card's metadata carries the
    /// <c>deletedByEffect</c> flag + the causing source card id; the AS-IS
    /// <c>Func&lt;ICardEffect,bool&gt;</c> condition maps to a predicate over that source card.</summary>
    public static bool IsByEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty ||
            !card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) || dead is null ||
            !dead.Metadata.TryGetValue(MatchStateMutationSink.DeletedByEffectKey, out object? raw) || raw is not true)
        {
            return false;
        }

        if (cardEffectSourceCondition is null)
        {
            return true;
        }

        if (!dead.Metadata.TryGetValue(MatchStateMutationSink.DeletedBySourceEntityIdKey, out object? rawSource) ||
            rawSource?.ToString() is not { Length: > 0 } sourceValue)
        {
            return false;
        }

        var sourceId = new HeadlessEntityId(sourceValue);
        HeadlessPlayerId sourceOwner = card.Context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
            ? src.OwnerId
            : default;
        return cardEffectSourceCondition(new CardSource(card.Context, sourceId, sourceOwner, sourceOwner));
    }

    /// <summary>AS-IS <c>IsJogress</c> (GetFromHashtable.cs:782): the driving event carried the DNA flag.</summary>
    public static bool IsJogress(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isJogress", out object? raw) && raw is true;

    /// <summary>AS-IS <c>CanTriggerWhenPermanentWouldPlay</c> (CanUseEffects/WhenPermanentWouldPlay.cs:11):
    /// a card is about to be PLAYED (not digivolved) — headless the BeforePayCost window (the EX8_074
    /// "would be played" seam), the event carrying <c>isEvolution:false</c>.</summary>
    public static bool CanTriggerWhenPermanentWouldPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        if (EventIsDigivolve(ctx) || ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        return cardCondition is null
            || cardCondition(new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject)));
    }

    /// <summary>AS-IS <c>CanTriggerWhenPermanentWouldDigivolve</c> (…/WhenPermanentWouldDigivolve.cs:23):
    /// a card is about to DIGIVOLVE — the event carries <c>isEvolution:true</c> + the target permanent.</summary>
    public static bool CanTriggerWhenPermanentWouldDigivolve(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? cardCondition = null)
    {
        if (!EventIsDigivolve(ctx) || ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (cardCondition is not null &&
            !cardCondition(new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject))))
        {
            return false;
        }

        if (permanentCondition is null)
        {
            return true;
        }

        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}targetCardId", out object? raw) ||
            raw?.ToString() is not { Length: > 0 } targetValue)
        {
            return false;
        }

        var targetId = new HeadlessEntityId(targetValue);
        return permanentCondition(new Permanent(card.Context, targetId, OwnerOfId(card.Context, targetId)));
    }

    /// <summary>AS-IS <c>CanTriggerWhenLinked</c> (CanUseEffects/WhenLinked.cs:45): a link attached — the
    /// HOST passes <paramref name="permanentCondition"/> and the LINK CARD passes
    /// <paramref name="sourceCondition"/>.</summary>
    public static bool CanTriggerWhenLinked(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? sourceCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(card.Context, host, OwnerOfId(card.Context, host))))
        {
            return false;
        }

        if (sourceCondition is null)
        {
            return true;
        }

        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}linkCardId", out object? raw) ||
            raw?.ToString() is not { Length: > 0 } linkValue)
        {
            return false;
        }

        var linkId = new HeadlessEntityId(linkValue);
        return sourceCondition(new CardSource(card.Context, linkId, OwnerOfId(card.Context, linkId), OwnerOfId(card.Context, linkId)));
    }

    /// <summary>AS-IS <c>CanTriggerOnAddDigivolutionCard</c> (CanUseEffects/OnAddDigivolutionCards.cs:10):
    /// digivolution sources were added — the receiving permanent, the causing effect's source, and at
    /// least one added card pass their predicates.</summary>
    public static bool CanTriggerOnAddDigivolutionCard(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition = null,
        Func<CardSource, bool>? cardEffectSourceCondition = null,
        Func<CardSource, bool>? cardCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        EngineContext context = card.Context;
        if (permanentCondition is not null && !permanentCondition(new Permanent(context, host, OwnerOfId(context, host))))
        {
            return false;
        }

        if (cardEffectSourceCondition is not null &&
            !cardEffectSourceCondition(new CardSource(context, ctx.EffectContext.SourceEntityId, OwnerOfId(context, ctx.EffectContext.SourceEntityId), OwnerOfId(context, ctx.EffectContext.SourceEntityId))))
        {
            return false;
        }

        if (cardCondition is null)
        {
            return true;
        }

        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}addedCardIds", out object? raw) ||
            raw?.ToString() is not { Length: > 0 } addedValue)
        {
            return false;
        }

        return addedValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(id => cardCondition(new CardSource(context, new HeadlessEntityId(id), OwnerOfId(context, new HeadlessEntityId(id)), OwnerOfId(context, new HeadlessEntityId(id)))));
    }

    /// <summary>AS-IS <c>CanTriggerOnMove</c> (the OnMove promotion window — CV-A4): the moved permanent
    /// passes the predicate.</summary>
    public static bool CanTriggerOnMove(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null) =>
        SubjectPermanentPasses(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>IsByBattle</c>: the deletion driving this window came from a BATTLE — headless the
    /// dead card carries the <c>deletedByBattle</c> marker (BattleResolver).</summary>
    public static bool IsByBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        return ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty &&
            card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null &&
            dead.Metadata.TryGetValue(BattleResolver.DeletedByBattleKey, out object? raw) && raw is true;
    }

    // --- (W6-T) hashtable-accessor mirrors — the event subject IS the AS-IS hashtable payload ---------

    /// <summary>AS-IS <c>GetPermanentFromHashtable</c> (GetFromHashtable.cs:700): the event subject as a
    /// Permanent view.</summary>
    public static Permanent? GetPermanentFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new Permanent(card.Context, subject, OwnerOfId(card.Context, subject))
            : null;

    /// <summary>AS-IS <c>GetPermanentsFromHashtable</c> (:500) — headless events carry ONE subject per
    /// firing (broadcast timings fire per permanent), so the list has at most one element.</summary>
    public static List<Permanent> GetPermanentsFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        GetPermanentFromHashtable(ctx, card) is Permanent p ? new List<Permanent> { p } : new List<Permanent>();

    /// <summary>AS-IS <c>GetCardFromHashtable</c> (:316): the event subject as a CardSource.</summary>
    public static CardSource? GetCardFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject))
            : null;

    /// <summary>AS-IS <c>GetPlayedPermanentsFromEnterFieldHashtable</c> (:234): the entered permanent(s)
    /// whose play ROOT (headless: the event's from-zone) passes the filter.</summary>
    public static List<Permanent> GetPlayedPermanentsFromEnterFieldHashtable(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null) =>
        EventRootPasses(ctx, rootCondition) ? GetPermanentsFromHashtable(ctx, card) : new List<Permanent>();

    private static HeadlessPlayerId OwnerOfId(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : default;

    /// <summary>AS-IS <c>CanTriggerWhenDeleteOpponentDigimonByBattle</c>
    /// (CanUseEffects/WhenDeleteOpponentDigimonByBattle.cs:10, verbatim verified): reads the battle result
    /// (winners / losers / actually-destroyed) — headless the OnEndBattle event carries them
    /// (winnerIds/loserIds/loserRealIds). Headless winners are the SURVIVORS, so the AS-IS *_real
    /// distinction collapses (replacement survivors never enter the deleted set) — documented reduction.</summary>
    public static bool CanTriggerWhenDeleteOpponentDigimonByBattle(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? winnerCondition,
        Func<Permanent, bool>? loserCondition,
        bool isOnlyWinnerSurvive,
        Func<Permanent, bool>? winnerRealCondition = null,
        Func<Permanent, bool>? loserRealCondition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        EngineContext context = card.Context;
        IReadOnlyList<Permanent> winners = EventPermanents(ctx, context, "winnerIds");
        IReadOnlyList<Permanent> losers = EventPermanents(ctx, context, "loserIds");
        IReadOnlyList<Permanent> losersReal = EventPermanents(ctx, context, "loserRealIds");

        // AS-IS WinnerCondition(): empty winners passes only an absent condition; otherwise some winner matches.
        bool winnerOk = winners.Count == 0
            ? winnerCondition is null
            : winners.Any(p => winnerCondition is null || winnerCondition(p));
        if (!winnerOk)
        {
            return false;
        }

        // AS-IS isOnlyWinnerSurvive: no LOSER may also satisfy the winner condition.
        if (isOnlyWinnerSurvive && winnerCondition is not null &&
            losers.Any(p => winnerCondition(p)))
        {
            return false;
        }

        if (loserCondition is not null && !losers.Any(p => loserCondition(p)))
        {
            return false;
        }

        if (loserRealCondition is not null && !losersReal.Any(p => loserRealCondition(p)))
        {
            return false;
        }

        if (winnerRealCondition is not null && !winners.Any(p => winnerRealCondition(p)))
        {
            return false;
        }

        return true;
    }

    /// <summary>AS-IS <c>CanTriggerWhenWinBattle</c>: this card's permanent is among the battle's winners.</summary>
    public static bool CanTriggerWhenWinBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        EventPermanents(ctx, card.Context, "winnerIds").Any(p =>
            p.InstanceId == card.InstanceId ||
            (card.Context.CardInstanceRepository.TryGetInstance(p.InstanceId, out CardInstanceRecord? rec) && rec is not null
                && rec.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
                && raw is IEnumerable<string> ids && ids.Contains(card.InstanceId.Value)));

    private static IReadOnlyList<Permanent> EventPermanents(Headless.Effects.CardEffectResolveContext ctx, EngineContext context, string key)
    {
        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{key}", out object? raw) ||
            raw?.ToString() is not { Length: > 0 } value)
        {
            return Array.Empty<Permanent>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => new HeadlessEntityId(id))
            .Select(id => new Permanent(context, id, OwnerOfId(context, id)))
            .ToArray();
    }

    /// <summary>AS-IS <c>CanTriggerWhenLinking</c> (WhenLinked.cs:10): a WOULD-LINK window where the LINK
    /// card is this card and the HOST passes the predicate.</summary>
    public static bool CanTriggerWhenLinking(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(card.Context, host, OwnerOfId(card.Context, host))))
        {
            return false;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}linkCardId", out object? raw)
            && raw?.ToString() == card.InstanceId.Value;
    }

    /// <summary>AS-IS <c>CanTriggerWhenWouldLink</c> (WhenWouldLink.cs:11).</summary>
    public static bool CanTriggerWhenWouldLink(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<CardSource, bool>? cardCondition = null, Func<Permanent, bool>? permanentCondition = null,
        Func<ChoiceZone, bool>? rootCondition = null, Func<CardSource, bool>? cardEffectSourceCondition = null)
    {
        EngineContext context = card.Context;
        HeadlessEntityId linkId = ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}linkCardId", out object? rawLink)
            && rawLink?.ToString() is { Length: > 0 } linkValue
            ? new HeadlessEntityId(linkValue)
            : ctx.EffectContext.TriggerEntityId ?? default;
        if (linkId.IsEmpty)
        {
            return false;
        }

        if (cardCondition is not null && !cardCondition(new CardSource(context, linkId, OwnerOfId(context, linkId), OwnerOfId(context, linkId))))
        {
            return false;
        }

        if (permanentCondition is not null &&
            (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host ||
             !permanentCondition(new Permanent(context, host, OwnerOfId(context, host)))))
        {
            return false;
        }

        if (!EventRootPasses(ctx, rootCondition))
        {
            return false;
        }

        return cardEffectSourceCondition is null
            || cardEffectSourceCondition(new CardSource(context, ctx.EffectContext.SourceEntityId, OwnerOfId(context, ctx.EffectContext.SourceEntityId), OwnerOfId(context, ctx.EffectContext.SourceEntityId)));
    }

    /// <summary>AS-IS <c>CanTriggerOnTrashHand</c> (OnTrashHand.cs:17): a hand card was discarded — the
    /// causing effect's source and at least one discarded card pass their predicates.</summary>
    public static bool CanTriggerOnTrashHand(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)
    {
        if (cardEffectSourceCondition is not null &&
            !cardEffectSourceCondition(new CardSource(card.Context, ctx.EffectContext.SourceEntityId, OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId), OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId))))
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>AS-IS <c>CanTriggerOnTrashSelfHand</c> (OnTrashHand.cs:10).</summary>
    public static bool CanTriggerOnTrashSelfHand(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerOnTrashHand(ctx, card, cardEffectSourceCondition, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerOnTrashSecurity</c> / <c>CanTriggerOnTrashSelfSecurity</c>
    /// (WhenDiscardSecurity.cs) — delegate to the trash-hand shape (AS-IS does the same).</summary>
    public static bool CanTriggerOnTrashSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition) =>
        CanTriggerOnTrashHand(ctx, card, cardEffectSourceCondition, cardCondition);

    public static bool CanTriggerOnTrashSelfSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerOnTrashSecurity(ctx, card, cardEffectSourceCondition, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerWhenDiscardLibrary</c> (WhenDiscardLibrary.cs:17) — the AS-IS
    /// <c>IsBeingRevealed</c> exclusion has no headless surface (reveals never route through the discard
    /// window here).</summary>
    public static bool CanTriggerWhenDiscardLibrary(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null) =>
        EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));

    /// <summary>AS-IS <c>CanTriggerWhenSelfDiscardLibrary</c> (WhenDiscardLibrary.cs:10).</summary>
    public static bool CanTriggerWhenSelfDiscardLibrary(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerWhenDiscardLibrary(ctx, card, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerOnTrashDigivolutionCard</c> (OnTrashDigivolutionCard.cs:35).</summary>
    public static bool CanTriggerOnTrashDigivolutionCard(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(card.Context, host, OwnerOfId(card.Context, host))))
        {
            return false;
        }

        if (cardEffectSourceCondition is not null &&
            !cardEffectSourceCondition(new CardSource(card.Context, ctx.EffectContext.SourceEntityId, OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId), OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId))))
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>AS-IS <c>CanTriggerOnTrashSelfDigivolutionCard</c> (OnTrashDigivolutionCard.cs:10).</summary>
    public static bool CanTriggerOnTrashSelfDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerOnTrashDigivolutionCard(ctx, card, permanentCondition: null, cardEffectSourceCondition, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerOnTrashLinkedCard</c> (OnTrashLinkedCard.cs:35) — same shape over the
    /// link-discard window.</summary>
    public static bool CanTriggerOnTrashLinkedCard(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition) =>
        CanTriggerOnTrashDigivolutionCard(ctx, card, permanentCondition, cardEffectSourceCondition, cardCondition);

    /// <summary>AS-IS <c>CanTriggerOnTrashBySelfDigiBurst</c> (OnTrashBySelfDigiBurst.cs:10) — Digi-Burst
    /// is not a modeled headless mechanism; the source-description probe has no surface. STOP.</summary>
    public static bool CanTriggerOnTrashBySelfDigiBurst(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        throw new NotSupportedException("Digi-Burst is not modeled — STOP (strong model).");

    /// <summary>AS-IS <c>CanTriggerWhenPermanentUnsuspends</c> (OnUnsuspend.cs:17 — delegates to the
    /// suspend shape).</summary>
    public static bool CanTriggerWhenPermanentUnsuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        CanTriggerWhenPermanentSuspends(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerWhenSelfPermanentUnsuspends</c> (OnUnsuspend.cs:10).</summary>
    public static bool CanTriggerWhenSelfPermanentUnsuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerWhenSelfPermanentSuspends</c> (OnSuspend.cs:10).</summary>
    public static bool CanTriggerWhenSelfPermanentSuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOnPermanentAttackTargetSwitch</c> (OnAttackTargetSwitch.cs:17 —
    /// delegates to the attack shape).</summary>
    public static bool CanTriggerOnPermanentAttackTargetSwitch(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        CanTriggerOnPermanentAttack(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerOnAttackTargetSwitch</c> (OnAttackTargetSwitch.cs:10).</summary>
    public static bool CanTriggerOnAttackTargetSwitch(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerWhenAddHand</c> (WhenAddHand.cs:10): a player added cards to hand.</summary>
    public static bool CanTriggerWhenAddHand(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<HeadlessPlayerId, bool>? playerCondition = null, Func<CardSource, bool>? cardEffectSourceCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (playerCondition is not null && !playerCondition(OwnerOfId(card.Context, subject)))
        {
            return false;
        }

        return cardEffectSourceCondition is null
            || cardEffectSourceCondition(new CardSource(card.Context, ctx.EffectContext.SourceEntityId, OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId), OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId)));
    }

    /// <summary>AS-IS <c>CanTriggerOnHandAdded</c> (OnCardsAddedToHand.cs:10) — player-specific form.</summary>
    public static bool CanTriggerOnHandAdded(Headless.Effects.CardEffectResolveContext ctx, CardSource card, HeadlessPlayerId player, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerWhenAddHand(ctx, card, p => p == player, cardEffectSourceCondition);

    /// <summary>AS-IS <c>CanTriggerWhenAddSecurity</c> (WhendAddSecurity.cs:10) — delegates to the
    /// lose-security shape (the gaining player's condition over the moved card's owner).</summary>
    public static bool CanTriggerWhenAddSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<HeadlessPlayerId, bool>? playerCondition = null) =>
        CanTriggerWhenLoseSecurity(ctx, card, playerCondition);

    /// <summary>AS-IS <c>CanTriggerWhenUseOption</c> (WhenUseOption.cs:21): an Option was used — the card
    /// and its paid cost pass their predicates.</summary>
    public static bool CanTriggerWhenUseOption(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<CardSource, bool>? cardCondition = null, Func<int, bool>? costCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId option || option.IsEmpty)
        {
            return false;
        }

        if (cardCondition is not null &&
            !cardCondition(new CardSource(card.Context, option, OwnerOfId(card.Context, option), OwnerOfId(card.Context, option))))
        {
            return false;
        }

        if (costCondition is null)
        {
            return true;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}cost", out object? raw)
            && raw is int cost && costCondition(cost);
    }

    /// <summary>AS-IS <c>CanTriggerWhenOwnerUseOption</c> (WhenUseOption.cs:11).</summary>
    public static bool CanTriggerWhenOwnerUseOption(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null, Func<int, bool>? costCondition = null) =>
        CanTriggerWhenUseOption(ctx, card, cs => cs.Owner == card.Owner && (cardCondition is null || cardCondition(cs)), costCondition);

    /// <summary>AS-IS <c>CanTriggerWhenCardsReturnToHandFromTrash</c> (OnCardsReturnToHandFromTrash.cs:21).</summary>
    public static bool CanTriggerWhenCardsReturnToHandFromTrash(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null) =>
        EventCards(ctx, card).Any(cs => !IsDigiEggType(cs) && (cardCondition is null || cardCondition(cs)));

    /// <summary>AS-IS <c>CanTriggerWhenOwnerCardsReturnToLibraryFromTrash</c>
    /// (OnCardsReturnToLibraryFromTrash.cs:11).</summary>
    public static bool CanTriggerWhenOwnerCardsReturnToLibraryFromTrash(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null) =>
        EventCards(ctx, card).Any(cs => !IsDigiEggType(cs) && cs.Owner == card.Owner && (cardCondition is null || cardCondition(cs)));

    /// <summary>AS-IS <c>CanTriggerOnReturnToLibraryBottomDigivolutionCard</c>
    /// (OnReturnLibraryBottomDigivolutionCards.cs:10): this card's OWN permanent returned digivolution
    /// cards to the deck bottom.</summary>
    public static bool CanTriggerOnReturnToLibraryBottomDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        if (!IsExistOnBattleArea(card) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host ||
            (card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default) != host)
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>AS-IS <c>CanTriggerWhenUseDigiBurst</c> — Digi-Burst is not modeled. STOP.</summary>
    public static bool CanTriggerWhenUseDigiBurst(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        throw new NotSupportedException("Digi-Burst is not modeled — STOP (strong model).");

    /// <summary>AS-IS <c>CanTriggerWhenTopCardTrashed</c> (WhenRemoveField.cs:37).</summary>
    public static bool CanTriggerWhenTopCardTrashed(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool> cardCondition)
    {
        ArgumentNullException.ThrowIfNull(cardCondition);
        return EventCards(ctx, card).Any(cardCondition);
    }

    /// <summary>AS-IS <c>CanTriggerOnPermanentLeave</c> (OnDeletion.cs:51).</summary>
    public static bool CanTriggerOnPermanentLeave(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool> permanentCondition) =>
        SubjectPermanentPasses(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerOnFaceUpSecurityIncreases</c> (OnFaceUpSecurityIncrease.cs:11).</summary>
    public static bool CanTriggerOnFaceUpSecurityIncreases(Headless.Effects.CardEffectResolveContext ctx, CardSource card, HeadlessPlayerId? player = null, Func<CardSource, bool>? cardCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (player is HeadlessPlayerId p && OwnerOfId(card.Context, subject) != p)
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>AS-IS <c>IsTopCardInTrashOnDeletion</c> (OnDeletion.cs:144): the deletion subject's top
    /// actually reached the trash (or is a token).</summary>
    public static bool IsTopCardInTrashOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        var view = new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject));
        return view.IsToken || IsExistOnTrash(view);
    }

    /// <summary>AS-IS <c>IsExistOnBattleAreaTrigger</c>/<c>IsExistOnBattleAreaActivate</c>
    /// (GameContextDeterminarion.cs:41/89): the AS-IS pair caches "the permanent at trigger time" and
    /// re-checks it at activation — headless the permanent identity IS the instance id (stacks keep their
    /// top instance across the window), so both collapse to the live battle-area check.</summary>
    public static bool IsExistOnBattleAreaTrigger(CardSource card, ICardEffect? cardEffect = null) =>
        IsExistOnBattleArea(card);

    public static bool IsExistOnBattleAreaActivate(CardSource card, ICardEffect? cardEffect = null) =>
        IsExistOnBattleArea(card);

    /// <summary>AS-IS <c>CanActivateOnDeletionWithContainingCardName</c> (OnDeletion.cs, verbatim): the
    /// deleted stack contains a card passing the predicate AND a deleted-card NAME contains
    /// <paramref name="name"/>. Headless: subject = the deleted top; the stack = subject + its snapshot
    /// sources.</summary>
    public static bool CanActivateOnDeletionWithContainingCardName(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, string name, Func<CardSource, bool>? cardCondition = null)
    {
        return DeletedStackPasses(ctx, card, cardCondition) &&
            DeletedStackCards(ctx, card).Any(cs => cs.ContainsCardName(name));
    }

    /// <summary>AS-IS <c>CanActivateOnDeletionWithContainingTrait</c>.</summary>
    public static bool CanActivateOnDeletionWithContainingTrait(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, string name, Func<CardSource, bool>? cardCondition = null)
    {
        return DeletedStackPasses(ctx, card, cardCondition) &&
            SubjectCard(ctx, card) is CardSource top && top.ContainsTraits(name);
    }

    /// <summary>AS-IS <c>CanActivateOnDeletionWithCardColors</c> — the deleted top's colour list passes.</summary>
    public static bool CanActivateOnDeletionWithCardColors(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<IReadOnlyList<string>, bool>? cardColorCondition, Func<CardSource, bool>? cardCondition = null)
    {
        return DeletedStackPasses(ctx, card, cardCondition) &&
            SubjectCard(ctx, card) is CardSource top &&
            (cardColorCondition is null || cardColorCondition(top.CardColors));
    }

    /// <summary>AS-IS <c>CanActivateOnDeletionWithSaveText</c> — the deleted top HAD [Save] (the P1/A4
    /// deletion-time keyword snapshot preserves it past the binding drop).</summary>
    public static bool CanActivateOnDeletionWithSaveText(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        if (!DeletedStackPasses(ctx, card, cardCondition) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject)
        {
            return false;
        }

        return card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null
            && dead.Metadata.TryGetValue(Headless.Runtime.DeletionReplacementGate.HasSaveKey, out object? raw) && raw is true;
    }

    /// <summary>AS-IS self wrappers (OnDeletion.cs:200/254/305/359 — original "Selef" spelling kept).</summary>
    public static bool CanActivateSelfOnDeletionWithContainingCardName(Headless.Effects.CardEffectResolveContext ctx, string name, CardSource card) =>
        CanActivateOnDeletionWithContainingCardName(ctx, card, name, cs => cs.InstanceId == card.InstanceId);

    public static bool CanActivateSelfOnDeletionWithContainingTrait(Headless.Effects.CardEffectResolveContext ctx, string name, CardSource card) =>
        CanActivateOnDeletionWithContainingTrait(ctx, card, name, cs => cs.InstanceId == card.InstanceId);

    public static bool CanActivateSelfOnDeletionWithCardColors(Headless.Effects.CardEffectResolveContext ctx, Func<IReadOnlyList<string>, bool>? cardColorCondition, CardSource card) =>
        CanActivateOnDeletionWithCardColors(ctx, card, cardColorCondition, cs => cs.InstanceId == card.InstanceId);

    public static bool CanActivateSelefOnDeletionWithSaveText(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanActivateOnDeletionWithSaveText(ctx, card, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentWouldDigivolveOfCard</c> (WhenPermanentWouldDigivolve.cs:11):
    /// the would-digivolve target is THIS card's own permanent.</summary>
    public static bool CanTriggerWhenPermanentWouldDigivolveOfCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        HeadlessEntityId ownTop = card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default;
        return !ownTop.IsEmpty &&
            CanTriggerWhenPermanentWouldDigivolve(ctx, card, p => p.InstanceId == ownTop, cardCondition);
    }

    /// <summary>AS-IS <c>CanJogressWithHandOrTrash</c> (DNADigivolveEffects.cs:231): the DNA card sits in
    /// the hand/trash and its recipe can be filled. The hand/trash-MATERIAL half rides the unmodeled
    /// temporary-permanent machinery (STOP) — battle-area materials are the modeled path.</summary>
    public static bool CanJogressWithHandOrTrash(
        CardSource source, HeadlessPlayerId owner, bool isWithHandCard, bool isIntoHandCard,
        Func<CardSource, bool>? targetCardCondition = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = isWithHandCard;
        if (!(isIntoHandCard ? IsExistOnHand(source) : IsExistOnTrash(source)) ||
            (targetCardCondition is not null && !targetCardCondition(source)))
        {
            return false;
        }

        return SpecialPlayRecipeRegistry.TryGet(source.CardNumber, out SpecialPlayRecipe? recipe) && recipe is not null
            && recipe.Kind == SpecialPlayKind.DnaDigivolve;
    }

    /// <summary>AS-IS <c>ChangeSecurityDigimonCardDPPlayerEffect</c> (GiveEffectToPlayer/ChangeCardDP.cs:10,
    /// verbatim): security Digimon gain ±DP for security battles (SecurityResolver folds the grant).</summary>
    public static bool ChangeSecurityDigimonCardDPPlayerEffect(
        Func<CardSource, bool>? cardCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)
    {
        if (changeValue == 0)
        {
            return false;
        }

        var extra = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Headless.Runtime.SecurityResolver.SecurityCardDpDeltaKey] = changeValue,
        };
        if (cardCondition is not null)
        {
            extra[Headless.Runtime.SecurityResolver.SecurityCardPredicateKey] = cardCondition;
        }

        return GainToPlayerScope(effectDuration, sourceCard, "changeSecurityCardDp", permanentCondition: null,
            extraValues: extra, scopeOverride: ContinuousModifierGate.Scope);
    }

    /// <summary>AS-IS <c>StartOfMainAttack</c> (GiveEffect/StartOfMainAttack.cs:5, verbatim): until the
    /// owner's turn end, at the start of the owner's main phase this Digimon MUST attack (the offer cannot
    /// be declined; player or any Digimon). Registered as a duration-tagged trigger binding whose effect
    /// opens the attack offer.</summary>
    public static void StartOfMainAttack(Permanent? targetPermanent, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId attackerId = targetPermanent.InstanceId;
        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, attackerId,
            triggerEntityId: null, targetEntityIds: new[] { attackerId },
            values: new Dictionary<string, object?>(StringComparer.Ordinal));
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:startOfMainAttack:{attackerId.Value}"),
                sourceCard.Controller, Headless.Effects.TriggerTimings.OnStartMainPhase, effectContext),
            keywords: null, EffectQueryRole.None, queryScopes: null,
            effect: new StartOfMainAttackEffect(context, attackerId),
            duration: EffectDuration.UntilOwnerTurnEnd));
    }

    /// <summary>AS-IS <c>GetCardEffectFromHashtable</c> (GetFromHashtable.cs:10) — headless the CAUSING
    /// effect is represented by its SOURCE CARD.</summary>
    public static CardSource? GetCardEffectFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.SourceEntityId.IsEmpty
            ? null
            : new CardSource(card.Context, ctx.EffectContext.SourceEntityId, OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId), OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId));

    /// <summary>AS-IS <c>GetAttackerFromHashtable</c> (:250): the attacking permanent = the event subject.</summary>
    public static Permanent? GetAttackerFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        GetPermanentFromHashtable(ctx, card);

    /// <summary>AS-IS <c>GetMovedPermanentFromHashtable</c> (OnMove.cs:30).</summary>
    public static Permanent? GetMovedPermanentFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        GetPermanentFromHashtable(ctx, card);

    /// <summary>AS-IS <c>GetTopCardFromOneHashtable</c> (:295) / <c>GetTopCardFromEffectHashtable</c>
    /// (:178): the deletion subject's top card.</summary>
    public static CardSource? GetTopCardFromOneHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectCard(ctx, card);

    public static CardSource? GetTopCardFromEffectHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectCard(ctx, card);

    /// <summary>AS-IS <c>GetFaceDownFromHashtable</c> (:337) — default true, like the original.</summary>
    public static bool GetFaceDownFromHashtable(Headless.Effects.CardEffectResolveContext ctx) =>
        !ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFaceDown", out object? raw)
        || raw is not bool b || b;

    /// <summary>AS-IS <c>GetCardSourcesFromHashtable</c> (:592) / <c>GetDiscardedCardsFromHashtable</c>
    /// (:569): the event's card list.</summary>
    public static List<CardSource> GetCardSourcesFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        EventCards(ctx, card).ToList();

    public static List<CardSource> GetDiscardedCardsFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        EventCards(ctx, card).ToList();

    /// <summary>AS-IS <c>GetDigivolutionRootsFromEnterFieldHashtable</c> (:661): the entered permanent's
    /// digivolution sources (all cards under the subject).</summary>
    public static List<CardSource> GetDigivolutionRootsFromEnterFieldHashtable(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return new List<CardSource>();
        }

        EngineContext context = card.Context;
        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(context, subject, OwnerOfId(context, subject))))
        {
            return new List<CardSource>();
        }

        return DeletedStackCards(ctx, card).Skip(1).ToList();   // stack minus the top = the roots
    }

    /// <summary>AS-IS <c>GetEvoRootTopsFromEnterFieldHashtable</c> (:200): the PRE-digivolve top(s) — the
    /// digivolve event carries the previous top (targetCardId).</summary>
    public static List<CardSource> GetEvoRootTopsFromEnterFieldHashtable(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)
    {
        EngineContext context = card.Context;
        if (ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty &&
            permanentCondition is not null &&
            !permanentCondition(new Permanent(context, subject, OwnerOfId(context, subject))))
        {
            return new List<CardSource>();
        }

        if (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}targetCardId", out object? raw) &&
            raw?.ToString() is { Length: > 0 } value)
        {
            var id = new HeadlessEntityId(value);
            return new List<CardSource> { new(context, id, OwnerOfId(context, id), OwnerOfId(context, id)) };
        }

        return new List<CardSource>();
    }

    // --- (W6 tail) shared event-card reader -----------------------------------------------------------

    /// <summary>The cards the driving event is about: an id-list value when the emission carries one
    /// (cardIds / addedCardIds), else the single subject.</summary>
    private static IReadOnlyList<CardSource> EventCards(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        EngineContext context = card.Context;
        foreach (string key in new[] { "cardIds", "addedCardIds", "discardedCardIds" })
        {
            if (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{key}", out object? raw) &&
                raw?.ToString() is { Length: > 0 } value)
            {
                return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => new HeadlessEntityId(id))
                    .Select(id => new CardSource(context, id, OwnerOfId(context, id), OwnerOfId(context, id)))
                    .ToArray();
            }
        }

        return ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new[] { new CardSource(context, subject, OwnerOfId(context, subject), OwnerOfId(context, subject)) }
            : Array.Empty<CardSource>();
    }

    private static bool DeletedStackPasses(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition) =>
        cardCondition is null
            ? ctx.EffectContext.TriggerEntityId is HeadlessEntityId s && !s.IsEmpty
            : DeletedStackCards(ctx, card).Any(cardCondition);

    private static CardSource? SubjectCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject))
            : null;

    /// <summary>The deleted permanent's stack (subject top + its snapshot digivolution sources).</summary>
    private static IReadOnlyList<CardSource> DeletedStackCards(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return Array.Empty<CardSource>();
        }

        EngineContext context = card.Context;
        var stack = new List<CardSource> { new(context, subject, OwnerOfId(context, subject), OwnerOfId(context, subject)) };
        if (context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null &&
            dead.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw) &&
            raw is IEnumerable<string> ids)
        {
            stack.AddRange(ids.Select(v => new HeadlessEntityId(v))
                .Select(id => new CardSource(context, id, OwnerOfId(context, id), OwnerOfId(context, id))));
        }

        return stack;
    }

    private static bool IsDigiEggType(CardSource cs) =>
        cs.Context.CardInstanceRepository.TryGetInstance(cs.InstanceId, out CardInstanceRecord? i) && i is not null
        && cs.Context.CardRepository.TryGetCard(i.DefinitionId, out CardRecord? d) && d is not null
        && (d.IsCardType("DigiEgg") || d.IsCardType("Digitama"));

    // --- (W6-T) shared readers over the enriched resolve context ------------------------------------

    private static bool EventIsDigivolve(Headless.Effects.CardEffectResolveContext ctx) =>
        (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{AutoProcessingTriggerCollector.TriggerTimingKey}", out object? raw)
            && raw is string timing && timing == Headless.Effects.TriggerTimings.WhenDigivolving)
        || (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isEvolution", out object? evo) && evo is true);

    private static bool EventRootPasses(Headless.Effects.CardEffectResolveContext ctx, Func<ChoiceZone, bool>? rootCondition)
    {
        if (rootCondition is null)
        {
            return true;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}fromZone", out object? raw)
            && raw is string zoneName && Enum.TryParse(zoneName, out ChoiceZone fromZone)
            && rootCondition(fromZone);
    }

    /// <summary>Mirror of the AS-IS <c>permanent.cardSources.Contains(card)</c> subject checks: the event
    /// subject is this card, or this card rides the subject's stack (digivolution source).</summary>
    private static bool SubjectPermanentContains(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (subject == card.InstanceId)
        {
            return true;
        }

        return card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
            && raw is IEnumerable<string> sources && sources.Contains(card.InstanceId.Value);
    }

    private static bool SubjectPermanentPasses(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, bool requireOnBattleArea = false)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        HeadlessPlayerId owner = card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : default;
        var view = new Permanent(card.Context, subject, owner);
        if (requireOnBattleArea && !IsPermanentExistsOnBattleArea(view))
        {
            return false;
        }

        return permanentCondition is null || permanentCondition(view);
    }

    // ===== (W6-G) Gain-keyword commons batch — 1:1 mirrors of KeyWordEffects/*.cs Gain* =====
    // AS-IS shape (verbatim verified, primitive_w6_design.md W6-G): guards (target on field, source valid)
    // -> target-locked permanentCondition -> live CanUse (on field && !CanNotBeAffected) -> the keyword's
    // StaticEffect -> AddEffectToPermanent(duration bucket). Headless: one duration-tagged, card-TARGETED
    // keyword binding (grant-time immunity refusal mirrors the AS-IS CanUse guard's first evaluation; the
    // live remainder rides ConditionKey). Synchronous, returns true when registered (Gain-commons norm).

    private static bool GainKeywordToPermanent(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard, string keyword, string gainName)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return false;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId targetId = targetPermanent.InstanceId;
        HeadlessPlayerId targetOwner = targetPermanent.OwnerId;
        var zones = (IZoneStateReader)context.ZoneMover;
        if (!zones.GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId))
        {
            return false;   // AS-IS IsPermanentExistsOnBattleArea guard.
        }

        // AS-IS CanUse first evaluation: !target.CanNotBeAffected(activateClass) — an immune target refuses.
        if (ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, sourceCard.InstanceId, context))
        {
            return false;
        }

        // AS-IS CanUseCondition is LIVE: on the battle area AND !CanNotBeAffected — a target that gains
        // immunity AFTER the grant turns the granted effect off.
        HeadlessEntityId grantSourceId = sourceCard.InstanceId;
        Func<bool> liveCondition = () =>
            ((IZoneStateReader)context.ZoneMover).GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId)
            && !ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, grantSourceId, context);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ContinuousSelfModifierEffect.ConditionKey] = liveCondition,
        };
        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: new[] { targetId }, values: values);
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:{gainName}:{targetId.Value}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: new[] { keyword }, EffectQueryRole.Continuous, queryScopes: null,
            effect: null, duration: effectDuration));
        return true;
    }

    /// <summary>(W6 process) shared timed target-modifier grant — the AS-IS ChangeDigimonDP/SAttack shape
    /// (verbatim verified): guards, live CanUse (on field && !CanNotBeAffected), duration bucket.</summary>
    private static bool ChangeDigimonStat(
        Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard,
        string deltaKey, string gainName)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty || changeValue == 0)
        {
            return false;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId targetId = targetPermanent.InstanceId;
        HeadlessPlayerId targetOwner = targetPermanent.OwnerId;
        var zones = (IZoneStateReader)context.ZoneMover;
        if (!zones.GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId))
        {
            return false;
        }

        if (ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, sourceCard.InstanceId, context))
        {
            return false;
        }

        HeadlessEntityId grantSourceId = sourceCard.InstanceId;
        Func<bool> liveCondition = () =>
            ((IZoneStateReader)context.ZoneMover).GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId)
            && !ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, grantSourceId, context);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [deltaKey] = changeValue,
            [ContinuousSelfModifierEffect.ConditionKey] = liveCondition,
        };
        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: new[] { targetId }, values: values);
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:{gainName}:{targetId.Value}:{Guid.NewGuid():N}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope },
            effect: null, duration: effectDuration));
        return true;
    }

    /// <summary>AS-IS <c>ChangeDigimonDP</c> (GiveEffect/GiveEffectToPermanent/ChangeDP.cs:10, verbatim
    /// verified): timed ±DP on the target permanent.</summary>
    public static bool ChangeDigimonDP(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard) =>
        ChangeDigimonStat(targetPermanent, changeValue, effectDuration, sourceCard, ModifierHelpers.DpDeltaKey, "changeDp");

    /// <summary>AS-IS <c>ChangeDigimonSAttack</c> (…/ChangeSAttack.cs:10; the overload's
    /// <paramref name="activateAnimation"/>/<paramref name="hashstring"/> are UI-only in the original).</summary>
    public static bool ChangeDigimonSAttack(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard,
        bool activateAnimation = true, string? hashstring = null)
    {
        _ = activateAnimation;
        _ = hashstring;
        return ChangeDigimonStat(targetPermanent, changeValue, effectDuration, sourceCard, ModifierHelpers.SecurityAttackDeltaKey, "changeSAttack");
    }

    /// <summary>AS-IS <c>ChangeDigimonDPPlayerEffect</c> (GiveEffect/GiveEffectToPlayer/ChangeDP.cs:10):
    /// timed ±DP on EVERY permanent matching the predicate — a duration-tagged PLAYER-SCOPE modifier
    /// (the AS-IS PermanentCondition folds the battle-area + !CanNotBeAffected guards; here the scope
    /// evaluation supplies the battle-area half and the predicate carries the rest verbatim).</summary>
    public static bool ChangeDigimonDPPlayerEffect(
        Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (changeValue == 0)
        {
            return false;
        }

        EngineContext context = sourceCard.Context;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModifierHelpers.DpDeltaKey] = changeValue,
            [Headless.Effects.PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [Headless.Effects.PlayerScopeContinuousHelpers.ScopePlayerIdKey] = sourceCard.Owner.Value,
        };
        if (permanentCondition is not null)
        {
            values[Headless.Effects.PlayerScopeContinuousHelpers.ScopePredicateKey] =
                (Func<CardSource, bool>)(cs => permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)));
        }

        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:changeDpPlayer:{Guid.NewGuid():N}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope },
            effect: null, duration: effectDuration));
        return true;
    }

    /// <summary>AS-IS <c>AddThisCardToHand</c> (CardEffectCommons.cs:424, UI waits elided): move this card
    /// to its owner's hand via the sink (immunity/centralised gates apply).</summary>
    public static async Task AddThisCardToHand(CardSource card1, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(card1);
        ArgumentNullException.ThrowIfNull(sourceCard);
        var sink = NewSink(card1.Context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind, sourceCard.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card1.InstanceId.Value }));
        await sink.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>PlayPermanentCards(cardSources, activateClass, payCost, isTapped, root,
    /// activateETB, isBreedingArea, fixedCost)</c> (CardEffectCommons.cs:23, verbatim verified): filter by
    /// <see cref="CanPlayAsNewPermanent"/> then play each as a new permanent via the sink's PlayCard
    /// mutation (cost = fixed / resolved play cost when <paramref name="payCost"/>). Note: an
    /// <paramref name="activateETB"/>=false suppression has no port surface (entry triggers derive from the
    /// zone move) — every current translated caller passes true; a false caller is a STOP.</summary>
    public static async Task PlayPermanentCards(
        IReadOnlyList<CardSource> cardSources, CardSource sourceCard, bool payCost, bool isTapped,
        ChoiceZone root, bool activateETB, bool isBreedingArea = false, int fixedCost = -1)
    {
        ArgumentNullException.ThrowIfNull(cardSources);
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (!activateETB)
        {
            throw new NotSupportedException("PlayPermanentCards(activateETB:false) has no headless surface — STOP (strong model).");
        }

        EngineContext context = sourceCard.Context;
        var playable = cardSources
            .Where(cs => cs is not null && CanPlayAsNewPermanent(cs, payCost, null, isPlayOption: false, fixedCost: fixedCost))
            .ToList();
        if (playable.Count == 0)
        {
            return;
        }

        var sink = NewSink(context);
        foreach (CardSource cs in playable)
        {
            int cost = 0;
            if (payCost)
            {
                int baseCost = context.CardInstanceRepository.TryGetInstance(cs.InstanceId, out CardInstanceRecord? inst) && inst is not null
                    && context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
                    ? def.PlayCost ?? 0
                    : 0;
                cost = fixedCost >= 0 ? fixedCost : Math.Max(0, ContinuousModifierGate.ResolvePlayCost(context, cs.InstanceId, baseCost));
            }

            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value,
                [MatchStateMutationSink.FromZoneKey] = root,
            };
            if (cost > 0)
            {
                values[MatchStateMutationSink.MemoryCostKey] = cost;
            }

            sink.Apply(new EffectMutation(MatchStateMutationSink.PlayCardKind, sourceCard.InstanceId, values));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (CardSource cs in playable)
        {
            if (isBreedingArea &&
                zones.GetCards(cs.Owner, ChoiceZone.BattleArea).Contains(cs.InstanceId))
            {
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(cs.Owner, cs.InstanceId, ChoiceZone.BattleArea, ChoiceZone.BreedingArea)).ConfigureAwait(false);
            }

            if (isTapped &&
                context.CardInstanceRepository.TryGetInstance(cs.InstanceId, out CardInstanceRecord? played) && played is not null)
            {
                context.CardInstanceRepository.Upsert(played with
                {
                    Metadata = new Dictionary<string, object?>(played.Metadata, StringComparer.Ordinal) { ["isSuspended"] = true }
                });
            }
        }
    }

    /// <summary>AS-IS <c>DigivolveIntoHandOrTrashCard</c> (CardEffectCommons.cs:756-1100, verbatim
    /// verified): choose a Digimon card from the HAND (or TRASH) that satisfies <paramref name="cardCondition"/>
    /// + the digivolution requirement onto <paramref name="targetPermanent"/> (waived under
    /// <paramref name="ignoreRequirements"/> / <paramref name="ignoreDigivolutionRequirementFixedCost"/>),
    /// digivolve it onto the target (cost = fixed / requirement-ignore fixed / evolution cost −
    /// <paramref name="reduceCostTuple"/> when <paramref name="payCost"/>), then branch on whether the
    /// digivolution ACTUALLY happened. NOTE: the recipe previously mis-mapped this commons to the
    /// de-digivolve factory — it is the OPPOSITE direction (digivolve INTO from hand/trash).</summary>
    public static Task DigivolveIntoHandOrTrashCard(
        Permanent? targetPermanent,
        Func<CardSource, bool>? cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        bool isHand,
        CardSource sourceCard,
        Func<Task>? successProcess,
        bool ignoreSelection = false,
        bool ignoreRequirements = false,
        Func<Task>? failedProcess = null,
        bool isOptional = true,
        CancellationToken cancellationToken = default) =>
        DigivolveIntoZoneCoreAsync(
            targetPermanent, cardCondition, payCost, reduceCostTuple, fixedCostTuple,
            ignoreDigivolutionRequirementFixedCost, isHand ? ChoiceZone.Hand : ChoiceZone.Trash, sourceCard,
            successProcess, failedProcess, ignoreSelection, ignoreRequirements, isOptional, cancellationToken);

    private static async Task DigivolveIntoZoneCoreAsync(
        Permanent? targetPermanent,
        Func<CardSource, bool>? cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        ChoiceZone rootZone,
        CardSource sourceCard,
        Func<Task>? successProcess,
        Func<Task>? failedProcess,
        bool ignoreSelection,
        bool ignoreRequirements,
        bool isOptional,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        bool successful = false;
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty || context.ZoneMover is not IZoneStateReader zones)
        {
            await Branch(false, successProcess, failedProcess).ConfigureAwait(false);
            return;
        }

        bool waiveRequirement = ignoreRequirements || ignoreDigivolutionRequirementFixedCost >= 0;
        HeadlessEntityId targetId = targetPermanent.InstanceId;

        bool CanSelect(HeadlessEntityId id)
        {
            var view = new CardSource(context, id, targetPermanent.OwnerId, targetPermanent.OwnerId);
            if (!view.IsDigimon || (cardCondition is not null && !cardCondition(view)))
            {
                return false;
            }

            if (ContinuousRestrictionGate.EvaluateDigivolve(context, targetId).IsRestricted)
            {
                return false;   // AS-IS !CanNotEvolve(targetPermanent)
            }

            return waiveRequirement
                || Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, id, targetId, out _, out _);
        }

        HeadlessEntityId selected = default;
        if (ignoreSelection)
        {
            selected = sourceCard.InstanceId;
        }
        else
        {
            List<ChoiceCandidate> candidates = zones.GetCards(targetPermanent.OwnerId, rootZone)
                .Where(CanSelect)
                .Select(id => new ChoiceCandidate(id, id.Value, rootZone, IsSelectable: true, ownerId: targetPermanent.OwnerId))
                .ToList();
            if (candidates.Count > 0)
            {
                var request = new ChoiceRequest(
                    ChoiceType.Card, targetPermanent.OwnerId, "Select 1 card to digivolve.",
                    minCount: isOptional ? 0 : 1, maxCount: 1, canSkip: isOptional, rootZone, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                if (!result.IsSkipped && result.SelectedIds.Count > 0)
                {
                    selected = result.SelectedIds[0];
                }
            }
        }

        if (!selected.IsEmpty)
        {
            // Cost (AS-IS): fixed / requirement-ignore fixed wins; else evolution cost − reduceCost; 0 floor.
            int cost = 0;
            if (payCost)
            {
                if (ignoreDigivolutionRequirementFixedCost >= 0)
                {
                    cost = ignoreDigivolutionRequirementFixedCost;
                }
                else if (fixedCostTuple is { } fixedTuple &&
                         (fixedTuple.fixedCostCardCondition is null || fixedTuple.fixedCostCardCondition(new CardSource(context, selected, targetPermanent.OwnerId, targetPermanent.OwnerId))))
                {
                    cost = fixedTuple.fixedCost;
                }
                else
                {
                    Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, selected, targetId, out cost, out _);
                    if (reduceCostTuple is { } reduceTuple &&
                        (reduceTuple.reduceCostCardCondition is null || reduceTuple.reduceCostCardCondition(new CardSource(context, selected, targetPermanent.OwnerId, targetPermanent.OwnerId))))
                    {
                        cost -= reduceTuple.reduceCost;
                    }
                }

                cost = Math.Max(0, cost);
            }

            if (!payCost || context.MemoryController.CanPay(cost))
            {
                // The Arts/ArtsDigivolve stacking sequence (target off -> card on -> fold under -> window).
                ChoiceZone targetZone = zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BreedingArea).Contains(targetId)
                    ? ChoiceZone.BreedingArea
                    : ChoiceZone.BattleArea;
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(targetPermanent.OwnerId, targetId, targetZone, ChoiceZone.None), cancellationToken).ConfigureAwait(false);
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(targetPermanent.OwnerId, selected, rootZone, targetZone), cancellationToken).ConfigureAwait(false);
                if (payCost && cost > 0)
                {
                    context.MemoryController.Pay(cost);
                }

                Headless.Runtime.DigivolveAction.AttachTargetAsSource(context.CardInstanceRepository, selected, targetId);
                // (W6 tail) stamp the causing effect (AS-IS Permanent.DigivolvingEffect — IsDigivolvedByTheEffect reads it).
                if (context.CardInstanceRepository.TryGetInstance(selected, out CardInstanceRecord? placedRec) && placedRec is not null)
                {
                    context.CardInstanceRepository.Upsert(placedRec with
                    {
                        Metadata = new Dictionary<string, object?>(placedRec.Metadata, StringComparer.Ordinal)
                        {
                            ["digivolvedByEffectSourceId"] = sourceCard.InstanceId.Value,
                        }
                    });
                }

                TriggerEventEmitter.Emit(context.GameEventQueue, Headless.Effects.TriggerTimings.WhenDigivolving,
                    actor: targetPermanent.OwnerId, subject: selected,
                    extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isEvolution"] = true });
                CardEffectRegistrar.RegisterCard(context, selected, targetPermanent.OwnerId);
                successful = zones.GetCards(targetPermanent.OwnerId, targetZone).Contains(selected);
            }
        }

        await Branch(successful, successProcess, failedProcess).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>SelectTrashDigivolutionCards</c> (TrashDigivolutionCards.cs:11-192, verbatim
    /// verified): repeatedly pick a battle-area permanent matching <paramref name="permanentCondition"/>,
    /// then trash up to the remaining budget of its digivolution sources matching
    /// <paramref name="cardCondition"/> — until <paramref name="maxCount"/> sources are trashed (or one
    /// permanent when <paramref name="isFromOnly1Permanent"/>).</summary>
    public static async Task SelectTrashDigivolutionCards(
        Func<Permanent, bool>? permanentCondition,
        Func<CardSource, bool>? cardCondition,
        int maxCount,
        bool canNoTrash,
        bool isFromOnly1Permanent,
        CardSource sourceCard,
        string selectString = "Digimon",
        Func<Permanent, IReadOnlyList<CardSource>, Task>? afterSelectionCoroutine = null,
        bool canEndNotMax = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (maxCount <= 0)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        int trashedTotal = 0;
        var usedHosts = new HashSet<HeadlessEntityId>();

        bool HostQualifies(HeadlessEntityId id, HeadlessPlayerId owner)
        {
            var view = new Permanent(context, id, owner);
            if (permanentCondition is not null && !permanentCondition(view))
            {
                return false;
            }

            return SourcesOf(id).Any(sid => SourceQualifies(sid, owner));
        }

        bool SourceQualifies(HeadlessEntityId sid, HeadlessPlayerId owner)
        {
            var view = new CardSource(context, sid, owner, owner);
            return cardCondition is null || cardCondition(view);
        }

        IReadOnlyList<HeadlessEntityId> SourcesOf(HeadlessEntityId hostId) =>
            context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null
                && host.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
                && raw is IEnumerable<string> ids
                ? ids.Select(v => new HeadlessEntityId(v)).ToArray()
                : Array.Empty<HeadlessEntityId>();

        while (trashedTotal < maxCount)
        {
            var hostCandidates = new List<ChoiceCandidate>();
            foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
            {
                if (player.IsEmpty)
                {
                    continue;
                }

                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
                {
                    if (!usedHosts.Contains(id) && HostQualifies(id, player))
                    {
                        hostCandidates.Add(new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: player));
                    }
                }
            }

            if (hostCandidates.Count == 0)
            {
                break;
            }

            bool optionalNow = (canNoTrash && trashedTotal == 0) || canEndNotMax;
            var hostRequest = new ChoiceRequest(
                ChoiceType.Card, sourceCard.Owner, $"Select 1 {selectString} that will trash digivolution cards.",
                minCount: optionalNow ? 0 : 1, maxCount: 1, canSkip: optionalNow, ChoiceZone.BattleArea, hostCandidates);
            ChoiceResult hostResult = await context.ChoiceProvider.ChooseAsync(hostRequest, cancellationToken).ConfigureAwait(false);
            if (hostResult.IsSkipped || hostResult.SelectedIds.Count == 0)
            {
                break;
            }

            HeadlessEntityId hostId = hostResult.SelectedIds[0];
            usedHosts.Add(hostId);
            HeadlessPlayerId hostOwner = context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? hostRec) && hostRec is not null
                ? hostRec.OwnerId
                : sourceCard.Owner;

            var sourceCandidates = SourcesOf(hostId)
                .Where(sid => SourceQualifies(sid, hostOwner))
                .Select(sid => new ChoiceCandidate(sid, sid.Value, ChoiceZone.DigivolutionCards, IsSelectable: true, ownerId: hostOwner))
                .ToList();
            int budget = Math.Min(maxCount - trashedTotal, sourceCandidates.Count);
            var sourceRequest = new ChoiceRequest(
                ChoiceType.Card, sourceCard.Owner, "Select digivolution cards to trash.",
                minCount: budget >= 2 && !isFromOnly1Permanent ? 1 : budget, maxCount: budget,
                canSkip: false, ChoiceZone.DigivolutionCards, sourceCandidates);
            ChoiceResult sourceResult = await context.ChoiceProvider.ChooseAsync(sourceRequest, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<HeadlessEntityId> picks = sourceResult.SelectedIds;
            int trashed = await Headless.Runtime.DigivolutionStackHelpers.TrashSpecificSourcesAsync(
                context.CardInstanceRepository, context.ZoneMover, hostId, picks, cancellationToken, context.GameEventQueue).ConfigureAwait(false);
            trashedTotal += trashed;

            if (afterSelectionCoroutine is not null)
            {
                await afterSelectionCoroutine(
                    new Permanent(context, hostId, hostOwner),
                    picks.Select(id => new CardSource(context, id, hostOwner, hostOwner)).ToArray()).ConfigureAwait(false);
            }

            if (isFromOnly1Permanent)
            {
                break;
            }
        }
    }

    /// <summary>AS-IS <c>DNADigivolvePermanentsIntoHandOrTrashCard</c> (DNADigivolveEffects.cs:458-624,
    /// verbatim verified): choose a DNA-capable card from the HAND (or TRASH), then perform the DNA
    /// digivolution (two battle-area materials, via the special-play pipeline). Material selection follows
    /// the port's parameterized-action policy (first valid backtracking assignment — the DigiXros/DNA
    /// reduction, fidelity_debt). <paramref name="permanentConditions"/> overrides the material predicates
    /// (AS-IS SetUpCustomPermanentConditions). Success = the fused card actually entered the battle area.</summary>
    public static async Task DNADigivolvePermanentsIntoHandOrTrashCard(
        Func<CardSource, bool>? canSelectDNACardCondition,
        bool payCost,
        bool isHand,
        CardSource sourceCard,
        Func<Permanent, bool>[]? permanentConditions = null,
        Func<CardSource, Task>? successProcess = null,
        bool ignoreSelection = false,
        Func<Task>? failedProcess = null,
        bool isOptional = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        _ = payCost;   // AS-IS predicate-form DNA is cost 0 (the recipe carries the cost when nonzero).
        EngineContext context = sourceCard.Context;
        HeadlessPlayerId owner = sourceCard.Owner;
        var zones = (IZoneStateReader)context.ZoneMover;

        int battleDigimon = zones.GetCards(owner, ChoiceZone.BattleArea)
            .Count(id => new CardSource(context, id, owner, owner).IsDigimon);
        if (battleDigimon < 2)
        {
            await Branch(false, null, failedProcess).ConfigureAwait(false);
            return;
        }

        ChoiceZone rootZone = isHand ? ChoiceZone.Hand : ChoiceZone.Trash;
        HeadlessEntityId dnaTarget = default;
        if (ignoreSelection)
        {
            dnaTarget = sourceCard.InstanceId;
        }
        else
        {
            List<ChoiceCandidate> candidates = zones.GetCards(owner, rootZone)
                .Where(id =>
                {
                    var view = new CardSource(context, id, owner, owner);
                    return (canSelectDNACardCondition is null || canSelectDNACardCondition(view))
                        && SpecialPlayRecipeRegistry.TryGet(view.CardNumber, out SpecialPlayRecipe? r) && r is not null
                        && r.Kind == SpecialPlayKind.DnaDigivolve;
                })
                .Select(id => new ChoiceCandidate(id, id.Value, rootZone, IsSelectable: true, ownerId: owner))
                .ToList();
            if (candidates.Count > 0)
            {
                var request = new ChoiceRequest(
                    ChoiceType.Card, owner, "Select 1 card to DNA digivolve.",
                    minCount: isOptional ? 0 : 1, maxCount: 1, canSkip: isOptional, rootZone, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                if (!result.IsSkipped && result.SelectedIds.Count > 0)
                {
                    dnaTarget = result.SelectedIds[0];
                }
            }
        }

        bool successful = false;
        if (!dnaTarget.IsEmpty)
        {
            var view = new CardSource(context, dnaTarget, owner, owner);
            SpecialPlayRecipe? original = null;
            bool overridden = false;
            if (permanentConditions is { Length: > 0 })
            {
                // AS-IS SetUpCustomPermanentConditions: the caller's material predicates replace the card's.
                SpecialPlayRecipeRegistry.TryGet(view.CardNumber, out original);
                var custom = permanentConditions
                    .Select((cond, i) => new SpecialPlayMaterial(
                        cs => cs.IsDigimon && cs.Owner == owner && cond(new Permanent(cs.Context, cs.InstanceId, cs.Owner)),
                        $"custom-{i}"))
                    .ToArray();
                SpecialPlayRecipeRegistry.Register(view.CardNumber, new SpecialPlayRecipe(
                    SpecialPlayKind.DnaDigivolve, custom, MemoryCost: original?.MemoryCost ?? 0, Condition: original?.Condition));
                overridden = true;
            }

            try
            {
                LegalAction? dna = new SpecialPlayAction().GetLegalActions(context, owner)
                    .FirstOrDefault(a => a.Parameters[HeadlessActionParameterKeys.CardId]?.ToString() == dnaTarget.Value);
                if (dna is not null && rootZone == ChoiceZone.Hand)
                {
                    // The special-play pipeline plays from hand (the AS-IS trash-root DNA is a rarer shape —
                    // the card must reach the hand-play seam; a trash-root caller is a STOP for now).
                    var result = await new SpecialPlayAction().ProcessAsync(dna, context, cancellationToken).ConfigureAwait(false);
                    successful = result.IsSuccess &&
                        zones.GetCards(owner, ChoiceZone.BattleArea).Contains(dnaTarget);
                }
            }
            finally
            {
                if (overridden && original is not null)
                {
                    SpecialPlayRecipeRegistry.Register(view.CardNumber, original);
                }
            }
        }

        if (successful && successProcess is not null)
        {
            await successProcess(new CardSource(context, dnaTarget, owner, owner)).ConfigureAwait(false);
        }
        else if (!successful && failedProcess is not null)
        {
            await failedProcess().ConfigureAwait(false);
        }
    }

    /// <summary>(W6 tail) a token's printed data — 1:1 the inline <c>new CEntity_Base{…}</c> specs in
    /// AS-IS <c>ContinuousController.CreateTokenData()</c> (ContinuousController.cs:151-506, verbatim
    /// verified). <see cref="EffectClassName"/> maps to the dispatch <c>effectClass</c> alias so a token
    /// with a card effect resolves it like any ported card.</summary>
    public sealed record TokenSpec(
        string CardNumber, string Name, string Color, int PlayCost, int Level, int Dp,
        string? EffectClassName = null, string? Type = null, string? Form = null, string? Attribute = null);

    /// <summary>The AS-IS token table (ContinuousController.cs:151-506).</summary>
    public static readonly IReadOnlyDictionary<string, TokenSpec> TokenSpecs =
        new Dictionary<string, TokenSpec>(StringComparer.Ordinal)
        {
            ["Diaboromon"] = new("BT2-082-token", "Diaboromon", "White", 14, 6, 3000, null, "Unidentified", "Mega", "Unknown"),
            ["Amon"] = new("BT14-018-token-red", "Amon of Crimson Flame", "Red", -1, 0, 6000, "BT4_038"),
            ["Umon"] = new("BT14-018-token-yellow", "Umon of Blue Thunder", "Yellow", -1, 0, 6000, "BT1_031"),
            ["Fujitsumon"] = new("EX5-058-token", "Fujitsumon", "Purple", -1, 0, 3000, "EX5_058_token"),
            ["Gyuukimon"] = new("LM-018-token", "Gyuukimon", "Purple", 7, 5, 3000, null, "Dark Animal", "Ultimate", "Virus"),
            ["KoHagurumon"] = new("BT16-052-token", "KoHagurumon", "Black", -1, 0, 1000, "BT16_052_token"),
            ["Familiar"] = new("EX7-030-token", "Familiar", "Yellow", -1, 0, 3000, "EX7_030_token"),
            ["SelfDeleteFamiliar"] = new("EX7-030-token-sd", "Familiar", "Yellow", -1, 0, 3000, "P_165_token"),
            ["VoleeZerdrucken"] = new("EX7-058-token", "Volée & Zerdrücken", "Purple", -1, 4, 5000, "EX7_058_token"),
            ["UkaNoMitama"] = new("EX8-037-token", "Uka-no-Mitama", "Yellow", -1, 0, 9000, "EX8_037_token"),
            ["WarGrowlmon"] = new("BT19-091-token-red", "WarGrowlmon", "Red", -1, 0, 6000),
            ["Taomon"] = new("BT19-091-token-yellow", "Taomon", "Yellow", -1, 0, 6000),
            ["Rapidmon"] = new("BT19-091-token-green", "Rapidmon", "Green", -1, 0, 6000),
            ["PipeFox"] = new("BT19-040-token", "Pipe-Fox", "Yellow", -1, 0, 6000, "BT19_040_token"),
            ["AthoRenePor"] = new("BT20-017-token", "Atho, René & Por", "White", -1, 0, 6000, "BT20_017_token"),
            ["Hinukamuy"] = new("BT23-057-token", "HinukamuyToken", "White", -1, 0, 6000, "BT23_057_token"),
            ["Petrification"] = new("BT21-029-token", "Petrification", "White", -1, 0, 3000, "BT21_029_token"),
        };

    /// <summary>AS-IS <c>PlayToken</c> (CardEffectCommons.cs:140-176, verbatim verified): materialize
    /// <paramref name="quantity"/> copies of the token as fresh instances and play them COST-FREE onto the
    /// chosen player's battle area (the AS-IS empty-frame count has no port model — no field-size limit is
    /// modeled anywhere). Tokens carry <c>isToken</c> and register their effect class via the dispatch
    /// alias. Returns the played instance ids.</summary>
    public static async Task<IReadOnlyList<HeadlessEntityId>> PlayToken(
        TokenSpec tokenData, CardSource sourceCard, bool isOwnerPermanent, bool isTapped, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(tokenData);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        HeadlessPlayerId player = isOwnerPermanent ? sourceCard.Owner : OpponentOf(sourceCard);
        if (player.IsEmpty || quantity <= 0)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var definitionId = new HeadlessEntityId($"TOKEN:{tokenData.CardNumber}");
        var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = tokenData.Dp,
            ["level"] = tokenData.Level,
            ["colors"] = new[] { tokenData.Color },
        };
        if (tokenData.EffectClassName is not null)
        {
            defMeta["effectClass"] = tokenData.EffectClassName;
        }

        if (tokenData.Type is not null)
        {
            defMeta["traits"] = new[] { tokenData.Type };
        }

        if (context.CardRepository is Headless.DataLoading.CardDatabase database)
        {
            database.Upsert(new CardRecord(
                definitionId, tokenData.CardNumber, tokenData.Name, defMeta, CardType: "Digimon",
                PlayCost: tokenData.PlayCost >= 0 ? tokenData.PlayCost : null));
        }

        var played = new List<HeadlessEntityId>();
        var sink = NewSink(context);
        for (int index = 0; index < quantity; index++)
        {
            var tokenId = new HeadlessEntityId($"{player.Value}:token:{tokenData.CardNumber}:{Guid.NewGuid():N}");
            context.CardInstanceRepository.Upsert(new CardInstanceRecord(
                tokenId, definitionId, player,
                Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["dp"] = tokenData.Dp,
                    ["isToken"] = true,
                    ["isSuspended"] = isTapped,
                }));
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.PlayCardKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = tokenId.Value,
                    [MatchStateMutationSink.FromZoneKey] = ChoiceZone.None,
                }));
            played.Add(tokenId);
        }

        await sink.FlushAsync().ConfigureAwait(false);
        return played;
    }

    /// <summary>AS-IS <c>PlayDiaboromonToken</c> (CardEffectCommons.cs:182).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayDiaboromonToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["Diaboromon"], sourceCard, isOwnerPermanent: true, isTapped: false, quantity);

    /// <summary>AS-IS <c>PlayAmonToken</c> (:197).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayAmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Amon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayUmonToken</c> (:211).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayUmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Umon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayFujitsumonToken</c> (:225) — enters SUSPENDED.</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayFujitsumonToken(CardSource sourceCard, bool isOwnerPermanent) =>
        PlayToken(TokenSpecs["Fujitsumon"], sourceCard, isOwnerPermanent, isTapped: true);

    /// <summary>AS-IS <c>PlayGyuukimonToken</c> (:239).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayGyuukimonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Gyuukimon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayKoHagurumonToken</c> (:253).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayKoHagurumonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["KoHagurumon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayFamiliarToken</c> (:267).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayFamiliarToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["Familiar"], sourceCard, isOwnerPermanent: true, isTapped: false, quantity);

    /// <summary>AS-IS <c>PlaySelfDeleteFamiliarToken</c> (:282).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlaySelfDeleteFamiliarToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["SelfDeleteFamiliar"], sourceCard, isOwnerPermanent: true, isTapped: false, quantity);

    /// <summary>AS-IS <c>PlayVoleeZerdrucken</c> (:297).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayVoleeZerdrucken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["VoleeZerdrucken"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayUkaNoMitama</c> (:311).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayUkaNoMitama(CardSource sourceCard) =>
        PlayToken(TokenSpecs["UkaNoMitama"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayWarGrowlmonToken</c> (:325).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayWarGrowlmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["WarGrowlmon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayTaomonToken</c> (:339).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayTaomonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Taomon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayRapidmonToken</c> (:353).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayRapidmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Rapidmon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayPipeFox</c> (:367).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayPipeFox(CardSource sourceCard) =>
        PlayToken(TokenSpecs["PipeFox"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayAthoRenePorToken</c> (:381).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayAthoRenePorToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["AthoRenePor"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayHinukamuyToken</c> (:395).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayHinukamuyToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Hinukamuy"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayPetrificationToken</c> (:409) — always the OPPONENT'S board.</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayPetrificationToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["Petrification"], sourceCard, isOwnerPermanent: false, isTapped: false, quantity);

    /// <summary>AS-IS <c>CanActivateSave</c> (KeyWordEffects/Save.cs:10, verbatim): the deletion subject's
    /// top reached the trash AND a receiving permanent matching the predicate exists.</summary>
    public static bool CanActivateSave(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? CanSelectPermanentCondition) =>
        IsTopCardInTrashOnDeletion(ctx, card) &&
        HasMatchConditionPermanent(card, p => CanSelectPermanentCondition is null || CanSelectPermanentCondition(p));

    /// <summary>AS-IS <c>SaveProcess</c> (Save.cs:25): choose 1 matching permanent; this card goes from the
    /// trash to the BOTTOM of its digivolution cards.</summary>
    public static async Task SaveProcess(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? CanSelectPermanentCondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!CanActivateSave(ctx, card, CanSelectPermanentCondition))
        {
            return;
        }

        EngineContext context = card.Context;
        List<ChoiceCandidate> candidates = EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Where(p => CanSelectPermanentCondition is null || CanSelectPermanentCondition(p))
            .Select(p => new ChoiceCandidate(p.InstanceId, p.InstanceId.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: p.OwnerId))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var request = new ChoiceRequest(
            ChoiceType.Card, card.Owner, "Select 1 permanent that will get a digivolution card.",
            minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.BattleArea, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        await Headless.Runtime.DigivolutionStackHelpers.AddSourcesBottomAsync(
            context.CardInstanceRepository, context.ZoneMover, result.SelectedIds[0],
            new[] { card.InstanceId }, ChoiceZone.Trash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>CanActivateBlitz</c> (KeyWordEffects/Blitz.cs:10, verbatim): on the battle area,
    /// able to attack, the MEMORY sits on the opponent's side (>= 1 for them ⇔ turn-axis current <= -1 —
    /// Blitz fires on its controller's own turn), and no attack is in flight.</summary>
    public static bool CanActivateBlitz(CardSource cardSource)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        EngineContext context = cardSource.Context;
        return IsExistOnBattleArea(cardSource)
            && !IsSuspended(cardSource, cardSource.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default)
            && context.MemoryController.Current.Current <= -1
            && !context.AttackController.Current.IsPending;
    }

    /// <summary>AS-IS <c>BlitzProcess</c> (Blitz.cs:31): open the attack offer (player + any Digimon,
    /// AS-IS SelectAttackEffect canAttackPlayer/defender = true).</summary>
    public static bool BlitzProcess(CardSource cardSource)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        if (!CanActivateBlitz(cardSource))
        {
            return false;
        }

        HeadlessEntityId attackerId = cardSource.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default;
        return !attackerId.IsEmpty && Headless.Runtime.EffectDrivenAttack.RequestChoice(
            cardSource.Context, attackerId,
            new Headless.Runtime.EffectAttackOptions(WithoutTap: false, AllowPlayerTarget: true, AllowDigimonTarget: true, TargetUnsuspended: true));
    }

    /// <summary>AS-IS <c>CanActivateFortitude</c> (KeyWordEffects/Fortitude.cs:16): this card is in the
    /// trash, was part of the deleted stack WITH at least one digivolution source, and can re-enter.</summary>
    public static bool CanActivateFortitude(Headless.Effects.CardEffectResolveContext ctx, CardSource card, bool isInheritedEffect = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!IsExistOnTrash(card) || (isInheritedEffect && !CanActivateOnDeletion(ctx, card)))
        {
            return false;
        }

        if (!SubjectPermanentContains(ctx, card) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject)
        {
            return false;
        }

        bool hadSources = card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null
            && dead.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
            && raw is IEnumerable<string> ids && ids.Any();
        return hadSources && CanPlayAsNewPermanent(card, payCost: false, null);
    }

    /// <summary>AS-IS <c>FortitudeProcess</c> (Fortitude.cs:54): replay this card from the trash, free.</summary>
    public static Task FortitudeProcess(CardSource card, CardSource sourceCard) =>
        PlayPermanentCards(new[] { card }, sourceCard, payCost: false, isTapped: false, root: ChoiceZone.Trash, activateETB: true);

    /// <summary>AS-IS <c>CanUseIgnoreBattle</c> (CanUseEffects/IgnoreBattle.cs:10) — delegates to the
    /// option-main gate.</summary>
    public static bool CanUseIgnoreBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerOptionMainEffect(ctx, card);

    /// <summary>AS-IS <c>EnforceLocationCheck</c> (GameContextDeterminarion.cs:12): invalidates the AS-IS
    /// trigger/activate permanence cache — headless the cache collapsed (permanent identity = instance id),
    /// so this is a no-op mirror.</summary>
    public static void EnforceLocationCheck()
    {
    }

    /// <summary>AS-IS <c>AddSelfDeleteEffect</c> (GiveEffect/DeleteSelf.cs:14): the permanent deletes
    /// itself at turn end (own / opponent's / each — <paramref name="deleteTiming"/>). Headless: a metadata
    /// marker the turn-end sweep consumes.</summary>
    public static void AddSelfDeleteEffect(Permanent? permanent, string deleteTiming, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (permanent is null || permanent.InstanceId.IsEmpty ||
            !sourceCard.Context.CardInstanceRepository.TryGetInstance(permanent.InstanceId, out CardInstanceRecord? rec) || rec is null)
        {
            return;
        }

        sourceCard.Context.CardInstanceRepository.Upsert(rec with
        {
            Metadata = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal)
            {
                [Headless.Runtime.GameFlowProcessor.DeleteAtTurnEndKey] = deleteTiming,
                [Headless.Runtime.GameFlowProcessor.DeleteAtTurnEndSourceKey] = sourceCard.InstanceId.Value,
            }
        });
    }

    /// <summary>AS-IS <c>BecomeDigimonThatCantDigivolve</c> (GiveEffect/TamerBecomesDigimon….cs:10,
    /// verbatim): the Tamer becomes a Digimon (TreatAsDigimon) with base DP set to <paramref name="DP"/>
    /// and cannot digivolve — three timed grants.</summary>
    public static bool BecomeDigimonThatCantDigivolve(Permanent? targetPermanent, int DP, EffectDuration effectDuration, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty || DP < 0 ||
            !IsPermanentExistsOnBattleArea(targetPermanent))
        {
            return false;
        }

        // treat as Digimon (the keyword the central IsDigimon chokepoint honours — K4).
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, Headless.Runtime.ContinuousKeywordGate.TreatAsDigimon, "becomeDigimon");
        // base DP OVERRIDE (delta to reach the requested value).
        ChangeBaseDigimonDP(targetPermanent, DP, effectDuration, sourceCard);
        // cannot digivolve.
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotDigivolveKey, "becomeDigimonNoEvolve");
        return true;
    }

    /// <summary>AS-IS <c>DrawAndDiscardCards</c> (CardEffectCommons.cs:1408, verbatim): draw N, then the
    /// trash player discards up to M chosen hand cards.</summary>
    public static async Task DrawAndDiscardCards(
        (HeadlessPlayerId drawPlayer, HeadlessPlayerId trashPlayer) player,
        int drawAmount, int trashAmount, CardSource sourceCard,
        Func<CardSource, bool>? canTrashTargetCondition = null,
        bool canNoSelect = false, bool canEndNotMax = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var sink = NewSink(context);
        if (drawAmount > 0)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DrawCardsKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.PlayerIdKey] = player.drawPlayer.Value,
                    [MatchStateMutationSink.CountKey] = drawAmount,
                }));
            await sink.FlushAsync().ConfigureAwait(false);
        }

        var zones = (IZoneStateReader)context.ZoneMover;
        List<ChoiceCandidate> candidates = zones.GetCards(player.trashPlayer, ChoiceZone.Hand)
            .Where(id => canTrashTargetCondition is null || canTrashTargetCondition(new CardSource(context, id, player.trashPlayer, player.trashPlayer)))
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true, ownerId: player.trashPlayer))
            .ToList();
        int max = Math.Min(trashAmount, candidates.Count);
        if (max <= 0)
        {
            return;
        }

        var request = new ChoiceRequest(
            ChoiceType.Card, player.trashPlayer, $"Discard {max} card(s).",
            minCount: canNoSelect ? 0 : (canEndNotMax ? 1 : max), maxCount: max,
            canSkip: canNoSelect, ChoiceZone.Hand, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.SelectedIds.Count == 0)
        {
            return;
        }

        var discardSink = NewSink(context);
        foreach (HeadlessEntityId id in result.SelectedIds)
        {
            discardSink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashCardKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
        }

        await discardSink.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>ReturnRevealedCardsToLibraryBottom</c> (RevealLibrary.cs:469, verbatim): one card
    /// goes straight to the bottom; two-plus open the AS-IS ordering pick (pick order = placement,
    /// lower numbers on top of the bottom stack).</summary>
    public static async Task ReturnRevealedCardsToLibraryBottom(
        IReadOnlyList<CardSource> remainingCards, CardSource sourceCard, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remainingCards);
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (remainingCards.Count == 0)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        IReadOnlyList<CardSource> ordered = remainingCards;
        if (remainingCards.Count >= 2)
        {
            var request = new ChoiceRequest(
                ChoiceType.Card, sourceCard.Owner, "Specify the order to place the cards at the bottom of the deck.",
                minCount: remainingCards.Count, maxCount: remainingCards.Count, canSkip: false, ChoiceZone.Library,
                remainingCards.Select(cs => new ChoiceCandidate(cs.InstanceId, cs.InstanceId.Value, ChoiceZone.Library, IsSelectable: true, ownerId: cs.Owner)).ToArray());
            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.SelectedIds.Count == remainingCards.Count)
            {
                ordered = result.SelectedIds
                    .Select(id => remainingCards.First(cs => cs.InstanceId == id))
                    .ToArray();
            }
        }

        var sink = NewSink(context);
        foreach (CardSource cs in ordered)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToDeckBottomKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>DigivolveIntoExcecutingAreaCard</c> (CardEffectCommons.cs:1106, verbatim —
    /// original spelling kept): the EXECUTION-zone variant of <see cref="DigivolveIntoHandOrTrashCard"/>;
    /// with a single candidate the effect's own card digivolves without a pick.</summary>
    public static async Task DigivolveIntoExcecutingAreaCard(
        Permanent? targetPermanent,
        Func<CardSource, bool>? cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        CardSource sourceCard,
        Func<Task>? successProcess,
        bool ignoreSelection = false,
        bool ignoreRequirements = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        bool onlySelf = ignoreSelection ||
            (context.ZoneMover is IZoneStateReader z &&
             z.GetCards(sourceCard.Owner, ChoiceZone.Execution).Count(id =>
                new CardSource(context, id, sourceCard.Owner, sourceCard.Owner).IsDigimon &&
                (cardCondition is null || cardCondition(new CardSource(context, id, sourceCard.Owner, sourceCard.Owner)))) <= 1);
        await DigivolveIntoZoneCoreAsync(
            targetPermanent, cardCondition, payCost, reduceCostTuple, fixedCostTuple,
            ignoreDigivolutionRequirementFixedCost, ChoiceZone.Execution, sourceCard,
            successProcess, failedProcess: null, ignoreSelection: onlySelf, ignoreRequirements, isOptional: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>ActivateMainOfOptionSide</c> (CardEffectCommons.cs:733): re-run the card's [Main]
    /// (OptionSkill) activated effect — headless the activation resolver drives it.</summary>
    public static Task<int> ActivateMainOfOptionSide(CardSource card, CardSource sourceCard, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ActivatedEffectResolver.ResolveAsync(card.Context, card.InstanceId, card.Owner, EffectTiming.OptionSkill, cancellationToken);
    }

    /// <summary>AS-IS <c>DNADigivolveWithHandOrTrashCardIntoHandOrTrash</c> (DNADigivolveEffects.cs:256)
    /// — plays a TEMPORARY permanent from hand/trash as one DNA material mid-flow (PlayTempPermanent +
    /// rollback). That transient-permanent machinery has no headless surface. STOP.</summary>
    public static Task DNADigivolveWithHandOrTrashCardIntoHandOrTrash(CardSource sourceCard) =>
        throw new NotSupportedException("DNA-with-temporary-material is not modeled — STOP (strong model).");

    /// <summary>AS-IS <c>AddEffectToPermanent(targetPermanent, effectDuration, card, cardEffect, timing)</c>
    /// (GiveEffect/GiveEffectToPermanentOrPlayer.cs:11, verbatim verified): register ANY ICardEffect on the
    /// target with a duration. The AS-IS owner-relative bucket swap (an "UntilOpponentTurnEnd" grant lands
    /// in the bucket that expires at the SOURCE owner's opponent's turn end regardless of the target's
    /// owner) is absorbed by the port's controller-relative duration expiry (proved in G9-067). The binding
    /// is re-registered with the duration tag and re-targeted at the permanent.</summary>
    public static void AddEffectToPermanent(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardEffect);
        _ = timing;   // the AS-IS timing selects the getCardEffect wrapper; headless bindings self-describe.
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return;
        }

        EffectBinding binding = cardEffect.ToBinding(
            $"{card.InstanceId.Value}:addEffect:{targetPermanent.InstanceId.Value}:{Guid.NewGuid():N}");
        var retargeted = new EffectContext(
            binding.Request.Context.SourcePlayerId,
            binding.Request.Context.OwnerPlayerId,
            binding.Request.Context.SourceEntityId,
            binding.Request.Context.TriggerEntityId,
            targetEntityIds: new[] { targetPermanent.InstanceId },
            values: binding.Request.Context.Values);
        card.Context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(binding.Request.EffectId, binding.Request.ControllerId, binding.Request.Timing, retargeted),
            binding.Keywords, binding.QueryRoles, binding.QueryScopes, binding.Effect, effectDuration));
    }

    /// <summary>(PRIM-P0 B.O.5-tail) AS-IS temp <c>AddEffectToPermanent</c> for a SELF-[On Deletion] grant — the
    /// nested effect must fire ON the target's OWN removal (e.g. EX8_059 "1 Digimon gains '[On Deletion] ...'
    /// until end of turn"). Same as <see cref="AddEffectToPermanent"/> but stamps the binding SurviveOwnLeave (so
    /// leave-play cleanup does not drop it before OnDeletion resolves) + DelayedOneShot (removed after it fires),
    /// with the <paramref name="effectDuration"/> as the backstop for a non-deletion departure. The nested effect
    /// should be built with the TARGET's CardSource and self-gate on the deletion subject (TriggerEntityId).</summary>
    public static void AddSelfRemovalEffectToPermanent(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardEffect);
        _ = timing;
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return;
        }

        EffectBinding binding = cardEffect.ToBinding(
            $"{card.InstanceId.Value}:addSelfRemovalEffect:{targetPermanent.InstanceId.Value}:{Guid.NewGuid():N}");
        var values = new Dictionary<string, object?>(binding.Request.Context.Values, StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.SurviveOwnLeaveKey] = true,
            [AutoProcessingTriggerCollector.DelayedOneShotKey] = true,
        };
        var retargeted = new EffectContext(
            binding.Request.Context.SourcePlayerId,
            binding.Request.Context.OwnerPlayerId,
            binding.Request.Context.SourceEntityId,
            binding.Request.Context.TriggerEntityId,
            targetEntityIds: new[] { targetPermanent.InstanceId },
            values: values);
        card.Context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(binding.Request.EffectId, binding.Request.ControllerId, binding.Request.Timing, retargeted),
            binding.Keywords, binding.QueryRoles, binding.QueryScopes, binding.Effect, effectDuration));
    }

    /// <summary>AS-IS <c>AddEffectToPlayer(effectDuration, card, cardEffect, timing, getCardEffect)</c>
    /// (GiveEffect/GiveEffectToPermanentOrPlayer.cs:57): register ANY ICardEffect at PLAYER scope with a
    /// duration (the AS-IS player duration buckets).</summary>
    public static void AddEffectToPlayer(
        EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardEffect);
        // (PRIM-P0 B.O.5) AS-IS AddEffectToPlayer registers a delayed effect that fires ONCE at `timing` then is
        // cleared (fire-then-clear). `timing` is carried by the delayed effect's own binding (e.g. a
        // TriggeredMemoryEffect(OnEndTurn)). We register it with duration=null so it survives the headless
        // turn-end expiry race (CLEAR-then-FIRE at MetadataActionProcessor), and stamp DelayedOneShotKey so
        // GameFlowProcessor removes the binding after it resolves — giving the single-fire AS-IS semantics.
        _ = effectDuration;
        _ = timing;
        EffectBinding binding = cardEffect.ToBinding(
            $"{card.InstanceId.Value}:addPlayerEffect:{Guid.NewGuid():N}");
        EffectContext ctx = binding.Request.Context;
        var mergedValues = new Dictionary<string, object?>(ctx.Values, StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.DelayedOneShotKey] = true,
        };
        var oneShotRequest = new EffectRequest(
            binding.Request.EffectId, binding.Request.ControllerId, binding.Request.Timing,
            new EffectContext(ctx.SourcePlayerId, ctx.OwnerPlayerId, ctx.SourceEntityId, ctx.TriggerEntityId, ctx.TargetEntityIds, mergedValues));
        card.Context.EffectRegistry.Register(new EffectBinding(
            oneShotRequest, binding.Keywords, binding.QueryRoles, binding.QueryScopes, binding.Effect, duration: null));
    }

    /// <summary>(W6-G) shared restriction-grant core — AS-IS GiveEffectToPermanent shape: target-locked,
    /// duration-tagged restriction binding with the LIVE CanUse (on field && !CanNotBeAffected) plus an
    /// optional counterpart predicate (attackerCondition / defenderCondition) evaluated by the gates.</summary>
    private static bool GainRestrictionToPermanent(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard,
        string restrictionKey, string gainName,
        Func<Permanent, bool>? counterpartCondition = null,
        Func<bool>? extraCondition = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return false;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId targetId = targetPermanent.InstanceId;
        HeadlessPlayerId targetOwner = targetPermanent.OwnerId;
        var zones = (IZoneStateReader)context.ZoneMover;
        if (!zones.GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId))
        {
            return false;
        }

        if (ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, sourceCard.InstanceId, context))
        {
            return false;
        }

        HeadlessEntityId grantSourceId = sourceCard.InstanceId;
        Func<bool> liveCondition = () =>
            ((IZoneStateReader)context.ZoneMover).GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId)
            && !ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, targetId, grantSourceId, context)
            && (extraCondition is null || extraCondition());
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [restrictionKey] = true,
            [ContinuousSelfModifierEffect.ConditionKey] = liveCondition,
        };
        if (counterpartCondition is not null)
        {
            // Adapt the AS-IS Permanent predicate to the gates' CardSource counterpart idiom.
            Func<CardSource, bool> counterpartPredicate = cs =>
                counterpartCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner));
            string predicateKey = restrictionKey == RestrictionHelpers.CannotAttackKey
                ? RestrictionHelpers.DefenderPredicateKey       // FR-P3 pre-existing key for CannotAttack
                : RestrictionHelpers.CounterpartPredicateKey;   // (W6-G) Block/BeAttacked/BeBlocked
            values[predicateKey] = counterpartPredicate;
        }

        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: new[] { targetId }, values: values);
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:{gainName}:{targetId.Value}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope },
            effect: null, duration: effectDuration));
        return true;
    }

    /// <summary>AS-IS <c>GainCanNotAttack</c> (GiveEffect/GiveEffectToPermanent/CanNotAttack.cs:10) —
    /// <paramref name="defenderCondition"/> narrows WHICH defenders this permanent cannot attack.</summary>
    public static bool GainCanNotAttack(
        Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotAttackKey, "gainCanNotAttack", defenderCondition);

    /// <summary>AS-IS <c>GainCanNotBlock</c> (…/CanNotBlock.cs:10) — <paramref name="attackerCondition"/>
    /// narrows WHICH attackers this permanent cannot block.</summary>
    public static bool GainCanNotBlock(
        Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotBlockKey, "gainCanNotBlock", attackerCondition);

    /// <summary>AS-IS <c>GainCanNotBeAttacked</c> (…/CanNotBeAttacked.cs:10).</summary>
    public static bool GainCanNotBeAttacked(
        Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be attacked") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotBeAttackedKey, "gainCanNotBeAttacked", attackerCondition);

    /// <summary>AS-IS <c>GainCanNotBeBlocked</c> (…/CanNotBeBlocked.cs:10).</summary>
    public static bool GainCanNotBeBlocked(
        Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be blocked") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotBeBlockedKey, "gainCanNotBeBlocked", defenderCondition);

    /// <summary>AS-IS <c>GainCanNotSuspend</c> (…/CanNotSuspend.cs:34).</summary>
    public static bool GainCanNotSuspend(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard,
        Func<bool>? condition = null, string effectName = "Can't suspend") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotSuspendKey, "gainCanNotSuspend", extraCondition: condition);

    /// <summary>AS-IS <c>GainCantSuspendUntilOpponentTurnEnd</c> (…/CanNotSuspend.cs:8).</summary>
    public static bool GainCantSuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard) =>
        GainCanNotSuspend(targetPermanent, EffectDuration.UntilOpponentTurnEnd, sourceCard);

    /// <summary>AS-IS <c>GainCanNotUnsuspend</c> (…/CanNotUnsuspend.cs:69).</summary>
    public static bool GainCanNotUnsuspend(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard,
        Func<bool>? condition = null, string effectName = "Can't unsuspend") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotUnsuspendKey, "gainCanNotUnsuspend", extraCondition: condition);

    /// <summary>AS-IS <c>GainCantUnsuspendUntilOpponentTurnEnd</c> (…/CanNotUnsuspend.cs:45).</summary>
    public static bool GainCantUnsuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard) =>
        GainCanNotUnsuspend(targetPermanent, EffectDuration.UntilOpponentTurnEnd, sourceCard);

    /// <summary>AS-IS <c>GainCantUnsuspendNextActivePhase</c> (…/CanNotUnsuspend.cs:10) — the AS-IS CanUse
    /// ("opponent turn AND active phase") is equivalent headless: the CannotUnsuspend gate is only
    /// consulted BY the unsuspend step, and <see cref="EffectDuration.UntilNextUntap"/> expires the grant
    /// right after that step.</summary>
    public static bool GainCantUnsuspendNextActivePhase(Permanent? targetPermanent, CardSource sourceCard) =>
        GainCanNotUnsuspend(targetPermanent, EffectDuration.UntilNextUntap, sourceCard);

    /// <summary>(W6 tail) shared PLAYER-SCOPE timed grant core — the AS-IS GiveEffectToPlayer shape
    /// (verbatim verified): a duration-tagged player-scope binding whose PermanentCondition folds the
    /// battle-area + live !CanNotBeAffected guards around the caller's predicate.</summary>
    private static bool GainToPlayerScope(
        EffectDuration effectDuration, CardSource sourceCard, string gainName,
        Func<Permanent, bool>? permanentCondition,
        string? keyword = null, string? valueKey = null, object? value = null,
        IReadOnlyDictionary<string, object?>? extraValues = null,
        Func<bool>? extraCondition = null,
        string? scopeOverride = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        HeadlessEntityId grantSourceId = sourceCard.InstanceId;

        // AS-IS _PermanentCondition: on the battle area && !CanNotBeAffected && caller predicate — LIVE.
        Func<CardSource, bool> scopePredicate = cs =>
            !ContinuousImmunityGate.BlocksOpponentEffect(
                context.EffectRegistry, context.CardInstanceRepository, cs.InstanceId, grantSourceId, context)
            && (permanentCondition is null || permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)));

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Headless.Effects.PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [Headless.Effects.PlayerScopeContinuousHelpers.ScopePlayerIdKey] = sourceCard.Owner.Value,
            [Headless.Effects.PlayerScopeContinuousHelpers.ScopePredicateKey] = scopePredicate,
        };
        if (valueKey is not null)
        {
            values[valueKey] = value;
        }

        if (extraValues is not null)
        {
            foreach (KeyValuePair<string, object?> pair in extraValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        if (extraCondition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = extraCondition;
        }

        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        string[]? scopes = keyword is not null ? null : new[] { scopeOverride ?? ContinuousRestrictionGate.Scope };
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:{gainName}:{Guid.NewGuid():N}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: keyword is null ? null : new[] { keyword },
            EffectQueryRole.Continuous, scopes, effect: null, duration: effectDuration));
        return true;
    }

    /// <summary>AS-IS <c>GainBlockerPlayerEffect</c> (KeyWordEffects/Blocker.cs:46, verbatim verified).</summary>
    public static bool GainBlockerPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard) =>
        GainToPlayerScope(effectDuration, sourceCard, "gainBlockerPlayer", permanentCondition, keyword: ContinuousKeywordGate.Blocker);

    /// <summary>AS-IS <c>GainRushPlayerEffect</c> (KeyWordEffects/Rush.cs:46).</summary>
    public static bool GainRushPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard) =>
        GainToPlayerScope(effectDuration, sourceCard, "gainRushPlayer", permanentCondition, keyword: ContinuousKeywordGate.Rush);

    /// <summary>AS-IS <c>GainAlliancePlayerEffect</c> (KeyWordEffects/Alliance.cs:180).</summary>
    public static bool GainAlliancePlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard) =>
        GainToPlayerScope(effectDuration, sourceCard, "gainAlliancePlayer", permanentCondition, keyword: ContinuousKeywordGate.Alliance);

    /// <summary>AS-IS <c>GainIcecladPlayerEffect</c> (KeyWordEffects/Iceclad.cs:46).</summary>
    public static bool GainIcecladPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard) =>
        GainToPlayerScope(effectDuration, sourceCard, "gainIcecladPlayer", permanentCondition, keyword: ContinuousKeywordGate.Iceclad);

    /// <summary>AS-IS <c>GainCanNotUnsuspendPlayerEffect</c> (GiveEffectToPlayer/CanNotUnsuspend.cs:10,
    /// verbatim): <paramref name="isOnlyActivePhase"/> narrows to the turn player's permanents — headless
    /// the unsuspend gate is only consulted BY the unsuspend step, so the phase half is equivalent; the
    /// turn-player half rides the predicate.</summary>
    public static bool GainCanNotUnsuspendPlayerEffect(
        Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard,
        bool isOnlyActivePhase = false, string effectName = "Can't unsuspend")
    {
        EngineContext context = sourceCard.Context;
        Func<Permanent, bool> composed = p =>
            (permanentCondition is null || permanentCondition(p))
            && (!isOnlyActivePhase || context.TurnController.Current.TurnPlayerId == p.OwnerId);
        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotUnsuspendPlayer", composed,
            valueKey: RestrictionHelpers.CannotUnsuspendKey, value: true);
    }

    /// <summary>AS-IS <c>GainCanNotSuspendPlayerEffect</c> (GiveEffectToPlayer/CanNotSuspend.cs:10).</summary>
    public static bool GainCanNotSuspendPlayerEffect(
        Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard,
        bool isOnlyActivePhase = false, string effectName = "Can't suspend")
    {
        EngineContext context = sourceCard.Context;
        Func<Permanent, bool> composed = p =>
            (permanentCondition is null || permanentCondition(p))
            && (!isOnlyActivePhase || context.TurnController.Current.TurnPlayerId == p.OwnerId);
        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotSuspendPlayer", composed,
            valueKey: RestrictionHelpers.CannotSuspendKey, value: true);
    }

    /// <summary>AS-IS <c>GainCanNotAttackPlayerEffect</c> (GiveEffectToPlayer/CanNotAttack.cs:10, verbatim):
    /// the ATTACKER filter rides the scope predicate; the DEFENDER filter rides the pair-gate key.</summary>
    public static bool GainCanNotAttackPlayerEffect(
        Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack")
    {
        var extra = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (defenderCondition is not null)
        {
            extra[RestrictionHelpers.DefenderPredicateKey] =
                (Func<CardSource, bool>)(cs => defenderCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)));
        }

        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotAttackPlayer", attackerCondition,
            valueKey: RestrictionHelpers.CannotAttackKey, value: true, extraValues: extra);
    }

    /// <summary>AS-IS <c>GainCanNotBlockPlayerEffect</c> (GiveEffectToPlayer/CanNotBlock.cs:10).</summary>
    public static bool GainCanNotBlockPlayerEffect(
        Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block")
    {
        // AS-IS naming quirk: the SUBJECT filter arrives as attackerCondition; the counterpart (the
        // attacker being blocked) as defenderCondition. The gate reads the counterpart predicate.
        var extra = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (defenderCondition is not null)
        {
            extra[RestrictionHelpers.CounterpartPredicateKey] =
                (Func<CardSource, bool>)(cs => defenderCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)));
        }

        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotBlockPlayer", attackerCondition,
            valueKey: RestrictionHelpers.CannotBlockKey, value: true, extraValues: extra);
    }

    /// <summary>AS-IS <c>GainCanNotBeDeletedPlayerEffect</c> (GiveEffectToPlayer/CanNotBeDeletedByBattle.cs:10)
    /// — the BATTLE-deletion immunity, player-scoped, with the 4-arg battle predicate.</summary>
    public static bool GainCanNotBeDeletedPlayerEffect(
        Func<Permanent, bool>? permanentCondition,
        Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted in battle")
    {
        var extra = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (canNotBeDestroyedByBattleCondition is not null)
        {
            extra[BattleDeletionGate.BattleConditionKey] = canNotBeDestroyedByBattleCondition;
        }

        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotBeDeletedPlayer", permanentCondition,
            valueKey: BattleDeletionGate.PreventBattleDeletionKey, value: true, extraValues: extra);
    }

    /// <summary>AS-IS <c>GainCanNotReturnToHand</c> (GiveEffectToPermanent/CanNotReturnToHand.cs:10) — the
    /// causing-effect predicate maps to the source-card predicate the return gate evaluates.</summary>
    public static bool GainCanNotReturnToHand(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotReturnToHandKey, "gainCanNotReturnToHand");

    /// <summary>AS-IS <c>GainCanNotReturnToDeck</c> (…/CanNoReturnToDeck.cs:10).</summary>
    public static bool GainCanNotReturnToDeck(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotReturnToDeckKey, "gainCanNotReturnToDeck");

    /// <summary>AS-IS <c>GainCanNotReturnToHandPlayerEffect</c> (GiveEffectToPlayer/CanNotReturnToHand.cs:10).</summary>
    public static bool GainCanNotReturnToHandPlayerEffect(
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand") =>
        GainToPlayerScope(effectDuration, sourceCard, "gainCanNotReturnToHandPlayer", permanentCondition,
            valueKey: RestrictionHelpers.CannotReturnToHandKey, value: true);

    /// <summary>AS-IS <c>GainCanNotReturnToDeckPlayerEffect</c> (GiveEffectToPlayer/CanNoReturnToDeck.cs:10).</summary>
    public static bool GainCanNotReturnToDeckPlayerEffect(
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck") =>
        GainToPlayerScope(effectDuration, sourceCard, "gainCanNotReturnToDeckPlayer", permanentCondition,
            valueKey: RestrictionHelpers.CannotReturnToDeckKey, value: true);

    /// <summary>AS-IS <c>GainImmuneFromDPMinus</c> (GiveEffectToPermanent/ImmuneFromDPMinus.cs:10):
    /// this permanent ignores DP-minus effects for the duration.</summary>
    public static bool GainImmuneFromDPMinus(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            ReplacementHelpers.ImmuneFromDpMinusKey, "gainImmuneFromDpMinus");

    /// <summary>AS-IS <c>GainImmuneFromDPMinusPlayerEffect</c> (GiveEffectToPlayer/ImmuneFromDPMinus.cs:10).</summary>
    public static bool GainImmuneFromDPMinusPlayerEffect(
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus") =>
        GainToPlayerScope(effectDuration, sourceCard, "gainImmuneFromDpMinusPlayer", permanentCondition,
            valueKey: ReplacementHelpers.ImmuneFromDpMinusKey, value: true);

    /// <summary>AS-IS <c>GainCanNotBeDeletedByEffect</c> (GiveEffectToPermanent/CanNotBeDeletedByEffect.cs:10)
    /// — skill/effect-deletion immunity for the duration (the effect-delete gate's key).</summary>
    public static bool GainCanNotBeDeletedByEffect(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted by effects") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotBeDeletedBySkillKey, "gainCanNotBeDeletedByEffect");

    /// <summary>AS-IS <c>ChangeDigimonSAttackPlayerEffect</c> (GiveEffectToPlayer/ChangeSAttack.cs:10).</summary>
    public static bool ChangeDigimonSAttackPlayerEffect(
        Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)
    {
        if (changeValue == 0)
        {
            return false;
        }

        var extra = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModifierHelpers.SecurityAttackDeltaKey] = changeValue,
        };
        return GainToPlayerScope(effectDuration, sourceCard, "changeSAttackPlayer", permanentCondition,
            extraValues: extra, scopeOverride: ContinuousModifierGate.Scope);
    }

    /// <summary>AS-IS <c>ChangePlayCostPlayerEffect</c> (GiveEffectToPlayer/ChangePlayCost.cs:11) —
    /// duration-tagged play-cost modifier over the matching permanents' cards. The AS-IS
    /// <c>setFixedCost</c> form pins the cost instead of shifting it.</summary>
    public static bool ChangePlayCostPlayerEffect(
        Func<Permanent, bool>? permanentCondition, int changeValue, bool setFixedCost,
        EffectDuration effectDuration, CardSource sourceCard)
    {
        if (changeValue == 0)
        {
            return false;
        }

        var extra = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [setFixedCost ? PlayCostHelpers.FixedPlayCostKey : ModifierHelpers.PlayCostDeltaKey] = changeValue,
        };
        return GainToPlayerScope(effectDuration, sourceCard, "changePlayCostPlayer", permanentCondition,
            extraValues: extra, scopeOverride: ContinuousModifierGate.Scope);
    }

    /// <summary>AS-IS <c>ChangeBaseDigimonDP</c> (GiveEffectToPermanent/ChangeOriginDP.cs:10, verbatim):
    /// SET the target's base DP to <paramref name="changeValue"/> for the duration (a base-DP override,
    /// not a delta).</summary>
    public static bool ChangeBaseDigimonDP(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard)
    {
        if (changeValue < 0 || targetPermanent is null)
        {
            return false;
        }

        int delta = changeValue - targetPermanent.BaseDP;
        return ChangeDigimonStat(targetPermanent, delta == 0 ? 0 : delta, effectDuration, sourceCard,
            ModifierHelpers.BaseDpDeltaKey, "changeBaseDp") || delta == 0;
    }

    /// <summary>AS-IS <c>GainBlocker</c> (KeyWordEffects/Blocker.cs:10).</summary>
    public static bool GainBlocker(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Blocker, "gainBlocker");

    /// <summary>AS-IS <c>GainRush</c> (KeyWordEffects/Rush.cs:10).</summary>
    public static bool GainRush(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Rush, "gainRush");

    /// <summary>AS-IS <c>GainPierce</c> (KeyWordEffects/Pierce.cs:54).</summary>
    public static bool GainPierce(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Piercing, "gainPierce");

    /// <summary>AS-IS <c>GainRetaliation</c> (KeyWordEffects/Retaliation.cs:136).</summary>
    public static bool GainRetaliation(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Retaliation, "gainRetaliation");

    /// <summary>AS-IS <c>GainCollision</c> (KeyWordEffects/Collision.cs:10).</summary>
    public static bool GainCollision(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Collision, "gainCollision");

    /// <summary>AS-IS <c>GainJamming</c> (KeyWordEffects/Jamming.cs:10).</summary>
    public static bool GainJamming(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Jamming, "gainJamming");

    /// <summary>AS-IS <c>GainReboot</c> (KeyWordEffects/Reboot.cs:10).</summary>
    public static bool GainReboot(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Reboot, "gainReboot");

    /// <summary>AS-IS <c>GainAlliance</c> (KeyWordEffects/Alliance.cs:136).</summary>
    public static bool GainAlliance(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Alliance, "gainAlliance");

    /// <summary>AS-IS <c>GainEvade</c> (KeyWordEffects/Evade.cs:53).</summary>
    public static bool GainEvade(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Evade, "gainEvade");

    /// <summary>AS-IS <c>GainRaid</c> (KeyWordEffects/Raid.cs:81).</summary>
    public static bool GainRaid(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Raid, "gainRaid");

    /// <summary>AS-IS <c>GainVortex</c> (KeyWordEffects/Vortex.cs:81).</summary>
    public static bool GainVortex(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Vortex, "gainVortex");

    /// <summary>AS-IS <c>GainExecute</c> (KeyWordEffects/Execute.cs:103).</summary>
    public static bool GainExecute(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Execute, "gainExecute");

    /// <summary>AS-IS <c>GainFortitude</c> (KeyWordEffects/Fortitude.cs:67).</summary>
    public static bool GainFortitude(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Fortitude, "gainFortitude");

    /// <summary>AS-IS <c>GainIceclad</c> (KeyWordEffects/Iceclad.cs:10).</summary>
    public static bool GainIceclad(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Iceclad, "gainIceclad");

    /// <summary>AS-IS <c>GainBarrier</c> (KeyWordEffects/Barrier.cs:65).</summary>
    public static bool GainBarrier(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard) =>
        GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Barrier, "gainBarrier");

    /// <summary>AS-IS <c>GainBlitz</c> (KeyWordEffects/Blitz.cs:51) — <c>isWhenDigivolving</c> accepted for
    /// source-signature fidelity (the AS-IS flag only decides whether the Blitz prompt opens inside the
    /// digivolve flow; the headless Blitz window reads the live keyword either way).</summary>
    public static bool GainBlitz(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard, bool isWhenDigivolving = false)
    {
        _ = isWhenDigivolving;
        return GainKeywordToPermanent(targetPermanent, effectDuration, sourceCard, ContinuousKeywordGate.Blitz, "gainBlitz");
    }

    /// <summary>It is the card owner's turn.</summary>
    public static bool IsOwnerTurn(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return TurnOwnershipHelpers.IsOwnerTurn(card.Context.TurnController.Current.TurnPlayerId, card.Owner);
    }

    /// <summary>(PRIM-P0 B.O.4 #1) True when the action currently paying cost matches <paramref name="root"/>.
    /// Gate a [BeforePayCost] effect with this so it fires only for the intended action (AS-IS ChangeCostClass
    /// rootCondition), since the BeforePayCost timing is shared by play / digivolve / option.</summary>
    public static bool IsPayCostRoot(CardSource card, Headless.Bridge.PayCostRoot root)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Context.CurrentPayCostRoot == root;
    }

    /// <summary>It is the opponent's turn.</summary>
    public static bool IsOpponentTurn(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return TurnOwnershipHelpers.IsOpponentTurn(card.Context.TurnController.Current.TurnPlayerId, card.Owner);
    }

    /// <summary>The card is part of a battle-area permanent (as the top card or a buried source).</summary>
    public static bool IsExistOnBattleArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !card.PermanentOfThisCard().IsEmpty;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>IsExistOnHand</c> (<c>card.Owner.HandCards
    /// .Contains(card)</c>): this card is in its owner's hand.</summary>
    public static bool IsExistOnHand(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand).Contains(card.InstanceId);
    }

    // ===== (W6-P) predicate commons batch — 1:1 name mirrors of GameContextDeterminarion.cs et al. =====
    // Verbatim AS-IS bodies verified 2026-07-02 (primitive_w6_design.md W6-P). These let a ported card's
    // condition closures be copied literally instead of intent-translated.

    /// <summary>AS-IS <c>IsExistOnField</c> (GameContextDeterminarion.cs:117): the card is part of ANY field
    /// permanent (battle or breeding area, top or buried).</summary>
    public static bool IsExistOnField(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return IsExistOnBattleArea(card) || IsExistOnBreedingArea(card);
    }

    /// <summary>AS-IS <c>IsExistOnBreedingArea</c> (:134).</summary>
    public static bool IsExistOnBreedingArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(card.Owner, ChoiceZone.BreedingArea))
        {
            if (top == card.InstanceId)
            {
                return true;
            }

            DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, top);
            if (stack.UnderCards.Any(under => under.InstanceId == card.InstanceId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsExistOnBattleAreaDigimon</c> (:188): on the battle area AND the permanent is a
    /// Digimon.</summary>
    public static bool IsExistOnBattleAreaDigimon(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return IsExistOnBattleArea(card) && new Permanent(card.Context, (card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default), card.Owner).IsDigimon;
    }

    /// <summary>AS-IS <c>IsExistOnTrash</c> (:243).</summary>
    public static bool IsExistOnTrash(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash).Contains(card.InstanceId);
    }

    /// <summary>AS-IS <c>IsExistOnExecutingArea</c> (:277): the card is being resolved as an Option.</summary>
    public static bool IsExistOnExecutingArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Execution).Contains(card.InstanceId);
    }

    /// <summary>AS-IS <c>IsExistInSecurity</c> (:291): in the owner's security with the given face state
    /// (<c>card.IsFlipped == isFlipped</c>; headless face state = the <c>isFlipped</c> instance flag,
    /// default face-down).</summary>
    public static bool IsExistInSecurity(CardSource card, bool isFlipped = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Security).Contains(card.InstanceId))
        {
            return false;
        }

        bool flipped = card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("isFlipped", out object? raw) && raw is true;
        return flipped == isFlipped;
    }

    /// <summary>AS-IS <c>CanPlayAsNewPermanent</c> (:303): the card could be played as a NEW permanent —
    /// (Option cards only with <paramref name="isPlayOption"/>) + cost affordable when
    /// <paramref name="payCost"/>. Headless notes (documented reductions): the empty-frame check has no
    /// port model (no field-size limit is modeled anywhere) and the DigiXros/Assembly in-flight-selection
    /// locks don't apply (material choices are action parameters, not persistent state).</summary>
    public static bool CanPlayAsNewPermanent(CardSource cardSource, bool payCost, ICardEffect? cardEffect, bool isPlayOption = false, int fixedCost = -1)
    {
        _ = cardEffect;
        if (cardSource is null || (!isPlayOption && cardSource.IsOption))
        {
            return false;
        }

        if (!payCost)
        {
            return true;
        }

        int baseCost = cardSource.Context.CardInstanceRepository.TryGetInstance(cardSource.InstanceId, out CardInstanceRecord? inst) && inst is not null
            && cardSource.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
            ? def.PlayCost ?? 0
            : 0;
        int cost = fixedCost >= 0 ? fixedCost : ContinuousModifierGate.ResolvePlayCost(cardSource.Context, cardSource.InstanceId, baseCost);
        return cardSource.Context.MemoryController.CanPay(Math.Max(0, cost));
    }

    /// <summary>AS-IS <c>IsPermanentExistsOnBattleArea</c> (:348).</summary>
    public static bool IsPermanentExistsOnBattleArea(Permanent? permanent)
    {
        return permanent is not null && !permanent.InstanceId.IsEmpty
            && ((IZoneStateReader)permanent.TopCard.Context.ZoneMover)
                .GetCards(permanent.OwnerId, ChoiceZone.BattleArea).Contains(permanent.InstanceId);
    }

    /// <summary>AS-IS <c>IsOwnerPermanent</c> (:388).</summary>
    public static bool IsOwnerPermanent(Permanent? permanent, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return permanent is not null && !permanent.InstanceId.IsEmpty && permanent.OwnerId == card.Owner;
    }

    /// <summary>AS-IS <c>IsOpponentPermanent</c> (:411).</summary>
    public static bool IsOpponentPermanent(Permanent? permanent, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return permanent is not null && !permanent.InstanceId.IsEmpty && !permanent.OwnerId.IsEmpty && permanent.OwnerId != card.Owner;
    }

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBattleArea</c> (:431).</summary>
    public static bool IsPermanentExistsOnOwnerBattleArea(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnBattleArea(permanent) && IsOwnerPermanent(permanent, card);

    /// <summary>AS-IS <c>IsPermanentExistsOnOpponentBattleArea</c> (:448).</summary>
    public static bool IsPermanentExistsOnOpponentBattleArea(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnBattleArea(permanent) && IsOpponentPermanent(permanent, card);

    /// <summary>AS-IS <c>IsPermanentExistsOnBattleAreaDigimon</c> (:499).</summary>
    public static bool IsPermanentExistsOnBattleAreaDigimon(Permanent? permanent) =>
        IsPermanentExistsOnBattleArea(permanent) && permanent!.IsDigimon;

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBattleAreaDigimon</c> (:516).</summary>
    public static bool IsPermanentExistsOnOwnerBattleAreaDigimon(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOwnerBattleArea(permanent, card) && permanent!.IsDigimon;

    /// <summary>AS-IS <c>IsPermanentExistsOnOpponentBattleAreaDigimon</c> (:533).</summary>
    public static bool IsPermanentExistsOnOpponentBattleAreaDigimon(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOpponentBattleArea(permanent, card) && permanent!.IsDigimon;

    /// <summary>AS-IS <c>IsPermanentExistsOnBattleAreaTamer</c> (GameContextDeterminarion.cs:550 — the
    /// Tamer sibling of the verified Digimon trio).</summary>
    public static bool IsPermanentExistsOnBattleAreaTamer(Permanent? permanent) =>
        IsPermanentExistsOnBattleArea(permanent) && permanent!.TopCard.IsTamer;

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBattleAreaTamer</c> (:567).</summary>
    public static bool IsPermanentExistsOnOwnerBattleAreaTamer(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOwnerBattleArea(permanent, card) && permanent!.TopCard.IsTamer;

    /// <summary>AS-IS <c>IsPermanentExistsOnOpponentBattleAreaTamer</c> (:584).</summary>
    public static bool IsPermanentExistsOnOpponentBattleAreaTamer(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOpponentBattleArea(permanent, card) && permanent!.TopCard.IsTamer;

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBreedingArea</c> (the breeding sibling of the verified
    /// battle-area form).</summary>
    public static bool IsPermanentExistsOnOwnerBreedingArea(Permanent? permanent, CardSource card)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || !IsOwnerPermanent(permanent, card))
        {
            return false;
        }

        return ((IZoneStateReader)card.Context.ZoneMover)
            .GetCards(permanent.OwnerId, ChoiceZone.BreedingArea).Contains(permanent.InstanceId);
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersSecurity</c>: any of the owner's security cards passes.</summary>
    public static bool HasMatchConditionOwnersSecurity(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Security)
            .Any(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>IsExistOnBreedingAreaDigimon</c> (GameContextDeterminarion.cs:151).</summary>
    public static bool IsExistOnBreedingAreaDigimon(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!IsExistOnBreedingArea(card))
        {
            return false;
        }

        foreach (HeadlessEntityId top in ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.BreedingArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, top);
            if ((top == card.InstanceId || stack.UnderCards.Any(u => u.InstanceId == card.InstanceId)) &&
                new Permanent(card.Context, top, card.Owner).IsDigimon)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsExistDigivolutionCards</c> (:219): this card rides a field permanent as a
    /// digivolution source (not the top).</summary>
    public static bool IsExistDigivolutionCards(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        PermanentView host = card.PermanentOfThisCard();
        return !host.IsEmpty && host.DigivolutionCards.Any(u => u.InstanceId == card.InstanceId);
    }

    /// <summary>AS-IS <c>IsExistLinked</c> (:231): this card is one of a field permanent's LINK cards.</summary>
    public static bool IsExistLinked(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(card.Owner, ChoiceZone.BattleArea))
        {
            if (card.Context.CardInstanceRepository.TryGetInstance(top, out CardInstanceRecord? host) && host is not null &&
                Headless.Runtime.LinkHelpers.ReadLinkedCardIds(host.Metadata).Contains(card.InstanceId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsExistInAnyTrash</c> (:257).</summary>
    public static bool IsExistInAnyTrash(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (!player.IsEmpty && zones.GetCards(player, ChoiceZone.Trash).Contains(card.InstanceId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsPermanentExistsOnField</c> (:323): breeding OR battle area.</summary>
    public static bool IsPermanentExistsOnField(Permanent? permanent) =>
        IsPermanentExistsOnBattleArea(permanent) || IsPermanentExistsOnBreedingArea(permanent);

    /// <summary>AS-IS <c>IsPermanentExistsOnBreedingArea</c> (:368) — the unary form.</summary>
    public static bool IsPermanentExistsOnBreedingArea(Permanent? permanent)
    {
        return permanent is not null && !permanent.InstanceId.IsEmpty
            && ((IZoneStateReader)permanent.TopCard.Context.ZoneMover)
                .GetCards(permanent.OwnerId, ChoiceZone.BreedingArea).Contains(permanent.InstanceId);
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersBreedingPermanent</c> (:693).</summary>
    public static bool HasMatchConditionOwnersBreedingPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.BreedingArea)
            .Select(id => new Permanent(card.Context, id, card.Owner))
            .Any(CanSelectPermanentCondition);
    }

    /// <summary>AS-IS <c>HasMatchConditionPermanentDigivolutionCards</c> (:705): any of THIS card's
    /// permanent's digivolution sources passes.</summary>
    public static bool HasMatchConditionPermanentDigivolutionCards(CardSource card, Func<CardSource, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return card.PermanentOfThisCard().DigivolutionCards
            .Any(u => CanSelectPermanentCondition(new CardSource(card.Context, u.InstanceId, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>MatchConditionOpponentsCardCountInTrash</c> (:747).</summary>
    public static int MatchConditionOpponentsCardCountInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        HeadlessPlayerId opponent = OpponentOf(card);
        return opponent.IsEmpty
            ? 0
            : ((IZoneStateReader)card.Context.ZoneMover).GetCards(opponent, ChoiceZone.Trash)
                .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, opponent, opponent)));
    }

    /// <summary>AS-IS <c>HasMatchConditionOpponentsCardInTrash</c> (:765).</summary>
    public static bool HasMatchConditionOpponentsCardInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition) =>
        MatchConditionOpponentsCardCountInTrash(card, CanSelectCardCondition) >= 1;

    /// <summary>AS-IS <c>GetUniqueColourCountOnOwnerBattleArea</c> (:828).</summary>
    public static int GetUniqueColourCountOnOwnerBattleArea(CardSource card, Func<Permanent, bool> canGetCardColour) =>
        UniqueColourCount(card, card.Owner, canGetCardColour);

    /// <summary>AS-IS <c>GetUniqueColourCountOnOpponentsBattleArea</c> (:843).</summary>
    public static int GetUniqueColourCountOnOpponentsBattleArea(CardSource card, Func<Permanent, bool> canGetCardColour) =>
        UniqueColourCount(card, OpponentOf(card), canGetCardColour);

    private static int UniqueColourCount(CardSource card, HeadlessPlayerId player, Func<Permanent, bool> canGetCardColour)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canGetCardColour);
        if (player.IsEmpty)
        {
            return 0;
        }

        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(player, ChoiceZone.BattleArea)
            .Select(id => new Permanent(card.Context, id, player))
            .Where(canGetCardColour)
            .SelectMany(p => p.TopCard.CardColors)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    /// <summary>AS-IS <c>IsMinCost</c> (MinMax_DP_Cost_Level/Cost/IsMinCost.cs, verbatim verified): among
    /// the owner's battle-area Digimon (or Digimon+Tamer), this permanent's PRINTED play cost is minimal.</summary>
    public static bool IsMinCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly, Func<Permanent, bool>? condition = null) =>
        IsCostExtremum(permanent, owner, IsDigimonOnly, condition, min: true);

    /// <summary>AS-IS <c>IsMaxCost</c> (…/IsMaxCost.cs).</summary>
    public static bool IsMaxCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly) =>
        IsCostExtremum(permanent, owner, IsDigimonOnly, condition: null, min: false);

    private static bool IsCostExtremum(Permanent? permanent, HeadlessPlayerId owner, bool digimonOnly, Func<Permanent, bool>? condition, bool min)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleArea(permanent) ||
            (!permanent.IsDigimon && !IsTamerPermanent(permanent)) ||
            (condition is not null && !condition(permanent)) ||
            !permanent.TopCard.HasPlayCost ||
            (digimonOnly && !permanent.IsDigimon))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> costs = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => digimonOnly ? p.IsDigimon : (p.IsDigimon || IsTamerPermanent(p)))
            .Where(p => p.TopCard.HasPlayCost)
            .Select(p => p.TopCard.GetCostItself)
            .ToList();
        return costs.Count >= 1 && permanent.TopCard.GetCostItself == (min ? costs.Min() : costs.Max());
    }

    /// <summary>AS-IS <c>GetNonMaxCostPermanents</c> (…/IsMaxCost.cs:36): the owner's permanents whose
    /// printed cost is BELOW the current maximum (cost-undefined ones included, per the original).</summary>
    public static List<Permanent> GetNonMaxCostPermanents(CardSource card, HeadlessPlayerId owner, bool digimonOnly = true)
    {
        ArgumentNullException.ThrowIfNull(card);
        EngineContext context = card.Context;
        List<Permanent> candidates = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => digimonOnly ? p.IsDigimon : (p.IsDigimon || IsTamerPermanent(p)))
            .ToList();
        if (candidates.Count == 0)
        {
            return new List<Permanent>();
        }

        int maxCost = candidates.Max(p => p.TopCard.HasPlayCost ? p.TopCard.GetCostItself : -1);
        return candidates.Where(p => !p.TopCard.HasPlayCost || p.TopCard.GetCostItself < maxCost).ToList();
    }

    /// <summary>AS-IS <c>IsMinDigivolutionCards</c> (…/DigivolutionCards/IsMinDigivolutionCards.cs).</summary>
    public static bool IsMinDigivolutionCards(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition = null)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, permanent.TopCard) ||
            (condition is not null && !condition(permanent)))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> counts = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && (condition is null || condition(p)))
            .Select(p => p.TopCard.PermanentOfThisCard().DigivolutionCards.Count)
            .ToList();
        return counts.Count >= 1 &&
            permanent.TopCard.PermanentOfThisCard().DigivolutionCards.Count == counts.Min();
    }

    /// <summary>AS-IS <c>IsMinLevelBoard</c> (…/Level/IsMinLevel.cs:24): min level over BOTH players'
    /// battle-area Digimon.</summary>
    public static bool IsMinLevelBoard(Permanent? permanent)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty ||
            !IsPermanentExistsOnBattleAreaDigimon(permanent) ||
            !permanent.TopCard.HasLevel)
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        var levels = new List<int>();
        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            levels.AddRange(zones.GetCards(player, ChoiceZone.BattleArea)
                .Select(id => new Permanent(context, id, player))
                .Where(p => p.IsDigimon && p.TopCard.HasLevel)
                .Select(p => p.Level));
        }

        return levels.Count >= 1 && permanent.Level == levels.Min();
    }

    /// <summary>AS-IS <c>IsBlock</c> (GetFromHashtable.cs:88): the driving event carried the block flag.</summary>
    public static bool IsBlock(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isBlock", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsFromSameDigimon</c> (:124).</summary>
    public static bool IsFromSameDigimon(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFromSameDigimon", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsFromDigimon</c> (:142).</summary>
    public static bool IsFromDigimon(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFromDigimon", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsFromDigimonDigivolutionCards</c> (:160).</summary>
    public static bool IsFromDigimonDigivolutionCards(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFromDigimonDigivolutionCards", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsLeavingForDigiXros</c> (:800).</summary>
    public static bool IsLeavingForDigiXros(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}digixros", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsDijiXros</c> (:817): this card's permanent entered via DigiXros and the material
    /// count passes.</summary>
    public static bool IsDijiXros(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<int, bool>? digixrosCountCondition)
    {
        if (!SubjectPermanentContains(ctx, card))
        {
            return false;
        }

        int count = ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}DigiXrosCount", out object? raw) && raw is int c
            ? c
            : 0;
        return digixrosCountCondition is null || digixrosCountCondition(count);
    }

    /// <summary>AS-IS <c>IsAlliance</c> (:765): the driving effect is the Alliance keyword's own window.</summary>
    public static bool IsAlliance(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isAlliance", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsDigivolvedFromSameLevelFromEnterFieldHashtable</c> (:720): the permanent
    /// digivolved without changing level (the event carries the pre-digivolve level).</summary>
    public static bool IsDigivolvedFromSameLevelFromEnterFieldHashtable(Headless.Effects.CardEffectResolveContext ctx, Permanent? permanent)
    {
        if (permanent is null || !IsPermanentExistsOnBattleArea(permanent) || !permanent.TopCard.HasLevel)
        {
            return false;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}oldLevel", out object? raw)
            && raw is int oldLevel && oldLevel == permanent.Level;
    }

    /// <summary>AS-IS <c>IsDigivolvedByTheEffect</c> (IsDigivolvedByTheEffect.cs:9): the permanent's top is
    /// this card and the digivolution was caused by the given effect source (the digivolve stamps the
    /// causing source id).</summary>
    public static bool IsDigivolvedByTheEffect(Permanent? permanent, CardSource cardSource, CardSource effectSourceCard)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        ArgumentNullException.ThrowIfNull(effectSourceCard);
        if (permanent is null || !IsPermanentExistsOnBattleArea(permanent) ||
            permanent.InstanceId != cardSource.InstanceId)
        {
            return false;
        }

        return cardSource.Context.CardInstanceRepository.TryGetInstance(cardSource.InstanceId, out CardInstanceRecord? rec) && rec is not null
            && rec.Metadata.TryGetValue("digivolvedByEffectSourceId", out object? raw)
            && raw?.ToString() == effectSourceCard.InstanceId.Value;
    }

    private static bool IsTamerPermanent(Permanent permanent) => permanent.TopCard.IsTamer;

    /// <summary>AS-IS <c>HasMatchConditionPermanent(Func&lt;Permanent,bool&gt;, isContainBreedingArea)</c> (:641)
    /// — the VIEW-predicate overload (both players' battle-area, optionally + breeding).</summary>
    public static bool HasMatchConditionPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition, bool isContainBreedingArea = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea).Any(CanSelectPermanentCondition);
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersPermanent</c> (:681).</summary>
    public static bool HasMatchConditionOwnersPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Any(p => IsOwnerPermanent(p, card) && CanSelectPermanentCondition(p));
    }

    /// <summary>AS-IS <c>MatchConditionOwnersPermanentCount</c> (:623).</summary>
    public static int MatchConditionOwnersPermanentCount(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Count(p => IsOwnerPermanent(p, card) && CanSelectPermanentCondition(p));
    }

    /// <summary>AS-IS <c>MatchConditionOpponentsPermanentCount</c> (:632).</summary>
    public static int MatchConditionOpponentsPermanentCount(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Count(p => IsOpponentPermanent(p, card) && CanSelectPermanentCondition(p));
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersHand</c> (:663).</summary>
    public static bool HasMatchConditionOwnersHand(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand)
            .Any(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>MatchConditionOwnersCardCountInHand</c> (:672).</summary>
    public static int MatchConditionOwnersCardCountInHand(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand)
            .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersCardInTrash</c> (:756).</summary>
    public static bool HasMatchConditionOwnersCardInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash)
            .Any(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>MatchConditionOwnersCardCountInTrash</c> (:738).</summary>
    public static int MatchConditionOwnersCardCountInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash)
            .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>HasNoElement</c> (:774).</summary>
    public static bool HasNoElement<T>(List<T> list) => list is null || list.Count <= 0;

    /// <summary>AS-IS <c>IsOwnerEffect</c> (:788) — headless the effect SOURCE is a CardSource (the port has
    /// no live ICardEffect.EffectSourceCard); translate <c>IsOwnerEffect(cardEffect, card)</c> as
    /// <c>IsOwnerEffect(cardEffect's source card, card)</c>.</summary>
    public static bool IsOwnerEffect(CardSource? effectSourceCard, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return effectSourceCard is not null && effectSourceCard.Owner == card.Owner;
    }

    /// <summary>AS-IS <c>IsOpponentEffect</c> (:808) — see <see cref="IsOwnerEffect"/>.</summary>
    public static bool IsOpponentEffect(CardSource? effectSourceCard, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return effectSourceCard is not null && !effectSourceCard.Owner.IsEmpty && effectSourceCard.Owner != card.Owner;
    }

    /// <summary>AS-IS <c>CanActivateSuspendCostEffect</c> (CanUseEffects/CanSuspend.cs:10-39, verbatim
    /// verified): this card's permanent is on the battle area (or, with <paramref name="includeBreeding"/>,
    /// the breeding area), UNSUSPENDED, and not suspend-locked — i.e. it could pay a "suspend this
    /// permanent" cost right now.</summary>
    public static bool CanActivateSuspendCostEffect(CardSource card, bool includeBreeding = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (IsExistOnBattleArea(card))
        {
            HeadlessEntityId top = card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default;
            if (!top.IsEmpty && !IsSuspended(card, top) &&
                !ContinuousRestrictionGate.EvaluateSuspend(card.Context, top).IsRestricted)
            {
                return true;
            }
        }

        if (includeBreeding && IsExistOnBreedingArea(card))
        {
            var zones = (IZoneStateReader)card.Context.ZoneMover;
            foreach (HeadlessEntityId hostId in zones.GetCards(card.Owner, ChoiceZone.BreedingArea))
            {
                DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, hostId);
                bool contains = hostId == card.InstanceId || stack.UnderCards.Any(under => under.InstanceId == card.InstanceId);
                if (contains && !IsSuspended(card, hostId) &&
                    !ContinuousRestrictionGate.EvaluateSuspend(card.Context, hostId).IsRestricted)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>CanDeclareOptionDelayEffect</c> (CanUseEffects/OptionEffect.cs:27): the [Delay]
    /// gate — on the battle area AND not the turn this permanent entered play.</summary>
    public static bool CanDeclareOptionDelayEffect(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!IsExistOnBattleArea(card))
        {
            return false;
        }

        HeadlessEntityId top = (card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default);
        return !(card.Context.CardInstanceRepository.TryGetInstance(top, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("enteredThisTurn", out object? raw) && raw is true);
    }

    /// <summary>AS-IS <c>CanUnsuspend(Permanent)</c> (CanUseEffects/CanUnsuspend.cs:10): suspended AND not
    /// unsuspend-locked.</summary>
    public static bool CanUnsuspend(Permanent? permanent)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty)
        {
            return false;
        }

        CardSource top = permanent.TopCard;
        return IsSuspended(top, permanent.InstanceId)
            && !ContinuousRestrictionGate.EvaluateUnsuspend(top.Context, permanent.InstanceId).IsRestricted;
    }

    /// <summary>AS-IS <c>IsMinDP</c> (MinMax_DP_Cost_Level/DP/IsMinDP.cs): among the owner's battle-area
    /// Digimon with a defined DP (printed DP or BaseDP&gt;0), this permanent's effective DP is the minimum.</summary>
    public static bool IsMinDP(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition = null) =>
        IsDpExtremum(permanent, owner, condition, min: true);

    /// <summary>AS-IS <c>IsMaxDP</c> (…/IsMaxDP.cs).</summary>
    public static bool IsMaxDP(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? permanentCondition = null) =>
        IsDpExtremum(permanent, owner, permanentCondition, min: false);

    /// <summary>AS-IS <c>IsMinLevel</c> (…/Level/IsMinLevel.cs): among the owner's battle-area Digimon with
    /// a printed level, this permanent's level is the minimum.</summary>
    public static bool IsMinLevel(Permanent? permanent, HeadlessPlayerId owner) =>
        IsLevelExtremum(permanent, owner, min: true);

    /// <summary>AS-IS <c>IsMaxLevel</c> (…/Level/IsMaxLevel.cs).</summary>
    public static bool IsMaxLevel(Permanent? permanent, HeadlessPlayerId owner) =>
        IsLevelExtremum(permanent, owner, min: false);

    private static bool IsDpExtremum(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition, bool min)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleAreaDigimon(permanent) ||
            (condition is not null && !condition(permanent)) ||
            (!permanent.TopCard.HasDP && permanent.BaseDP <= 0))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> dps = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && (condition is null || condition(p)) && (p.TopCard.HasDP || p.BaseDP > 0))
            .Select(p => p.DP)
            .ToList();
        return dps.Count >= 1 && permanent.DP == (min ? dps.Min() : dps.Max());
    }

    private static bool IsLevelExtremum(Permanent? permanent, HeadlessPlayerId owner, bool min)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleAreaDigimon(permanent) ||
            !permanent.TopCard.HasLevel)
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> levels = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && p.TopCard.HasLevel)
            .Select(p => p.Level)
            .ToList();
        return levels.Count >= 1 && permanent.Level == (min ? levels.Min() : levels.Max());
    }

    private static IEnumerable<Permanent> EnumerateFieldPermanentViews(CardSource card, bool isContainBreedingArea)
    {
        EngineContext context = card.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                yield return new Permanent(context, id, player);
            }

            if (isContainBreedingArea)
            {
                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BreedingArea))
                {
                    yield return new Permanent(context, id, player);
                }
            }
        }
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>IsSuspended</c>: <paramref name="id"/>'s permanent
    /// is currently suspended (tapped). Reads the live <c>isSuspended</c> instance-metadata flag the engine
    /// maintains on tap/unsuspend.</summary>
    public static bool IsSuspended(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !id.IsEmpty
            && card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            && instance.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>MatchConditionPermanentCount(predicate,
    /// isContainBreedingArea)</c>: the number of battle-area (optionally + breeding) permanents, across BOTH
    /// players, that satisfy <paramref name="condition"/>. The original takes a <c>Func&lt;Permanent,bool&gt;</c>;
    /// the headless uses the established entity-id predicate idiom (see <see cref="IsOpponentBattleAreaDigimon"/>),
    /// so card-side predicates compose CardEffectCommons helpers (IsSuspended, …) on the id.</summary>
    public static int MatchConditionPermanentCount(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(condition);
        int count = 0;
        foreach (HeadlessEntityId id in AllFieldPermanents(card, isContainBreedingArea))
        {
            if (condition(id))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>HasMatchConditionPermanent</c>: at least one
    /// matching permanent exists (count &gt;= 1).</summary>
    public static bool HasMatchConditionPermanent(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false) =>
        MatchConditionPermanentCount(card, condition, isContainBreedingArea) >= 1;

    /// <summary>Both players' battle-area cards (optionally + breeding-area), in turn order. Enumerates raw
    /// instance ids; the caller's predicate decides Digimon-ness / ownership / suspendability.</summary>
    private static IEnumerable<HeadlessEntityId> AllFieldPermanents(CardSource card, bool isContainBreedingArea)
    {
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                yield return id;
            }

            if (isContainBreedingArea)
            {
                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BreedingArea))
                {
                    yield return id;
                }
            }
        }
    }

    // (W6-P) the earlier owner-only simplifications of IsPermanentExistsOn(Owner|Opponent)BattleAreaDigimon
    // were replaced by the faithful full-check mirrors above (battle-area + Digimon + ownership).

    /// <summary><paramref name="id"/> is an opponent's battle-area Digimon (entity-id predicate form used
    /// by SelectPermanentEffect target conditions).</summary>
    public static bool IsOpponentBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsBattleAreaDigimon(card, id, opponent: true);

    /// <summary><paramref name="id"/> is one of the card owner's battle-area Digimon.</summary>
    public static bool IsOwnerBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsBattleAreaDigimon(card, id, opponent: false);

    /// <summary>(EX8-1) Mirror of the original <c>IsPermanentExistsOnBattleAreaDigimon(permanent)</c>:
    /// <paramref name="id"/> is a battle-area Digimon owned by EITHER player (used by "suspend 1 Digimon"
    /// targets and by the suspended-count threshold).</summary>
    public static bool IsBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsOwnerBattleAreaDigimon(card, id) || IsOpponentBattleAreaDigimon(card, id);

    private static bool IsBattleAreaDigimon(CardSource card, HeadlessEntityId id, bool opponent)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        bool isOpponentOwned = instance.OwnerId != card.Owner;
        if (isOpponentOwned != opponent)
        {
            return false;
        }

        var zones = (IZoneStateReader)card.Context.ZoneMover;
        if (!zones.GetCards(instance.OwnerId, ChoiceZone.BattleArea).Contains(id))
        {
            return false;
        }

        return card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            && def is not null
            && def.IsCardType("Digimon");
    }

    /// <summary>Resolved current DP of a battle-area card (base printed DP folded with continuous DP
    /// modifiers via <see cref="ContinuousDpGate"/>). Used by DP-threshold target predicates (e.g. ST1_15
    /// "Digimon with 4000 DP or less").</summary>
    public static int CurrentDp(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        int baseDp = 0;
        if (card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null)
        {
            baseDp = ReadDp(instance.Metadata)
                ?? (card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
                    ? ReadDp(def.Metadata) ?? 0
                    : 0);
        }

        return ContinuousDpGate.ResolveDp(card.Context, id, baseDp);
    }

    private static int? ReadDp(IReadOnlyDictionary<string, object?> metadata)
    {
        foreach (string key in new[] { "dp", "DP" })
        {
            if (metadata.TryGetValue(key, out object? raw))
            {
                if (raw is int i) return i;
                if (raw is long l) return (int)l;
                if (raw is string s && int.TryParse(s, out int p)) return p;
            }
        }

        return null;
    }

    /// <summary>Mirror of the original <c>AddActivateMainOptionSecurityEffect</c>: reuse the Option's [Main]
    /// skill from security. The security-skill activation flow is not yet ported (kept for source fidelity,
    /// not auto-registered).</summary>
    public static void AddActivateMainOptionSecurityEffect(CardSource card, ref List<ICardEffect> cardEffects, string effectName)
    {
        ArgumentNullException.ThrowIfNull(cardEffects);
        cardEffects.Add(new ReuseMainOptionEffect(effectName));
    }

    /// <summary>Mirror of the original <c>Permanent.HasNoDigivolutionCards</c> (entity-id form): the
    /// battle-area permanent topped by <paramref name="id"/> has no digivolution (under) cards.</summary>
    public static bool HasNoDigivolutionCards(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty)
        {
            return false;
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, id);
        return stack.UnderCards.Count == 0;
    }

    /// <summary>Metadata flag marking a digivolution source as protected from being trashed (mirror of the
    /// original <c>CardSource.CanNotTrashFromDigivolutionCards</c>). Stamped on the source instance.</summary>
    public const string TrashProtectedKey = "cannotTrashFromDigivolution";

    /// <summary>Query scope for the dynamic delete-DP threshold raise effects (mirror of
    /// <c>MaxDP_DeleteEffect</c>'s raise-able cap).</summary>
    public const string MaxDpDeleteScope = "DeleteThreshold";

    /// <summary>The per-player additive delta a delete-threshold-raise effect carries.</summary>
    public const string MaxDpDeleteDeltaKey = "maxDpDeleteDelta";

    /// <summary>Mirror of the original <c>card.Owner.MaxDP_DeleteEffect(baseThreshold, ...)</c>: the current
    /// delete-DP threshold for the card's owner = <paramref name="baseThreshold"/> plus any raise effects
    /// (continuous bindings scoped to <see cref="MaxDpDeleteScope"/> carrying <see cref="MaxDpDeleteDeltaKey"/>
    /// for that owner). A "delete a Digimon with N DP or less" gate compares against this, not a flat base.</summary>
    public static int MaxDpDeleteThreshold(CardSource card, int baseThreshold)
    {
        ArgumentNullException.ThrowIfNull(card);
        int total = baseThreshold;
        foreach (EffectRequest effect in card.Context.EffectRegistry.GetContinuousEffects(new EffectQueryContext(MaxDpDeleteScope)))
        {
            if (effect.Context.OwnerPlayerId == card.Owner
                && effect.Context.Values.TryGetValue(MaxDpDeleteDeltaKey, out object? raw)
                && raw is int delta)
            {
                total += delta;
            }
        }

        return total;
    }

    /// <summary>Mirror of the original target gate
    /// <c>permanent.DigivolutionCards.Count(c =&gt; !c.CanNotTrashFromDigivolutionCards(...))</c>: the number of
    /// the host permanent's digivolution (under) cards that are NOT trash-protected.</summary>
    public static int TrashableDigivolutionCount(CardSource card, HeadlessEntityId hostId)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (hostId.IsEmpty)
        {
            return 0;
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, hostId);
        int count = 0;
        foreach (StackedCard under in stack.UnderCards)
        {
            if (!IsTrashProtectedSource(card, under.InstanceId))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The host has at least one trashable (non-protected) digivolution card.</summary>
    public static bool HasTrashableDigivolutionCards(CardSource card, HeadlessEntityId hostId) =>
        TrashableDigivolutionCount(card, hostId) >= 1;

    private static bool IsTrashProtectedSource(CardSource card, HeadlessEntityId sourceId)
    {
        return !sourceId.IsEmpty
            && card.Context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? instance)
            && instance is not null
            && instance.Metadata.TryGetValue(TrashProtectedKey, out object? raw) && raw is true;
    }

    /// <summary>Mirror of the original <c>permanent.TopCard.HasLevel</c>: the host's top card carries a
    /// printed level (Digimon / DigiEgg do; Tamers / Options do not).</summary>
    public static bool TopCardHasLevel(CardSource card, HeadlessEntityId id) => LevelOf(card, id) > 0;

    /// <summary>Mirror of the original <c>Permanent.Level</c> (entity-id form): the printed level of the
    /// battle-area card topped by <paramref name="id"/> (0 when unknown), read from instance/def metadata.</summary>
    public static int LevelOf(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return 0;
        }

        return ReadLevel(instance.Metadata)
            ?? (card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
                ? ReadLevel(def.Metadata) ?? 0
                : 0);
    }

    private static int? ReadLevel(IReadOnlyDictionary<string, object?> metadata)
    {
        foreach (string key in new[] { "level", "Level" })
        {
            if (metadata.TryGetValue(key, out object? raw))
            {
                if (raw is int i) return i;
                if (raw is long l) return (int)l;
                if (raw is string s && int.TryParse(s, out int p)) return p;
            }
        }

        return null;
    }

    /// <summary>Mirror of the original <c>HasMatchConditionOpponentsPermanent</c> (entity-id predicate form):
    /// the opponent has at least one battle-area Digimon matching <paramref name="condition"/>.</summary>
    public static bool HasMatchConditionOpponentsPermanent(CardSource card, Func<HeadlessEntityId, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(condition);
        foreach (HeadlessEntityId id in OpponentBattleAreaDigimon(card))
        {
            if (condition(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Mirror of the original <c>card.Owner.SecurityCards.Count</c>: the number of cards in the
    /// owner's security stack (used by security-count conditions, e.g. ST3_05 "4 or more", ST3_09 "3 or less").</summary>
    public static int SecurityCount(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Security).Count;
    }

    /// <summary>Mirror of the original <c>IsDPZeroDelete(hashtable)</c>: the just-deleted permanent (the
    /// trigger subject) was deleted by dropping to 0 DP — distinguished by the <c>DPZero</c> marker that
    /// <see cref="DpZeroDeletionHelpers"/> stamps (vs a battle or direct-Delete-effect deletion).</summary>
    public static bool IsDPZeroDelete(CardSource card, CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.Context.TriggerEntityId is not { } deleted || deleted.IsEmpty)
        {
            return false;
        }

        return card.Context.CardInstanceRepository.TryGetInstance(deleted, out CardInstanceRecord? instance)
            && instance is not null
            && instance.Metadata.TryGetValue(DpZeroDeletionHelpers.DpZeroKey, out object? raw) && raw is true;
    }

    /// <summary>Mirror of the original <c>CanTriggerOnPermanentDeleted(hashtable, permanentCondition)</c>: a
    /// permanent was just deleted (the trigger subject) and it satisfies <paramref name="permanentCondition"/>.</summary>
    public static bool CanTriggerOnPermanentDeleted(CardSource card, CardEffectResolveContext context, Func<HeadlessEntityId, bool> permanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(permanentCondition);
        return context.Request.Context.TriggerEntityId is { } deleted && !deleted.IsEmpty && permanentCondition(deleted);
    }

    /// <summary>The deleted-subject ownership/type predicate: <paramref name="id"/> is (was) an opponent's
    /// Digimon — zone-agnostic (the card may already be in the trash), so usable in deletion triggers.</summary>
    public static bool IsOpponentOwnedDigimon(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        if (instance.OwnerId == card.Owner)
        {
            return false;
        }

        return card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            && def is not null
            && def.IsCardType("Digimon");
    }

    /// <summary>Mirror of the original <c>card.PermanentOfThisCard().battle.enemyPermanent(...)</c>: the
    /// entity this card's permanent is currently battling (the other participant of the in-progress attack),
    /// or empty when this permanent is not in a battle. Read from <c>AttackController.Current</c>.</summary>
    public static HeadlessEntityId CurrentBattleOpponent(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        HeadlessEntityId self = card.PermanentOfThisCard().TopInstanceId;
        if (self.IsEmpty)
        {
            return default;
        }

        HeadlessAttackState attack = card.Context.AttackController.Current;
        HeadlessEntityId attacker = attack.AttackerId ?? default;
        HeadlessEntityId defender = attack.BlockerId ?? attack.TargetId ?? default;
        if (self == attacker)
        {
            return defender;
        }

        if (self == defender)
        {
            return attacker;
        }

        return default;
    }

    /// <summary>The opponent player id (the first player in turn order that is not the card owner). Empty
    /// when there is no distinct opponent (e.g. uninitialized turn order).</summary>
    public static HeadlessPlayerId OpponentOf(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (player != card.Owner)
            {
                return player;
            }
        }

        return default;
    }

    /// <summary>The opponent's battle-area Digimon top cards (entity ids).</summary>
    private static IEnumerable<HeadlessEntityId> OpponentBattleAreaDigimon(CardSource card)
    {
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (player == card.Owner)
            {
                continue;
            }

            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (IsOpponentBattleAreaDigimon(card, id))
                {
                    yield return id;
                }
            }
        }
    }
}

/// <summary>
/// (G6-001) Maps a card number to its ported effect class. A ported card is a non-abstract
/// <see cref="CEntity_Effect"/> subclass whose type name equals the card number (e.g. class
/// <c>ST1_01</c> -> card "ST1_01"), so the dispatch is discovered by reflection — no manual table, and it
/// auto-grows as cards are ported. Un-ported cards (skeleton files with no class) simply aren't found.
/// </summary>
public static class CardEffectDispatch
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ByCardNumber = new(Build);

    private static IReadOnlyDictionary<string, Type> Build()
    {
        var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in typeof(CEntity_Effect).Assembly.GetTypes())
        {
            if (type.IsAbstract
                || !type.IsSubclassOf(typeof(CEntity_Effect))
                || type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            map[type.Name] = type;
        }

        return map;
    }

    public static int Count => ByCardNumber.Value.Count;

    public static bool TryCreate(string? cardNumber, out CEntity_Effect? effect)
    {
        effect = null;
        if (string.IsNullOrWhiteSpace(cardNumber) || !ByCardNumber.Value.TryGetValue(cardNumber.Trim(), out Type? type))
        {
            return false;
        }

        effect = (CEntity_Effect)Activator.CreateInstance(type)!;
        return true;
    }

    /// <summary>
    /// Resolves a card's effect class honoring the <c>effectClass</c> alias. cards.json carries an
    /// <c>effectClass</c> per card which is authoritative: for most cards it equals the card number, but
    /// alias cards (e.g. ST2_07 / ST3_07 reuse <c>ST1_06</c>, and every alternate-art reprint <c>*_P2</c>
    /// reuses its base) point at another class. When the metadata carries a non-empty effectClass we resolve
    /// by it exclusively (an un-ported alias is a no-op, like an un-ported card); otherwise we fall back to
    /// the card number — so test-constructed records without effectClass metadata behave exactly as before.
    /// </summary>
    public static bool TryCreateForCard(CardRecord def, out CEntity_Effect? effect)
    {
        effect = null;
        if (def is null)
        {
            return false;
        }

        if (def.Metadata.TryGetValue("effectClass", out object? raw)
            && raw is string alias
            && !string.IsNullOrWhiteSpace(alias))
        {
            return TryCreate(alias, out effect);
        }

        return TryCreate(def.CardNumber, out effect);
    }
}

/// <summary>
/// The runtime seam: builds a card's effect bindings (across the given timings) and registers them into
/// the EffectRegistry. Call when a card enters play. Returns the registered bindings for inspection.
/// </summary>
public static class CardEffectRegistrar
{
    /// <summary>The timings auto-registered when a card enters play (continuous + passive triggers).
    /// Player-activated abilities (<see cref="EffectTiming.OptionSkill"/> / <see cref="EffectTiming.SecuritySkill"/>)
    /// are intentionally excluded — their activation flow is built in a later wave.</summary>
    public static readonly IReadOnlyList<EffectTiming> AllTimings = Array.AsReadOnly(new[]
    {
        EffectTiming.None,
        EffectTiming.OnEnterFieldAnyone,
        EffectTiming.OnDetermineDoSecurityCheck,
        EffectTiming.OnUseAttack,
        EffectTiming.WhenDigivolving,
        EffectTiming.OnDestroyedAnyone,
        EffectTiming.OnAllyAttack,
        EffectTiming.OnBlockAnyone,
        // (EX8-3) OnEndTurn self-statics (e.g. <Vortex> via VortexSelfEffect) register at enter-play like the
        // other self-static keyword timings; GR-006's EndOfTurnEffectAttack then reads the live binding at
        // turn end. The original keys <Vortex> under EffectTiming.OnEndTurn (EX8_074 region "Vortex").
        EffectTiming.OnEndTurn,
        EffectTiming.OnStartTurn,
        // (PRIM-P0-timing) auto-register passive triggers on the new high-volume timings. Cards that don't
        // return effects on these timings are unaffected (CardEffects returns empty for the unmatched branch).
        EffectTiming.OnStartMainPhase,
        EffectTiming.OnEndBattle,
        EffectTiming.OnDeclaration,
        // (PRIM-P0-timing batch 2) already-emitted timings, enum-only additions.
        EffectTiming.OnTappedAnyone,
        EffectTiming.OnUnTappedAnyone,
        EffectTiming.OnCounterTiming,
        EffectTiming.WhenLinked,
        EffectTiming.OnLinkCardDiscarded,
        EffectTiming.OnAddDigivolutionCards,
        EffectTiming.OnUseOption,
        EffectTiming.OnDiscardSecurity,
        EffectTiming.AfterPayCost,
        EffectTiming.WhenTopCardTrashed,
        EffectTiming.OnFaceUpSecurityIncreased,
        // (PRIM-P0-timing batch 3a) derived-from-CardMoved timings, enum-only additions.
        EffectTiming.WhenRemoveField,
        EffectTiming.OnLoseSecurity,
        EffectTiming.OnDiscardHand,
        EffectTiming.OnAddHand,
        EffectTiming.OnDiscardLibrary,
        EffectTiming.OnAddSecurity,
        EffectTiming.WhenReturntoHandAnyone,
        EffectTiming.WhenReturntoLibraryAnyone,
        EffectTiming.OnSecurityCheck,
        EffectTiming.OnReturnCardsToHandFromTrash,
        EffectTiming.OnPermamemtReturnedToHand,
        EffectTiming.OnRemovedField,
        EffectTiming.OnLeaveFieldAnyone,
        EffectTiming.OnReturnCardsToLibraryFromTrash,
        // (PRIM-P0-timing batch 3b) end-of-single-attack, collected by EndAttackTriggerHook.
        EffectTiming.OnEndAttack,
        // (PRIM-P0-timing batch 3b) new emit sites (source-discard / attack-target-change).
        EffectTiming.OnDigivolutionCardDiscarded,
        EffectTiming.OnAttackTargetChanged,
        // (PRIM-P0-timing batch 4) would-be-deleted window — registered so HasPreOption can find the effect.
        EffectTiming.WhenPermanentWouldBeDeleted,
    });

    /// <summary>(G6-001) Auto-register the effects of the card instance entering play, resolved from the
    /// dispatch by its card number. No-op (returns false) for cards with no ported effect class — so
    /// un-ported cards are unaffected.</summary>
    public static bool RegisterCard(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        RegisterOnEnterPlay(context, effect, def.CardNumber, new CardSource(context, instanceId, controller, instance.OwnerId));
        return true;
    }

    /// <summary>(G6-001) Remove every binding sourced from <paramref name="instanceId"/> (the card left
    /// play). Returns the number of bindings removed.</summary>
    public static int UnregisterCard(EngineContext context, HeadlessEntityId instanceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty)
        {
            return 0;
        }

        return context.EffectRegistry.RemoveWhere(binding => binding.Request.Context.SourceEntityId == instanceId);
    }

    public static IReadOnlyList<EffectBinding> RegisterOnEnterPlay(
        EngineContext context,
        CEntity_Effect effect,
        string cardNumber,
        CardSource card)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardNumber);
        ArgumentNullException.ThrowIfNull(card);

        var registered = new List<EffectBinding>();
        int index = 0;
        foreach (EffectTiming timing in AllTimings)
        {
            foreach (ICardEffect cardEffect in effect.CardEffects(timing, card))
            {
                // Activated / choice effects are resolved via the activation flow, not auto-registered.
                if (cardEffect is IActivatedCardEffect)
                {
                    continue;
                }

                EffectBinding binding = cardEffect.ToBinding($"{card.InstanceId.Value}:{cardNumber}:{timing}:{index++}");
                context.EffectRegistry.Register(binding);
                registered.Add(binding);
            }
        }

        return registered;
    }
}
