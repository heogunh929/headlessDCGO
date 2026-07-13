// 1:1 mirror of the original BT15_083 (BT15/Blue) — a Tamer. Non-inherited top-permanent witness for the
// F1-Tier1 OnAddHand activated bridge (SELF player-scope + a CAUSE condition on the causing effect).
//
// Ported effects (AS-IS BT15_083.cs):
//   * [Your Turn] "When one of your Digimon's effects adds cards to your hand, by suspending this Tamer, gain 1
//     memory." (AS-IS :83-146) -> uniform ActivatedEffect at OnAddHand whose BODY is SuspendSelfAndGainMemoryBody(1)
//     (AS-IS ActivateCoroutine: SuspendPermanentsClass(self).Tap() then card.Owner.AddMemory(1)), isOptional TRUE
//     ("by suspending"), maxCountPerTurn null (AS-IS -1, uncapped). CanUse = IsExistOnBattleArea + IsOwnerTurn +
//     CanTriggerWhenAddHand(player == card.Owner, CAUSE = IsOwnerEffect && IsDigimonEffect). The PLAYER-SCOPE half
//     self-scopes on the GAINING player being the OWNER (AS-IS `player => player == card.Owner`); the CAUSE half
//     mirrors AS-IS `cardEffect => IsOwnerEffect(cardEffect, card) && cardEffect.IsDigimonEffect` — the add must be
//     driven by one of the OWNER'S DIGIMON effects (cause card owned by the owner AND a Digimon). A non-effect add
//     (turn draw, no cause) threads NO cause => the condition sees null => false. CanActivate = IsExistOnBattleArea
//     + CanActivateSuspendCostEffect. AS-IS `new ActivateClass()` (uniform ACTIVATED) => rides the OnAddHand
//     EventBroadcast activated bridge (HasActivatedEffectsAt true), single-fires. NON-inherited TOP-permanent reactor.
//   * SecuritySkill -> PlaySelfTamerSecurityEffect (AS-IS :152, self-Tamer security play).
//
// STOP / design item F1-BT15_083-REVEAL — the AS-IS [On Play] OnEnterFieldAnyone effect (AS-IS :14-80: "reveal
// top 3, add 1 [Gabumon]/[Garurumon] to hand, return the rest to the deck bottom") is NOT ported here: it is a
// reveal-and-conditional-select-to-hand whose name-match select primitive is orthogonal to the OnAddHand bridge
// under test. Deliberately omitted rather than mis-modelled (it does not affect the OnAddHand reactor's fidelity).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT15.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT15_083 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [Your Turn] When one of your Digimon's effects adds cards to your hand (timing OnAddHand)
        if (timing == EffectTiming.OnAddHand)
        {
            // AS-IS CardEffectCondition: IsOwnerEffect(cardEffect, card) && cardEffect.IsDigimonEffect. In headless
            // the cause is the causing effect's SOURCE card (null when the add carried no effect cause).
            bool CauseCondition(CardSource? cause) =>
                cause is not null && cause.Owner == card.Owner && cause.IsDigimon;

            // AS-IS CanUseCondition: IsExistOnBattleArea && IsOwnerTurn && CanTriggerWhenAddHand(player == owner, cause).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerWhenAddHand(ctx, card, player => player == card.Owner, CauseCondition);

            // AS-IS CanActivateCondition: IsExistOnBattleArea && CanActivateSuspendCostEffect.
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanActivateSuspendCostEffect(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAddHand,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SuspendSelfAndGainMemoryBody(1),
                maxCountPerTurn: null,
                isOptional: true,
                description: "[Your Turn] When one of your Digimon's effects adds cards to your hand, by suspending this Tamer, gain 1 memory."));
        }
        #endregion

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
