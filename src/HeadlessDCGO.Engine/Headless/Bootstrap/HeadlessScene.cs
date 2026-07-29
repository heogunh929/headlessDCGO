// ============================================================================================================
// THE HEADLESS REPLACEMENT FOR UNITY'S SCENE.
//
// WHY THIS FILE EXISTS. In Unity a scene is DATA, not code: a `.unity` file listing GameObjects, the components
// on them, and the serialized references between them. There is no AS-IS *file* that builds the battle scene,
// so there is nothing to mirror — this is the substrate's job, exactly like the yield types and the object
// model. Without it the AS-IS engine compiles and then finds every one of its references null.
//
// WHAT THE ORIGINAL SCENE PROVIDES, AND HOW MUCH OF IT IS REPRODUCED HERE
//
//   The GManager GameObject carries the engine's service components. The AS-IS sources reach them through
//   `GManager.instance.GetComponent<T>()` — measured across the tree:
//       SelectPermanentEffect 3423 · SelectCardEffect 1242 · SelectHandEffect 1181 · Effects 748 ·
//       SelectAttackEffect 142 · SelectDigiXrosClass 20 · SelectCountEffect 14 · SelectAssemblyClass 13 ·
//       SelectDNACondition 6 · OptionalSkill 1
//   All ten are AS-IS classes, so their behaviour already exists; the scene only has to ATTACH them.
//
//   Player carries a frame hierarchy that `Player.Start()` (Player.cs:15-60) walks to build `fieldCardFrames`:
//       BattleAreaFrameParent
//         └ slot_i          (needs childCount >= 2, or the slot is skipped)
//             ├ Frame        -> GetChild(0).gameObject, must carry an Image
//             └ Frame_Select -> GetChild(1).GetComponent<Image>()
//       BreedingAreaFrameParent  (needs childCount >= 2: Frame, Frame_Select)
//
//   THIS IS LOAD-BEARING AND FAILS QUIETLY. `Player.Start()` guards the whole block with
//   `if (BattleAreaFrameParent != null && BreedingAreaFrameParent != null)`. Leave those null and there is no
//   crash — `fieldCardFrames` is simply empty, no card can ever be placed, and nothing reports why.
//
//   BATTLE-AREA SLOT COUNT is the sanctioned exception E-01. The original's slot array is a screen-layout
//   structure; the real card game has no battle-area cap. AS-IS enforces the cap in code
//   (`Permanent.CanMove`, the play path's empty-frame search) and that code runs here UNCHANGED — what changes
//   is only how many slots the scene supplies. `BattleAreaSlots` below is deliberately generous so the cap
//   never binds. See docs/audit/sanctioned_exceptions.md.
//
// LIFECYCLE. Unity calls `Awake` on every component, then `Start` on every component, and it calls them BY
// REFLECTION regardless of accessibility — `GManager.Awake` and `Player.Start` are both `private`. That order
// is reproduced here. Note `AddComponent` itself does NOT invoke Awake (see
// Headless/Unity/UnityEngineObjectModel.cs); this file is the only thing that does, so the order stays visible
// in one place instead of being buried in the object model.
//
// NOT DONE HERE, ON PURPOSE
//   * No prefab DATA. `GManager.CardPrefab` and friends are Unity assets whose fields were set in the editor;
//     the scene can attach a bare instance but cannot invent its contents. Card definitions come from
//     DCGO/Assets/CardBaseEntity/**.asset — supplying them is roadmap step 2.3, not this file.
//   * No presentation wiring. The ~261 inspector fields across GManager/Player/Effects/ContinuousController are
//     left null until something actually dereferences one. Wiring them speculatively would mean inventing a
//     scene; letting the run report them keeps the list honest.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Bootstrap;

using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds the object graph Unity's battle scene would have provided, and runs the Unity lifecycle over
/// it. See the file header for what is reproduced and what is deliberately left out.</summary>
public sealed class HeadlessScene
{
    /// <summary>Battle-area slots supplied to each player. Deliberately generous — see E-01 in the file
    /// header: the AS-IS capacity checks run unchanged, they simply never run out.</summary>
    public const int BattleAreaSlots = 64;

