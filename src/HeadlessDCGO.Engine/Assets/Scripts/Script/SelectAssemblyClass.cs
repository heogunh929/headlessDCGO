// Source: Assets/Scripts/Script/SelectAssemblyClass.cs
// Decision: PORT
// Category: AIUseful
// Priority: MEDIUM
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (AD1-A) Mirror of the AS-IS <c>SelectAssemblyClass</c> material logic — headless, the interactive
/// selection becomes a parameterized play action, so this class contributes the FEASIBILITY / matching half:
/// <list type="bullet">
/// <item><see cref="TryMatchMaterials"/> — mirror of <c>CanFulfillConditions</c> (SelectAssemblyClass.cs:110-160):
/// per-element BACKTRACKING assignment over the OWNER'S TRASH (each element's predicate × its
/// <c>ElementCount</c>, honoring <c>CanTargetCondition_ByPreSelecetedList</c> against the already-selected
/// materials, distinct cards). Field-permanent substitution (<c>CanSubstituteForAssemblyCondition</c>,
/// Permanent.cs:3843-3886) is an explicit REDUCTION — no <c>ICanSelectAssemblyEffect</c> user exists in the
/// AD1/BT22/EX9/EX11/BT24 assembly set; wire when such a card is ported (fidelity_debt).</item>
/// </list>
/// </summary>
public static class SelectAssemblyClass
{
    /// <summary>Backtracking material assignment from the owner's trash. Returns the materials in ELEMENT
    /// ORDER (element 0's cards first — the order they are stacked under the permanent).</summary>
    public static bool TryMatchMaterials(
        EngineContext context,
        CardSource playCard,
        AssemblyCondition condition,
        out List<HeadlessEntityId> materials)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(playCard);
        ArgumentNullException.ThrowIfNull(condition);
        materials = new List<HeadlessEntityId>();

        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        // AS-IS: materials come from the owner's trash (MatchConditionOwnersCardCountInTrash — TrashCards),
        // tokens/excluded barred (a trash zone holds no tokens headless-side).
        IReadOnlyList<HeadlessEntityId> trash = zones.GetCards(playCard.Owner, ChoiceZone.Trash);

        // Flatten elements into per-slot predicates (element repeated ElementCount times), keeping the
        // owning element for the pre-selected-list gate.
        var slots = new List<AssemblyConditionElement>();
        foreach (AssemblyConditionElement element in condition.elements)
        {
            for (int i = 0; i < element.ElementCount; i++)
            {
                slots.Add(element);
            }
        }

        if (slots.Count == 0)
        {
            return false;
        }

        var selectedViews = new List<CardSource>();
        return Assign(0, materials, selectedViews);

        bool Assign(int slotIndex, List<HeadlessEntityId> assigned, List<CardSource> selected)
        {
            if (slotIndex >= slots.Count)
            {
                return true;
            }

            AssemblyConditionElement slot = slots[slotIndex];
            foreach (HeadlessEntityId id in trash)
            {
                if (assigned.Contains(id))
                {
                    continue;
                }

                var candidate = new CardSource(context, id, playCard.Owner, playCard.Owner);
                if (!slot.CardCondition(candidate))
                {
                    continue;
                }

                if (slot.CanTargetCondition_ByPreSelecetedList is not null &&
                    !slot.CanTargetCondition_ByPreSelecetedList(selected, candidate))
                {
                    continue;
                }

                assigned.Add(id);
                selected.Add(candidate);
                if (Assign(slotIndex + 1, assigned, selected))
                {
                    return true;
                }

                assigned.RemoveAt(assigned.Count - 1);
                selected.RemoveAt(selected.Count - 1);
            }

            return false;
        }
    }

    /// <summary>Mirror of AS-IS <c>CanFulfillConditions</c>: enough matching trash cards to fill EVERY
    /// element (full set — partial assembly gives no discount and is never offered).</summary>
    public static bool CanFulfillConditions(EngineContext context, CardSource playCard, AssemblyCondition condition) =>
        TryMatchMaterials(context, playCard, condition, out _);

    /// <summary>Validates an explicit material list (a parameterized action's payload) against the
    /// condition: element order, per-slot predicates, pre-selected-list gates, distinctness, and that every
    /// card is in the OWNER'S TRASH right now.</summary>
    public static bool ValidateMaterials(
        EngineContext context,
        CardSource playCard,
        AssemblyCondition condition,
        IReadOnlyList<HeadlessEntityId> materials)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(playCard);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(materials);

        if (materials.Count != condition.elementCount ||
            materials.Distinct().Count() != materials.Count ||
            context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> trash = zones.GetCards(playCard.Owner, ChoiceZone.Trash);
        var selected = new List<CardSource>();
        int index = 0;
        foreach (AssemblyConditionElement element in condition.elements)
        {
            for (int i = 0; i < element.ElementCount; i++, index++)
            {
                HeadlessEntityId id = materials[index];
                if (!trash.Contains(id))
                {
                    return false;
                }

                var candidate = new CardSource(context, id, playCard.Owner, playCard.Owner);
                if (!element.CardCondition(candidate))
                {
                    return false;
                }

                if (element.CanTargetCondition_ByPreSelecetedList is not null &&
                    !element.CanTargetCondition_ByPreSelecetedList(selected, candidate))
                {
                    return false;
                }

                selected.Add(candidate);
            }
        }

        return true;
    }
}
