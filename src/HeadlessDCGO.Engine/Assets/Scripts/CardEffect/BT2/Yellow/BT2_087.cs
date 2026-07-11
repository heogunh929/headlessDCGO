// 1:1 mirror of BT2_087 — a Tamer.
//   [Start of Your Turn] If owner has ≤3 security cards, gain 1 memory.
//   [Security] Play this Tamer.
// AS-IS BT2_087.cs:27-52 gates: CanUseCondition = IsExistOnBattleArea(card) && IsOwnerTurn(card) &&
// SecurityCount <= 3; CanActivateCondition = isExistOnField + battle-area membership. The battle-area guard is
// LOAD-BEARING: AS-IS scans the hand and trash for effects too (AutoProcessing.cs:815-857) and relies on
// CanTrigger's zone guard to filter — a guard-less mirror gains memory from the HAND once the headless bridge
// scan covers those zones (C-5 adversarial review P0-1).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_087 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn
            && CardEffectCommons.IsExistOnBattleArea(card)
            && CardEffectCommons.IsOwnerTurn(card)
            && CardEffectCommons.SecurityCount(card) <= 3)
        {
            cardEffects.Add(CardEffectFactory.GainMemoryActivatedEffect(
                card,
                amount: 1,
                description: "[Start of Your Turn] If you have 3 or fewer security cards, gain 1 memory."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}