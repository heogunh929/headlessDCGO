// Source: Assets/Scripts/CardEffect/EX8/Black/EX8_051.cs (1:1 mirror)
// <Collision> — AS-IS (:14-18): timing == OnCounterTiming -> CollisionSelfStaticEffect(false, card, null).
//   Verbatim factory mirror (SelfKeywordByNameEffect -> ContinuousKeywordGate.Collision). The AS-IS keys
//   the static grant under OnCounterTiming; the registrar registers that timing at enter-play like the
//   other self-static keyword timings, and BlockTiming reads the live keyword binding.
// <Piercing> — AS-IS (:21-25): timing == OnDetermineDoSecurityCheck -> PierceSelfEffect(false, card, null).
//   Verbatim factory mirror (same shape as BT1_022 / BT1_026 / ST4_13 / ST7_10).
// <Fragment <3>> — AS-IS (:28-38): timing == WhenPermanentWouldBeDeleted ->
//   FragmentSelfEffect(false, card, null, trashValue: 3, effectName: "Fragment <3>", discription).
//   Verbatim factory mirror; trashValue:3 is stored on the grant and read by
//   DeletionReplacementGate.FragmentCostOf (CanFragment requires >= 3 sources; the PRE window pays 3).
// ESS "When effects trash this card from digivolution cards of a [Mineral] or [Rock] trait Digimon,
// <De-Digivolve 1> 1 of your opponent's Digimon."
//   AS-IS (:41-105): ActivateClass on EffectTiming.OnDigivolutionCardDiscarded, SetIsInheritedEffect(true).
//     CanUseCondition (:57-61): trashedPermanent = GetPermanentFromHashtable(hashtable);
//       (trashedPermanent.TopCard.ContainsTraits("Mineral") || ...("Rock")) &&
//       CanTriggerOnTrashSelfDigivolutionCard(hashtable, cardEffect => cardEffect != null, card).
//     CanActivateCondition (:63-74): IsExistOnTrash(card) && HasMatchConditionOpponentsPermanent(card,
//       CanSelectOpponentsDigimon), CanSelectOpponentsDigimon = IsPermanentExistsOnOpponentBattleAreaDigimon.
//     ORDER=-1, ISOPTIONAL=false. ActivateCoroutine (:76-104): SelectPermanentEffect(Mode.Custom,
//       maxCount:1, canNoSelect:false, canEndNotMax:false) -> new IDegeneration(pick, 1, activateClass).
//   Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom) with the sink-scoped
//   DeDigivolvePermanent(count:1) follow-up. Fires from the TRASH via the EVENT-BROADCAST bridge
//   (the AS-IS GetSkillInfos "Trash card effect" region scan — WindowResolverWiring).
//   NOTE (by-effect clause): the AS-IS cardEffectSourceCondition `cardEffect => cardEffect != null`
//   ("trashed BY an effect") is structurally implied by the headless emit sites — only the effect-driven
//   source-trash paths (DigivolutionStackHelpers) emit OnDigivolutionCardDiscarded; the deletion path's
//   DiscardEvoRoots direct trash-add deliberately does NOT (MatchStateMutationSink.cs:817) and no game
//   rule trashes sources. The headless event carries no causing-effect payload to evaluate the predicate
//   against, so it is not passed (design item C5W-1: thread the causing effect through the event if a
//   card ever needs a NARROWER by-effect filter than "any effect").
//   NOTE: AS-IS SetIsInheritedEffect(true) — same accepted, documented systemic gap as BT1_022 / BT1_079.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_051 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region Collision
        if (timing == EffectTiming.OnCounterTiming)
        {
            cardEffects.Add(CardEffectFactory.CollisionSelfStaticEffect(false, card, null));
        }
        #endregion

        #region Piercing
        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Fragment
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            // AS-IS EX8_051.cs:29-32 (local EffectDiscription; AS-IS param spelling kept by the mirror factory).
            string EffectDiscription()
            {
                return "<Fragment <3>> (When this Digimon would be deleted, by trashing any 3 of its digivolution cards, it isn't deleted.)";
            }

            cardEffects.Add(CardEffectFactory.FragmentSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null,
                trashValue: 3,
                effectName: "Fragment <3>",
                effectDiscription: EffectDiscription()));
        }
        #endregion

        #region Trash Source - ESS
        if (timing == EffectTiming.OnDigivolutionCardDiscarded)
        {
            // AS-IS CanSelectOpponentsDigimon (EX8_051.cs:52-55).
            bool CanSelectOpponentsDigimon(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            const string desc = "When effects trash this card from digivolution cards of a [Mineral] or [Rock] trait Digimon, <De-Digivolve 1> 1 of your opponent's Digimon.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDigivolutionCardDiscarded,
                // AS-IS CanUseCondition (EX8_051.cs:57-61): host-permanent trait check FIRST, then the
                // self-source-trashed trigger (the by-effect clause is implied by the emit sites — header note).
                canUse: ctx => CardEffectCommons.GetPermanentFromHashtable(ctx, card) is Permanent trashedPermanent
                    && (trashedPermanent.TopCard.ContainsTraits("Mineral") || trashedPermanent.TopCard.ContainsTraits("Rock"))
                    && CardEffectCommons.CanTriggerOnTrashSelfDigivolutionCard(ctx, card),
                // AS-IS CanActivateCondition (EX8_051.cs:63-74).
                canActivate: () => CardEffectCommons.IsExistOnTrash(card)
                    && CardEffectCommons.HasMatchConditionOpponentsPermanent(card, CanSelectOpponentsDigimon),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelectOpponentsDigimon,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "Select 1 Digimon to De-Digivolve.",
                    // AS-IS SelectPermanentCoroutine (:99-102): new IDegeneration(permanent, 1, activateClass).
                    onEachSelectedWithSink: (c, sink, id) => CardEffectCommons.DeDigivolvePermanent(sink, c, id, count: 1)),
                maxCountPerTurn: null,
                isOptional: false,
                description: desc));
        }
        #endregion

        return cardEffects;
    }
}
