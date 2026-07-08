// 1:1 mirror of the original BT3_040 (BT3/Yellow).
//   (static, no printed timing) On your turn, this card is also treated as blue.
//              -> ChangeCardColorClass (the same headless mirror class as AS-IS; adds "Blue" to this card's
//              resolved color list while on the battle area during the owner's turn).
//   (static, no printed timing) On the opponent's turn, 1 of the opponent's Digimon with no digivolution
//              cards gets -1 Security Attack.
//              -> STOP: CardEffectFactory.ChangeSAttackStaticEffect is hard-scoped to the CARD OWNER
//              (PlayerScopeModifierEffect.ToBinding always sets ScopePlayerIdKey = Card.Owner and there is
//              no scopeAnyPlayer parameter on this factory overload — unlike
//              ChangeSecurityDigimonCardDPStaticEffect, which explicitly supports scopeAnyPlayer for the DP
//              analogue). Using it here would silently apply to the OWNER's Digimon instead of the
//              opponent's (or to nothing), which is a fidelity break, not an approximation — no any-player
//              continuous Security-Attack-modifier primitive exists, so this timing stays STOP.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT3_040 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool CanUseCondition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);
            }

            List<string> ChangeCardColors(CardSource cardSource, List<string> cardColors)
            {
                if (cardSource.InstanceId == card.InstanceId)
                {
                    cardColors.Add("Blue");
                }

                return cardColors;
            }

            ChangeCardColorClass changeCardColorClass = new ChangeCardColorClass();
            changeCardColorClass.SetUpICardEffect("Also treated as blue", CanUseCondition, card);
            changeCardColorClass.SetUpChangeCardColorClass(ChangeCardColors);
            cardEffects.Add(changeCardColorClass);
        }

        if (timing == EffectTiming.None)
        {
            // STOP: on the opponent's turn, 1 of the opponent's Digimon with no digivolution cards gets
            // -1 Security Attack — no any-player-scope continuous Security-Attack modifier primitive
            // exists (see file header).
        }

        return cardEffects;
    }
}
