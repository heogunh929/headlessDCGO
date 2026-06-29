// Source: Assets/Scripts/Script/CardEffectCommons/GetFromHashtable.cs (IsJogress / IsDPZeroDelete / ...)
// Decision: PORT
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
//
// (F-8.5) Special trigger-context predicates the original reads off the effect hashtable, mirrored 1:1.
// In headless the "hashtable" is the effect/event context values dictionary:
//   - IsJogress / IsDigiXros — was the digivolve a DNA(Jogress) / DigiXros fusion? (stamped by
//     FusionDigivolveHelpers on the WhenDigivolving event).
//   - IsDpZeroDelete — was the deletion caused by DP reaching 0? (stamped by DpZeroDeletionHelpers).
//   - IsTopCardInTrash — is the card the top (most recently trashed) card of its owner's trash?

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static class SpecialConditionHelpers
{
    public const string IsJogressKey = "isJogress";
    public const string IsDigiXrosKey = "isDigiXros";
    public const string DpZeroKey = "DPZero";

    /// <summary>(AS-IS IsJogress) The triggering digivolve was a DNA (Jogress) digivolve.</summary>
    public static bool IsJogress(IReadOnlyDictionary<string, object?> values) => ReadBool(values, IsJogressKey);

    /// <summary>The triggering digivolve was a DigiXros.</summary>
    public static bool IsDigiXros(IReadOnlyDictionary<string, object?> values) => ReadBool(values, IsDigiXrosKey);

    /// <summary>(AS-IS IsDPZeroDelete) The triggering deletion was caused by DP reaching 0.</summary>
    public static bool IsDpZeroDelete(IReadOnlyDictionary<string, object?> values) => ReadBool(values, DpZeroKey);

    /// <summary>Whether <paramref name="cardId"/> is the top (most recently added) card of
    /// <paramref name="ownerId"/>'s trash.</summary>
    public static bool IsTopCardInTrash(IZoneStateReader zones, HeadlessPlayerId ownerId, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(zones);
        // Engine convention (matches security): index 0 is the top of a pile.
        IReadOnlyList<HeadlessEntityId> trash = zones.GetCards(ownerId, ChoiceZone.Trash);
        return trash.Count > 0 && trash[0] == cardId;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.TryGetValue(key, out object? raw) && raw is bool b && b;
    }
}
