// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Pierce.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Piercing). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Piercing's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
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
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainPierce` (CardEffectCommons.cs:3413). Kept in the flat `...Script.CardEffectCommons` namespace (not
// the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainPierce(...)</c> (KeyWordEffects/Pierce.cs:54) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainPierce(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainPierce(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }
    }
}
