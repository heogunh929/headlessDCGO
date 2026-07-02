// Source: Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Partition.cs
// AS-IS mirror of CardEffectFactory.PartitionEffect — a convenience factory that builds the Partition keyword
// effect (KeywordBaseBatch2). 1:1 map: trigger WhenRemoveField && !IsByBattle && !IsByOwnerEffect
// (CanTriggerPartition); CanActivatePartition (DigivolutionCards.Count >= 2) -> DeletionReplacementTiming
// PartitionOption; PartitionProcess (play one source per colour group as new permanents, payCost:false) ->
// DeletionReplacementGate.TryPartitionPlaySourceAsync driven as a repeated single-select (2 picks). The
// per-card colour groups (PartitionConditions) map to the IDeletionReplacementCandidateConditions seam
// (default = any Digimon source).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (A4) AS-IS <c>PartitionCondition</c> (Partition.cs:6-36) 1:1 — a Partition holder always carries exactly
/// TWO of these: [0] defines colour group 1, [1] group 2. Three constructor modes (mutually exclusive):
/// one-colour (exact level + colour), two-colour (exact level + either colour), by-name (level ignored).
/// Colours are strings headless-side (no CardColor enum — card-facing tolerance per the porting standard).
/// Stored VERBATIM on the Partition grant under <see cref="PartitionConditionsKey"/> and consumed by
/// DeletionReplacementTiming's per-group candidate filter.
/// </summary>
public sealed class PartitionCondition
{
    /// <summary>Binding-values key carrying the holder's <c>IReadOnlyList&lt;PartitionCondition&gt;</c>.</summary>
    public const string PartitionConditionsKey = "partition.conditions";

    public int Level;
    public string? Color;
    public string? Color2;
    public string? Name;
    public bool HasOneColour;
    public bool hasTwoColor;
    public bool hasName;

    public PartitionCondition(int level, string color)
    {
        Level = level;
        Color = color;
        HasOneColour = true;
    }

    public PartitionCondition(int level, string color, string color2)
    {
        Level = level;
        Color = color;
        Color2 = color2;
        hasTwoColor = true;
    }

    public PartitionCondition(string cardName)
    {
        Name = cardName;
        hasName = true;
    }
}

public static class Partition
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Partition,
            sourceEntityId,
            targetEntityId);
    }
}
