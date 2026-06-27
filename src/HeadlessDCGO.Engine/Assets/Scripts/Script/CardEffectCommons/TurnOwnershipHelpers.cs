// Source: Assets/Scripts/Script/ICardEffect.cs / Permanent.cs (IsOwnerTurn / Owner checks)
// Decision: PORT
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
//
// (F-8.4) Turn / ownership predicates used pervasively in card conditions ("during your turn",
// "your opponent's Digimon", "an effect you control"). Pure comparisons over the current turn player
// and a card/effect's owning player — the original reads these off GManager.turnPlayer / Owner.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Services;

public static class TurnOwnershipHelpers
{
    /// <summary>It is <paramref name="owner"/>'s turn.</summary>
    public static bool IsOwnerTurn(HeadlessPlayerId? turnPlayerId, HeadlessPlayerId owner)
    {
        return turnPlayerId is HeadlessPlayerId turn && !turn.IsEmpty && !owner.IsEmpty && turn == owner;
    }

    /// <summary>It is the opponent's turn (a known turn player that is not <paramref name="owner"/>).</summary>
    public static bool IsOpponentTurn(HeadlessPlayerId? turnPlayerId, HeadlessPlayerId owner)
    {
        return turnPlayerId is HeadlessPlayerId turn && !turn.IsEmpty && !owner.IsEmpty && turn != owner;
    }

    /// <summary><paramref name="playerId"/> is the owner (same player).</summary>
    public static bool IsOwner(HeadlessPlayerId playerId, HeadlessPlayerId owner)
    {
        return !playerId.IsEmpty && !owner.IsEmpty && playerId == owner;
    }

    /// <summary><paramref name="playerId"/> is the opponent of <paramref name="owner"/> (a different player).</summary>
    public static bool IsOpponent(HeadlessPlayerId playerId, HeadlessPlayerId owner)
    {
        return !playerId.IsEmpty && !owner.IsEmpty && playerId != owner;
    }
}
