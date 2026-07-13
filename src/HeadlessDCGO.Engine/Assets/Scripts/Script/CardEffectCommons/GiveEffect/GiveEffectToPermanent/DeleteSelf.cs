// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/DeleteSelf.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `AddSelfDeleteEffect` (CardEffectCommons.cs:2663, which takes the mirror's `string deleteTiming` shape —
// "own"/"opponent"/"each", see ActivatedEffects.cs:2570). The AS-IS `DeleteTiming` enum is re-declared here
// (1:1 with DeleteSelf.cs:9-14) and mapped onto the substrate's string keys.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>CardEffectCommons.DeleteTiming</c> (GiveEffect/GiveEffectToPermanent/DeleteSelf.cs:9-14).</summary>
    public enum DeleteTiming
    {
        AtTurnEnd,
        AtOwnTurnEnd,
        AtOpponentTurnEnd
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.AddSelfDeleteEffect(...)</c> (GiveEffect/GiveEffectToPermanent/DeleteSelf.cs:14) — AS-IS-signature overload; delegates to the verified substrate implementation. <paramref name="deleteTiming"/> maps onto the substrate's "own"/"opponent"/"each" string keys verbatim (AS-IS <c>AtOwnTurnEnd</c>/<c>AtOpponentTurnEnd</c>/<c>AtTurnEnd</c>).</summary>
    public static async Task AddSelfDeleteEffect(Permanent permanent, DeleteTiming deleteTiming, ICardEffect activateClass)
    {
        string mirrorTiming = deleteTiming switch
        {
            DeleteTiming.AtOwnTurnEnd => "own",
            DeleteTiming.AtOpponentTurnEnd => "opponent",
            _ => "each",
        };
        AddSelfDeleteEffect(permanent, mirrorTiming, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
