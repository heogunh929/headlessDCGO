// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Execute.cs
// (P6 cluster2) Genuine STOP: AS-IS CanActivateExecute/ExecuteProcess depend on Permanent.CanAttack (the
// general attack-eligibility gate, Permanent.cs:2090) and SelectAttackEffect (no mirror component) — same gap
// as Vortex (design item RD-P6C2-8, this file's own entry RD-P6C2-9). The LIVE Execute end-of-turn attack path
// is already implemented independently via EndOfTurnEffectAttack + EffectDrivenAttack
// (Headless/Runtime/EndOfTurnEffectAttack.cs "Execute-1"), so this old-model ActivateClass path is
// dead-relative to actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CanActivateExecute</c> (KeyWordEffects/Execute.cs:8). STOP — design item RD-P6C2-9.</summary>
    public static bool CanActivateExecute(CardSource cardSource, ICardEffect activateClass)
    {
        throw new NotSupportedException(
            "CanActivateExecute: AS-IS Permanent.CanAttack has no mirror — design item RD-P6C2-9, " +
            "docs/audit/rebuild_p6_cluster2_notes.md.");
    }

    /// <summary>AS-IS <c>ExecuteProcess</c> (KeyWordEffects/Execute.cs:18). STOP — design item RD-P6C2-9.</summary>
    public static Task ExecuteProcess(CardSource cardSource, ICardEffect activateClass)
    {
        throw new NotSupportedException(
            "ExecuteProcess: AS-IS Permanent.CanAttack/SelectAttackEffect have no mirror — design item RD-P6C2-9, " +
            "docs/audit/rebuild_p6_cluster2_notes.md.");
    }
}
