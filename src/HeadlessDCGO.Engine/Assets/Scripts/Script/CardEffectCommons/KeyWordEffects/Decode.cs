// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Decode.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Decode). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Decode's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateDecode). The LIVE play-a-source-for-free path is engine plumbing in
// DeletionReplacementTiming (DecodeOption) + DeletionReplacementGate.TryDecodePlaySourceAsync — this
// branch is the grant/mirror layer (resolving emits GrantDecode -> hasDecode).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch2Effect
{
    private CardEffectCanResolveResult CanResolveDecode(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedFromField, out bool removedFromField)
            || !removedFromField)
        {
            return Failure("Decode requires a field removal event.", "removedFromField", context, target.InstanceId);
        }

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedCardId, out HeadlessEntityId removedCardId)
            || removedCardId != target.InstanceId)
        {
            return Failure("Decode requires the keyword target to be removed.", "removedCardId", context, target.InstanceId);
        }

        // AS-IS !IsByBattle: Decode fires only on a non-battle field-leave (CanTriggerWhenRemoveField).
        if (context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.DeletedByBattle, out bool deletedByBattle)
            && deletedByBattle)
        {
            return Failure("Decode does not trigger on battle removal.", "deletedByBattle", context, target.InstanceId);
        }

        if (target.SourceIds.Count < 1)
        {
            return Failure("Decode requires at least one digivolution source to play.", "sourceIds", context, target.InstanceId);
        }

        return CardEffectCanResolveResult.Success("Decode can play a digivolution source for free.", BaseValues(context, target));
    }
}
