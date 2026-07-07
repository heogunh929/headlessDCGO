// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_098.cs (ref BT1_017 / BT1_025, ST3_14 / BT1_093)
// 1:1 mirror of the original BT1_098 — an Option.
//   [Main]     Select 1 of your Digimon (mandatory pick of min(1, available), Mode.Custom-shaped
//              SelectPermanentEffect): it gains <Jamming> (can't be deleted in battles against Security
//              Digimon) for the turn — AS-IS ActivateCoroutine's SelectPermanentCoroutine calls
//              CardEffectCommons.GainJamming(targetPermanent, EffectDuration.UntilEachTurnEnd, activateClass).
//              -> STOP: no headless primitive applies a per-target KEYWORD GRANT to a select-once target.
//              The uniform IEffectBody catalog (Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs,
//              grepped 2x) has: DrawBody, MemoryBody, RecoveryBody, TrashSecurityBody,
//              SelectTrashHandThenSelfMutationBody, SuspendSelfAndGainMemoryBody, SelfToHandBody,
//              GrantContinuousBody, and SelectBody. SelectBody's Mode.Custom is EXPLICITLY a no-op in
//              SelectPermanentEffect.BuildMutation ("Mode.Attack or Mode.Custom => null" —
//              Assets/Scripts/Script/SelectPermanentEffect.cs:213), matching the BT1_086 precedent (its
//              [Your Turn] branch hit the identical Mode.Custom-is-a-no-op wall). GrantContinuousBody
//              registers a FIXED continuous effect and ignores the `selected` list entirely (its Apply
//              signature takes `selected` but never reads it) — it cannot bind the grant to a dynamically
//              chosen target, only to the card itself. CardEffectFactory (grepped 2x) has no
//              "SelectAndGrantKeyword"/"SelectAndGrantJamming" style factory either (only
//              SelectAndBuffDpEffect / SelectAndBuffSAttackEffect / SelectAndRestrictEffect, which all carry
//              a numeric modifier delta, not a keyword-gate registration). CardEffectCommons.GainJamming
//              (CardPortingFramework.cs:8927, a 1:1 mirror of the AS-IS static helper) exists but nothing
//              wires it to a per-target select flow yet — that composed "select 1 own Digimon, mandatory
//              min(1, available), then grant it a keyword for the turn" body does not exist. Adding it is
//              engine-file (ActivatedEffect.cs) primitive work, out of scope for a single-card porting pass
//              per rule 4. Left unregistered; no new primitive added, no engine change made, no throw.
//   [Security] Add this card to its owner's hand. -> AddThisCardToHandEffect (mirrors ST3_14 / BT1_093;
//              distinct from AddActivateMainOptionSecurityEffect — this Security effect does NOT reuse
//              [Main], it is AS-IS's own independent "Add this card to hand" ActivateClass gated by
//              CanTriggerSecurityEffect, same shape as ST3_14/BT1_093's Security branch).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_098 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "1 of your Digimon gains <Jamming> for the turn" — needs a composed
        // select-1-then-grant-keyword-to-the-selected-target activated body. No such primitive exists
        // (see file header). Not registered; no new primitive added, no engine change made.

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
