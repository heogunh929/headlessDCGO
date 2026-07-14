// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Execute.cs
// (R2-A) R1 now provides Permanent.CanAttack, so CanActivateExecute is rehoused 1:1 with AS-IS. ExecuteProcess
// remains STOP: AS-IS appends three effects to Permanent.UntilEndAttackEffects (a restrict-defender gate, the
// end-of-attack self-delete, and the detail text) and that per-attack effect list has no mirror Permanent
// member (design item RD-R2-01). The LIVE Execute end-of-turn attack path is implemented independently via
// EndOfTurnEffectAttack + EffectDrivenAttack (Headless/Runtime/EndOfTurnEffectAttack.cs "Execute-1", with
// SelfDeleteAtEndOfAttack), so this old-model ActivateClass Process path is dead-relative to actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>(R2-A) AS-IS <c>CanActivateExecute</c> (KeyWordEffects/Execute.cs:8, verbatim): on the battle
    /// area and able to make an Execute attack (R1 <c>Permanent.CanAttack(isExecute: true)</c>).</summary>
    public static bool CanActivateExecute(CardSource cardSource, ICardEffect activateClass)
    {
        Permanent selfPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);

        return IsExistOnBattleArea(cardSource)
            && selfPermanent != null
            && selfPermanent.CanAttack(activateClass, isExecute: true);
    }

    /// <summary>(R2-A) AS-IS <c>ExecuteProcess</c> (KeyWordEffects/Execute.cs:18): attack (player or any Digimon,
    /// incl. unsuspended) then self-delete at the attack's end. STOP — AS-IS appends to
    /// <c>Permanent.UntilEndAttackEffects</c> (the restrict-defender gate + the end-of-attack DeleteSelfEffect +
    /// the detail text), a per-attack effect list with no mirror <see cref="Permanent"/> member. R1 provides
    /// <c>CanAttack</c> and the SelectAttackEffect step is the <c>EffectDrivenAttack</c> substrate, but the
    /// UntilEndAttackEffects appends cannot be reproduced — design item RD-R2-01. The live Execute path is
    /// implemented independently via <see cref="Headless.Runtime.EndOfTurnEffectAttack"/> +
    /// <see cref="Headless.Runtime.EffectDrivenAttack"/> (SelfDeleteAtEndOfAttack).</summary>
    public static Task ExecuteProcess(CardSource cardSource, ICardEffect activateClass)
    {
        throw new NotSupportedException(
            "ExecuteProcess: AS-IS appends the restrict-defender gate + end-of-attack self-delete + detail to " +
            "Permanent.UntilEndAttackEffects, which has no mirror Permanent member — design item RD-R2-01. The " +
            "live Execute attack path is EndOfTurnEffectAttack + EffectDrivenAttack (SelfDeleteAtEndOfAttack).");
    }
}
