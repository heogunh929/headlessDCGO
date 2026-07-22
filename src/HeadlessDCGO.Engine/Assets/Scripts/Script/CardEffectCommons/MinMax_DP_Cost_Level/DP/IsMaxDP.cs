// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level/DP/IsMaxDP.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs into the AS-IS mirrored path.
// Same partial class; folded IsDpExtremum un-folded here. Substrate: Player owner -> HeadlessPlayerId owner;
// GetBattleAreaDigimons -> IZoneStateReader.GetCards(owner, BattleArea). The optional permanentCondition
// gates BOTH the subject guard AND the scan, verbatim AS-IS.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>IsMaxDP</c> (MinMax_DP_Cost_Level/DP/IsMaxDP.cs): among the owner's battle-area Digimon
    /// with a defined DP (printed DP or BaseDP&gt;0) that satisfy <paramref name="permanentCondition"/>, this
    /// permanent's effective DP is the maximum.</summary>
    public static bool IsMaxDP(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? permanentCondition = null)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleAreaDigimon(permanent) ||
            (permanentCondition is not null && !permanentCondition(permanent)) ||
            (!permanent.TopCard.HasDP && permanent.BaseDP <= 0))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> dps = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && (permanentCondition is null || permanentCondition(p)) && (p.TopCard.HasDP || p.BaseDP > 0))
            .Select(p => p.DP)
            .ToList();
        return dps.Count >= 1 && permanent.DP == dps.Max();
    }
}
