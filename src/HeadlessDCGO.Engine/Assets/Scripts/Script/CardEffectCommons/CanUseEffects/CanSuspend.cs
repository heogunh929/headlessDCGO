// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/CanSuspend.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    // AS-IS CanActivateSuspendCostEffect(CardSource, bool) is already defined in CardEffectCommons.cs
    // (substrate reimplementation, identical signature — no Hashtable/ctx param to make it an overload).
    // Duplicating it verbatim would be CS0111, and CardEffectCommons.cs must not be edited, so it is
    // omitted here. See docs/audit/rebuild_p5_gates_missing.md. The verbatim body was:
    //   return CanActivatePermanentSuspendCostEffect(ICardEffect.ResolvePermanentOfThisCard(card), includeBreeding);

    #region Can activate effects by suspending permanent
    public static bool CanActivatePermanentSuspendCostEffect(Permanent permanent, bool includeBreeding = false)
    {
        if (IsPermanentExistsOnBattleArea(permanent))
        {
            if (!permanent.IsSuspended && permanent.CanSuspend)
            {
                return true;
            }
        }

        if (includeBreeding)
        {
            if (IsPermanentExistsOnBreedingArea(permanent))
            {
                if (!permanent.IsSuspended && permanent.CanSuspend)
                {
                    return true;
                }
            }
        }

        return false;
    }
    #endregion
}
