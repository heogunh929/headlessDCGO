// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/ArtsDigivolve.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS ArtsDigivolve.cs factory partial.
// Returns the OptionResolutionClass kind-class. Replaces the monolith's old invented ArtsDigivolveSelfEffect-based
// ArtsDigivolveEffect.
// VERBATIM-MISSING infra (kept AS-IS per standing no-simplification directive; logged in
// docs/audit/rebuild_p4_factory_missing.md): OptionResolutionClass / SetUpOptionResolutionClass,
// ContinuousController, PlayCardClass. SelectPermanentEffect / SelectCardEffect are the mirror types.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // kind-class layer

public partial class CardEffectFactory
{
    #region Processing of [Arts Digivolve]
    // STOP (design item RD-P6C2-10, docs/audit/rebuild_p6_cluster2_notes.md): AS-IS ArtsDigivolveEffect depends
    // on `CardSource.CanPlayCardTargetFrame`/`Permanent.PermanentFrame` (CardSource.cs/Permanent.cs, out of this
    // cluster's KeyWordEffects/CanUseEffects/kind-class/DataBase scope) and `ContinuousController`/the AS-IS
    // Hashtable-ctor `PlayCardClass` (both already flagged VERBATIM-MISSING by this file's own P4 header,
    // docs/audit/rebuild_p4_factory_missing.md — a standing gap, not newly introduced here). The declaration
    // gate (CanUseCondition/IsExistOnExecutingArea) is real and portable; the resolution path is not, so its
    // closures throw rather than reference the missing members.
    public static OptionResolutionClass ArtsDigivolveEffect(CardSource card)
    {
        if (card == null) return null;
        OptionResolutionClass artsDigivolutionClass = new();
        artsDigivolutionClass.SetUpICardEffect("Arts Digivolve", CanUseCondition, card);
        artsDigivolutionClass.SetUpOptionResolutionClass(ResolutionCoroutine, CanResolveCondition);
        return artsDigivolutionClass;

        bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnExecutingArea(card);

        bool CanResolveCondition(CardSource optionCard)
        {
            throw new NotSupportedException(
                "ArtsDigivolveEffect.CanResolveCondition: AS-IS CardSource.CanPlayCardTargetFrame/" +
                "Permanent.PermanentFrame have no mirror — design item RD-P6C2-10, " +
                "docs/audit/rebuild_p6_cluster2_notes.md.");
        }

        Task ResolutionCoroutine(CardSource optionCard)
        {
            throw new NotSupportedException(
                "ArtsDigivolveEffect.ResolutionCoroutine: AS-IS ContinuousController/PlayCardClass(Hashtable ctor) " +
                "have no mirror — design item RD-P6C2-10, docs/audit/rebuild_p6_cluster2_notes.md.");
        }
    }
    #endregion
}
