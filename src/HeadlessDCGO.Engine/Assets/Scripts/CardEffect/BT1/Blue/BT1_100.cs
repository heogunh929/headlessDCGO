// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_100.cs — an Option (two timings).
// 1:1 mirror of the AS-IS BT1_100.
//   [Main] (OptionSkill) "Until the end of your opponent's next turn, their Digimon with no digivolution cards
//     can't attack." [Security] (SecuritySkill) "Your opponent's Digimon with no digivolution cards can't attack
//     for the turn." BOTH: ActivateClass(CanUseCondition = CanTriggerOptionMainEffect / CanTriggerSecurityEffect,
//     ORDER=-1, ISOPTIONAL=false). ActivateCoroutine has NO SelectPermanentEffect step — it directly calls
//     CardEffectCommons.GainCanNotAttackPlayerEffect(attackerCondition = opponent battle-area Digimon with no
//     digivolution cards, defenderCondition = null [AS-IS returns true — a no-op], effectDuration:
//     UntilOpponentTurnEnd (Main) / UntilEachTurnEnd (Security)), broadly granting the restriction to EVERY
//     currently/future-qualifying opponent Digimon with no player choice.
// Headless mirror: uniform ActivatedEffect (per timing) whose body is GrantPlayerScopeRestrictionBody — the
//   no-select player-scope grant that invokes GainCanNotAttackPlayerEffect (verbatim AS-IS mirror) directly.
//   The battle-area + !CanNotBeAffected guards ride the GainToPlayerScope live-CanUse, so the attacker predicate
//   carries only "opponent battle-area Digimon && HasNoDigivolutionCards". CanActivate is null (AS-IS
//   CanActivateCondition = null): the grant registers unconditionally, its scope predicate evaluated live.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_100 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: null,
                body: new GrantPlayerScopeRestrictionBody(c => CardEffectCommons.GainCanNotAttackPlayerEffect(
                    attackerCondition: p => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(p, c) && p.HasNoDigivolutionCards,
                    defenderCondition: null,
                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                    sourceCard: c,
                    effectName: "Can't Attack")),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Until the end of your opponent's next turn, their Digimon with no digivolution cards can't attack."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.SecuritySkill,
                canUse: ctx => CardEffectCommons.CanTriggerSecurityEffect(ctx, card),
                canActivate: null,
                body: new GrantPlayerScopeRestrictionBody(c => CardEffectCommons.GainCanNotAttackPlayerEffect(
                    attackerCondition: p => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(p, c) && p.HasNoDigivolutionCards,
                    defenderCondition: null,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    sourceCard: c,
                    effectName: "Can't Attack")),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Security] Your opponent's Digimon with no digivolution cards can't attack for the turn."));
        }

        return cardEffects;
    }
}
