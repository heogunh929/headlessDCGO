// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_107.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass) for [Main], and
// AddActivateMainOptionSecurityEffect (= AS-IS reuse-[Main]-from-security) for [Security].
//   [Main]     Trigger <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.)
//              -> ActivatedEffect(OptionSkill, CanUse=CanTriggerOptionMainEffect, CanActivate=null [AS-IS
//                 ActivateCoroutine has no CanActivateCondition], body=RecoveryBody(1),
//                 maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false [AS-IS ISOPTIONAL=false]).
//   [Security] (reuse the Main effect)
//              -> AddActivateMainOptionSecurityEffect
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_107 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: null,
                body: new RecoveryBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Trigger <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.)"));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: "Recovery +1 (Deck)");
        }

        return cardEffects;
    }
}