    /// <summary>Inspector slots the AS-IS code dereferences WITHOUT a null guard, keyed by the type that
    /// declares them. EVERY entry was added because a run threw a NullReferenceException at the line in its
    /// comment — this registry is evidence, not prediction. Fields the AS-IS code DOES guard are deliberately
    /// absent: filling them would flip a branch the original takes.
    ///
    /// Scene STRUCTURE (which object holds which, e.g. `GManager.You`) cannot be discovered this way and is
    /// wired explicitly in <see cref="Build"/> — see the note there.</summary>
    private static readonly Dictionary<Type, string[]> UnguardedFields = new()
    {
        [typeof(GManager)] = new[]
        {
            "playLog",                  // GManager.cs:273
            "hideCannotSelectObject",   // GManager.cs:275
            "LoadingObject",            // GManager.cs:384
            "selectCommandPanel",       // GManager.cs:386
        },
        [typeof(Effects)] = new[]
        {
            "ShowUseHandCardParent",    // Effects.cs:16  .gameObject.SetActive(false)
            "ShowUseHandCard",          // Effects.cs:17
            "ShowCardParent",           // Effects.cs:1024
            "ShowCardParent2",          // Effects.cs:1148
            "renderingFiedlPermanentCard",// Effects.cs:22
            "renderingHandCard",        // Effects.cs:23
            "ShowEffectDiscriptionObjectParent",// Effects.cs:25
        },
        [typeof(Player)] = new[]
        {
            "TrashHandCard",            // Player.cs:76   .gameObject.SetActive(false)
            "FirstObject",              // Player.cs:84   .SetActive(false)
            "playMatCardFrame",         // Player.cs:86   .OffFrame_Select()
            "securityObject",           // Player.cs:160  .SetSecurity(this)          (via SetPlayerUI)
            "LibraryCountText",         // Player.cs:293  .text                       (via SetLibraryCountText)
            "DeckCardImages",           // Player.cs:297  .Count  — collection, empty list is faithful
            "DigitamaLibraryCountText", // Player.cs:325  .text                  (via SetDigitamaLibraryCountText)
            "DigitamaDeckCardImages",   // Player.cs:329  .Count — collection, empty list is faithful
        },
        [typeof(SecurityObject)] = new[]
        {
            "securityBreakGlass",       // SecurityObject.cs:46  .Init(null)
            "SecurityText",             // SecurityObject.cs:128
        },
        [typeof(PlayLog)] = new[]
        {
            "_logText",                 // PlayLog.cs:111
        },
        [typeof(LoadingObject)] = new[]
        {
            "anim",                     // LoadingObject.cs:26
            "LoadingText",              // LoadingObject.cs:27
            "AnimationParent",          // LoadingObject.cs:41
            "Agumon",                   // LoadingObject.cs:43
        },
    };

    /// <summary>Abstract inspector slots and the concrete widget the original scene put there. Only entries
    /// where the choice is genuinely ambiguous need to be listed; a single concrete subclass is picked
    /// automatically.</summary>
    private static readonly Dictionary<Type, Type> AbstractSlotChoices = new()
    {
        // TMP_Text has two concrete forms: TextMeshProUGUI (Canvas UI) and TextMeshPro (world-space).
        // Every AS-IS slot of this type is a UI label, so the Canvas form is the one the scene held.
        [typeof(TMPro.TMP_Text)] = typeof(TMPro.TextMeshProUGUI),
    };

    /// <summary>AS-IS types the scene must NOT invent. These carry GAME STATE and are created by the game
    /// itself (dealt, played, digivolved) — a scene-made instance would be a ghost that no rule ever updates.
    /// Everything else in the global namespace is a scene object (panels, loading screens, log widgets) and is
    /// filled like the Unity widgets are.</summary>
    private static readonly HashSet<string> GameStateTypes = new(StringComparer.Ordinal)
    {
        "Player", "CardSource", "Permanent", "GameContext", "CEntity_Base", "CEntity_Effect",
        "FieldCardFrame", "DeckData", "TurnStateMachine", "GManager", "ContinuousController",
    };

    /// <summary>How deep the blanket fill follows references before giving up. AS-IS scene objects reference
    /// each other (`GameContext.You`, `BrainStormObject.player`), so an unbounded walk would not terminate.</summary>
    private const int MaxFillDepth = 6;

    private readonly HashSet<object> _filled = new(ReferenceEqualityComparer.Instance);

    /// <summary>Every slot the blanket fill supplied, as "Type.field : FieldType". A blanket rule can hide a
    /// genuinely missing object, so what it did stays readable.</summary>
    public List<string> FilledSlots { get; } = new();

