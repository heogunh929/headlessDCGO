// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_025.cs
// 1:1 mirror of the original BT3_025 (BT3/Blue) — a Digimon.
//   [When Digivolving] Unsuspend 1 of your level 4 or lower Digimon. AS-IS: ActivateClass on
//   OnEnterFieldAnyone, CanUseCondition = CanTriggerWhenDigivolving(hashtable, card), CanActivateCondition =
//   IsExistOnBattleArea(card) && HasMatchConditionPermanent(own battle-area Digimon && Level<=4 &&
//   TopCard.HasLevel), ORDER=-1 (mandatory, no once-per-turn cap), ISOPTIONAL=false, ActivateCoroutine =
//   SelectPermanentEffect(Mode.UnTap, maxCount=Min(1,count), canNoSelect:false, canEndNotMax:false).
// Headless mirror: CardEffectFactory.SelectAndUnsuspendEffect (AS-IS SelectPermanentEffect Mode.UnTap),
// maxCount:1 — engine clamps to what exists (BT1_036 precedent). Declared under EffectTiming.WhenDigivolving
// (the DigivolveAction-wired timing the bridge resolves activated selects at), not OnEnterFieldAnyone — same
// idiom as BT1_074/BT1_025/ST4_10 (the bridge excludes OnEnterFieldAnyone for activated selects). AS-IS
// CanUseCondition/CanActivateCondition are subsumed by the DigivolveAction dispatch (only invoked for the
// digivolving card) plus SelectAndUnsuspendEffect's own no-op-if-nothing-matches behaviour.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_025 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelect(HeadlessEntityId id) =>
                CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)
                && CardEffectCommons.LevelOf(card, id) <= 4
                && CardEffectCommons.TopCardHasLevel(card, id);

            cardEffects.Add(CardEffectFactory.SelectAndUnsuspendEffect(
                card: card,
                canTarget: CanSelect,
                maxCount: 1,
                canEndNotMax: false,
                description: "[When Digivolving] Unsuspend 1 of your level 4 or lower Digimon."));
        }

        return cardEffects;
    }
}
