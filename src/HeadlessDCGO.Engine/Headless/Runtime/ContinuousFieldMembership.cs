namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (E-3 extraction) The AS-IS field-effect-list MEMBERSHIP rule shared by every JOINT continuous scan that
/// mirrors <c>player.GetFieldPermanents().EffectList(EffectTiming.None)</c> — the R1-e getter
/// <c>CardSource.CanNotTrashFromDigivolutionCards</c> and
/// <see cref="CanNotPlayOptionScan"/> (CardSource.CanNotPlayThisOption). AS-IS
/// <c>Permanent.EffectList(timing) = EffectList_ForCard(timing, TopCard)</c> (Permanent.cs:1373-1376), whose
/// per-cardSource membership (Permanent.cs:1497-1546) is:
///   ① a FLIPPED source contributes nothing (<c>if (!cardSource.IsFlipped)</c>);
///   ② a NON-TOP source contributes only when the permanent <c>IsDigimon</c>;
///   ③ an effect marked <c>IsInheritedEffect</c> is taken only from a NON-TOP source; the TOP card contributes
///     only its non-inherited (and, when linked, linked) effects. No linked-effect producer exists for these
///     scopes, so the IsLinkedEffect branch has no headless mirror here (add it with its first witness).
/// The AS-IS scan population is <c>player.GetFieldPermanents()</c> (battle + breeding, Player.cs:665) — a granter
/// that is part of NO field permanent is never enumerated. A binding registered at PLAYER scope (AS-IS
/// <c>player.EffectList</c> region, e.g. an EX1_072-style duration-bound player effect whose granter card sits in
/// the trash) is NOT a field permanent and must bypass this check via its own player-scope marker BEFORE calling
/// here (see <see cref="CanNotPlayOptionScan"/>).
/// </summary>
public static class ContinuousFieldMembership
{
    // AS-IS GetFieldPermanents = battle area + breeding area frames (Player.cs:665-681).
    private static readonly ChoiceZone[] FieldZones = { ChoiceZone.BattleArea, ChoiceZone.BreedingArea };

    /// <summary>True when the continuous <paramref name="request"/>'s granter card is currently exposed to the
    /// field scan under the AS-IS stack-position membership rules (①②③ above). A field-scope joint scan calls this
    /// so a tucked / flipped / off-field / top-vs-inherited granter contributes exactly as AS-IS
    /// <c>EffectList_ForCard</c> would.</summary>
    public static bool GranterMembershipHolds(
        ICardInstanceRepository repository,
        Bridge.EngineContext context,
        EffectRequest request)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

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
            request.Context.Values.TryGetValue(ContinuousScopeEvaluation.InheritedEffectKey, out object? inheritedRaw)
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

    private static IReadOnlyList<string> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DigivolutionStackHelpers.SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
            ? ids.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
            : Array.Empty<string>();
}
