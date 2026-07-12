// 1:1 mirror of the original BT9_062 (BT9/Black) — the F1-Tier2 OnEndAttack INHERITED (digivolution-source) witness.
//
// Ported effect (AS-IS BT9_062.cs:16-89, timing OnEndAttack):
//   * [End of Attack] "If this Digimon has [Alphamon] in its name, delete 1 of your opponent's Digimon with a play
//     cost of 5 or less." — AS-IS `new ActivateClass()` with SetIsInheritedEffect(true) (:21) and NO SetHashString,
//     SetUpActivateClass(..., -1, false, ...) = maxActivationCount -1 (UNCAPPED) + isOptional FALSE.
//     This is an INHERITED effect: AS-IS Permanent.EffectList_ForCard exposes it ONLY while THIS card is a NON-TOP
//     digivolution source of a Digimon permanent (never as a top card). Ported as a uniform ActivatedEffect with
//     isInheritedEffect: true — the activated bridge's digivolution-source scan (WindowResolverWiring.ScanZones)
//     collects it from under an Alphamon host and runs the resolver in inherited-scan mode; a TOP-permanent BT9_062
//     does NOT fire it (membership). It is the OnEndAttack analogue of BT9_021 (OnAddHand), proving the shared
//     inherited-source scan reaches the newly EventBroadcast-registered OnEndAttack timing.
//     CanUse (AS-IS :46-49) = CanTriggerOnAttack(hashtable, card) — the self/attacker gate shared by OnEndAttack
//       (OnEndAttack.cs:10-13 → OnAttack.cs). For an inherited source the bridge threads the attack-end event whose
//       Subject is the HOST (top) permanent; SubjectPermanentContains passes because the host's SourceIds contain
//       this card (AS-IS AttackingPermanent.cardSources.Contains(card)).
//     CanActivate (AS-IS :52-64) = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanent) &&
//       card.PermanentOfThisCard().TopCard.ContainsCardName("Alphamon") — the Alphamon-name gate reads the HOST's
//       top card (an inherited source's PermanentOfThisCard() resolves to its host permanent).
//     Body (AS-IS ActivateCoroutine :66-88) = SelectPermanentEffect(Mode.Destroy, maxCount Min(1,count),
//       canNoSelect:false, canEndNotMax:false) over CanSelectPermanentCondition = an opponent battle-area Digimon
//       with a printed play cost of 5 or less.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_062 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [End of Attack] Alphamon → delete 1 opponent Digimon with cost <= 5 (timing OnEndAttack, INHERITED)
        if (timing == EffectTiming.OnEndAttack)
        {
            const string description =
                "[End of Attack] If this Digimon has [Alphamon] in its name, delete 1 of your opponent's Digimon with a play cost of 5 or less.";

            // AS-IS CanSelectPermanentCondition (BT9_062.cs:44-58): opponent battle-area Digimon whose printed play
            // cost is 5 or less (GetCostItself <= 5 && HasPlayCost).
            bool CanSelectPermanent(Permanent? permanent) =>
                permanent is not null
                && CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                && permanent.TopCard.GetCostItself <= 5
                && permanent.TopCard.HasPlayCost;

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            // AS-IS card.PermanentOfThisCard().TopCard.ContainsCardName("Alphamon") — the HOST top card's name (an
            // inherited source resolves PermanentOfThisCard() to its host permanent).
            bool HostIsAlphamon()
            {
                PermanentView host = card.PermanentOfThisCard();
                return !host.IsEmpty
                    && !host.TopInstanceId.IsEmpty
                    && new CardSource(card.Context, host.TopInstanceId, card.Owner).ContainsCardName("Alphamon");
            }

            // AS-IS CanUseCondition (BT9_062.cs:46-49).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerOnAttack(ctx, card);

            // AS-IS CanActivateCondition (BT9_062.cs:52-64).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanent)
                && HostIsAlphamon();

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndAttack,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SelectBody(
                    card: card,
                    canTarget: id => CanSelectPermanent(PermanentOf(id)),
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    description: "Select 1 of your opponent's Digimon with a play cost of 5 or less to delete."),
                maxCountPerTurn: null, // AS-IS SetUpActivateClass(..., -1, ...) = UNCAPPED (fires every attack-end)
                isOptional: false,
                description: description,
                isInheritedEffect: true));
        }
        #endregion

        return cardEffects;
    }
}
