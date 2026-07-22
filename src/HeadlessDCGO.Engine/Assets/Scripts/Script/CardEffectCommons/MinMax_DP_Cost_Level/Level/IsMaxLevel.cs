// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level/Level/IsMaxLevel.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs into the AS-IS mirrored path.
// Same partial class; folded IsLevelExtremum un-folded here. Substrate: Player owner -> HeadlessPlayerId owner;
// GetBattleAreaDigimons -> IZoneStateReader.GetCards(owner, BattleArea).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>IsMaxLevel</c> (…/Level/IsMaxLevel.cs): among the owner's battle-area Digimon with a
    /// printed level, this permanent's level is the maximum.</summary>
    public static bool IsMaxLevel(Permanent? permanent, HeadlessPlayerId owner)
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
        return levels.Count >= 1 && permanent.Level == levels.Max();
    }
}
