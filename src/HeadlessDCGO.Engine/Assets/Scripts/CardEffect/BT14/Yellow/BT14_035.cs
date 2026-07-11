// Source: Assets/Scripts/CardEffect/BT14/Yellow/BT14_035.cs (1:1 mirror)
// <Barrier> (When this Digimon would be deleted in battle, by trashing the top card of your security
// stack, prevent that deletion.)
// AS-IS (BT14_035.cs:12-15): the ONLY effect — timing == WhenPermanentWouldBeDeleted ->
//   CardEffectFactory.BarrierSelfEffect(isInheritedEffect: false, card, condition: null).
// Headless mirror: verbatim factory call. The grant registers the Barrier keyword
// (SelfKeywordByNameEffect -> ContinuousKeywordGate.Barrier); the C-5 PRE would-be-deleted window
// (DeletionReplacementTiming / DeletionReplacementGate.TryBarrierAsync) recognises it via
// HasReplacementKeyword and offers "trash your top security to survive" on a by-battle deletion.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT14.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT14_035 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
