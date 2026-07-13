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


/// <summary>(PRIM-P0-flow B.O.3) The cost treatment of a select-and-digivolve (AS-IS payCost / reduceCost /
/// fixedCost knobs).</summary>
public enum DigivolveCost
{
    Free,     // payCost:false
    Normal,   // the resolved evolution cost (ContinuousModifierGate-folded)
    Reduced,  // Normal minus a fixed amount (floored at 0)
    Fixed,    // a fixed literal cost
}

