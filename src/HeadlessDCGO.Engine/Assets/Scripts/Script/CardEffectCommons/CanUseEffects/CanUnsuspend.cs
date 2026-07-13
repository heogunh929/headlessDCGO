// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/CanUnsuspend.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    // AS-IS CanUnsuspend(Permanent) is already defined in CardEffectCommons.cs (substrate reimplementation,
    // identical signature — no Hashtable/ctx param to make it an overload). Duplicating it verbatim would be
    // CS0111, and CardEffectCommons.cs must not be edited, so it is omitted here.
    // See docs/audit/rebuild_p5_gates_missing.md. The verbatim body was:
    //   return permanent != null && permanent.TopCard != null && permanent.IsSuspended && permanent.CanUnsuspend;
}