    /// <summary>Unity widget components the AS-IS sources fetch off their OWN GameObject with
    /// <c>GetComponent&lt;T&gt;()</c> — measured across the tree: RectTransform 18, GridLayoutGroup 17,
    /// Image 16, Animator 14, AudioSource 4, and so on. In the original scene the prefab carries them; a
    /// GameObject without them returns null and the caller dereferences it unguarded
    /// (`SoundObject.cs:12` `GetComponent&lt;AudioSource&gt;().clip = clip`).
    ///
    /// Attached to every scene object for the same reason the inspector slots are filled: the original scene
    /// HAS these, so an object without them is the state that diverges. They are all inert declarations —
    /// nothing draws, plays or animates.</summary>
    private static readonly Type[] SceneWidgets =
    {
        typeof(RectTransform), typeof(Image), typeof(AudioSource), typeof(Animator),
        typeof(GridLayoutGroup), typeof(VerticalLayoutGroup), typeof(HorizontalLayoutGroup),
        typeof(ContentSizeFitter), typeof(Text), typeof(TMPro.TextMeshProUGUI), typeof(Button),
        typeof(Outline), typeof(LineRenderer), typeof(BoxCollider),

        // Click handling. The AS-IS UI registers pointer callbacks on the card image
        // (`handCard.CardImage.GetComponent<EventTrigger>().triggers.Clear()`, SelectCardPanel.cs:264) — the
        // registration runs even though nothing here clicks, so the component has to exist.
        typeof(UnityEngine.EventSystems.EventTrigger),

        // PUN attaches a PhotonView to every networked object, and the AS-IS engine fetches it off its own
        // GameObject to send RPCs — `TurnStateMachine.Init()` seeds the shared RNG through
        // `ContinuousController.instance.GetComponent<PhotonView>().RPC("SetRandom", …)`. Without the view
        // there is no RPC and the seed never arrives. Dispatch is local; see Headless/Unity/PhotonPunBehaviours.cs.
        typeof(Photon.Pun.PhotonView),
    };

    /// <summary>AS-IS components a prefab carries, keyed by the prefab's own type. The scene widgets above are
    /// Unity types every object has; these are game-specific and belong only on the object that needs them, so
    /// attaching them everywhere would be inventing scene structure.
    ///
    /// Each entry is a measured `GetComponent&lt;T&gt;()` on that object — e.g.
    /// `handCard.GetComponent&lt;Draggable_HandCard&gt;().startScale = …` (CardObjectController.cs:655),
    /// unguarded, on the object `Instantiate(handCardPrefab, …)` just returned.</summary>
    private static readonly Dictionary<Type, Type[]> PrefabComponents = new()
    {
        [typeof(HandCard)] = new[] { typeof(Draggable_HandCard) },
    };

    /// <summary>Prefab slots on <see cref="GManager"/> — templates for <c>Instantiate</c>, not game state.
    /// Listed explicitly because their field types are in <see cref="GameStateTypes"/>, which the blanket fill
    /// skips on purpose.</summary>
    private static readonly string[] PrefabSlots =
    {
        "CardPrefab",           // CardObjectController.cs:351  every CardSource in the game
        "fieldCardPrefab",      // CardObjectController.cs:495  field permanents
        "handCardPrefab",       // CardObjectController.cs:651  hand cards
    };

    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<Component> _components = new();

    /// <summary>The GameObject carrying <see cref="GManager"/> and the engine's service components.</summary>
    public GameObject ManagerObject { get; private set; } = null!;

    /// <summary>The GameObject carrying <see cref="ContinuousController"/>.</summary>
    public GameObject ContinuousObject { get; private set; } = null!;

    public GameObject YouObject { get; private set; } = null!;

    public GameObject OpponentObject { get; private set; } = null!;

    static HeadlessScene()
    {
        Component.HostCreated += AttachSceneWidgets;

        // A Unity prefab is a fully built object; `Instantiate` here constructs a bare one. Give the copy the
        // same widgets and slots the scene gives its own objects, or the caller's next line NREs — see
        // CardObjectController.CreateCardSource.
        Object.Instantiated += component => Current?.Outfit(component);
        Object.InstantiatedObject += OutfitObject;
    }

    /// <summary>The scene currently being built, so the static Unity hooks can reach its fill logic. There is
    /// one scene per process, as there is one Unity scene per player.</summary>
    private static HeadlessScene? Current { get; set; }

    /// <summary>Brings a freshly instantiated component up to the state a prefab copy would have been in:
    /// the widgets a prefab carries, and the inspector slots it had filled.</summary>
    private void Outfit(Component component)
    {
        OutfitObject(component.gameObject);

        if (PrefabComponents.TryGetValue(component.GetType(), out Type[]? extras))
        {
            foreach (Type extra in extras)
            {
                if (component.gameObject.GetComponent(extra) is null)
                {
                    Track(component.gameObject.AddComponent(extra));
                }
            }
        }

        FillSlotsOf(component);
    }

