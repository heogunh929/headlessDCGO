// 1:1 mirror of the original BT22_003 (BT22/Yellow) — an F1-Tier2 WhenLinked INHERITED (digivolution-source),
// Digimon-self witness.
//
// Ported effect (AS-IS BT22_003.cs:14-78, timing WhenLinked; the card's ONLY effect):
//   * [Your Turn][Once Per Turn] "When this Digimon gets linked, 1 of your opponent's Digimon gets -2000 DP for the
//     turn." — AS-IS `new ActivateClass()` with SetIsInheritedEffect(true) (:20) + SetHashString("BT22_003_DpMinus")
//     + SetUpActivateClass(..., 1, false, ...) = maxActivationCount 1 (ONCE PER TURN), isOptional FALSE.
//     This is an INHERITED effect: AS-IS Permanent.EffectList_ForCard exposes it ONLY while THIS card is a NON-TOP
//     digivolution source of a Digimon permanent. Ported as a uniform ActivatedEffect with isInheritedEffect: true —
//     the activated bridge's digivolution-source scan (WindowResolverWiring.ScanZones) collects it from under the
//     linked host and runs the resolver in inherited-scan mode; a TOP-permanent BT22_003 does NOT fire it. It is the
//     WhenLinked analogue of BT9_062 (OnEndAttack), proving the shared inherited-source scan reaches the newly
//     EventBroadcast-registered WhenLinked timing.
//     CanUse (AS-IS :30-33) = IsExistOnBattleAreaDigimon(card) && CanTriggerWhenLinked(permanent ==
//       card.PermanentOfThisCard()) — the Digimon-self HOST gate (the reacting card's OWN permanent is the one that
//       got linked). Mirrored by BT22_035's IsEntermon (permanent.InstanceId == card.InstanceId).
//     CanActivate (AS-IS :35-39) = IsExistOnBattleAreaDigimon && IsOwnerTurn && an opponent battle-area Digimon exists.
//     Body (AS-IS ActivateCoroutine :43-72) = SelectPermanentEffect(Mode.Custom, maxCount Min(1,count),
//       canNoSelect:false, canEndNotMax:false) over opponent battle-area Digimon -> ChangeDigimonDP(-2000,
//       UntilEachTurnEnd) on the pick (mirrors BT1_054's select-then-DP shape).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT22_003 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [Your Turn][Once Per Turn] When this Digimon gets linked, -2000 DP to 1 opponent (WhenLinked, INHERITED)
        if (timing == EffectTiming.WhenLinked)
        {
            // AS-IS CanTriggerWhenLinked(permanent => permanent == card.PermanentOfThisCard()) — the Digimon-self host
            // gate (the reacting card's OWN permanent is the one that got linked). NOTE: unlike BT22_035's top-instance
            // IsEntermon (permanent.InstanceId == card.InstanceId), this card is INHERITED — `card` is the digivolution
            // SOURCE, so its InstanceId is NOT the host's. The 1:1 mirror of `permanent == card.PermanentOfThisCard()`
            // compares the linked permanent to the HOST of this source: PermanentOfThisCard().TopInstanceId (which is
            // card.InstanceId for a top card and the host id for an inherited source).
            bool IsThisPermanent(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId;

            // AS-IS OpponentCondition (BT22_003.cs:41): IsPermanentExistsOnOpponentBattleAreaDigimon.
            bool OpponentCondition(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            const string desc =
                "[Your Turn] [Once Per Turn] When this Digimon gets linked, 1 of your opponent's Digimon gets -2000 DP for the turn.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenLinked,
                canUse: ctx => CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenLinked(ctx, card, IsThisPermanent, null),
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOpponentsPermanent(card, OpponentCondition),
                body: new SelectBody(
                    card: card,
                    canTarget: OpponentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: desc,
                    onEachSelected: id => CardEffectCommons.ChangeDigimonDP(
                        new Permanent(card.Context, id,
                            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? tgtRec) && tgtRec is not null
                                ? tgtRec.OwnerId : card.Owner),
                        changeValue: -2000, EffectDuration.UntilEachTurnEnd, card)),
                maxCountPerTurn: 1,       // AS-IS ORDER=1 — [Once Per Turn]
                isOptional: false,
                description: desc,
                capHash: "BT22_003_DpMinus", // AS-IS SetHashString("BT22_003_DpMinus")
                isInheritedEffect: true));   // AS-IS SetIsInheritedEffect(true)
        }
        #endregion

        return cardEffects;
    }
}
