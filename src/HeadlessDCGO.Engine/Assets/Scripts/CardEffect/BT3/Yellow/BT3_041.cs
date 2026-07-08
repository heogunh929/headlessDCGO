// STOP: AS-IS BT3_041 [When Attacking] (security <= 3, owner has a yellow Digimon in the trash, and
// CanAddSecurity) selects 1 owner's yellow Digimon card FROM THE TRASH and places it face-down on top of
// the owner's security stack (SelectCardEffect root: Trash, mode: Custom -> CardObjectController.AddSecurityCard
// + a recovery visual + IAddSecurity trigger). No existing headless primitive selects from Trash and adds
// the result to the SECURITY zone: PlacePermanentInSecurityAndProcessAccordingToResult only moves a
// BATTLE-AREA permanent's top card into security, and SelectAndPlayFromZoneEffect/SelectAndTrashDigivolutionEffect
// operate over Trash/Hand for play/trash-digivolution, not for adding to Security. Approximating with any
// of these would move the wrong zone or the wrong destination (forbidden), so this stays STOP.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_041 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [When Attacking] If security <= 3, place 1 owner's yellow Digimon card from the trash on
        // top of security face down — no select-from-Trash-to-Security primitive exists (see file header).

        return cardEffects;
    }
}