    /// <summary>Brings a GameObject up to prefab shape: the widgets a prefab carries, and the placeholder
    /// children the AS-IS presentation indexes into. A prefab copy has both; a bare `new GameObject()` has
    /// neither, and `Effects.BattleEffect` walks `GetChild(0).GetChild(0)` on objects it just instantiated
    /// (Effects.cs:2113).</summary>
    private static void OutfitObject(GameObject target)
    {
        AttachSceneWidgets(target);

        if (target.transform.childCount == 0)
        {
            AddPlaceholderChildren(target, PlaceholderDepth);
        }
    }

    /// <summary>Creates the object graph. Nothing has run yet — call <see cref="RunLifecycle"/> next.</summary>
    public void Build()
    {
        Current = this;

        RunRuntimeInitializers();

        // ContinuousController first: GManager's bootstrap waits on `ContinuousController.instance != null`
        // (GManager.cs:291) and reads `ContinuousController.instance.isAI` before that (:256).
        ContinuousObject = new GameObject("ContinuousController");
        AttachSceneWidgets(ContinuousObject);
        Track(ContinuousObject.AddComponent<ContinuousController>());

        ManagerObject = new GameObject("GManager");
        AttachSceneWidgets(ManagerObject);
        Track(ManagerObject.AddComponent<GManager>());
        Effects effects = ManagerObject.AddComponent<Effects>();
        Track(effects);


        // Effects.cs:30 does `ShowEffectDiscriptionObjectParent.GetComponent<VerticalLayoutGroup>().enabled`.
        // In the original scene that object carries the layout component; the slot alone is not enough.
        if (FieldOf(effects, "ShowEffectDiscriptionObjectParent") is not Transform descriptionParent)
        {
            throw new InvalidOperationException("ShowEffectDiscriptionObjectParent was not filled.");
        }

        Track(descriptionParent.gameObject.AddComponent<VerticalLayoutGroup>());

        Track(ManagerObject.AddComponent<SelectPermanentEffect>());
        Track(ManagerObject.AddComponent<SelectCardEffect>());
        Track(ManagerObject.AddComponent<SelectHandEffect>());
        Track(ManagerObject.AddComponent<SelectAttackEffect>());
        Track(ManagerObject.AddComponent<SelectDigiXrosClass>());
        Track(ManagerObject.AddComponent<SelectCountEffect>());
        Track(ManagerObject.AddComponent<SelectAssemblyClass>());
        Track(ManagerObject.AddComponent<SelectDNACondition>());
        Track(ManagerObject.AddComponent<OptionalSkill>());

        YouObject = BuildPlayer("You", isYou: true);
        OpponentObject = BuildPlayer("Opponent", isYou: false);

        // GManager.You / .Opponent are inspector links in the original scene. They are NOT discoverable by the
        // run-and-see loop that produced UnguardedPlayerFields: leave them null and the failure surfaces inside
        // TurnStateMachine's FIELD INITIALISER (`new bool[GManager.instance.You.fieldCardFrames.Count]`,
        // TurnStateMachine.cs:871-876), which runs before its constructor body and reports a stack that never
        // mentions GManager. Scene STRUCTURE has to be read out of the AS-IS sources; only leaf slots can be
        // enumerated by running.
        GManager manager = ManagerObject.GetComponent<GManager>()!;

        // PREFAB slots. `GameStateTypes` keeps the blanket fill from inventing a `CardSource` — a card the game
        // never dealt — but a PREFAB is not game state, it is the TEMPLATE `Instantiate` copies. The AS-IS deck
        // build is `Instantiate(GManager.instance.CardPrefab, …)` (CardObjectController.cs:351), which needs a
        // non-null template or no card can be created at all. The template carries no data (see the note on
        // Instantiate in Headless/Unity/UnityEngineStatics.cs — the copy gets default fields, which is the
        // honest result of having no Unity prefab asset), so an empty instance is exactly right.
        foreach (string prefab in PrefabSlots)
        {
            Fill(ManagerObject, manager, prefab);
        }


        SetField(manager, "You", YouObject.GetComponent<Player>());
        SetField(manager, "Opponent", OpponentObject.GetComponent<Player>());

        FillAllSlots();
    }

