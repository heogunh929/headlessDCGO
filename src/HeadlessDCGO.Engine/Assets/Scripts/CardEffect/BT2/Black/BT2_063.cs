// Source: DCGO/Assets/Scripts/CardEffect/BT2/Black/BT2_063.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_063 (BT2/Black).
//   1) [Reboot] static self-effect (not inherited, no condition).
//   2) Inherited [Your Turn + self has Reboot] -> Security Attack +1.
// AS-IS structure kept verbatim: both timing==EffectTiming.None blocks use the REAL AS-IS
// `CardEffectFactory.RebootSelfStaticEffect`/`ChangeSelfSAttackStaticEffect` calls (verified present in
// DCGO CardEffectFactory — not old-model inventions), so no inline ActivateClass restructuring is needed here.
// `card.PermanentOfThisCard().HasReboot` -> (R1-c) the rehoused `new Permanent(card.Context, card.InstanceId).HasReboot`.
// NAMESPACE FIX: this file lives under BT2/Black but previously declared the bare `CardEffect.BT2` namespace
// (pre-existing bug, same class as BT2_002/BT2_010) — corrected to `CardEffect.BT2.Black` below.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;

public sealed class BT2_063 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (new Permanent(card.Context, card.InstanceId).HasReboot)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
