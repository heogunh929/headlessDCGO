// Source: DCGO/Assets/Scripts/Script/CardController.cs:1100-1146 (`class OnEnterFieldHashtableParams`, nested
// inside the `#region Play permanents` / `#region Hashtable setting class` regions of the ~5988-line Unity
// `CardController` MonoBehaviour). 1:1 port of JUST that class — a pure-data holder describing the
// digivolution-root/level/root-kind context of a just-entered-field permanent, built by
// `HashtableSetting.OnEnterFieldHashtable` and consumed by OnEnterFieldAnyone-timing card effects. The
// surrounding CardController class (and its other nested play/permanent classes — PlayPermanentClass,
// UseOptionClass, HatchDigiEggClass, etc.) is out of scope for this port.
//
// Namespace: `...Script.CardEffectCommons`, matching `Permanent`/`CardSource` (CardEffectCommons) and
// `SelectCardEffect.Root` (referenced verbatim — `SelectCardEffect` already exists at
// Assets/Scripts/Script/SelectCardEffect.cs, `...Script` namespace, `using`d below) which this class's ctor
// takes as parameters.
//
// No Unity/Photon references in the AS-IS class body — ported verbatim (`.Clone()` is the already-ported
// IEnumerableExtension.Clone, `...Script` namespace, `using`d below).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

// AS-IS CardController.cs:1100-1146.
public class OnEnterFieldHashtableParams
{
    public OnEnterFieldHashtableParams(
        Permanent permanent,
        List<CardSource> evoRoots,
        List<CardSource> evoRootTops,
        SelectCardEffect.Root root,
        List<int> oldLevels,
        bool isFromDigimonDigivolutionCards,
        int digiXrosCount = 0,
        int assemblyCount = 0)
    {
        Permanent = permanent;

        if (evoRoots != null)
        {
            EvoRoots = evoRoots.Clone();
        }

        if (evoRootTops != null)
        {
            EvoRootTops = evoRootTops.Clone();
        }

        Root = root;

        if (oldLevels != null)
        {
            OldLevels = oldLevels.Clone();
        }

        IsFromDigimonDigivolutionCards = isFromDigimonDigivolutionCards;

        DigixrosCount = digiXrosCount;

        AssemblyCount = assemblyCount;
    }

    public Permanent Permanent { get; private set; } = null;
    public List<CardSource> EvoRoots = new List<CardSource>();
    public List<CardSource> EvoRootTops { get; private set; } = new List<CardSource>();
    public SelectCardEffect.Root Root { get; private set; } = SelectCardEffect.Root.None;
    public List<int> OldLevels { get; private set; } = new List<int>();
    public bool IsFromDigimonDigivolutionCards { get; private set; } = false;
    public int DigixrosCount { get; private set; } = 0;
    public int AssemblyCount { get; private set; } = 0;
}