    /// <summary>Exceptions raised by a component's lifecycle message. Unity logs these and carries on with the
    /// remaining components rather than aborting the scene; so does <see cref="RunLifecycle"/>, and this is
    /// where they end up.</summary>
    public List<Exception> LifecycleErrors { get; } = new();

    /// <summary>Supplies the game data the original gets from Unity assets and the player's collection:
    /// the card definitions (<c>ContinuousController.SortedCardList</c>, which
    /// <c>getCardEntityByCardID</c> searches with <c>First</c>) and the deck to play
    /// (<c>ContinuousController.BattleDeckData</c>, which <c>CardObjectController.DeckRecipie</c> reads on the
    /// vs-AI path). Call after <see cref="Build"/> and before <see cref="RunLifecycle"/>.</summary>
    public void SupplyGameData(CEntity_Base[] cards, string deckCode)
    {
        ArgumentNullException.ThrowIfNull(cards);

        ContinuousController continuous = ContinuousObject.GetComponent<ContinuousController>()!;
        continuous.CardList = cards;
        continuous.SortedCardList = cards;
        continuous.BattleDeckData = DataLoading.StarterDeckCatalog.Build(deckCode, cards);

        // The original decides vs-AI in the MENU: `SelectBattleMode` sets `ContinuousController.isAI`
        // (SelectBattleMode.cs:134) and `GManager.AwakeCoroutine` copies it into `GManager.IsAI`
        // (GManager.cs:256-261). There is no menu here, so the scene makes that choice — and it must, because
        // GManager's other path to IsAI is `if (!PhotonNetwork.IsConnected)`, which no longer fires now that
        // the Photon shim reports a live local room.
        //
        // vs-AI is the mode this engine runs: it is what a self-play trainer needs, and it is the path
        // `CardObjectController.DeckRecipie` takes to read `BattleDeckData` instead of a Photon room property.
        continuous.isAI = true;
    }

    // AS-IS also has an `isAuto` developer mode and six `auto*` gameplay options that answer for the player.
    // NONE are used here (user decision, 2026-07-29): `isAuto` is an unverified developer leftover, and the
    // six options remove decisions an agent has to make — every one of them shrinks the action space. All are
    // left at their AS-IS default of false, and the virtual player answers instead.

