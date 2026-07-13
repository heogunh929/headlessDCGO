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
/// (BT-PRE-A2) Mirror of the original <c>SimplifiedSelectCardConditionClass</c>
/// (DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs): a single "select up to
/// <see cref="MaxCount"/> revealed cards matching <see cref="CanTargetCondition"/>, sending the chosen ones
/// to <see cref="SelectedTo"/>" condition, used inside <see cref="SimplifiedRevealAndSelectEffect"/>. The
/// AS-IS <c>mode</c> (<c>SelectCardEffect.Mode</c>) is represented by a <see cref="RevealDestination"/> — the
/// dominant BT usage is <c>Mode.AddHand</c> → <see cref="RevealDestination.Hand"/> (tutor). The AS-IS
/// <c>selectCardCoroutine</c> (Mode.Custom) per-card action is a per-card follow-up (not modeled here).
/// </summary>
// (EFFECT-MODEL REBUILD / bridge W3) `partial` added so the AS-IS-signature constructor overloads +
// AS-IS-shape members (Func<CardSource,bool> predicate, SelectCardEffect.Mode, per-card coroutine) can live in
// the AS-IS-path file Script/CardEffectCommons/RevealLibrary.cs without touching this legacy body. Purely
// syntactic (no behaviour change to this part).
public sealed partial class SimplifiedSelectCardConditionClass
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
// (EFFECT-MODEL REBUILD / bridge W3) `partial` added — same reason as SimplifiedSelectCardConditionClass above.
public sealed partial class SelectCardConditionClass
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

/// <summary>(PRIM-W5) A material condition for a Blast-DNA digivolution (AS-IS <c>BlastDNACondition</c>).
/// <see cref="Matches"/> preserves the original's per-material predicate 1:1; use <see cref="ByName"/> for the
/// name-equality subset.
/// (P6C1) Extended ADDITIVELY with the AS-IS shape (DCGO KeyWordEffects/BlastDNADigivolution.cs:8-20 — a
/// top-level class holding <c>Name</c> plus the mutable <c>Permanents</c>/<c>CardSources</c> working sets and a
/// <c>(string name)</c> ctor) so the 1:1 <c>CardEffectFactory.BlastDNADigivolveEffect</c> mirror compiles.
/// Existing consumers (Matches/Label/ByName) untouched; two <c>ByName</c> instances were ALREADY unequal
/// (per-instance lambda in the primary-ctor equality), so the added mutable auto-properties do not change any
/// observable record equality. See docs/audit/rebuild_p6_cluster1_notes.md.</summary>
public sealed record BlastDNACondition(Func<CardSource, bool> Matches, string Label)
{
    public static BlastDNACondition ByName(string name) => new(cs => cs.EqualsCardName(name), name);

    /// <summary>(P6C1) AS-IS <c>BlastDNACondition(string name)</c> ctor (BlastDNADigivolution.cs:14-19) — the
    /// name predicate is exactly <see cref="ByName"/>'s.</summary>
    public BlastDNACondition(string name)
        : this(cs => cs.EqualsCardName(name), name)
    {
    }

    /// <summary>(P6C1) AS-IS <c>BlastDNACondition.Name</c> — the material card name (== <see cref="Label"/>;
    /// computed, so it stays out of the record equality contract).</summary>
    public string Name => Label;

    /// <summary>(P6C1) AS-IS <c>BlastDNACondition.Permanents</c> — the matching field permanents, (re)filled by
    /// <c>HasValidDNATargets</c> on every evaluation.</summary>
    public List<Permanent> Permanents { get; set; } = new List<Permanent>();

    /// <summary>(P6C1) AS-IS <c>BlastDNACondition.CardSources</c> — the matching hand cards, (re)filled by
    /// <c>HasValidDNATargets</c> on every evaluation.</summary>
    public List<CardSource> CardSources { get; set; } = new List<CardSource>();
}

// (EFFECT-MODEL REBUILD / fidelity) AS-IS <c>IgnoreRequirement</c> is NESTED inside the CardEffectCommons class
// (CardEffectCommons.cs:11), so every AS-IS reference reads `CardEffectCommons.IgnoreRequirement`. It was
// previously a TOP-LEVEL namespace enum here, which broke that 1:1 text (a `using CardEffectCommons` binds the
// bare `CardEffectCommons.` prefix to the class, not the namespace → CS0426). Moved to nest inside the
// CardEffectCommons class in CardEffectCommons.cs to match AS-IS exactly.

/// <summary>(#5) Which cost a <c>CannotReduceCost</c> immunity protects — the headless projection of the AS-IS
/// <c>targetPermanentsCondition</c> (count&gt;=1 =&gt; a digivolution is happening, so only the DIGIVOLUTION cost;
/// otherwise the PLAY cost). <c>Both</c> = the trivial-predicate case (protect either).</summary>
public enum CostReductionScope
{
    Both,
    Play,
    Digivolve,
}

/// <summary>The follow-up applied to the reveal-selected card in <see cref="RevealSelectThenPlaySelectedEffect"/>.</summary>
public enum RevealPlayMode
{
    /// <summary>Costless single-target digivolve of the selected card onto THIS card's own permanent (AS-IS
    /// PlayCardClass with targetPermanent: card.PermanentOfThisCard(), payCost:false, root:Library). BT1_078.</summary>
    DigivolveOntoSelf,

    /// <summary>Play the selected card as a NEW permanent, cost-free (AS-IS PlayPermanentCards(payCost:false,
    /// isTapped:false, root:Library, activateETB:true)). BT3_063 / BT3_070 / BT3_073.</summary>
    PlayAsNewPermanent,
}

/// <summary>Shared post-de-digivolve helpers for the G10 conditional-destroy effects.</summary>
internal static class DeDigivolveDestroyHelpers
{
    internal static HeadlessPlayerId OwnerOf(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? i) && i is not null ? i.OwnerId : default;
}
