// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_103.cs (an Option).
// AS-IS:
//   [Main] (OptionSkill) "Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>."
//     CanUseCondition = CanTriggerOptionMainEffect(hashtable, card). CanSelectPermanentCondition(permanent) =
//     IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) (no further narrowing). ORDER=-1, ISOPTIONAL=false.
//     ActivateCoroutine, gated on HasMatchConditionPermanent: SelectPermanentEffect(selectPlayer: card.Owner,
//     mode: Custom, maxCount = Min(1, MatchConditionPermanentCount), canNoSelect:false, canEndNotMax:false)
//     picking exactly 1 own Digimon, THEN (SelectPermanentCoroutine) CardEffectCommons.GainBlocker(
//     targetPermanent: selectedPermanent, effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass).
//   [Security] "Trigger <Draw 1>. Then, add this card to your hand." — INDEPENDENT of [Main] (does not reuse
//     it): CanUseCondition = CanTriggerSecurityEffect(hashtable, card). ActivateCoroutine: new DrawClass(
//     card.Owner, 1, activateClass).Draw() THEN CardEffectCommons.AddThisCardToHand(card, activateClass).
//
// STOP ([Main] — genuine uniform-activated-effect primitive gap, not a per-card shortcut; grepped twice per
// rule 4): needs a body of "interactively select up to 1 of the owner's own battle-area Digimon (Mode.Custom
// shape: mandatory pick of min(1, available), no further narrowing beyond IsOwnerBattleAreaDigimon), THEN grant
// the SELECTED target the <Blocker> keyword for EffectDuration.UntilOpponentTurnEnd (the AS-IS GainKeywordToPermanent
// binding shape: keywords:[Blocker] on a duration-tagged EffectBinding, queried by ContinuousKeywordGate.HasKeyword
// — a DIFFERENT query plane than a numeric deltaKey/QueryScopes buff)."
// Grepped (2x) the wired select+apply activated-effect catalog:
//   (1) Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs's uniform IEffectBody set — SelectBody
//       delegates entirely to SelectPermanentEffect.Mode, and Mode.Custom is EXPLICITLY a no-op in
//       SelectPermanentEffect.BuildMutation ("Mode.Attack or Mode.Custom => null" — Assets/Scripts/Script/
//       SelectPermanentEffect.cs:213) — it cannot emit a keyword-grant from the pick. GrantContinuousBody only
//       registers an ALREADY-BUILT ICardEffect's binding (no selected-target wiring) — it has no path to target
//       the interactively-chosen id either.
//   (2) CardPortingFramework.cs's legacy select-composite catalog — ActivatedSelectEffect / ActivatedTargetBuffEffect
//       (ST1_13/ST3_13/ST3_15 shape: select target(s) -> ApplyBuff registers keywords:null, QueryScopes:
//       [ContinuousModifierGate.Scope], values:[deltaKey]=changeValue) only composes a NUMERIC delta grant, not a
//       keywords:[name] grant — it cannot drive CardEffectCommons.GainBlocker's keyword-list binding shape.
//       CardEffectCommons.GainBlocker(Permanent?, EffectDuration, CardSource) itself IS a verbatim-verified AS-IS
//       mirror (CardPortingFramework.cs:8907, via the shared GainKeywordToPermanent core at :7329) but no activated-
//       effect composes a SELECT's answer into a call to it (or any of the sibling GainRush/GainPierce/... keyword
//       grants — none is called from any ported card yet). Per rule 4 this is a primitive gap requiring a new
//       composed IEffectBody (a "SelectBody + per-target keyword grant with duration" shape) in the shared catalog,
//       out of scope for a single-card porting pass. No cardEffects registered for OptionSkill.
//
// [Security] IS portable with existing wired primitives (independent of the [Main] gap): DrawEffect (draw N,
// live-resolved by ActivatedEffectResolver's `case DrawEffect draw: draw.Apply(sink);`) followed by
// AddThisCardToHandEffect (AS-IS AddThisCardToHand mirror) — the same two-entry SecuritySkill composition already
// used by ST3_13/ST3_14/BT1_096 (AddThisCardToHandEffect has no live ActivatedEffectResolver switch case yet —
// same accepted "resolved imperatively until the interactive activation flow is wired" state as those cards, not a
// gap introduced here).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_103 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>." needs a
        // "select 1 own Digimon -> grant <Blocker> until opponent turn end" activated body that does not exist
        // yet (see file header).
        // if (timing == EffectTiming.OptionSkill) { ... }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new DrawEffect(card, drawCount: 1, "[Security] Trigger <Draw 1>."));
            cardEffects.Add(new AddThisCardToHandEffect(card, "Then, add this card to your hand."));
        }

        return cardEffects;
    }
}
