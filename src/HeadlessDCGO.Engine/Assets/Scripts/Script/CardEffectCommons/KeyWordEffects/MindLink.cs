// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/MindLink.cs
// Decision: PORT
// Category: CardEffect
// Priority: HIGH
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (K5) AS-IS <c>MindLinkClass</c> — Mind Link is NOT a keyword grant: it is an OnDeclaration PROCESS a
/// card script invokes (<c>new MindLinkClass(tamer, digimonCondition, activateClass).MindLink()</c>):
/// select 1 of the owner's battle-area permanents that has no Tamer among its digivolution cards
/// (non-token, matching <c>digimonCondition</c>), then place the Tamer PERMANENT at the bottom of the
/// selected Digimon's digivolution cards (AS-IS <c>IPlacePermanentToDigivolutionCards</c>). The selection
/// is optional (<c>canNoSelect: true</c>). The AS-IS <c>HasMindLink</c> property is UI-only
/// (description-string sniffing consumed by PermanentDetail) and is intentionally not ported.
///
/// Headless split mirrors <c>SelectPermanentEffect</c> authoring: <see cref="BuildRequest"/> enumerates the
/// AS-IS <c>CanSelectPermanentCondition</c> candidates; <see cref="MindLink(HeadlessEntityId, CancellationToken)"/>
/// performs the placement for the resolved selection.
/// </summary>
public sealed class MindLinkClass
{
    private readonly Permanent _tamer;
    private readonly Func<Permanent, bool>? _digimonCondition;
    private readonly EngineContext _context;

    /// <summary><paramref name="activateClass"/> is the AS-IS ctor's effect handle (source attribution /
    /// CanNotBeAffected origin); the headless mutations are attributed via the zone/stack helpers, so it is
    /// accepted for signature fidelity only.</summary>
    public MindLinkClass(Permanent tamer, Func<Permanent, bool>? digimonCondition, ICardEffect? activateClass)
    {
        _tamer = tamer ?? throw new ArgumentNullException(nameof(tamer));
        _digimonCondition = digimonCondition;
        _ = activateClass;
        _context = tamer.TopCard.Context;
    }

    /// <summary>AS-IS <c>CanSelectPermanentCondition</c> (MindLink.cs:17) 1:1: the tamer is on the battle
    /// area, the candidate is on the SAME owner's battle area, is not a token, has NO Tamer among its
    /// digivolution cards, and matches <c>digimonCondition</c>. The AS-IS face-up narrowing
    /// (<c>!cs.IsFlipped</c>) is the default state — flipped under-cards are not modeled headless, so every
    /// under-card counts as face-up (1:1 for the current engine).</summary>
    public bool CanSelectPermanentCondition(Permanent permanent)
    {
        ArgumentNullException.ThrowIfNull(permanent);
        return TamerOnBattleArea()
            && OnTamerOwnersBattleArea(permanent.InstanceId)
            && !permanent.IsToken
            && permanent.DigivolutionCards.Count(cs => cs.IsTamer) == 0
            && (_digimonCondition is null || _digimonCondition(permanent));
    }

    /// <summary>All battle-area permanents passing <see cref="CanSelectPermanentCondition"/>.</summary>
    public IReadOnlyList<HeadlessEntityId> Candidates()
    {
        var zones = (IZoneStateReader)_context.ZoneMover;
        var candidates = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId id in zones.GetCards(_tamer.OwnerId, ChoiceZone.BattleArea))
        {
            if (CanSelectPermanentCondition(new Permanent(_context, id, _tamer.OwnerId)))
            {
                candidates.Add(id);
            }
        }

        return candidates;
    }

    /// <summary>The AS-IS selection: owner picks, optional (<c>canNoSelect: true</c>), max 1.</summary>
    public ChoiceRequest BuildRequest()
    {
        var candidates = Candidates()
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.BattleArea, isSelectable: true, _tamer.OwnerId))
            .ToList();
        return EffectChoiceHelpers.CreatePermanentRequest(
            _tamer.OwnerId,
            "Select 1 Digimon that will get a digivolution card.",
            minCount: 0,
            maxCount: Math.Min(1, candidates.Count),
            canSkip: true,
            candidates);
    }

    /// <summary>Places the tamer PERMANENT at the bottom of <paramref name="selectedDigimonId"/>'s
    /// digivolution cards (AS-IS <c>IPlacePermanentToDigivolutionCards</c>): the tamer's top card first,
    /// then its own under-cards re-parented after it (stack order preserved). The tamer leaves play, so its
    /// registered continuous/trigger bindings are removed (mirror of the sink's leave-play cleanup); its
    /// inherited effects reach the new host through the normal under-card machinery. Returns false when the
    /// selection no longer satisfies <see cref="CanSelectPermanentCondition"/>.</summary>
    public async Task<bool> MindLink(HeadlessEntityId selectedDigimonId, CancellationToken cancellationToken = default)
    {
        HeadlessEntityId tamerId = _tamer.InstanceId;
        if (selectedDigimonId.IsEmpty ||
            selectedDigimonId == tamerId ||   // engine invariant: a permanent cannot be placed under itself
            !CanSelectPermanentCondition(new Permanent(_context, selectedDigimonId, _tamer.OwnerId)))
        {
            return false;
        }

        await DigivolutionStackHelpers.AddSourcesBottomAsync(
            _context.CardInstanceRepository, _context.ZoneMover, selectedDigimonId, new[] { tamerId },
            ChoiceZone.BattleArea, cancellationToken).ConfigureAwait(false);
        DigivolutionStackHelpers.MoveSourcesBottom(
            _context.CardInstanceRepository, tamerId, selectedDigimonId, int.MaxValue);
        _context.EffectRegistry.RemoveWhere(binding => binding.Request.Context.SourceEntityId == tamerId);
        return true;
    }

    private bool TamerOnBattleArea() => OnTamerOwnersBattleArea(_tamer.InstanceId);

    private bool OnTamerOwnersBattleArea(HeadlessEntityId id) =>
        _context.ZoneMover is IZoneStateReader zones &&
        zones.GetCards(_tamer.OwnerId, ChoiceZone.BattleArea).Contains(id);
}
