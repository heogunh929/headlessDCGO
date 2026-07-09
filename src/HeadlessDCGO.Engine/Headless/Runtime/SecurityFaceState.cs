namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (faceup security) Persistent per-instance face state for security cards — the headless analog of AS-IS
/// <c>CardSource.IsFlipped</c> / <c>IsFaceUp</c> (CardSource.cs:56-70). AS-IS <c>AddSecurityCard</c> calls
/// <c>SetReverse()</c> (face-down) or <c>SetFace()</c> (face-up) on placement, and every DP / immunity /
/// battle-deletion getter scans <c>player.SecurityCards where !IsFlipped</c> as a continuous-effect source
/// population alongside the field permanents.
///
/// The zone mover tracks only zone membership (ordered id lists) and records <c>faceUp</c> in the transient
/// CardMoved event log, not on the instance — so before this there was no queryable "is this security card
/// face-up" state. This helper stamps a persistent <c>securityFaceUp</c> instance-metadata flag on every
/// security placement and reads it back, gated on the card still being in the Security zone (a card that has
/// left security is never a face-up-security source regardless of a stale flag, so no leave-time cleanup is
/// needed). Absent flag = face-down (AS-IS security default via <c>SetReverse()</c>).
/// </summary>
public static class SecurityFaceState
{
    /// <summary>Instance-metadata flag: <c>true</c> = the card was placed face-up in security. Dedicated key
    /// (distinct from the field-ACE <c>isFlipped</c> and the transient move <c>faceUp</c>) to avoid conflation.</summary>
    public const string FaceUpKey = "securityFaceUp";

    /// <summary>Persist the face state for a card placed into security (AS-IS SetFace/SetReverse). Stamped for
    /// BOTH values so a re-add overwrites a stale flag.</summary>
    public static void Stamp(ICardInstanceRepository repository, HeadlessEntityId cardId, bool faceUp)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (cardId.IsEmpty || !repository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [FaceUpKey] = faceUp,
        };
        repository.Upsert(record with { Metadata = metadata });
    }

    /// <summary>True when <paramref name="cardId"/> is currently in its owner's Security zone AND was placed
    /// face-up (AS-IS <c>SecurityCards.Contains(cardSource) &amp;&amp; !cardSource.IsFlipped</c>).</summary>
    public static bool IsFaceUpInSecurity(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record)
            || record is null)
        {
            return false;
        }

        if (!record.Metadata.TryGetValue(FaceUpKey, out object? raw) || raw is not true)
        {
            return false;
        }

        var zones = (IZoneStateReader)context.ZoneMover;
        return zones.GetCards(record.OwnerId, ChoiceZone.Security).Contains(cardId);
    }

    /// <summary>The owner's face-up security cards, in security-stack order (AS-IS <c>player.SecurityCards
    /// where !IsFlipped</c>).</summary>
    public static IReadOnlyList<HeadlessEntityId> FaceUpSecurityCards(EngineContext context, HeadlessPlayerId owner)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (owner.IsEmpty)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var zones = (IZoneStateReader)context.ZoneMover;
        var faceUp = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId cardId in zones.GetCards(owner, ChoiceZone.Security))
        {
            if (context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record)
                && record is not null
                && record.Metadata.TryGetValue(FaceUpKey, out object? raw) && raw is true)
            {
                faceUp.Add(cardId);
            }
        }

        return faceUp;
    }
}
