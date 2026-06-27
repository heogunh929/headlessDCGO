// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Pierce.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Piercing). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Piercing's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch1Effect
{
    private CardEffectCanResolveResult CanResolvePiercing(
        CardEffectResolveContext context,
        MatchState state,
        CardInstanceState target)
    {
        if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.BattleDeletedByBattle, out bool deletedByBattle)
            || !deletedByBattle)
        {
            return Failure("Piercing requires battle deletion.", "battleDeletedByBattle", context, target.InstanceId);
        }

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.BattleWinnerCardId, out HeadlessEntityId winnerId)
            || winnerId != target.InstanceId)
        {
            return Failure("Piercing requires the keyword target to be the battle winner.", "battleWinnerCardId", context, target.InstanceId);
        }

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.BattleLoserCardId, out HeadlessEntityId loserId)
            || !state.CardInstances.TryGetValue(loserId, out CardInstanceState? loser)
            || loser.OwnerId == target.OwnerId)
        {
            return Failure("Piercing requires an opponent Digimon deleted by battle.", "battleLoserCardId", context, target.InstanceId);
        }

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.OpponentSecurityCount, out int securityCount)
            || securityCount < 1)
        {
            return Failure("Piercing requires opponent security.", "opponentSecurityCount", context, target.InstanceId);
        }

        bool alreadyChecking = context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.DoSecurityCheck, out bool doSecurityCheck)
            && doSecurityCheck;
        if (alreadyChecking)
        {
            return Failure("Piercing cannot resolve after security check is already enabled.", "doSecurityCheck", context, target.InstanceId);
        }

        return CardEffectCanResolveResult.Success("Piercing enables an additional security check.", BaseValues(context, target));
    }
}
