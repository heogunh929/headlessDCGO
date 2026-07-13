// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Retaliation.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Retaliation). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Retaliation's resolution branch (1:1 with the original).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.Services;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveRetaliation(
            CardEffectResolveContext context,
            MatchState state,
            CardInstanceState target)
        {
            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.DeletedByBattle, out bool deletedByBattle)
                || !deletedByBattle)
            {
                return Failure("Retaliation requires battle deletion.", "deletedByBattle", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.DeletedCardId, out HeadlessEntityId deletedCardId)
                || deletedCardId != target.InstanceId)
            {
                return Failure("Retaliation requires the keyword target to be deleted.", "deletedCardId", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.OpponentBattleCardId, out HeadlessEntityId opponentId)
                || !state.CardInstances.TryGetValue(opponentId, out CardInstanceState? opponent)
                || opponent.OwnerId == target.OwnerId)
            {
                return Failure("Retaliation requires an opponent battle target.", "opponentBattleCardId", context, target.InstanceId);
            }

            return CardEffectCanResolveResult.Success("Retaliation can delete the opposing Digimon.", BaseValues(context, target));
        }
    }
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainRetaliation` (CardEffectCommons.cs:3417). Kept in the flat `...Script.CardEffectCommons` namespace
// (not the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainRetaliation(...)</c> (KeyWordEffects/Retaliation.cs:136) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainRetaliation(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainRetaliation(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }
    }
}
