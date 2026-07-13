namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// Headless mirror of the original <c>CardSource</c> — the handle a card-effect builder receives. Carries
/// the live instance id, the controlling / owning player, and the live <see cref="EngineContext"/> so
/// condition predicates can read turn / zone / stack state.
/// </summary>
public sealed class CardSource
{
    public CardSource(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller, HeadlessPlayerId? owner = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty)
        {
            throw new ArgumentException("Card source instance id must not be empty.", nameof(instanceId));
        }

        if (controller.IsEmpty)
        {
            throw new ArgumentException("Card source controller id must not be empty.", nameof(controller));
        }

        Context = context;
        InstanceId = instanceId;
        Controller = controller;
        Owner = owner ?? controller;
    }

    public EngineContext Context { get; }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId Controller { get; }

    public HeadlessPlayerId Owner { get; }

    // (EFFECT-MODEL REBUILD, design item CARDSOURCE-EQUALITY) AS-IS relies on stable per-card object identity for
    // `==`/`!=`/`Contains` on CardSource (CanActivate's inherited/linked determination, IsSameEffect, factory
    // PermanentCondition `permanent == targetPermanent`, LinkedCards.Contains(card), …). The mirror CardSource is a
    // lightweight VIEW reconstructed on every access, so without value equality every such comparison is
    // reference-unequal and silently wrong (a bug the big-bang red window hides). Identity = the card INSTANCE
    // (InstanceId) within the same match (Context) — two views of the same live card compare equal.
    public override bool Equals(object? obj) =>
        obj is CardSource other && InstanceId.Equals(other.InstanceId) && ReferenceEquals(Context, other.Context);

    public override int GetHashCode() => InstanceId.GetHashCode();

    public static bool operator ==(CardSource? left, CardSource? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(CardSource? left, CardSource? right) => !(left == right);

    /// <summary>(EFFECT-MODEL REBUILD / FOUNDATION) AS-IS <c>CardSource.cEntity_EffectController</c>
    /// (CardSource.cs:50, a plain public field on the original Unity Component) — the per-card-instance
    /// use-count / effect-list layer <c>ICardEffect.CanTrigger/CanActivate</c> (Assets/Scripts/Script/
    /// ICardEffect.cs, this goal) reach through. Backed by <see cref="CEntity_EffectControllerStore"/> (see
    /// its header for why a store is needed: this <see cref="CardSource"/> is a view reconstructed on every
    /// access, not a stable per-card object) — always returns the SAME controller for the same
    /// (<see cref="Context"/>, <see cref="InstanceId"/>) pair.</summary>
    public CEntity_EffectController cEntity_EffectController =>
        CEntity_EffectControllerStore.GetOrCreate(Context, InstanceId);

    /// <summary>Mirror of <c>CardSource.PermanentOfThisCard()</c>: the permanent (stack) this card is part
    /// of, whether it is the top card or a buried digivolution source. Empty if the card is not in a
    /// battle-area permanent.</summary>
    public PermanentView PermanentOfThisCard()
    {
        var zones = (IZoneStateReader)Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(Owner, ChoiceZone.BattleArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(Context.CardInstanceRepository, Context.CardRepository, top);
            if (top == InstanceId || stack.UnderCards.Any(under => under.InstanceId == InstanceId))
            {
                return new PermanentView(stack);
            }
        }

        return new PermanentView(DigivolutionStack.Empty);
    }

    // ===== (EFFECT-MODEL REBUILD / P2) AS-IS CardSource.EffectList family (CardSource.cs:981-1035) ==========
    // Thin delegation to the per-card-instance controller (cEntity_EffectController.GetCardEffects, ported P1) +
    // the ported IEnumerableExtension.Filter. 1:1 with AS-IS. Every returned effect with no source card is
    // stamped with THIS card (AS-IS's SetEffectSourceCard back-fill).

    /// <summary>AS-IS <c>CardSource.EffectList(EffectTiming)</c> (CardSource.cs:981).</summary>
    public List<ICardEffect> EffectList(EffectTiming timing)
    {
        return EffectList_ForCard(timing, this);
    }

    /// <summary>AS-IS <c>CardSource.EffectList_ForCard(EffectTiming, CardSource)</c> (CardSource.cs:990).</summary>
    public List<ICardEffect> EffectList_ForCard(EffectTiming timing, CardSource cardSource)
    {
        List<ICardEffect> _EffectList = cEntity_EffectController
            .GetCardEffects(timing, cardSource)
            .Filter(cardEffect => cardEffect != null);

        foreach (ICardEffect cardEffect in _EffectList)
        {
            if (cardEffect.EffectSourceCard == null)
            {
                cardEffect.SetEffectSourceCard(this);
            }
        }

        return _EffectList;
    }

    /// <summary>AS-IS <c>CardSource.EffectList_ExceptAddedEffects(EffectTiming)</c> (CardSource.cs:1011).</summary>
    public List<ICardEffect> EffectList_ExceptAddedEffects(EffectTiming timing)
    {
        return EffectList_ForCard_ExceptAddedEffects(timing, this);
    }

    /// <summary>AS-IS <c>CardSource.EffectList_ForCard_ExceptAddedEffects(EffectTiming, CardSource)</c> (CardSource.cs:1020).</summary>
    public List<ICardEffect> EffectList_ForCard_ExceptAddedEffects(EffectTiming timing, CardSource cardSource)
    {
        List<ICardEffect> _EffectList = cEntity_EffectController
            .GetCardEffects_ExceptAddedEffects(timing, cardSource)
            .Filter(cardEffect => cardEffect != null);

        foreach (ICardEffect cardEffect in _EffectList)
        {
            if (cardEffect.EffectSourceCard == null)
            {
                cardEffect.SetEffectSourceCard(this);
            }
        }

        return _EffectList;
    }

    // ===== (PRIM-W5-0) card-query view — the member surface card predicates read =====================
    // Backed by the definition CardRecord (colors/level/traits/type) + instance metadata. Enables 1:1
    // mirror of the original `cardSource.<X>` / `permanent.TopCard.<X>` predicates.

    private CardRecord? Definition =>
        Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? inst) && inst is not null
            && Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) ? def
            : (Context.CardRepository.TryGetCard(InstanceId, out CardRecord? self) ? self : null);

    private static IReadOnlyList<string> ReadStrings(IReadOnlyDictionary<string, object?>? meta, string key)
    {
        if (meta is null || !meta.TryGetValue(key, out object? raw) || raw is null) return Array.Empty<string>();
        return raw switch
        {
            IEnumerable<string> ss => ss.ToArray(),
            string s => s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => Array.Empty<string>(),
        };
    }

    // ===== (P6C3, COLOR-MODEL-DUALITY reconciliation) string <-> CardColor conversion =====================
    // The mirror colour accessors below keep their established STRING signatures (their consumer corpus:
    // OptionColorRequirement, BT2_099, CardEffectCommons colour predicates), while the fold now runs through
    // the AS-IS-typed kind-class interfaces (IChange(Base)CardColorEffect transform List<CardColor>). The
    // enum is closed (CEntity_Base.cs CardColor) and the mirror's string values are exactly the enum names,
    // so the conversion is lossless; an unparseable string (possible only in hand-written fixtures) is
    // dropped from the enum view rather than guessed.

    /// <summary>(P6C3) The AS-IS-typed view of a colour-name list (shared with HashtableSetting's
    /// "CardColors" payload and AddDigivolutionRequirement's enum comparison).</summary>
    public static List<CardColor> ToCardColorList(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var colors = new List<CardColor>();
        foreach (string name in names)
        {
            if (Enum.TryParse(name, ignoreCase: true, out CardColor color))
            {
                colors.Add(color);
            }
        }

        return colors;
    }

    /// <summary>(P6C3) The mirror string view of an AS-IS colour list.</summary>
    public static List<string> ToColorNames(IEnumerable<CardColor> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return colors.Select(c => c.ToString()).ToList();
    }

    // (P6C3) AS-IS colour fold shape (CardSource.cs:364-401 BaseCardColors / :446-483 CardColors): the
    // effects of ITSELF apply only while the card is NOT a permanent (`PermanentOfThisCard() == null`),
    // then the effects of all field permanents apply (gameContext.Players_ForTurnPlayer scan). Substrate:
    // `new GameContext(Context)` is the same per-match view GManager.instance.turnStateMachine.gameContext
    // resolves to.
    private List<CardColor> FoldColorEffects<TInterface>(List<CardColor> colors, Func<TInterface, List<CardColor>, List<CardColor>> apply)
        where TInterface : class
    {
        if (PermanentOfThisCard().IsEmpty)
        {
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is TInterface transform && cardEffect.CanUse(null))
                {
                    colors = apply(transform, colors);
                }
            }
        }

        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is TInterface transform && cardEffect.CanUse(null))
                    {
                        colors = apply(transform, colors);
                    }
                }
            }
        }

        return colors;
    }

    /// <summary>(A3 / P6C3 re-fold) The card's BASE colors (mirror of <c>BaseCardColors</c>,
    /// CardSource.cs:364-401): printed colors transformed by every active
    /// <see cref="IChangeBaseCardColorEffect"/> (self while not in play + all field permanents), Distinct.</summary>
    public IReadOnlyList<string> BaseCardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(ReadStrings(Definition?.Metadata, "colors"));
            colors = FoldColorEffects<IChangeBaseCardColorEffect>(colors, (e, c) => e.GetBaseCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(A3 / P6C3 re-fold) The card's colors (mirror of <c>CardColors</c>, CardSource.cs:446-483):
    /// seeds from the fully-resolved <see cref="BaseCardColors"/> (base-change BEFORE change, AS-IS two-stage
    /// order), then every active <see cref="IChangeCardColorEffect"/> transforms the list, Distinct.</summary>
    public IReadOnlyList<string> CardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(BaseCardColors);
            colors = FoldColorEffects<IChangeCardColorEffect>(colors, (e, c) => e.GetCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(RD-2 latent-1 / AS-IS metadata key for a DUAL card's separate option colour requirement list —
    /// <c>CEntity_Base.OptionCardColorRequirements</c>, CEntity_Base.cs:41.)</summary>
    public const string OptionColorRequirementsKey = "optionColorRequirements";

    /// <summary>(RD-2 latent-1) The card's BASE dual colours (mirror of <c>BaseDualCardColors</c>,
    /// CardSource.cs:403-441): seeds from <see cref="OptionColorRequirementsKey"/> (a dual card's option-play
    /// colour requirement, distinct from its printed Digimon colours) then folds every active
    /// <see cref="CardEffects.ChangeBaseCardColorClass"/> effect — the same two-stage colour machinery as
    /// <see cref="BaseCardColors"/>, seeded differently.</summary>
    public IReadOnlyList<string> BaseDualCardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(ReadStrings(Definition?.Metadata, OptionColorRequirementsKey));
            colors = FoldColorEffects<IChangeBaseCardColorEffect>(colors, (e, c) => e.GetBaseCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(RD-2 latent-1) The card's dual colours (mirror of <c>DualCardColors</c>, CardSource.cs:283-341):
    /// <see cref="BaseDualCardColors"/> then every active <see cref="CardEffects.ChangeCardColorClass"/> effect.
    /// AS-IS uses this (not <see cref="CardColors"/>) as the option colour requirement when the played card is a
    /// Digimon (a dual Digimon+Option card).</summary>
    public IReadOnlyList<string> DualCardColors
    {
        get
        {
            List<CardColor> colors = ToCardColorList(BaseDualCardColors);
            colors = FoldColorEffects<IChangeCardColorEffect>(colors, (e, c) => e.GetCardColors(c, this));
            return ToColorNames(colors.Distinct().ToList());
        }
    }

    /// <summary>(A3 / P6C3 re-fold) The card's traits (mirror of <c>CardTraits</c>, CardSource.cs:2581-2604):
    /// printed traits transformed by the card's OWN <see cref="IChangeTraitsEffect"/> effects
    /// (AS-IS scans self EffectList only, ungated by permanent membership; no Distinct).</summary>
    public IReadOnlyList<string> CardTraits
    {
        get
        {
            List<string> traits = ReadStrings(Definition?.Metadata, "traits").ToList();
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is IChangeTraitsEffect transform && cardEffect.CanUse(null))
                {
                    traits = transform.ChangTraits(traits, this);
                }
            }

            return traits;
        }
    }

    // (P6C3) The pre-flip registry-fold helpers (FoldListTransforms / SelfTransforms over the dead per-kind
    // binding keys) are retired with the interface-scan re-folds above — no producer writes those keys since
    // the kind-class 1:1 rebuild.

    // (A3) the AS-IS `cardEffect.CanUse(null)` gate — the binding's stored continuous condition.
    internal static bool EffectConditionPasses(EffectRequest effect) =>
        !effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? raw)
        || raw is not Func<bool> condition
        || condition();

    /// <summary>(bridge W4) AS-IS <c>CardSource.BaseENGCardNameFromEntity</c> (CardSource.cs:1359,
    /// <c>_cEntity_Base.CardName_ENG</c>) — the PRINTED base-entity English name, UNtransformed (unlike
    /// <see cref="CardNames"/>, no ChangeBaseCardName/ChangeCardNames folds — AS-IS reads the raw entity).
    /// Card-corpus use: the informational <c>effectName</c> argument to <c>SetUpICardEffect</c>
    /// (BT1_092/BT1_094 pattern).</summary>
    public string BaseENGCardNameFromEntity => Definition?.Name ?? string.Empty;

    /// <summary>Continuous-binding key for an added card name (AS-IS ChangeCardNamesClass).</summary>
    public const string AddedCardNameKey = "addedCardName";

    /// <summary>(P6C3 re-fold) Mirror of AS-IS <c>CardSource.BaseCardNames</c> (CardSource.cs:1371-1436):
    /// the printed name, transformed by active <see cref="IChangeBaseCardNameEffect"/> effects (a REPLACE;
    /// BT14_097 "original name is [Sukamon]"). AS-IS branch structure preserved: a DIGIVOLUTION SOURCE folds
    /// only its own non-granted effects (<see cref="EffectList_ExceptAddedEffects(EffectTiming)"/>); any other
    /// card folds self (only while not a permanent) + all field permanents + player effects. Design item
    /// RD-P6C3-A1: the AS-IS dual-card branch (<c>!isPermanent &amp;&amp; _cEntity_Base.IsDualCard</c> adds
    /// <c>dualEffect</c>, the second printed name) has no mirror data carrier yet — lands with dual-card
    /// definition data.</summary>
    public IReadOnlyList<string> BaseCardNames
    {
        get
        {
            var baseCardNames = Definition is { } d ? new List<string> { d.Name } : new List<string>();

            PermanentView thisPermanent = PermanentOfThisCard();
            bool isPermanent = !thisPermanent.IsEmpty;
            bool isDigivolutionCard = isPermanent && thisPermanent.DigivolutionCards.Any(under => under.InstanceId == InstanceId);

            if (isDigivolutionCard)
            {
                foreach (ICardEffect cardEffect in EffectList_ExceptAddedEffects(EffectTiming.None))
                {
                    if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                    {
                        baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                    }
                }
            }
            else
            {
                if (!isPermanent)
                {
                    foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                        {
                            baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                        }
                    }
                }

                foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
                {
                    foreach (Permanent permanent in player.GetFieldPermanents())
                    {
                        foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                        {
                            if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                            {
                                baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                            }
                        }
                    }

                    foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IChangeBaseCardNameEffect transform && cardEffect.CanUse(null))
                        {
                            baseCardNames = transform.ChangeBaseCardNames(baseCardNames, this);
                        }
                    }
                }
            }

            return baseCardNames;
        }
    }

    /// <summary>(P6C3 re-fold) Mirror of AS-IS <c>CardSource.CardNames</c> (CardSource.cs:1442-1460):
    /// <see cref="BaseCardNames"/> extended by the card's own <see cref="IChangeCardNamesEffect"/> effects
    /// (self <c>EffectList_ExceptAddedEffects</c> scan), Distinct. The substrate
    /// <see cref="AddedCardNameKey"/> registry read is KEPT alongside: it is the old-model
    /// <c>ChangeCardNamesClass</c> lowering still produced by ContinuousAndRestrictionEffects.cs
    /// (GrantAdditionalCardName) — a new-model grant enumerates through the interface scan instead.</summary>
    public IReadOnlyList<string> CardNames
    {
        get
        {
            List<string> cardNames = BaseCardNames.ToList();

            foreach (ICardEffect cardEffect in EffectList_ExceptAddedEffects(EffectTiming.None))
            {
                if (cardEffect is IChangeCardNamesEffect transform && cardEffect.CanUse(null))
                {
                    cardNames = transform.ChangeCardNames(cardNames, this);
                }
            }

            foreach (EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
                new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: InstanceId)))
            {
                if (effect.Context.Values.TryGetValue(AddedCardNameKey, out object? raw) && raw is string added && !string.IsNullOrWhiteSpace(added))
                {
                    cardNames.Add(added);
                }
            }

            return cardNames.Distinct().ToList();
        }
    }

    /// <summary>(P6C3) Mirror of AS-IS <c>CardSource.CardNames_DigiXros</c> (CardSource.cs:2193-2207):
    /// <see cref="CardNames"/> extended by the card's OWN <see cref="IChangeCardNamesForDigiXrosEffect"/>
    /// effects (AS-IS scans self <c>EffectList(EffectTiming.None)</c> only, same shape as
    /// <see cref="CardTraits"/> — ungated by permanent membership, no re-Distinct after the fold).</summary>
    public IReadOnlyList<string> CardNames_DigiXros
    {
        get
        {
            List<string> cardNames = CardNames.ToList();
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is IChangeCardNamesForDigiXrosEffect transform && cardEffect.CanUse(null))
                {
                    cardNames = transform.ChangeCardNamesForDigiXros(cardNames, this);
                }
            }

            return cardNames;
        }
    }

    /// <summary>The card's PRINTED level, or -1. AS-IS <c>HasLevel</c> is printed-data based
    /// (CEntity_Base.cs:317) — level-change folds never alter it.</summary>
    private int PrintedLevel => Definition?.Metadata is { } m && m.TryGetValue("level", out object? raw) && raw is int lv ? lv : -1;

    /// <summary>(A3 / P6C3 re-fold) The card's level (mirror of <c>Level =&gt; TreatedLevel</c>,
    /// CardSource.cs:941-975): printed level transformed by the card's OWN
    /// <see cref="IChangeCardLevelEffect"/> effects (AS-IS scans self EffectList only). -1 mirrors the AS-IS
    /// no-level sentinel (1145140) — no gameplay code compares the sentinel; all consumers guard on
    /// <see cref="HasLevel"/> first.</summary>
    public int Level
    {
        get
        {
            int level = PrintedLevel;
            foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
            {
                if (cardEffect is IChangeCardLevelEffect transform && cardEffect.CanUse(null))
                {
                    level = transform.GetCardLevel(level, this);
                }
            }

            return level;
        }
    }

    /// <summary>The card's printed number (e.g. "BT10-012"), used as the SpecialPlayRecipe key.</summary>
    public string CardNumber => Definition?.CardNumber ?? string.Empty;

    // (C7) type judgements go through CardRecord.IsCardType — AS-IS CardKinds is a LIST, so a dual card
    // (e.g. Digimon/Option hybrid) reports true for BOTH kinds.
    public bool IsDigimon => Definition?.IsCardType("Digimon") == true;
    public bool IsTamer => Definition?.IsCardType("Tamer") == true;
    public bool IsOption => Definition?.IsCardType("Option") == true;
    /// <summary>(MIG2) AS-IS <c>CardSource.IsDigiEgg</c> (CardSource.cs:3466) — both fixture spellings.</summary>
    public bool IsDigiEgg => Definition?.IsCardType("DigiEgg") == true || Definition?.IsCardType("Digitama") == true;
    /// <summary>(MIG2 substrate) Whether a card DEFINITION is registered at all. AS-IS every CardSource carries
    /// a CEntity — a definition-less instance exists only in abstract test fixtures; rule predicates that would
    /// trash a card for its TYPE gate on this (fixture guard, same family as the D-2 defined-DP guard).</summary>
    public bool HasDefinition => Definition is not null;
    /// <summary>(MIG2) AS-IS <c>CardSource.IsFlipped</c> — the shared face-down instance flag.</summary>
    public bool IsFlipped => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? f) && f is not null
        && f.Metadata.TryGetValue("isFlipped", out object? flip) && flip is true;
    /// <summary>(joint-migration) public card-type check for scope synthesis in restriction producers.</summary>
    public bool IsCardType(string cardType) => Definition?.IsCardType(cardType) == true;
    public bool IsToken => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isToken", out object? t) && t is bool b && b;

    // (A3) printed-data based like AS-IS CEntity_Base.HasLevel — a level-change fold does not grant a level.
    public bool HasLevel => PrintedLevel >= 0;

    /// <summary>(Jogress by levels / P6C3 re-fold) 1:1 of AS-IS <c>Permanent.Levels_ForJogress(CardSource)</c>
    /// (Permanent.cs:3554-3605): the levels THIS card's permanent counts as when used as a Jogress /
    /// DNA-Digivolution material against <paramref name="jogressCard"/> (the digivolving card). AS-IS seeds
    /// the MATERIAL PERMANENT's <c>Level</c> gated on <c>cardSource.HasLevel</c> (the jogress card's printed
    /// level — verbatim AS-IS gate), then adds every level contributed by an active
    /// <see cref="IAddJogressLevelsEffect"/> across all field permanents' and players' effects
    /// (<c>GetJogressLevels(cardSource, this)</c>). The mirror keeps this accessor on CardSource (its
    /// established consumer surface); the permanent identity is resolved live.</summary>
    public IReadOnlyList<int> JogressLevelsAgainst(CardSource jogressCard)
    {
        ArgumentNullException.ThrowIfNull(jogressCard);
        var levels = new List<int>();
        Permanent material = ICardEffect.ResolvePermanentOfThisCard(this) ?? new Permanent(Context, InstanceId, Owner);
        if (jogressCard.HasLevel)
        {
            levels.Add(material.Level);
        }

        foreach (Player player in new GameContext(Context).Players_ForTurnPlayer)
        {
            foreach (Permanent permanent in player.GetFieldPermanents())
            {
                foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IAddJogressLevelsEffect addLevels && cardEffect.CanUse(null))
                    {
                        levels.AddRange(addLevels.GetJogressLevels(jogressCard, material));
                    }
                }
            }

            foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
            {
                if (cardEffect is IAddJogressLevelsEffect addLevels && cardEffect.CanUse(null))
                {
                    levels.AddRange(addLevels.GetJogressLevels(jogressCard, material));
                }
            }
        }

        return levels;
    }

    /// <summary>(W6-P) printed-data based like AS-IS <c>HasDP</c> — the card defines a DP at all.</summary>
    public bool HasDP => Definition?.Metadata.TryGetValue("dp", out object? dp) == true && dp is int;

    /// <summary>(W6 tail) AS-IS <c>HasPlayCost</c> — the card defines a play cost.</summary>
    public bool HasPlayCost => Definition?.PlayCost is not null;

    /// <summary>(W6 tail) The card's PRINTED play cost — behaviourally this mirrors AS-IS
    /// <c>BasePlayCostFromEntity</c> (CardSource.cs:757), NOT the AS-IS <c>GetCostItself</c> (CardSource.cs:769),
    /// which is modifier-FOLDED: <c>Max(0, GetChangedCostItselef(BasePlayCostFromEntity, Root.None,
    /// [PermanentOfThisCard()]))</c>. The name is kept for the existing call sites; a caller whose AS-IS analog
    /// reads the folded <c>GetCostItself</c> (Min/MaxCost comparisons, CostJustBeforeRemoveField) carries a
    /// documented reduction here (no per-card cost-change effects fold in); a caller whose AS-IS analog reads
    /// <c>BasePlayCostFromEntity</c> (e.g. BT22_035's "play cost 4 or less") is exact.</summary>
    public int GetCostItself => Definition?.PlayCost ?? 0;

    /// <summary>(C9) mirror of AS-IS <c>CardSource.IsLinked</c> (CardSource.cs:2947):
    /// <c>PermanentOfThisCard().LinkedCards.Contains(this)</c> — true while this card is a LINK card of a
    /// battle-area permanent (link cards are tracked separately from digivolution sources:
    /// <c>LinkHelpers.LinkedCardIdsKey</c>). Evaluated LIVE — breaking the link flips it false.</summary>
    public bool IsLinked
    {
        get
        {
            var zones = (IZoneStateReader)Context.ZoneMover;
            foreach (HeadlessEntityId hostId in zones.GetCards(Owner, ChoiceZone.BattleArea))
            {
                if (Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null
                    && LinkHelpers.ReadLinkedCardIds(host.Metadata).Contains(InstanceId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool IsLevel(int level) => Level == level;
    public bool HasCardColor(string color) => CardColors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
    public bool EqualsCardName(string name) => CardNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    public bool ContainsCardName(string fragment) => CardNames.Any(n => n.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    public bool EqualsTraits(string trait) => CardTraits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));
    public bool ContainsTraits(string fragment) => CardTraits.Any(t => t.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>(bridge W5) 1:1 mirror of AS-IS <c>CardSource.HasXAntibodyTraits</c> (CardSource.cs:1975):
    /// <c>CardTraits.Some(DataBase.IsXAntibodyString)</c> — any trait normalising to "xantibody"
    /// (BT9_109 "[When Attacking] … digivolve into a Digimon card with [X Antibody] in its traits").</summary>
    public bool HasXAntibodyTraits => CardTraits.Some(DataBase.IsXAntibodyString);

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.CanNotBeAffected(cardEffect)</c> (CardSource.cs:1060,
    /// 504 card-effect call sites): whether an active <c>ICanNotAffectedEffect</c> shields THIS card from the
    /// given causing effect. Delegates to the verified <see cref="ContinuousImmunityGate.BlocksOpponentEffect"/>
    /// (the immunity scan the mirror command classes already use). A null/empty cause is never blocked (AS-IS
    /// :1062 <c>if (_cardEffect == null) return false</c>). The <c>cardEffect</c> argument is the causing effect's
    /// source id, matching the goal-1 SwitchDefender / goal-3 causeEffectSourceId precedent.</summary>
    public bool CanNotBeAffected(HeadlessEntityId? causeEffectSourceId)
    {
        if (causeEffectSourceId is not { IsEmpty: false } cause)
        {
            return false;
        }

        return ContinuousImmunityGate.BlocksOpponentEffect(
            Context.EffectRegistry, Context.CardInstanceRepository, InstanceId, cause, Context);
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.CanNotTrashFromDigivolutionCards(cardEffect)</c>
    /// (CardSource.cs:2478, 149 call sites): this source is protected from digivolution-stack trashing — the
    /// in-flight <c>willBeRemoveSources</c> mark (AS-IS :2480) OR an active trash-protection effect (the static
    /// grant flag or the continuous scan). Delegates to <see cref="TrashProtectionScan.IsProtected"/> — the same
    /// filter <see cref="Assets.Scripts.Script.ITrashDigivolutionCards"/> applies privately, promoted to the
    /// public AS-IS surface. The continuous scan needs a cause; without one only the marks apply.</summary>
    public bool CanNotTrashFromDigivolutionCards(HeadlessEntityId? causeEffectSourceId)
    {
        if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
        {
            // AS-IS :2480 `if (willBeRemoveSources) return true;` — the "already being removed this pass" mark
            // (ITrashLinkCards.WillBeRemoveSourcesKey = "willBeRemoveSources").
            if (record.Metadata.TryGetValue("willBeRemoveSources", out object? mark) && mark is true)
            {
                return true;
            }

            // The static-grant form of the protection (CardEffectCommons.TrashProtectedKey).
            if (record.Metadata.TryGetValue(CardEffectCommons.TrashProtectedKey, out object? stamped) && stamped is true)
            {
                return true;
            }
        }

        if (causeEffectSourceId is not { IsEmpty: false } cause)
        {
            return false;
        }

        return TrashProtectionScan.IsProtected(
            Context.EffectRegistry, Context.CardInstanceRepository, Context, InstanceId, cause);
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.HasSameCardName(cardSource)</c> (CardSource.cs:1465):
    /// whether ANY of <paramref name="other"/>'s (folded) names equals one of THIS card's names — AS-IS folds
    /// <paramref name="other"/>.CardNames through THIS card's <see cref="EqualsCardName"/>.</summary>
    public bool HasSameCardName(CardSource other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.CardNames.Any(name => EqualsCardName(name));
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.CanLink(PayCost, allowBreeding)</c>
    /// (CardSource.cs:3140-3199): whether THIS card's declared <see cref="LinkCondition"/> is satisfied by at
    /// least one owner permanent — the SAME <c>canLinkSomewhere</c> scan <see cref="CanLinkToTargetPermanent"/>
    /// performs inline, exposed here as the AS-IS-named surface. <paramref name="allowBreeding"/> widens the
    /// scan from owner BATTLE-area Digimon to owner BATTLE+BREEDING permanents (no Digimon filter) — the AS-IS
    /// branch asymmetry. <paramref name="payCost"/> would add the MaxMemoryCost vs GetChangedLinkCost check
    /// (CardSource.cs:3149/3175); no headless folded-link-cost primitive exists (design item C2-02 /
    /// MIG5-CANLINK-PAYCOST), so <c>payCost: true</c> throws.</summary>
    public bool CanLink(bool payCost = false, bool allowBreeding = false)
    {
        LinkCondition? link = LinkConditionOf();
        if (link is null)
        {
            return false;
        }

        if (payCost)
        {
            throw new NotSupportedException(
                "CardSource.CanLink(payCost: true) has no headless GetChangedLinkCost primitive — design item C2-02 / MIG5-CANLINK-PAYCOST.");
        }

        var zones = (IZoneStateReader)Context.ZoneMover;
        return allowBreeding
            ? zones.GetCards(Owner, ChoiceZone.BattleArea).Concat(zones.GetCards(Owner, ChoiceZone.BreedingArea))
                .Any(id => link.digimonCondition(new Permanent(Context, id, Owner)))
            : zones.GetCards(Owner, ChoiceZone.BattleArea)
                .Any(id => CardEffectCommons.IsOwnerBattleAreaDigimon(this, id)
                    && link.digimonCondition(new Permanent(Context, id, Owner)));
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.HasDigimonColor(color)</c> (CardSource.cs:1580-1585):
    /// <see cref="HasCardColor"/> gated on <see cref="IsDigimon"/> (AS-IS <c>DigimonCardColors</c> == the card's
    /// colours when it is a Digimon, empty otherwise).</summary>
    public bool HasDigimonColor(string color) => IsDigimon && HasCardColor(color);

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.HasOptionColor(color)</c> (CardSource.cs:1587-1592):
    /// gated on <see cref="IsOption"/>; a DUAL card (also Digimon) reads <see cref="DualCardColors"/> instead of
    /// <see cref="CardColors"/> — the base/dual split this file already models.</summary>
    public bool HasOptionColor(string color)
    {
        if (!IsOption)
        {
            return false;
        }

        IReadOnlyList<string> colors = IsDigimon ? DualCardColors : CardColors;
        return colors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>(MIG5 goal-5 surface) AS-IS <c>CardSource.CanNotEvolve(targetPermanent)</c> (CardSource.cs:1291-
    /// 1350): whether an active <c>ICanNotDigivolveEffect</c> forbids THIS card from digivolving onto
    /// <paramref name="targetPermanent"/> — a token on EITHER side always blocks; otherwise the joint scan.
    /// Delegates to <see cref="ContinuousRestrictionGate.EvaluateDigivolve"/> (subject = the target evolved
    /// onto, counterpart = this digivolving card).</summary>
    public bool CanNotEvolve(Permanent targetPermanent)
    {
        ArgumentNullException.ThrowIfNull(targetPermanent);
        if (targetPermanent.IsToken || IsToken)
        {
            return true;
        }

        return ContinuousRestrictionGate.EvaluateDigivolve(Context, targetPermanent.InstanceId, InstanceId).IsRestricted;
    }

    /// <summary>(W6-L / P6C3 re-fold) Mirror of AS-IS <c>CardSource.linkCondition</c> (CardSource.cs:2727-2741):
    /// the first usable <see cref="IAddLinkConditionEffect"/>'s condition from THIS card's live
    /// <see cref="EffectList(EffectTiming)"/> (which already enumerates the dispatched per-card effect class —
    /// the pre-flip dispatch-first/registry-fallback split is superseded by the flip's enumeration model).</summary>
    public LinkCondition? LinkConditionOf()
    {
        foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddLinkConditionEffect link && cardEffect.CanUse(null) &&
                link.GetLinkCondition(this) is LinkCondition found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>(C-2 witness / BT22_035) 1:1 mirror of AS-IS <c>CardSource.CanLinkToTargetPermanent(target,
    /// PayCost:false, allowBreeding:false)</c> (CardSource.cs:3337) folded with <c>CanLink(false)</c>
    /// (CardSource.cs:3140): whether THIS card may be attached as a LINK card onto
    /// <paramref name="targetPermanent"/>. The host must be a real (non-token) battle-area — not breeding-area
    /// — permanent, and THIS card must carry a declared <see cref="LinkCondition"/> whose <c>digimonCondition</c>
    /// matches BOTH the host AND (CanLink) at least one owner battle-area Digimon (a battle-area host trivially
    /// satisfies that second clause). PayCost=false skips the memory check; the PayCost / allowBreeding variants
    /// land with their first witness (design item C2-02).</summary>
    public bool CanLinkToTargetPermanent(Permanent? targetPermanent, bool PayCost = false, bool allowBreeding = false)
    {
        if (targetPermanent is null)
        {
            return false;
        }

        // AS-IS: target.TopCard != null && !target.TopCard.IsToken.
        if (targetPermanent.TopCard.IsToken)
        {
            return false;
        }

        var zones = (IZoneStateReader)Context.ZoneMover;
        // AS-IS (CardSource.cs:3343): `allowBreeding || !target.Owner.GetBreedingAreaPermanents().Contains(target)`
        // — a breeding-area permanent is a valid link host ONLY when the caller allows breeding (the rule
        // predicate IsDigimonLackLinkCondition passes true; its trash-list re-filter passes false).
        if (!allowBreeding && zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BreedingArea).Contains(targetPermanent.InstanceId))
        {
            return false;
        }

        // AS-IS this.CanLink(false, allowBreeding) (CardSource.cs:3140): THIS card declares a link condition
        // matched by >= 1 owner permanent — GetBattleAreaDigimons normally, GetFieldPermanents (battle +
        // breeding, NO Digimon filter) when allowBreeding — the AS-IS branch asymmetry, preserved.
        LinkCondition? link = LinkConditionOf();
        if (link is null)
        {
            return false;
        }

        bool canLinkSomewhere = allowBreeding
            ? zones.GetCards(Owner, ChoiceZone.BattleArea).Concat(zones.GetCards(Owner, ChoiceZone.BreedingArea))
                .Any(id => link.digimonCondition(new Permanent(Context, id, Owner)))
            : zones.GetCards(Owner, ChoiceZone.BattleArea)
                .Any(id => CardEffectCommons.IsOwnerBattleAreaDigimon(this, id)
                    && link.digimonCondition(new Permanent(Context, id, Owner)));
        if (!canLinkSomewhere)
        {
            return false;
        }

        // AS-IS PayCost branch (GetChangedLinkCost vs Owner.MaxMemoryCost) = design item C2-02 (lands with its
        // first witness; every in-scope caller passes false, matching the AS-IS rule-process call sites).
        // AS-IS linkCondition.digimonCondition(target).
        return link.digimonCondition(targetPermanent);
    }

    /// <summary>(W6-F / P6C3 re-fold) Mirror of AS-IS <c>CardSource.appFusionCondition</c>
    /// (CardSource.cs:3005-3027 shape): the first usable <see cref="IAddAppFusionConditionEffect"/>'s
    /// condition from THIS card's live <see cref="EffectList(EffectTiming)"/> (dispatch covered by the
    /// flip's enumeration model).</summary>
    public AppFusionCondition? AppFusionConditionOf()
    {
        foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddAppFusionConditionEffect appfusion && cardEffect.CanUse(null) &&
                appfusion.GetAppFusionCondition(this) is AppFusionCondition found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>(AD1-A) Mirror of AS-IS <c>CardSource.HasAssembly</c> (CardSource.cs:2575).</summary>
    public bool HasAssembly => AssemblyConditionOf() is not null;

    /// <summary>(AD1-A) Mirror of AS-IS <c>CardSource.assemblyCondition</c> (CardSource.cs:3043-3065): the
    /// first USABLE <c>IAddAssemblyConditionEffect</c>'s condition for THIS card. The AS-IS accessor reads
    /// the card's own EffectList — zone-independent (a HAND card has it, which is when Assembly matters) —
    /// so the primary source is the card's dispatched effect class; registered bindings are the fallback
    /// (test fixtures / registry-only setups).</summary>
    public AssemblyCondition? AssemblyConditionOf()
    {
        // (P6C3 re-fold) AS-IS assemblyCondition (CardSource.cs:3043-3065): first usable
        // IAddAssemblyConditionEffect from the live EffectList scan (dispatch covered by the flip's
        // enumeration model; the pre-flip dispatch-first/registry-fallback split is superseded).
        foreach (ICardEffect cardEffect in EffectList(EffectTiming.None))
        {
            if (cardEffect is IAddAssemblyConditionEffect assembly && cardEffect.CanUse(null) &&
                assembly.GetAssemblyCondition(this) is AssemblyCondition found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>(P6C3) AS-IS <c>CardSource.IsBeingRevealed</c> (CardSource.cs:3565, a public auto-property
    /// on the persistent Unity component). The mirror CardSource is a transient view, so the flag lives in
    /// instance metadata (the <c>IsSuspended</c> setter pattern). Design item RD-P6C3-A2: the AS-IS WRITERS
    /// (the reveal pipeline stamping the flag around a reveal) are unported — until that slice lands the
    /// flag is only ever its default false, exactly like an AS-IS card that is not mid-reveal.</summary>
    public bool IsBeingRevealed
    {
        get => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue("isBeingRevealed", out object? raw) && raw is true;
        set
        {
            if (Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? record) && record is not null)
            {
                var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    ["isBeingRevealed"] = value,
                };
                Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            }
        }
    }

    /// <summary>(P6C3) AS-IS <c>CardSource.PermanentJustBeforeRemoveField</c> (CardSource.cs:3571, a public
    /// auto-property: the permanent this card belonged to just before it left the field — read by the
    /// OnDeletion Hashtable gates). Transient-view carrier = a per-match service store keyed by InstanceId
    /// (the <c>Permanent.oldIsTapped_playCard</c> substrate pattern). Design item RD-P6C3-A3: the AS-IS
    /// WRITER (CardController stamps it right before RemoveFromAllArea) belongs to the unported
    /// CardController deletion slice — until it lands the property is null, as for an AS-IS card that never
    /// left the field.</summary>
    public Permanent? PermanentJustBeforeRemoveField
    {
        get => Context.TryGetService(out PermanentJustBeforeRemoveFieldStore? store) && store is not null
            && store.Values.TryGetValue(InstanceId, out Permanent? permanent) ? permanent : null;
        set
        {
            if (!Context.TryGetService(out PermanentJustBeforeRemoveFieldStore? store) || store is null)
            {
                store = new PermanentJustBeforeRemoveFieldStore();
                Context.RegisterService(store);
            }

            store.Values[InstanceId] = value;
        }
    }

    /// <summary>(P6C3) Per-match backing store for <see cref="PermanentJustBeforeRemoveField"/>.</summary>
    private sealed class PermanentJustBeforeRemoveFieldStore
    {
        public Dictionary<HeadlessEntityId, Permanent?> Values { get; } = new Dictionary<HeadlessEntityId, Permanent?>();
    }

    /// <summary>(P6C3) AS-IS <c>CardSource.HasSaveText</c> (CardSource.cs:2181 =
    /// <c>HasText("&lt;Save&gt;")</c>, a printed-text scan). The mirror carries no rules text; the
    /// established mirror carrier of "&lt;Save&gt; is on this card" is the instance <c>hasSave</c> metadata
    /// flag OR a live Save keyword grant — exactly the pair the live deletion-replacement pipeline gates on
    /// (<see cref="Headless.Runtime.DeletionReplacementGate.TrySaveAsync"/>).</summary>
    public bool HasSaveText =>
        Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? saveRecord) && saveRecord is not null
        && Headless.Runtime.DeletionReplacementGate.HasReplacementKeyword(
            saveRecord, Headless.Runtime.DeletionReplacementGate.HasSaveKey, Headless.Runtime.ContinuousKeywordGate.Save, Context.EffectRegistry);
}


// ===== (EFFECT-MODEL REBUILD / path-fidelity) AS-IS places every play-condition type in CardSource.cs as
// separate top-level classes (CardSource.cs:4182-4358). Relocated here from the mirror-invented Conditions.cs
// so the file grouping matches AS-IS 1:1. =====================================================================

/// <summary>(AD1-A) 1:1 mirror of AS-IS <c>AssemblyConditionElement</c> (CardSource.cs:4339-4358): one
/// material slot of an Assembly condition — an arbitrary card predicate, a required count, and an optional
/// gate against the already-selected materials.</summary>
public sealed class AssemblyConditionElement
{
    public AssemblyConditionElement(
        Func<CardSource, bool> cardCondition,
        bool skipAllIfNoSelect = true,
        string? selectMessage = null,
        int elementCount = 0,
        Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList = null)
    {
        CardCondition = cardCondition ?? throw new ArgumentNullException(nameof(cardCondition));
        this.skipAllIfNoSelect = skipAllIfNoSelect;
        this.selectMessage = selectMessage ?? string.Empty;
        ElementCount = elementCount;
        this.CanTargetCondition_ByPreSelecetedList = CanTargetCondition_ByPreSelecetedList;
    }

    public Func<CardSource, bool> CardCondition { get; }
    public bool skipAllIfNoSelect { get; }
    public int ElementCount { get; }
    public Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList { get; }
    public string selectMessage { get; }
}


/// <summary>(AD1-A) 1:1 mirror of AS-IS <c>AssemblyCondition</c> (CardSource.cs:4313-4337): the material
/// element list plus ONE flat <c>reduceCost</c>, applied only when the FULL set is assembled. Materials come
/// from the OWNER'S TRASH and end up UNDER the played permanent as digivolution cards.</summary>
public sealed class AssemblyCondition
{
    /// <summary>Old single-condition form ("1 condition × N times").</summary>
    public AssemblyCondition(
        AssemblyConditionElement element,
        Func<List<CardSource>, CardSource, bool>? CanTargetCondition_ByPreSelecetedList,
        string? selectMessage, int elementCount, int reduceCost)
    {
        ArgumentNullException.ThrowIfNull(element);
        elements = new List<AssemblyConditionElement>
        {
            new(element.CardCondition, element.skipAllIfNoSelect, selectMessage ?? element.selectMessage,
                elementCount, CanTargetCondition_ByPreSelecetedList ?? element.CanTargetCondition_ByPreSelecetedList),
        };
        this.elementCount = elementCount;
        this.reduceCost = reduceCost;
    }

    /// <summary>The A×B×C… DigiXros-like form (each element carries its own count).</summary>
    public AssemblyCondition(List<AssemblyConditionElement> elements, int reduceCost)
    {
        this.elements = elements ?? throw new ArgumentNullException(nameof(elements));
        elementCount = elements.Sum(element => element.ElementCount);
        this.reduceCost = reduceCost;
    }

    public List<AssemblyConditionElement> elements { get; }
    public int elementCount { get; }
    public int reduceCost { get; }
}


/// <summary>(W6-L) 1:1 mirror of AS-IS <c>LinkCondition</c> (CardSource.cs:4286): "this card may LINK onto
/// an owner battle-area Digimon matching <c>digimonCondition</c>, paying <c>cost</c> memory". LinkDP is NOT
/// declared here — it is per-card data (definition metadata <c>linkDP</c>, folded by LinkHelpers).</summary>
public sealed class LinkCondition
{
    public LinkCondition(Func<Permanent, bool> digimonCondition, int cost)
    {
        this.digimonCondition = digimonCondition ?? throw new ArgumentNullException(nameof(digimonCondition));
        this.cost = cost;
    }

    public Func<Permanent, bool> digimonCondition { get; }
    public int cost { get; }
}


/// <summary>(W6-F) 1:1 mirror of AS-IS <c>AppFusionCondition</c> (CardSource.cs:4298): "may App-Fuse onto an
/// owner Digimon whose TOP matches one material and one of whose LINK cards matches a DIFFERENT material,
/// paying <c>cost</c>". Executed as an EVOLUTION (the chosen link card joins the fused sources).</summary>
public sealed class AppFusionCondition
{
    public AppFusionCondition(Func<Permanent, CardSource, bool> linkedCondition, Func<Permanent, bool> digimonCondition, int cost)
    {
        this.linkedCondition = linkedCondition ?? throw new ArgumentNullException(nameof(linkedCondition));
        this.digimonCondition = digimonCondition ?? throw new ArgumentNullException(nameof(digimonCondition));
        this.cost = cost;
    }

    public Func<Permanent, CardSource, bool> linkedCondition { get; }
    public Func<Permanent, bool> digimonCondition { get; }
    public int cost { get; }
}


/// <summary>1:1 mirror of AS-IS <c>JogressConditionElement</c> (CardSource.cs:4204-4229): one evo-root slot of
/// a Jogress (DNA-digivolution) condition — a permanent predicate plus its selection message.
/// <c>EvoRootCondition</c> wraps the raw predicate so a null predicate reads as "any permanent qualifies".</summary>
public sealed class JogressConditionElement
{
    public JogressConditionElement(Func<Permanent, bool> evoRootCondition, string selectMessage)
    {
        EvoRootCondition = evoRootCondition;
        SelectMessage = selectMessage;
    }

    Func<Permanent, bool>? _evoRootCondition = null;

    public Func<Permanent, bool> EvoRootCondition
    {
        get { return permanent => _evoRootCondition == null || _evoRootCondition(permanent); }
        private set { _evoRootCondition = value; }
    }

    public string SelectMessage { get; private set; } = "";
}


/// <summary>1:1 mirror of AS-IS <c>JogressCondition</c> (CardSource.cs:4182-4202): exactly TWO evo-root slots
/// plus a memory <c>cost</c>. The ctor's guarded copy (only copies when the incoming array length matches the
/// fixed 2) is preserved verbatim.</summary>
public sealed class JogressCondition
{
    public JogressCondition(JogressConditionElement[] elements, int cost)
    {
        this.elements = new JogressConditionElement[2];

        if (this.elements.Length == elements.Length)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                this.elements[i] = elements[i];
            }
        }

        this.cost = cost;
    }

    public JogressConditionElement[] elements { get; private set; } = new JogressConditionElement[2];
    public int cost { get; private set; } = 0;
}


/// <summary>1:1 mirror of AS-IS <c>DigiXrosConditionElement</c> (CardSource.cs:4252-4265): one material slot of
/// a DigiXros — a card predicate, a selection message, and whether failing to select this slot skips the whole
/// DigiXros.</summary>
public sealed class DigiXrosConditionElement
{
    public DigiXrosConditionElement(Func<CardSource, bool> cardCondition, string selectMessage, bool skipAllIfNoSelect = false)
    {
        CardCondition = cardCondition;
        this.selectMessage = selectMessage;
        this.skipAllIfNoSelect = skipAllIfNoSelect;
    }

    public Func<CardSource, bool> CardCondition { get; private set; }
    public string selectMessage { get; private set; } = "";
    public bool skipAllIfNoSelect { get; private set; } = false;
}


/// <summary>1:1 mirror of AS-IS <c>DigiXrosCondition</c> (CardSource.cs:4231-4250): the material element list,
/// an optional cross-material gate against the already-selected set, and a per-material memory cost reduction.</summary>
public sealed class DigiXrosCondition
{
    public DigiXrosCondition(List<DigiXrosConditionElement> elements, Func<List<CardSource>, CardSource, bool> CanTargetCondition_ByPreSelecetedList, int reduceCostPerCard)
    {
        this.elements = new List<DigiXrosConditionElement>();

        foreach (DigiXrosConditionElement element in elements)
        {
            this.elements.Add(element);
        }

        this.CanTargetCondition_ByPreSelecetedList = CanTargetCondition_ByPreSelecetedList;
        this.reduceCostPerCard = reduceCostPerCard;
    }

    public List<DigiXrosConditionElement> elements { get; private set; } = new List<DigiXrosConditionElement>();
    public Func<List<CardSource>, CardSource, bool> CanTargetCondition_ByPreSelecetedList { get; private set; }
    public int reduceCostPerCard { get; private set; } = 0;
}


/// <summary>1:1 mirror of AS-IS <c>BurstDigivolutionCondition</c> (CardSource.cs:4267-4284): a burst-digivolve
/// onto a matching tamer + matching digimon for a memory <c>cost</c>, each side carrying its own predicate and
/// selection message.</summary>
public sealed class BurstDigivolutionCondition
{
    public BurstDigivolutionCondition(Func<Permanent, bool> tamerCondition, string selectTamerMessage, Func<Permanent, bool> digimonCondition, string selectDigimonMessage, int cost)
    {
        this.tamerCondition = tamerCondition;
        this.selectTamerMessage = selectTamerMessage;
        this.digimonCondition = digimonCondition;
        this.selectDigimonMessage = selectDigimonMessage;
        this.cost = cost;
    }

    public Func<Permanent, bool> tamerCondition { get; private set; }
    public string selectTamerMessage { get; private set; }
    public Func<Permanent, bool> digimonCondition { get; private set; }
    public string selectDigimonMessage { get; private set; }

    public int cost { get; private set; } = 0;
}

