// 1:1 mirror of the original BT8_092 (BT8/Black) — OnMove witness for the F1 Tier1 activated bridge (ANYONE / cross-card).
//
// Ported effect (AS-IS BT8_092.cs:22-84):
//   * [Your Turn] "When one of your Digimon with [X Antibody] in its traits is moved from your breeding area to
//     your battle area, gain 1 memory and <Draw 1>." — timing OnMove -> uniform ActivatedEffect (mandatory,
//     maxCountPerTurn -1 = uncapped).
//     CanUse (AS-IS CanUseCondition, :41-59) = IsExistOnBattleArea(card) && IsOwnerTurn(card)
//       && CanTriggerOnMove(permanent => IsPermanentExistsOnOwnerBattleAreaDigimon(permanent) && HasXAntibodyTraits).
//       This is an ANYONE / cross-card reactor: the reacting Tamer is a DIFFERENT card than the moved Digimon, and
//       its PermanentCondition matches the moved SUBJECT by trait (not by identity). It rides the F1 OnMove
//       EventBroadcast bridge: headless derives OnMove from the promoted card's CardMoved (BreedingArea->BattleArea,
//       TriggerTimingMap), threads that as the driving event, and CanTriggerOnMove reads the subject (the moved
//       permanent) + requires it to be on the battle area (matches AS-IS IsPermanentExistsOnBattleArea).
//     CanActivate (AS-IS CanActivateCondition, :61-77) = IsExistOnBattleArea && (Owner.CanAddMemory || Library>=1).
//       In headless the memory-gain is sink-gated (a gain is applicable unless a CanNotAddMemory restriction blocks
//       it), so the `CanAddMemory` disjunct is effectively true and the OR reduces to always-true — same precedent
//       as BT1_076/BT1_077 (which drop the redundant CanAddMemory check). So CanActivate == IsExistOnBattleArea.
//     Body (AS-IS ActivateCoroutine, :79-84) = Draw 1 THEN gain 1 memory -> CompositeBody(DrawBody(1), MemoryBody(1)).
//
// STOP / design item F1-M2-BT8_092-ALLYATTACK — the AS-IS second effect ([Your Turn] OnAllyAttack: "when one of your
// black Digimon with [X Antibody] attacks, you may suspend this Tamer to place 1 [X Antibody] card from hand under
// that Digimon as its bottom digivolution card", BT8_092.cs:87+) is NOT ported: it is a suspend-cost + place-under-
// digivolution-source body outside the OnMove bridge under test. Deliberately omitted rather than mis-modelled; this
// witness exercises only the OnMove EventBroadcast bridge.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT8_092 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region [Your Turn] When moving (timing OnMove)
        if (timing == EffectTiming.OnMove)
        {
            const string description =
                "[Your Turn] When one of your Digimon with [X Antibody] in its traits is moved from your breeding area to your battle area, gain 1 memory and <Draw 1>.";

            // AS-IS PermanentCondition: the moved permanent is your battle-area Digimon with the [X Antibody] trait.
            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.TopCard.EqualsTraits("X Antibody");

            // AS-IS CanUseCondition: IsExistOnBattleArea && IsOwnerTurn && CanTriggerOnMove(PermanentCondition).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerOnMove(ctx, card, PermanentCondition);

            // AS-IS CanActivateCondition: IsExistOnBattleArea && (CanAddMemory || Library>=1) — reduces to
            // IsExistOnBattleArea in headless (memory-gain is sink-gated; BT1_076/077 precedent).
            bool CanActivate() => CardEffectCommons.IsExistOnBattleArea(card);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnMove,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new CompositeBody(new DrawBody(1), new MemoryBody(1)),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }
        #endregion

        return cardEffects;
    }
}
