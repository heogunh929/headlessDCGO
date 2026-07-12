// 1:1 mirror of the original BT19_071 (BT19/Purple) OnDiscardLibrary block — the F1-Tier1 OnDiscardLibrary witness.
//
// Ported effect (AS-IS BT19_071.cs, timing OnDiscardLibrary):
//   * [All Turns][Once Per Turn] "When effects trash cards from your deck, delete 1 of your opponent's level 5 or
//     lower Digimon." -> uniform ActivatedEffect (maxCountPerTurn 1, isOptional FALSE, capHash
//     "DeleteDigimon_BT19_071" = AS-IS SetHashString). CanUse = IsExistOnBattleAreaDigimon +
//     CanTriggerWhenDiscardLibrary(cardSource.Owner == card.Owner) — the AS-IS OnDiscardLibrary gate has NO
//     CardEffect!=null requirement (WhenDiscardLibrary.cs), only a discarded-card any-match (here: a card from YOUR
//     deck). Body = ActivatedSelectEffect(Mode.Destroy) over the opponent's battle-area Digimon of level <= 5,
//     maxCount 1, canNoSelect false. This is an ANYONE/cross-card reactor (it deletes an OPPONENT'S Digimon when
//     YOUR deck is trashed), so it needs the EventBroadcast field scan, not a subject-scoped path.
//     OnDiscardLibrary is an F1-Tier1 EventBroadcast bridge timing: headless derives it from the trashed deck card's
//     CardMoved (Library->Trash, TriggerTimingMap), threads that as the driving event (subject = the trashed card),
//     and the gate any-matches the trashed card's owner.
//
// The AS-IS [On Play] and [When Digivolving] blocks (trash top 2 of deck, then gain <Blocker>) are intentionally
// OMITTED — this witness exercises only the OnDiscardLibrary bridge (same scoping as the other F1 witnesses).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

public sealed class BT19_071 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region All Turns (timing OnDiscardLibrary)
        if (timing == EffectTiming.OnDiscardLibrary)
        {
            const string description =
                "[All Turns][Once Per Turn] When effects trash cards from your deck, delete 1 of your opponent's level 5 or lower Digimon.";

            // AS-IS CanSelectPermanentCondition: opponent battle-area Digimon with a printed level <= 5.
            bool CanSelectPermanent(Permanent? permanent) =>
                permanent is not null
                && CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                && permanent.TopCard.HasLevel && permanent.Level <= 5;

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            // AS-IS CanUseCondition: IsExistOnBattleAreaDigimon && CanTriggerWhenDiscardLibrary(cardSource.Owner == card.Owner).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && CardEffectCommons.CanTriggerWhenDiscardLibrary(ctx, card, cardSource => cardSource.Owner == card.Owner);

            // AS-IS CanActivateCondition: IsExistOnBattleAreaDigimon && HasMatchConditionPermanent(CanSelectPermanent).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanent);

            var body = new ActivatedSelectEffect(
                card,
                canTarget: id => CanSelectPermanent(PermanentOf(id)),
                maxCount: 1,
                canNoSelect: false,
                canEndNotMax: false,
                SelectPermanentEffect.Mode.Destroy,
                description);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDiscardLibrary,
                canUse: CanUse,
                canActivate: CanActivate,
                body: body,
                maxCountPerTurn: 1,
                isOptional: false,
                description: description,
                capHash: "DeleteDigimon_BT19_071"));
        }
        #endregion

        return cardEffects;
    }
}
