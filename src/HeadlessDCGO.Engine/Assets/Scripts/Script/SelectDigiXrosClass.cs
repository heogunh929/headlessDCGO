// Source: DCGO/Assets/Scripts/Script/SelectDigiXrosClass.cs (1048 lines)
// (P6C1) PARTIAL mirror of the AS-IS SelectDigiXrosClass component — the DigiXros pre-play material-selection
// component the play pipeline resets/consults (PlayCardClass, CardController.cs:306/744-748/791; the AS-IS
// cost pipeline also reads `playCard`/`selectedDigicrossCards` in CardSource.GetPayingCostWithBaseCost).
// Mirrored 1:1: the STATE surface (:12-32 — selectedDigicrossCards/addDigivolutionCardInfos/excludedCards/
// playCard + ResetSelectDigiXrosClass/AddDigivolutionCardInfos/SetExcludedCards) and the AddDigivolutionCardsInfo
// holder (:1033-1048).
// STOP (design item RD-P6C1-5, docs/audit/rebuild_p6_cluster1_notes.md): the INTERACTIVE selection flow
// (`Select(card)` :368-880 and the AddDigivolutiuonCards halves :882-1018 — a ~700-line Photon/UI multi-select
// over the DigiXros condition elements). Headless-side the verified DigiXros path is the parameterized
// special-play action (SpecialPlayAction / DigiXrosEffects); re-housing the AS-IS component flow is a separate
// goal. NOTE the mirror's `SelectAssemblyClass` sibling took the OPPOSITE shape (a static feasibility helper),
// which is why PlayCardClass STOPs its Assembly component calls at the call site instead.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class SelectDigiXrosClass
{
    // AS-IS :12-15.
    public List<CardSource> selectedDigicrossCards { get; private set; } = new List<CardSource>();
    public List<AddDigivolutionCardsInfo> addDigivolutionCardInfos { get; private set; } = new List<AddDigivolutionCardsInfo>();
    public List<CardSource> excludedCards { get; private set; } = new List<CardSource>();
    public CardSource playCard { get; private set; } = null;

    // AS-IS :17-22 (verbatim: excludedCards is deliberately NOT reset here).
    public void ResetSelectDigiXrosClass()
    {
        selectedDigicrossCards = new List<CardSource>();
        addDigivolutionCardInfos = new List<AddDigivolutionCardsInfo>();
        playCard = null;
    }

    // AS-IS :24-27.
    public void AddDigivolutionCardInfos(AddDigivolutionCardsInfo digivolutionCardsInfo)
    {
        addDigivolutionCardInfos.Add(digivolutionCardsInfo);
    }

    // AS-IS :29-32.
    public void SetExcludedCards(List<CardSource> excluded)
    {
        excludedCards = excluded;
    }

    // AS-IS :368-880 `IEnumerator Select(CardSource card)` — the interactive DigiXros material pick (per-element
    // area-choice loop over Hand/Field/Trash/Tamer + End, each dispatching to SelectHandEffect /
    // SelectPermanentEffect / SelectCardEffect; maxTrashCount/maxUnderTamerCount scans; ValueSelection/Photon RPC
    // area transport). STOP (design item RD-R5-04, superseding RD-P6C1-5): two hard blockers live outside this
    // batch's edit scope —
    //   (1) the core gate `CanSelectDigiXros` reads `Permanent.CanSubstituteForDigiXrosCondition(card)`, which is
    //       not on the mirror Permanent yet (Permanent.cs — a different R1 cluster); and
    //   (2) the Hand sub-select delegates to `SelectHandEffect`, whose mirror is still a 7-line skeleton stub
    //       (its own R5 batch owns it).
    // The area-choice transport itself IS adaptable (ModeChoice, as SelectAppFusion/BurstDigivolution do here),
    // and the maxTrashCount/maxUnderTamer/is* helpers are feasible against existing surfaces — this method can be
    // completed 1:1 once (1) and (2) land. The verified headless DigiXros path meanwhile is the parameterized
    // special-play action (SpecialPlayAction / FusionKind.DigiXros). No guess.
    public Task Select(CardSource card)
    {
        _ = card;
        throw new NotSupportedException(
            "STOP: SelectDigiXrosClass.Select (AS-IS SelectDigiXrosClass.cs:368-880) — blocked by the missing " +
            "Permanent.CanSubstituteForDigiXrosCondition (R1) and the SelectHandEffect mirror stub (R5); design " +
            "item RD-R5-04, docs/audit/rebuild_p6_cluster1_notes.md.");
    }
}

/// <summary>(P6C1) 1:1 mirror of AS-IS <c>AddDigivolutionCardsInfo</c> (SelectDigiXrosClass.cs:1033-1048): one
/// "these cards were stacked under by this effect" record of the DigiXros/Assembly stacking bookkeeping.</summary>
public class AddDigivolutionCardsInfo
{
    public AddDigivolutionCardsInfo(ICardEffect cardEffect, List<CardSource> cardSources)
    {
        this.cardEffect = cardEffect;
        this.cardSources = new List<CardSource>();

        foreach (CardSource cardSource in cardSources)
        {
            this.cardSources.Add(cardSource);
        }
    }

    public ICardEffect cardEffect { get; private set; } = null;
    public List<CardSource> cardSources { get; private set; } = new List<CardSource>();
}