    /// <summary>Runs every <c>[RuntimeInitializeOnLoadMethod]</c>, which Unity calls during player startup and
    /// which therefore has no other caller here.
    ///
    /// It is load-bearing, not editor bookkeeping: `PacketRegistration.RegisterAll` registers the five
    /// `MainPhaseAction` types with `GamePacketFactory`, and `TurnStateMachine.QueueMainPhaseAction` serialises
    /// every action through that factory (TurnStateMachine.cs:3027). Skip it and the first action a player
    /// takes throws "PassAction is not a registered packet".</summary>
    private static void RunRuntimeInitializers()
    {
        foreach (Type type in typeof(GManager).Assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() is null
                    || method.GetParameters().Length > 0)
                {
                    continue;
                }

                method.Invoke(null, null);
            }
        }
    }

    /// <summary>Runs Unity's message order: every <c>Awake</c>, then every <c>Start</c>. Both are invoked by
    /// reflection because the AS-IS declarations are private, exactly as Unity invokes them. A component that
    /// throws does NOT stop the others — Unity is resilient here, and a scene that aborted on the first bad
    /// component would hide every requirement behind it.</summary>
    public void RunLifecycle()
    {
        Invoke("Awake");
        Invoke("Start");
    }

    private GameObject BuildPlayer(string name, bool isYou)
    {
        GameObject playerObject = new(name);
        AttachSceneWidgets(playerObject);
        Player player = playerObject.AddComponent<Player>();
        Track(player);

        SetField(player, "isYou", isYou);

        // Player.Start() walks these to build fieldCardFrames. Both must be non-null or the whole block is
        // skipped in silence — see the file header.
        SetField(player, "BattleAreaFrameParent", BuildFrameParent(playerObject, "BattleArea", BattleAreaSlots));
        SetField(player, "BreedingAreaFrameParent", BuildBreedingFrameParent(playerObject));

        SetField(player, "HandTransform", NewChild(playerObject, "Hand").transform);
        SetField(player, "PermanentTransform", NewChild(playerObject, "Permanents").transform);

        // The play-mat frame is an inspector slot of the same SHAPE the battle-area slots have: a Frame
        // GameObject under a parent, plus a Frame_Select Image. `TurnStateMachine.ResetUI` walks it as
        // `playMatCardFrame.Frame.transform.parent.gameObject.SetActive(false)` (TurnStateMachine.cs:3011), so
        // a bare Frame with no parent is not enough — the blanket fill can only construct the object, not give
        // it the scene shape.
        SetField(player, "playMatCardFrame", BuildFrame(playerObject, "PlayMat", player));

        // Inspector slots the AS-IS code dereferences WITHOUT a null guard. EVERY entry here was added
        // because a run threw a NullReferenceException at that exact line — this list is evidence, not
        // prediction. Fields the AS-IS code DOES guard (`PlayerNameText`, `HatchObject`, `_handCountText`, …)
        // are deliberately left null: filling them would flip a branch the original takes.

        return playerObject;
    }


    /// <summary>Fills one inspector slot the way the Unity scene would have: a child GameObject carrying a
    /// component of the field's declared type. Plain <c>GameObject</c> and <c>Transform</c> fields get the
    /// object itself. Types that are neither (a plain data class such as <c>FieldCardFrame</c>) are constructed
    /// directly.</summary>
    private object? Fill(GameObject owner, object target, string fieldName)
    {
        FieldInfo field = FindField(target.GetType(), fieldName)
            ?? throw new InvalidOperationException(
                $"{target.GetType().Name} has no field '{fieldName}'. The AS-IS declaration moved or was " +
                "renamed; Headless/Bootstrap/HeadlessScene.cs must follow it.");

        if (field.GetValue(target) is not null)
        {
            return null;
        }

        Type type = field.FieldType;

        // Each filled slot gets its own CONTAINER, and the widget hangs under that. The AS-IS presentation
        // closes things by deactivating a widget's PARENT — `X.transform.parent.gameObject.SetActive(false)`
        // appears throughout `Effects` — and in the original scene that parent is an intermediate panel
        // object, not the manager. Attach the widget directly to the owner and that idiom deactivates the
        // OWNER instead: doing so on the GManager object stopped every coroutine its components had started,
        // including `TurnStateMachine`'s `GameStateMachine`, and the match silently ended.
        GameObject container = NewChild(owner, $"{fieldName}_Container");
        GameObject child = NewChild(container, fieldName);

        object value;

        if (type == typeof(GameObject))
        {
            value = child;
        }
        else if (type == typeof(Transform))
        {
            // Every GameObject already carries its Transform; Unity never adds a second one, and this one has
            // no public constructor for exactly that reason.
            value = child.transform;
        }
        else if (typeof(Component).IsAssignableFrom(type))
        {
            // An abstract slot (e.g. TMPro.TMP_Text) cannot be constructed. Unity's scene held a concrete
            // widget there; pick the one concrete subclass the substrate declares, and say so loudly if the
            // choice is ambiguous rather than guessing.
            if (type.IsAbstract)
            {
                Type[] concrete = type.Assembly.GetTypes()
                    .Where(candidate => !candidate.IsAbstract && type.IsAssignableFrom(candidate))
                    .ToArray();

                if (AbstractSlotChoices.TryGetValue(type, out Type? chosen))
                {
                    type = chosen;
                }
                else
                {
                    type = concrete.Length == 1
                        ? concrete[0]
                        : throw new InvalidOperationException(
                        $"'{fieldName}' is declared as abstract {type.FullName}; the substrate offers "
                        + $"{concrete.Length} concrete subclasses, so the scene cannot pick one. "
                            + "Headless/Bootstrap/HeadlessScene.cs must name it explicitly.");
                }
            }

            Component component = child.AddComponent(type);
            Track(component);
            value = component;
        }
        else
        {
            value = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not construct {type.FullName} for '{fieldName}'.");
        }

        field.SetValue(target, value);

        return value;
    }

    private static FieldInfo? FindField(Type declaring, string fieldName)
    {
        for (Type? type = declaring; type is not null; type = type.BaseType)
        {
            if (type.GetField(fieldName, AnyInstance) is { } field)
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Builds one FieldCardFrame in the shape `Player.Start()` builds its own: a slot object holding
    /// Frame and Frame_Select, both carrying an Image.</summary>
    private static FieldCardFrame BuildFrame(GameObject owner, string name, Player player)
    {
        GameObject slot = NewChild(owner, name);
        GameObject frame = NewFrame(slot, "Frame");
        GameObject select = NewFrame(slot, "Frame_Select");

        return new FieldCardFrame
        {
            Frame = frame,
            Frame_Select = select.GetComponent<Image>()!,
            FrameID = -1,
            player = player,
        };
    }

    /// <summary>Battle area: a parent whose children are slots, each slot carrying Frame and Frame_Select.
    /// `Player.Start()` requires <c>slot.childCount >= 2</c> or it skips the slot.</summary>
    private static Transform BuildFrameParent(GameObject owner, string name, int slots)
    {
        GameObject parent = NewChild(owner, name);

        for (int i = 0; i < slots; i++)
        {
            GameObject slot = NewChild(parent, $"Slot{i}");
            NewFrame(slot, "Frame");
            NewFrame(slot, "Frame_Select");
        }

        return parent.transform;
    }

    /// <summary>Breeding area: the parent ITSELF carries Frame and Frame_Select as its first two children
    /// (`BreedingAreaFrameParent.GetChild(0)` / `GetChild(1)`), unlike the battle area's per-slot shape.</summary>
    private static Transform BuildBreedingFrameParent(GameObject owner)
    {
        GameObject parent = NewChild(owner, "BreedingArea");
        NewFrame(parent, "Frame");
        NewFrame(parent, "Frame_Select");

        return parent.transform;
    }

    private static GameObject NewFrame(GameObject parent, string name)
    {
        GameObject frame = NewChild(parent, name);

        // Player.Start() does `Frame.GetComponent<Image>().color = …` and
        // `GetChild(1).GetComponent<Image>()` — both would NRE without a real Image attached.
        frame.AddComponent<Image>();

        return frame;
    }

    private static GameObject NewChild(GameObject parent, string name)
    {
        GameObject child = new(name);
        child.transform.SetParent(parent.transform);
        AttachSceneWidgets(child);
        AddPlaceholderChildren(child, PlaceholderDepth);

        return child;
    }

    /// <summary>Gives an object the placeholder children the AS-IS presentation indexes into. See
    /// <see cref="PlaceholderChildren"/>.</summary>
    private static void AddPlaceholderChildren(GameObject target, int depth)
    {
        if (depth <= 0)
        {
            return;
        }

        for (int i = 0; i < PlaceholderChildren; i++)
        {
            GameObject child = new($"{target.name}_{i}");
            child.transform.SetParent(target.transform);
            AttachSceneWidgets(child);
            AddPlaceholderChildren(child, depth - 1);
        }
    }

    /// <summary>How many placeholder children every scene object gets.
    ///
    /// The AS-IS presentation walks FIXED child indices — `transform.GetChild(0)`, and at the deepest
    /// `BattleAnimationObject.transform.GetChild(0).transform.GetChild(0)` (Effects.cs:2113). In the original
    /// those children are parts of a prefab; an object built here has none, and the walk throws
    /// ArgumentOutOfRange in the middle of a battle. Measured across the tree the highest index used is 3, and
    /// the deepest chain is two levels, so four children two levels deep covers every site.</summary>
    private const int PlaceholderChildren = 4;

    private const int PlaceholderDepth = 2;

    /// <summary>Gives a scene object the widget components the original prefab carried. See
    /// <see cref="SceneWidgets"/>. Public because a component can materialise its own host GameObject
    /// (<c>Component.gameObject</c> creates one on first access when nothing attached it), and that host needs
    /// the same widgets — `SoundObject` reaches itself that way and then does
    /// `GetComponent&lt;AudioSource&gt;().clip = clip`.</summary>
    public static void AttachSceneWidgets(GameObject target)
    {
        foreach (Type widget in SceneWidgets)
        {
            if (target.GetComponent(widget) is null)
            {
                target.AddComponent(widget);
            }
        }
    }

    private void Track(Component component)
    {
        _components.Add(component);

        if (UnguardedFields.TryGetValue(component.GetType(), out string[]? fields))
        {
            foreach (string field in fields)
            {
                Fill(component.gameObject, component, field);
            }
        }

    }

    /// <summary>Supplies every empty component slot on every scene object, the way the Unity scene did. The
    /// original scene HAS those panels, loading screens and labels; leaving them null is the state that
    /// DIVERGES from the original, not the state that matches it — which is why this is a blanket rule and not
    /// a list of individually justified fields.
    ///
    /// Types in <see cref="GameStateTypes"/> are skipped: those are created by the game (dealt, played,
    /// digivolved), and a scene-made instance would be a ghost no rule ever updates.
    ///
    /// Iterative, not recursive: a filled slot can itself carry empty slots, and AS-IS scene objects reference
    /// each other (`GameContext.You`, `BrainStormObject.player`), so a naive walk does not terminate.</summary>
    private void FillAllSlots()
    {
        foreach (Component component in _components.ToArray())
        {
            FillSlotsOf(component);
        }
    }

    /// <summary>Fills the empty component slots on one object and, transitively, on what it gets filled with.</summary>
    private void FillSlotsOf(object root)
    {
        Queue<(object Target, int Depth)> pending = new();
        pending.Enqueue((root, 0));

        while (pending.Count > 0)
        {
            (object target, int depth) = pending.Dequeue();

            if (depth > MaxFillDepth || !_filled.Add(target))
            {
                continue;
            }

            for (Type? type = target.GetType(); type is not null && type != typeof(Component); type = type.BaseType)
            {
                // Only slots the AS-IS sources DECLARE. Substrate shim types live in a namespace
                // (UnityEngine, TMPro, DG.Tweening, …); AS-IS types are in the global namespace or DCGO.*.
                // Without this the fill walks into the shims' own auto-property backing fields — which are
                // not scene slots at all — and tries to instantiate abstract widget bases.
                if (!IsAsIsDeclared(type))
                {
                    continue;
                }

                foreach (FieldInfo field in type.GetFields(AnyInstance | BindingFlags.DeclaredOnly))
                {
                    bool isComponent = typeof(Component).IsAssignableFrom(field.FieldType)
                        || field.FieldType == typeof(GameObject);

                    // Collections too: the original scene supplies an assigned (often empty) list, and the
                    // AS-IS guards read `.Count` unguarded — `GManager.bgms` at TurnStateMachine.cs:289 is
                    // `if (bgms.Count >= 1)`, which NREs on a null list but is simply false on an empty one.
                    bool isCollection = field.FieldType.IsGenericType
                        && field.FieldType.GetGenericTypeDefinition() == typeof(List<>);

                    if (!isComponent && !isCollection)
                    {
                        continue;
                    }

                    if (isCollection)
                    {
                        if (field.GetValue(target) is null)
                        {
                            field.SetValue(target, Activator.CreateInstance(field.FieldType));
                            FilledSlots.Add($"{type.Name}.{field.Name} : {field.FieldType.Name} (empty)");
                        }

                        continue;
                    }

                    if (GameStateTypes.Contains(field.FieldType.Name) || field.GetValue(target) is not null)
                    {
                        continue;
                    }

                    if (Fill(HostOf(target), target, field.Name) is not { } made)
                    {
                        continue;
                    }

                    FilledSlots.Add($"{type.Name}.{field.Name} : {field.FieldType.Name}");

                    if (made is Component nested)
                    {
                        pending.Enqueue((nested, depth + 1));
                    }
                }
            }
        }
    }

    /// <summary>True when the type comes from the AS-IS sources rather than a substrate shim. AS-IS declares
    /// its types in the global namespace (or <c>DCGO.CardEffects.*</c> for the card classes); every shim type
    /// is inside a vendor namespace.</summary>
    private static bool IsAsIsDeclared(Type type) =>
        type.Namespace is null || type.Namespace.StartsWith("DCGO", StringComparison.Ordinal);

    private static GameObject HostOf(object target) => target is Component component
        ? component.gameObject
        : throw new InvalidOperationException($"{target.GetType().Name} is not a Component.");

    /// <summary>Reads a field the scene has already filled.</summary>
    private static object? FieldOf(object target, string fieldName) =>
        FindField(target.GetType(), fieldName)?.GetValue(target);

    private void Invoke(string message)
    {
        foreach (Component component in _components.ToArray())
        {
            MethodInfo? method = component.GetType().GetMethod(message, AnyInstance, null, Type.EmptyTypes, null);

            if (method is null)
            {
                continue;
            }

            try
            {
                method.Invoke(component, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                LifecycleErrors.Add(ex.InnerException);
            }
            catch (Exception ex)
            {
                LifecycleErrors.Add(ex);
            }
        }
    }

    /// <summary>Assigns a field Unity's serializer would have populated from the scene. Walks the base chain
    /// because the AS-IS declarations are often <c>[SerializeField] private</c>.</summary>
    private void SetField(object target, string fieldName, object? value)
    {
        FieldInfo field = FindField(target.GetType(), fieldName)
            ?? throw new InvalidOperationException(
                $"{target.GetType().Name} has no field '{fieldName}'. The AS-IS declaration moved or was " +
                "renamed; Headless/Bootstrap/HeadlessScene.cs must follow it.");

        field.SetValue(target, value);
    }
}
