// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level/Cost/IsMaxCost.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs into the AS-IS mirrored path.
// Same partial class (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons) so this
// merges transparently; the folded IsCostExtremum helper is un-folded per file to match AS-IS's per-file
// structure. Substrate translation only: AS-IS Player owner -> HeadlessPlayerId owner; AS-IS
// GetBattleAreaDigimons/GetBattleAreaPermanents -> IZoneStateReader.GetCards(owner, BattleArea) projection.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>IsMaxCost</c> (MinMax_DP_Cost_Level/Cost/IsMaxCost.cs): among the owner's battle-area
    /// Digimon (or Digimon+Tamer when <paramref name="IsDigimonOnly"/> is false), this permanent's PRINTED
    /// play cost is the maximum.</summary>
    public static bool IsMaxCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleArea(permanent) ||
            (!permanent.IsDigimon && !IsTamerPermanent(permanent)) ||
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
        return costs.Count >= 1 && permanent.TopCard.GetCostItself == costs.Max();
    }

    /// <summary>AS-IS <c>GetNonMaxCostPermanents</c> (…/IsMaxCost.cs:36): the owner's permanents whose printed
    /// cost is BELOW the current maximum (cost-undefined ones included, per the original).</summary>
    public static List<Permanent> GetNonMaxCostPermanents(CardSource card, HeadlessPlayerId owner, bool digimonOnly = true)
    {
        ArgumentNullException.ThrowIfNull(card);
        EngineContext context = card.Context;
        List<Permanent> candidates = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => digimonOnly ? p.IsDigimon : (p.IsDigimon || IsTamerPermanent(p)))
            .ToList();
        if (candidates.Count == 0)
        {
            return new List<Permanent>();
        }

        int maxCost = candidates.Max(p => p.TopCard.HasPlayCost ? p.TopCard.GetCostItself : -1);
        return candidates.Where(p => !p.TopCard.HasPlayCost || p.TopCard.GetCostItself < maxCost).ToList();
    }
}
