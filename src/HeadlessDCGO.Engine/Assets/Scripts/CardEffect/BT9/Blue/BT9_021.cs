// 1:1 mirror of the original BT9_021 (BT9/Blue) — the F1-M1-INHERITSCAN witness: an INHERITED (digivolution-source)
// activated reactor at OnAddHand.
//
// Ported effect (AS-IS BT9_021.cs:76-156, timing OnAddHand):
//   * [Your Turn][Once Per Turn] "When an effect adds a card to your hand, return 1 of your opponent's level 3
//     Digimon to its owner's hand." — AS-IS `new ActivateClass()` with SetIsInheritedEffect(true) (:81) and
//     SetHashString("Bounce_BT9_021") (:82). This is an INHERITED effect: AS-IS Permanent.EffectList_ForCard
//     exposes it ONLY while THIS card is a NON-TOP digivolution source of a Digimon permanent (never as a top
//     card). Ported as a uniform ActivatedEffect with isInheritedEffect: true — the activated bridge's
//     digivolution-source scan (WindowResolverWiring.ScanZones) collects it from under a host and runs the
//     resolver in inherited-scan mode, while a TOP-permanent BT9_021 does NOT fire it (membership).
//     CanUse (AS-IS :106-120) = IsExistOnBattleArea(card) && IsOwnerTurn(card) && CanTriggerWhenAddHand(player ==
//       card.Owner, cardEffect => cardEffect != null). For an inherited source, IsExistOnBattleArea/IsOwnerTurn
//       read through the SOURCE's CardSource, whose PermanentOfThisCard() resolves to the HOST permanent (the
//       card sits under it), so the gate is host-aware exactly as AS-IS `card.PermanentOfThisCard()`.
//     CanActivate (AS-IS :122-130) = IsExistOnBattleArea(card). The AS-IS HasMatchConditionPermanent guard lives
//       INSIDE the coroutine (:134), so the effect activates (consumes the [Once Per Turn] use) even with no
//       target and the select no-ops — mirrored by SelectBody's clamp-to-live-candidates.
//     Body (AS-IS ActivateCoroutine :132-155) = SelectPermanentEffect(Mode.Bounce, maxCount Min(1,count),
//       canNoSelect:false) over CanSelectPermanentCondition = opponent battle-area Digimon with Level == 3 and a
//       printed level -> return the pick to its owner's hand.
//     maxCountPerTurn 1 (AS-IS order 1), isOptional FALSE, capHash "Bounce_BT9_021" (AS-IS SetHashString).
//
// The AS-IS OnEnterFieldAnyone [On Play] effect (:15-74: "when you play a blue Tamer, <Draw 1>") is a NON-inherited
// TOP-card [On Play] reactor served by the action-wired OnEnterFieldAnyone path, orthogonal to the inherited
// OnAddHand bridge under test; it is deliberately OMITTED (same scoping as the other F1 witnesses).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_021 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [Your Turn] When an effect adds a card to your hand (timing OnAddHand, INHERITED)
        if (timing == EffectTiming.OnAddHand)
        {
            const string description =
                "[Your Turn][Once Per Turn] When an effect adds a card to your hand, return 1 of your opponent's level 3 Digimon to its owner's hand.";

            // AS-IS CanSelectPermanentCondition (BT9_021.cs:90-104): opponent battle-area Digimon with Level == 3.
            bool CanSelectPermanent(Permanent? permanent) =>
                permanent is not null
                && CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                && permanent.Level == 3 && permanent.TopCard.HasLevel;

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            // AS-IS CardEffectCondition (BT9_021.cs:112): cardEffect => cardEffect != null (an EFFECT-driven add).
            bool CauseCondition(CardSource? cause) => cause is not null;

            // AS-IS CanUseCondition (BT9_021.cs:106-120).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerWhenAddHand(ctx, card, player => player == card.Owner, CauseCondition);

            // AS-IS CanActivateCondition (BT9_021.cs:122-130): IsExistOnBattleArea only (the match guard is inside
            // the coroutine — SelectBody no-ops when no candidate matches).
            bool CanActivate() => CardEffectCommons.IsExistOnBattleArea(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAddHand,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SelectBody(
                    card: card,
                    canTarget: id => CanSelectPermanent(PermanentOf(id)),
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Bounce,
                    description: "Select 1 of your opponent's level 3 Digimon to return to its owner's hand."),
                maxCountPerTurn: 1,
                isOptional: false,
                description: description,
                capHash: "Bounce_BT9_021",
                isInheritedEffect: true));
        }
        #endregion

        return cardEffects;
    }
}
