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
    /// <summary>(C-1 witness) Binding-values key carrying the AS-IS <c>DecodeSelfEffect</c> per-card
    /// <c>sourceCondition</c> (<c>Func&lt;CardSource,bool&gt;</c>): the predicate that a leaving card's
    /// digivolution source must pass to be a free-play candidate (BT19_024: Blue &amp;&amp; Lv.4). Stored
    /// VERBATIM on the Decode grant and snapshotted onto the leaving card's metadata
    /// (<see cref="HeadlessDCGO.Engine.Headless.Runtime.CardLeavePlayCleanup"/>) so the PRE candidate filter
    /// (<see cref="HeadlessDCGO.Engine.Headless.Runtime.DeletionReplacementTiming"/>) evaluates it live —
    /// AS-IS <c>decodeStrings</c> is display-only, the real filter is this predicate. Null = any Digimon source.</summary>
    public const string DecodeSourceConditionKey = "decode.sourceCondition";

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
