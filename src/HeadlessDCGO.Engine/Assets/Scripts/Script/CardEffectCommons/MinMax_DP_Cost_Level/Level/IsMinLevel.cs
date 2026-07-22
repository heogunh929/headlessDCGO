// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level/Level/IsMinLevel.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs into the AS-IS mirrored path.
// Same partial class; folded IsLevelExtremum un-folded here; IsMinLevelBoard (both-players variant) moved
// alongside per AS-IS (it lives in the same IsMinLevel.cs file). Substrate: Player owner -> HeadlessPlayerId;
// GetBattleAreaDigimons -> IZoneStateReader.GetCards(owner, BattleArea); GManager.turnStateMachine.gameContext
// .Players -> context.TurnController.Current.PlayerOrder.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>IsMinLevel</c> (…/Level/IsMinLevel.cs): among the owner's battle-area Digimon with a
    /// printed level, this permanent's level is the minimum.</summary>
    public static bool IsMinLevel(Permanent? permanent, HeadlessPlayerId owner)
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
        return levels.Count >= 1 && permanent.Level == levels.Min();
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
}
