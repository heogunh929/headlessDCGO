// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level/Cost/IsMinCost.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs into the AS-IS mirrored path.
// Same partial class; folded IsCostExtremum un-folded here. Substrate: Player owner -> HeadlessPlayerId owner;
// GetBattleAreaDigimons/GetBattleAreaPermanents -> IZoneStateReader.GetCards(owner, BattleArea). The optional
// AS-IS `condition` predicate gates the SUBJECT only (never the scan), verbatim.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>IsMinCost</c> (MinMax_DP_Cost_Level/Cost/IsMinCost.cs, verbatim verified): among the
    /// owner's battle-area Digimon (or Digimon+Tamer), this permanent's PRINTED play cost is minimal.</summary>
    public static bool IsMinCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly, Func<Permanent, bool>? condition = null)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleArea(permanent) ||
            (!permanent.IsDigimon && !IsTamerPermanent(permanent)) ||
            (condition is not null && !condition(permanent)) ||
            !permanent.TopCard.HasPlayCost ||
            (IsDigimonOnly && !permanent.IsDigimon))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> costs = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => IsDigimonOnly ? p.IsDigimon : (p.IsDigimon || IsTamerPermanent(p)))
            .Where(p => p.TopCard.HasPlayCost)
            .Select(p => p.TopCard.GetCostItself)
            .ToList();
        return costs.Count >= 1 && permanent.TopCard.GetCostItself == costs.Min();
    }
}
