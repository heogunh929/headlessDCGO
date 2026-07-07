// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_054.cs
//   [When Attacking] If you have 3 or more memory, 1 of your opponent's Digimon gets -2000 DP for the turn.
// 1:1 mirror of the AS-IS BT1_054: ActivateClass on OnAllyAttack. CanUseCondition = CanTriggerOnAttack.
//   CanActivateCondition = IsExistOnBattleArea && HasMatchConditionPermanent(opponent battle-area Digimon) &&
//   card.Owner.MemoryForPlayer >= 3. ORDER=-1, ISOPTIONAL=false. ActivateCoroutine: SelectPermanentEffect
//   (Mode.Custom, maxCount=Min(1,count)) -> ChangeDigimonDP(-2000, UntilEachTurnEnd).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom) with the AS-IS SelectPermanentCoroutine
//   follow-up (onEachSelected -> ChangeDigimonDP(-2000)); the memory precondition uses CardEffectCommons.
//   MemoryForPlayer (the AS-IS Player.MemoryForPlayer mirror: owner-relative read of the turn-player-relative gauge).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_054 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelect(HeadlessEntityId id) => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelect)
                    && CardEffectCommons.MemoryForPlayer(card) >= 3,
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelect,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[When Attacking] If you have 3 or more memory, 1 of your opponent's Digimon gets -2000 DP for the turn.",
                    onEachSelected: id => CardEffectCommons.ChangeDigimonDP(
                        new Permanent(card.Context, id, card.Owner), changeValue: -2000, EffectDuration.UntilEachTurnEnd, card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Attacking] If you have 3 or more memory, 1 of your opponent's Digimon gets -2000 DP for the turn."));
        }

        return cardEffects;
    }
}
