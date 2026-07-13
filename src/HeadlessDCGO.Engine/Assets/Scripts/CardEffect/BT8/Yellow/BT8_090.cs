// 1:1 mirror of the original BT8_090 (BT8/Yellow) — a Tamer. Non-inherited top-permanent witness for the
// F1-Tier1 OnAddSecurity activated bridge (SELF player-scope, NO cause gate).
//
// Ported effects (AS-IS BT8_090.cs):
//   * OnStartTurn -> SetMemoryTo3TamerEffect (AS-IS :16, "[Start of Your Turn] if 2 or less memory, set to 3").
//   * [Your Turn] "When a card is added to your security stack, you may suspend this Tamer to gain 1 memory."
//     (AS-IS :20-67) -> uniform ActivatedEffect at OnAddSecurity whose BODY is SuspendSelfAndGainMemoryBody(1)
//     (AS-IS ActivateCoroutine: SuspendPermanentsClass(self).Tap() then card.Owner.AddMemory(1)), isOptional TRUE
//     ("you may"), maxCountPerTurn null (AS-IS -1, uncapped). CanUse = IsExistOnBattleArea + IsOwnerTurn +
//     CanTriggerWhenAddSecurity(player == card.Owner) — the PLAYER-SCOPE gate self-scopes on the GAINING player
//     being the OWNER (AS-IS `player => player == card.Owner`, no cause). CanActivate = IsExistOnBattleArea +
//     CanActivateSuspendCostEffect. AS-IS represents it as `new ActivateClass()` (the uniform ACTIVATED shape), so
//     it rides the OnAddSecurity EventBroadcast activated bridge (HasActivatedEffectsAt true) and single-fires (no
//     scheduler-half double-collect). NON-inherited (no SetIsInheritedEffect), a TOP-permanent reactor.
//   * SecuritySkill -> PlaySelfTamerSecurityEffect (AS-IS :71, self-Tamer security play, as ST1_12/ST4_14).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT8_090 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        #region [Your Turn] When a card is added to your security stack (timing OnAddSecurity)
        if (timing == EffectTiming.OnAddSecurity)
        {
            // AS-IS CanUseCondition: IsExistOnBattleArea && IsOwnerTurn && CanTriggerWhenAddSecurity(player == owner).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerWhenAddSecurity(ctx, card, player => player == card.Owner);

            // AS-IS CanActivateCondition: IsExistOnBattleArea && CanActivateSuspendCostEffect.
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanActivateSuspendCostEffect(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAddSecurity,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new SuspendSelfAndGainMemoryBody(1),
                maxCountPerTurn: null,
                isOptional: true,
                description: "[Your Turn] When a card is added to your security stack, you may suspend this Tamer to gain 1 memory."));
        }
        #endregion

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
