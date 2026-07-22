// TEST FIXTURE (not a real card). [Main] (OptionSkill) offers a mode menu (AS-IS UserSelectionManager
// SetBool/IntSelection): "Draw 1" / "Draw 3", plus a conditional "Draw 5" available only when the card's
// "extraMode" metadata flag is set (mirrors the original conditional selectionElements.Add). Used by tests/PRIM-P0
// (mode-choice primitive). Inert in actual play (no real card numbered "TfxSelectMode").
// (R7 종점) Re-pointed off the retired invented `ModeChoiceEffect` carrier (+ its bespoke resolver switch arm)
// onto the AS-IS inline `new ActivateClass()` idiom: the ActivateCoroutine itself presents the mode menu
// (ChoiceType.ModeChoice) and runs the chosen branch's ActivateClass — resolved by the resolver's generic
// ActivateICardEffect case. Each branch is the AS-IS `new DrawClass(...).Draw()` coroutine (BT1_046 idiom).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSelectMode : CEntity_Effect
{
    private const string ModeToken = "mode";

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing != EffectTiming.OptionSkill)
        {
            return effects;
        }

        bool extraMode =
            card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) &&
            record is not null &&
            record.Metadata.TryGetValue("extraMode", out object? raw) && raw is bool b && b;

        const string description = "Choose one effect to activate.";
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect(description, _ => true, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, description);
        effects.Add(activateClass);
        return effects;

        // Inline AS-IS draw ActivateClass (BT1_046 idiom): draw `count` via new DrawClass(...).Draw().
        ActivateClass DrawMode(int count)
        {
            ActivateClass mode = new ActivateClass();
            mode.SetUpICardEffect($"Draw {count}", _ => true, card);
            mode.SetUpActivateClass(null, DrawCoroutine, -1, false, $"Draw {count} card(s).");
            return mode;

            async Task DrawCoroutine(Hashtable _ht)
            {
                await new DrawClass(card.Context, card.Owner, count, mode).Draw();
            }
        }

        // AS-IS "choose one" menu: available modes only (the conditional Draw 5 is omitted unless extraMode),
        // present a mandatory mode select, then run the chosen branch's coroutine.
        async Task ActivateCoroutine(Hashtable hashtable)
        {
            var available = new List<(string Label, ActivateClass Branch)>
            {
                ("Draw 1 card.", DrawMode(1)),
                ("Draw 3 cards.", DrawMode(3)),
            };
            if (extraMode)
            {
                available.Add(("Draw 5 cards.", DrawMode(5)));
            }

            var candidates = new List<ChoiceCandidate>(available.Count);
            for (int i = 0; i < available.Count; i++)
            {
                candidates.Add(new ChoiceCandidate(
                    new HeadlessEntityId($"{card.InstanceId.Value}#{ModeToken}#{i}"),
                    available[i].Label, ChoiceZone.BattleArea, IsSelectable: true, ownerId: card.Owner));
            }

            ChoiceResult result = await card.Context.ChoiceProvider.ChooseAsync(
                new ChoiceRequest(ChoiceType.ModeChoice, card.Owner, "Choose one effect to activate.",
                    minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, candidates),
                CancellationToken.None).ConfigureAwait(false);
            if (result.IsSkipped || result.SelectedIds.Count == 0)
            {
                return;
            }

            string[] parts = result.SelectedIds[0].Value.Split('#');
            if (int.TryParse(parts.Length > 2 ? parts[2] : null, out int index) && index >= 0 && index < available.Count)
            {
                await available[index].Branch.Activate(hashtable).ConfigureAwait(false);
            }
        }
    }
}
