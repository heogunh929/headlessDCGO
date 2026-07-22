// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level/DigivolutionCards/IsMinDigivolutionCards.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs into the AS-IS mirrored path.
// Same partial class. Substrate: Player owner -> HeadlessPlayerId owner; GetBattleAreaDigimons ->
// IZoneStateReader.GetCards(owner, BattleArea). The optional condition gates BOTH the subject guard AND scan.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>IsMinDigivolutionCards</c> (…/DigivolutionCards/IsMinDigivolutionCards.cs): among the
    /// owner's battle-area Digimon (that satisfy <paramref name="condition"/>), this permanent has the fewest
    /// digivolution cards under it.</summary>
    public static bool IsMinDigivolutionCards(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition = null)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, permanent.TopCard) ||
            (condition is not null && !condition(permanent)))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> counts = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && (condition is null || condition(p)))
            .Select(p => p.TopCard.PermanentOfThisCard().DigivolutionCards.Count)
            .ToList();
        return counts.Count >= 1 &&
            permanent.TopCard.PermanentOfThisCard().DigivolutionCards.Count == counts.Min();
    }
}
