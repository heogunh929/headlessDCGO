// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_093.cs — a Red Option.
// 1:1 mirror of the AS-IS BT1_093.
//   [Main] (OptionSkill) "1 of your Digimon gets +2000 DP and <Security Attack +1> for the turn." ActivateClass
//     (CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false). ActivateCoroutine (guarded by
//     HasMatchConditionPermanent): SelectPermanentEffect.SetUp(mode: Custom, maxCount = Min(1, count),
//     canNoSelect:false, canEndNotMax:false). SelectPermanentCoroutine(permanent) applies BOTH modifiers to the
//     SAME selected permanent: CardEffectCommons.ChangeDigimonDP(permanent, changeValue: 2000,
//     EffectDuration.UntilEachTurnEnd, activateClass) THEN CardEffectCommons.ChangeDigimonSAttack(permanent,
//     changeValue: 1, EffectDuration.UntilEachTurnEnd, activateClass). (AS-IS uses ChangeDigimonDP — a
//     current-DP delta — NOT ChangeBaseDigimonDP; and ChangeDigimonSAttack, a current Security-Attack delta.)
//   [Security] (SecuritySkill) AddThisCardToHand(card, activateClass) — its own CanTriggerSecurityEffect-gated
//     ActivateClass (does NOT reuse [Main]).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom) with the AS-IS SelectPermanentCoroutine
//   follow-up wired via SelectBody.onEachSelected -> ChangeDigimonDP + ChangeDigimonSAttack, both on the SAME
//   picked id (a single select feeds both modifiers, matching the AS-IS one-coroutine shape).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_093 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: () => CardEffectCommons.HasMatchConditionPermanent(card, CanSelect),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[Main] 1 of your Digimon gets +2000 DP and <Security Attack +1> for the turn.",
                    onEachSelected: id =>
                    {
                        var target = new Permanent(card.Context, id, card.Owner);
                        CardEffectCommons.ChangeDigimonDP(target, changeValue: 2000, EffectDuration.UntilEachTurnEnd, card);
                        CardEffectCommons.ChangeDigimonSAttack(target, changeValue: 1, EffectDuration.UntilEachTurnEnd, card);
                    }),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] 1 of your Digimon gets +2000 DP and <Security Attack +1> for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new AddThisCardToHandEffect(card, "[Security] Add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
