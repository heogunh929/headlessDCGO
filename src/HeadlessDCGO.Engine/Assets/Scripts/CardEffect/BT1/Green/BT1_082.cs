// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_082.cs
// 1:1 mirror of the original BT1_082 (BT1/Green).
//   [Opponent's Turn] When an opponent's Digimon attacks a player, if this Digimon is suspended, spend 1 of
//   your opponent's Digimon.
//   AS-IS: ActivateClass on EffectTiming.OnAllyAttack,
//   CanUseCondition = IsExistOnBattleArea(card) && IsOpponentTurn(card)
//       && CanTriggerOnPermanentAttack(hashtable, PermanentCondition)
//       && GManager.instance.attackProcess.DefendingPermanent == null,
//   PermanentCondition(permanent) = IsOpponentPermanent(permanent, card) — the ATTACKING permanent (event
//       subject) is an opponent's permanent (relative to this card's owner).
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition)
//       && card.PermanentOfThisCard().IsSuspended,
//   CanSelectPermanentCondition(permanent) = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
//       && !permanent.HasBlocker,
//   ORDER=-1 (maxCountPerTurn:null), ISOPTIONAL=false,
//   ActivateCoroutine = SelectPermanentEffect(Mode.Tap) with maxCount = Min(1, MatchConditionPermanentCount),
//   canNoSelect:false, canEndNotMax:false, mode:Tap.
// Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with body=SelectBody(Mode.Tap) — same
// CanSelectPermanentCondition/select shape as the sibling BT1_079/BT6_054 ports (AS-IS permanent.HasBlocker
// mirrored via the self-static keyword gate ContinuousKeywordGate.HasKeyword, negated for "without <Blocker>").
// "GManager.instance.attackProcess.DefendingPermanent == null" (the attack targets the player directly, no
// Digimon target chosen) is the headless AttackController.Current.IsDirectAttack flag (HeadlessAttackState),
// read live off card.Context — 1:1 with the AS-IS BT1_001 sibling's "!= null" check for the opposite ("attacks
// an opponent's Digimon") phrasing. "if this Digimon is suspended" reads THIS card's own permanent via
// CardEffectCommons.IsSuspended(card, card.PermanentOfThisCard().TopInstanceId) — the BT1_053 convention.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_082 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool PermanentCondition(Permanent permanent) => CardEffectCommons.IsOpponentPermanent(permanent, card);

            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && !ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker);

            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOpponentTurn(card)
                    && CardEffectCommons.CanTriggerOnPermanentAttack(ctx, card, PermanentCondition)
                    && card.Context.AttackController.Current.IsDirectAttack;

            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition)
                    && CardEffectCommons.IsSuspended(card, card.PermanentOfThisCard().TopInstanceId);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelectPermanentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Tap,
                    description: "[Opponent's Turn] When an opponent's Digimon attacks a player, if this Digimon is suspended, spend 1 of your opponent's Digimon."),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Opponent's Turn] When an opponent's Digimon attacks a player, if this Digimon is suspended, spend 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
