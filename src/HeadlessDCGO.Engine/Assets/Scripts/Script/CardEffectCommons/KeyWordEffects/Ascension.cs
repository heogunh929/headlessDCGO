// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Ascension.cs
// (P6 cluster2) 1:1 port. CanTrigger/CanActivate delegate to the existing Hashtable-based On-Deletion gates
// (CanUseEffects/OnDeletion.cs) exactly as AS-IS. AscensionProcess is a genuine STOP: AS-IS calls
// `CardObjectController.AddSecurityCard(card, true)` — that static zone-move helper class does not exist on
// the mirror at all (masked-verbatim gap, independently noted by the P6A PlayCardClass/CardObjectController
// foundation notes) — design item RD-P6C2-1.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CanTriggerAscension</c> (KeyWordEffects/Ascension.cs:10, verbatim).</summary>
    public static bool CanTriggerAscension(Hashtable hashtable, CardSource card) =>
        CanTriggerOnDeletion(hashtable, card);

    /// <summary>AS-IS <c>CanTriggerPermanentAscension</c> (KeyWordEffects/Ascension.cs:15, verbatim).</summary>
    public static bool CanTriggerPermanentAscension(Hashtable hashtable, Func<Permanent, bool> permanentCondition) =>
        CanTriggerOnPermanentDeleted(hashtable, permanentCondition);

    /// <summary>AS-IS <c>CanActivateAscension</c> (KeyWordEffects/Ascension.cs:22, verbatim).</summary>
    public static bool CanActivateAscension(Hashtable hashtable, CardSource card) =>
        CanActivateOnDeletion(hashtable, card);

    /// <summary>AS-IS <c>AscensionProcess</c> (KeyWordEffects/Ascension.cs:29): owner chooses whether to place
    /// this deleted card as the top security card. STOP (design item RD-P6C2-1). (R2-B) The
    /// <c>card.Owner.CanAddSecurity</c> gate and the Yes/No <c>userSelectionManager.SetBoolSelection</c>/
    /// <c>WaitForEndSelect</c>/<c>SelectedBoolValue</c> selection now all have mirror substrate — the remaining
    /// gap is only <c>CardObjectController.AddSecurityCard(card, true)</c> (RemoveFromAllArea + DigiEgg/Token
    /// branches + Insert-at-security-top + IAddSecurity emit): a whole CardObjectController zone-move helper,
    /// which is a CardController-region re-housing (R2 CardController / zone-move), out of R2-B's keyword-Process
    /// scope — porting only part of it would be simplification. Kept STOP, gap narrowed.</summary>
    public static Task AscensionProcess(Hashtable hashtable, ICardEffect activateClass, CardSource card)
    {
        throw new NotSupportedException(
            "AscensionProcess: AS-IS CardObjectController.AddSecurityCard(card, true) has no mirror zone-move " +
            "primitive yet — design item RD-P6C2-1, docs/audit/rebuild_p6_cluster2_notes.md.");
    }
}
