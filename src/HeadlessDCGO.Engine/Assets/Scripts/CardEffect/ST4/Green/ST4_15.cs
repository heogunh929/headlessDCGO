// Source: Assets/Scripts/CardEffect/ST4/Green/ST4_15.cs
// 1:1 mirror of the original ST4_15 (Option):
//   [Main]     Suspend 1 of your opponent's Digimon.                    -> SelectAndSuspendEffect (Tap, max 1)
//   [Security] Suspend 1 of your opponent's Digimon. Then, add this     -> AddActivateMainOptionSecurityEffect
//              card to your hand.                                          (reuse [Main]) + afterMainBody SelfToHandBody
// NOTE (Main): select -> Tap via SelectAndSuspendEffect. AS-IS maxCount = Math.Min(1, matchCount); the
// headless factory takes the fixed cap (1) directly (SelectPermanentEffect.BuildRequest clamps to the live
// candidate count), matching the sibling Option ports (ST1_15/ST1_16/ST2_15/ST2_16/ST3_16).
// NOTE (Security): the AS-IS SecuritySkill re-runs [Main] then adds this card to hand (afterMainEffect
// callback). Mirrored via AddActivateMainOptionSecurityEffect(afterMainBody: SelfToHandBody) — the follow-up
// body resolves right after the reused [Main] in the same SecuritySkill pass (driven live by SecurityResolver
// -> ActivatedEffectResolver; SecuritySkill is NOT inert — it is excluded from AllTimings auto-registration
// only, since the security-check path activates it directly, like OptionSkill).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_15 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndSuspendEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[Main] Suspend 1 of your opponent's Digimon."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            // [Security] reuse [Main] (suspend 1 opponent Digimon), THEN add this card to hand — the AS-IS
            // afterMainEffect callback, mirrored via the afterMainBody follow-up (SelfToHandBody).
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card, cardEffects: ref cardEffects,
                effectName: "Suspend 1 Digimon and add this card to hand",
                afterMainBody: new SelfToHandBody());
        }

        return cardEffects;
    }
}
