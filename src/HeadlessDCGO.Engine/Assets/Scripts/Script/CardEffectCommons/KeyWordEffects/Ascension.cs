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

    /// <summary>(R3-A) 1:1 of AS-IS <c>AscensionProcess</c> (KeyWordEffects/Ascension.cs:29-51): the owner of a
    /// deleted card with &lt;Ascension&gt; chooses whether to place it as the top security card. RD-P6C2-1 RESOLVED:
    /// the last gap — <c>CardObjectController.AddSecurityCard(card, true)</c> — now exists (mirror
    /// <see cref="CardObjectController.AddSecurityCard"/>, R3-A). ADAPTATION: AS-IS <c>card.Owner.CanAddSecurity(x)</c>
    /// (Player method) → <c>new Player(card.Context, card.Owner).CanAddSecurity(x.EffectSourceCard?.InstanceId)</c>
    /// (mirror <c>card.Owner</c> is a <c>HeadlessPlayerId</c>); the bool selection is the verified W5 substrate
    /// (SetBoolSelection/WaitForEndSelect/SelectedBoolValue, BT1_111 precedent). unused <paramref name="hashtable"/>
    /// kept for AS-IS surface parity.</summary>
    public static async Task AscensionProcess(Hashtable hashtable, ICardEffect activateClass, CardSource card)
    {
        _ = hashtable; // AS-IS AscensionProcess never reads its hashtable param.
        if (new Player(card.Context, card.Owner).CanAddSecurity(activateClass?.EffectSourceCard?.InstanceId))
        {
            string selectPlayerMessage = "Will you place this card in security?";
            string notSelectPlayerMessage = "The opponent is choosing if they will use Ascension.";

            List<SelectionElement<bool>> command_SelectCommands = new List<SelectionElement<bool>>()
            {
                new SelectionElement<bool>(message: $"Yes", value: true, spriteIndex: 0),
                new SelectionElement<bool>(message: $"No", value: false, spriteIndex: 1),
            };

            GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: command_SelectCommands, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

            await GManager.instance.userSelectionManager.WaitForEndSelect();

            if (GManager.instance.userSelectionManager.SelectedBoolValue)
            {
                await CardObjectController.AddSecurityCard(card, true);
            }
        }
    }
}
