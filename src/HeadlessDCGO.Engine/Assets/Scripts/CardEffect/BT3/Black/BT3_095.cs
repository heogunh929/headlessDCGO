// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_095.cs
// 1:1 mirror of the original BT3_095 (a Tamer, two branches).
//   [Start of Your Turn] If you have a Digimon with <Blocker> in play, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnStartTurn, +1, mandatory ("gain", AS-IS ISOPTIONAL=false), no once-per-turn
//      cap needed beyond the timing itself firing once per turn — AS-IS order=-1).
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (verbatim factory match, mirrors
//      ST1_12/ST2_12/ST3_12).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_095 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            bool HasOwnerBlockerDigimon(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)
                    && ContinuousKeywordGate.HasKeyword(card.Context, id, ContinuousKeywordGate.Blocker);
            }

            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.MatchConditionPermanentCount(card, HasOwnerBlockerDigimon) >= 1;
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnStartTurn,
                amount: 1,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                description: "[Start of Your Turn] If you have a Digimon with <Blocker> in play, gain 1 memory.",
                isOptional: false));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
