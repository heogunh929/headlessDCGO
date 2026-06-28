// Source: Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Decode.cs
// AS-IS mirror of CardEffectFactory.DecodeSelfEffect / DecodeEffect — a convenience factory that builds the
// Decode keyword effect (KeywordBaseBatch2). 1:1 keyword-name/timing map: trigger WhenRemoveField && !IsByBattle
// (CanTriggerWhenRemoveField); CanActivateDecode -> DeletionReplacementTiming.PostOptions(DecodeOption);
// DecodeProcess (select 1 matching source -> PlayPermanentCards payCost:false) ->
// DeletionReplacementGate.TryDecodePlaySourceAsync; the per-card sourceCondition (decodeStrings colour)
// maps to IDeletionReplacementCandidateConditions (default = any Digimon source).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Decode
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Decode,
            sourceEntityId,
            targetEntityId);
    }
}
