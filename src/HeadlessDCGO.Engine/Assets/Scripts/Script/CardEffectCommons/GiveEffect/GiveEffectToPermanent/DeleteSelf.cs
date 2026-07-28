// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/DeleteSelf.cs
// (GAMEFLOW re-migration / design item RD-EOT-SELFDELETE RESOLVED) This file now carries the AS-IS body 1:1.
// It previously delegated to a SUBSTRATE relay in CardEffectCommons.cs that stamped two instance-metadata
// markers (`GameFlowProcessor.DeleteAtTurnEndKey` / `DeleteAtTurnEndSourceKey`) for the substrate
// GameFlowProcessor turn-end deletion sweep to consume. That sweep — and the EndPhase marker PROMOTION that
// fed it (HeadlessEndTurnCleanupFlow) — are both retired, so the markers had ZERO readers: the effect was a
// dead write. The AS-IS mechanism is used instead: two `permanent.PermanentEffects` producers, one returning
// `PermanentEffectFactory.DeleteSelfEffect(...)` for `EffectTiming.OnEndTurn` and one returning
// `PermanentEffectFactory.AddDetailClass(...)` for `EffectTiming.None` — both of which are already LIVE mirrors
// (PermanentEffectFactory.cs:152 / :206) reached through the AS-IS OnEndTurn window (EX8-3 registered
// OnEndTurn in the enter-play registrar's timings).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using PermanentEffectFactory = HeadlessDCGO.Engine.Assets.Scripts.Script.PermanentEffectFactory;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>CardEffectCommons.DeleteTiming</c> (GiveEffect/GiveEffectToPermanent/DeleteSelf.cs:9-14).</summary>
    public enum DeleteTiming
    {
        AtTurnEnd,
        AtOwnTurnEnd,
        AtOpponentTurnEnd
    }

    /// <summary>1:1 mirror of AS-IS <c>CardEffectCommons.AddSelfDeleteEffect(permanent, deleteTiming,
    /// activateClass)</c> (GiveEffect/GiveEffectToPermanent/DeleteSelf.cs:16-45), verbatim: derive the two
    /// whose-turn flags and the display message from <paramref name="deleteTiming"/>, then append the two
    /// <c>PermanentEffects</c> producers (OnEndTurn ⇒ DeleteSelfEffect, None ⇒ AddDetailClass). AS-IS is an
    /// <c>IEnumerator</c> whose only yield is the trailing <c>yield return null</c>; the mirror is an already-
    /// completed <c>Task</c> (substrate translation, no suspension point).</summary>
    public static Task AddSelfDeleteEffect(Permanent permanent, DeleteTiming deleteTiming, ICardEffect activateClass)
    {
        if (permanent is null)
        {
            return Task.CompletedTask;
        }

        bool deleteOnOwnturn = deleteTiming != DeleteTiming.AtOpponentTurnEnd;
        bool deleteOnOpponentsTurn = deleteTiming != DeleteTiming.AtOwnTurnEnd;
        string message = "Delete this at turn end";
        if (deleteOnOpponentsTurn && !deleteOnOwnturn)
            message = "Delete this at opponent's turn end";
        if (deleteOnOwnturn && !deleteOnOpponentsTurn)
            message = "Delete this at your turn end.";
        permanent.PermanentEffects.Add(GetCardEffect);
        permanent.PermanentEffects.Add(GetDetailEffect);

        ICardEffect GetCardEffect(EffectTiming timing)
        {
            if (timing == EffectTiming.OnEndTurn)
            {
                return PermanentEffectFactory.DeleteSelfEffect(permanent, activateClass, deleteOnOwnturn, deleteOnOpponentsTurn);
            }

            return null;
        }

        ICardEffect GetDetailEffect(EffectTiming timing)
        {
            if (timing == EffectTiming.None)
            {
                return PermanentEffectFactory.AddDetailClass(permanent, message, true, activateClass);
            }

            return null;
        }

        return Task.CompletedTask;
    }
}
