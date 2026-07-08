// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_096.cs
// 1:1 mirror of the original BT3_096 (BT3/Purple), partial:
//   [All Turns] When a player uses an Option card, you may suspend this Tamer to gain 1 memory.
//   STOP: AS-IS ActivateClass registers on EffectTiming.OnUseOption (CanTriggerWhenUseOption — same gap
//   family as BT3_091's second block). The headless EffectTiming enum has no OnUseOption member and no
//   resolver call site ever dispatches it. Not registered (primitive/timing gap, no engine mod).
//
//   [Security] Play this Tamer.
//   -> CardEffectFactory.PlaySelfTamerSecurityEffect(card) (security-skill flow, mirrors ST1_12/ST2_12/
//      ST3_12/ST4_14).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_096 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [All Turns] When a player uses an Option card, you may suspend this Tamer to gain 1
        // memory — see file-header STOP note (no EffectTiming.OnUseOption dispatch surface). Not
        // registered.

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
