// Source: Assets/Scripts/CardEffect/BT3/Red/BT3_097.cs (an Option, mixed timings)
// AS-IS has two branches:
//   [Main] 1 of your Digimon gains "This Digimon doesn't activate the [Security] effects of any Option
//   cards it checks" for the turn. -> ActivateClass on OptionSkill gated by CanTriggerOptionMainEffect;
//   mandatory select 1 own Digimon (SelectPermanentEffect.Mode.Custom — no built-in mutation), then a nested
//   DisableEffectClass is added to the selection's UntilEachTurnEndEffects, invalidating any [Security]
//   ICardEffect from an Option source while this permanent is the one checking security.
//   [Security] Add this card to its owner's hand. -> CardEffectCommons.AddThisCardToHand (SecuritySkill).
//
// STOP (genuine primitive gap, [Main]/OptionSkill branch only): the headless select flow has no
// "select 1 permanent, then grant a per-turn continuous immunity-to-a-specific-effect-kind onto that
// selection" hook (Mode.Custom is an explicit no-op in SelectPermanentEffect.BuildMutation,
// SelectPermanentEffect.cs:213). Compounding this, the AS-IS nested primitive itself
// (Assets/Scripts/Script/CardEffects/DisableEffectClass.cs) is still an unported skeleton in this headless
// tree — there is no ToBinding-capable "disable this effect kind on this permanent" continuous effect to
// even compose with a select body. Composing "select 1 own Digimon, then grant it a this-turn immunity to
// Option-card [Security] effects" is new engine-layer work (both the select-then-grant hook and
// DisableEffectClass itself), out of scope for a single-card porting pass. No cardEffects registered for
// OptionSkill.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_097 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "1 of your Digimon gains 'This Digimon doesn't activate the [Security] effects of
        // any Option cards it checks' for the turn." — needs a select-then-grant-continuous-immunity
        // activated body that does not exist yet (see file header).
        // if (timing == EffectTiming.OptionSkill) { ... }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
