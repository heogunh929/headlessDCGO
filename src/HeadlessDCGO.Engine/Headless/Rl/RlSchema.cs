// ============================================================================================================
// THE FIXED ACTION/OBSERVATION SCHEMA (obsSchemaVersion "infoset-v2-asis", actionSchemaVersion
// "lanes-v2-asis").
//
// WHY LANES, NOT PRODUCTS. The old factored space multiplied argument axes (Digivolve 16×16, Attack 17×16 →
// 599 lanes at hand-cap 16); at the measured hand cap of 50 that explodes past 1,100. Multi-argument moves
// are instead decomposed into SEQUENTIAL decision points (stage-4 design; same shape as the AS-IS
// incremental single-pick loops), so ONE flat table of 101 lanes covers every question:
//
//     0            NULL  — pass / decline / keep / no-select / end-selection
//     1            YES   — bool questions (hatch, redraw, optional)
//     2..51        hand[0..49]      — observation slot i ↔ this lane (슬롯↔레인 정렬 불변식)
//     52..67       myField[0..15]
//     68..83       foeField[0..15]
//     84           foePlayer        — security attack target
//     85..100      choice[0..15]    — materialised candidate list (buttons, count values, panel cards)
//
// The policy learns lane meaning from the observation's decision-kind one-hot. The trainer is
// schema-dynamic (host describe is the truth), so these constants owe compatibility only to themselves.
// Caps: maxHand 50 = deck structural bound (measured 45 in random self-play); maxField 16 (E-01 slack);
// maxChoice 16. Overflow NEVER truncates silently — the host logs it (stage-4 design rule).
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Rl;

public static class RlSchema
{
    public const int MaxHand = 50;

    /// <summary>필드 레인은 씬이 실제로 공급하는 구조 상한에서 파생한다: E-01 배틀에어리어 슬롯
    /// (HeadlessScene.BattleAreaSlots) + 브리딩 1. 하드코딩(구값 16)은 17번째 이후 퍼머넌트를 마스크에서
    /// 소리 없이 떨어뜨리는 결함이었다 — 소스 상수가 변하면 스키마·해시가 따라 변해 트레이너가 알아챈다.</summary>
    public const int MaxField = Bootstrap.HeadlessScene.BattleAreaSlots + 1;

    /// <summary>choice 레인의 구조 상한 = 한 좌석의 전체 덱(메인 50 + 디지타마 5) — 가장 큰 실체화
    /// 후보 집합은 "덱/트래시 전체 공개 후 선택"이다(사용자 교정 2026-07-29: 구값 16은 덱 서치 후보
    /// 17장째부터를 소리 없이 절사하는 결함). 커맨드 버튼(≤5)·카운트 후보는 이보다 항상 작다.
    /// 초과는 포획 지점이 로그한다(무음 절사 금지).</summary>
    public const int MaxChoice = 55;

    public const int LaneNull = 0;
    public const int LaneYes = 1;
    public const int LaneHand = 2;                          // ..51
    public const int LaneMyField = LaneHand + MaxHand;      // 52..67
    public const int LaneFoeField = LaneMyField + MaxField; // 68..83
    public const int LaneFoePlayer = LaneFoeField + MaxField; // 84
    public const int LaneChoice = LaneFoePlayer + 1;        // 85..100
    public const int ActionSize = LaneChoice + MaxChoice;   // 101

    public const string ObsSchemaVersion = "infoset-v2-asis";
    public const string ActionSchemaVersion = "lanes-v2-asis";

    private const int DecisionKinds = 9;
    private const int Phases = 6;

    /// <summary>Feature names in vector order. `.cardId` suffix marks embedding channels — the python
    /// extractor matches exactly that suffix (rl/dcgo_rl/policy/extractor.py:17).</summary>
    public static IReadOnlyList<string> FeatureNames { get; } = BuildFeatureNames();

    public static int ObsSize => FeatureNames.Count;

    private static List<string> BuildFeatureNames()
    {
        List<string> names = new()
        {
            "global.turn", "global.myTurn", "global.memory",
        };

        for (int i = 0; i < Phases; i++) names.Add($"global.phase[{i}]");
        for (int i = 0; i < DecisionKinds; i++) names.Add($"global.decision[{i}]");

        foreach (string side in new[] { "me", "foe" })
        {
            names.Add($"{side}.handCount");
            names.Add($"{side}.deckCount");
            names.Add($"{side}.securityCount");
            names.Add($"{side}.trashCount");
            names.Add($"{side}.breeding.cardId");
            names.Add($"{side}.breeding.level");
            names.Add($"{side}.breeding.digivCount");

            if (side == "me")
            {
                for (int i = 0; i < MaxHand; i++) names.Add($"me.hand[{i}].cardId");
            }

            for (int j = 0; j < MaxField; j++)
            {
                names.Add($"{side}.field[{j}].cardId");
                names.Add($"{side}.field[{j}].level");
                names.Add($"{side}.field[{j}].dp");
                names.Add($"{side}.field[{j}].suspended");
                names.Add($"{side}.field[{j}].digivCount");
            }
        }

        for (int i = 0; i < MaxChoice; i++) names.Add($"choice[{i}].cardId");
        names.Add("choice.count");
        names.Add("choice.selectedCount");

        return names;
    }

    /// <summary>Encodes the seat-perspective information set + the pending question. Hidden zones stay
    /// counts-only (protocol anti-cheat shape). Overflow beyond a cap is reported via
    /// <paramref name="overflow"/> — never silently dropped.</summary>
    public static double[] Encode(DecisionPoint point, CardVocabulary vocab, int turnNumber, Action<string> overflow)
    {
        GameContext context = GManager.instance!.turnStateMachine.gameContext;
        Player me = point.Seat;
        Player foe = context.You == me ? context.Opponent : context.You;

        double[] obs = new double[ObsSize];
        int k = 0;

        obs[k++] = turnNumber;
        obs[k++] = context.TurnPlayer == me ? 1 : 0;

        // Memory is stored from You's perspective; normalise to "positive = mine".
        int memory = context.Memory;
        obs[k++] = me == context.You ? memory : -memory;

        for (int i = 0; i < Phases; i++) obs[k++] = (int)context.TurnPhase == i ? 1 : 0;
        for (int i = 0; i < DecisionKinds; i++) obs[k++] = (int)point.Kind == i ? 1 : 0;

        foreach ((Player side, bool mine) in new[] { (me, true), (foe, false) })
        {
            obs[k++] = side.HandCards.Count;
            obs[k++] = side.LibraryCards.Count;
            obs[k++] = side.SecurityCards.Count;
            obs[k++] = side.TrashCards.Count;

            Permanent? breeding = side.GetBreedingAreaPermanents() is { Count: > 0 } raised ? raised[0] : null;
            obs[k++] = breeding?.TopCard is { } bTop ? vocab.IdOf(bTop.CardID) : CardVocabulary.PadId;
            obs[k++] = breeding?.Level ?? 0;
            obs[k++] = breeding?.DigivolutionCards.Count ?? 0;

            if (mine)
            {
                if (side.HandCards.Count > MaxHand) overflow($"hand {side.HandCards.Count}>{MaxHand}");

                for (int i = 0; i < MaxHand; i++)
                {
                    obs[k++] = i < side.HandCards.Count
                        ? vocab.IdOf(side.HandCards[i].CardID)
                        : CardVocabulary.PadId;
                }
            }

            // GetFieldPermanents()가 액션 인덱스의 정본(AttackPermanentAction, TSM:1599와 동일 기준) —
            // 슬롯↔레인 정렬 불변식은 이 리스트를 기준으로 성립한다. 브리딩 퍼머넌트가 포함되면
            // breeding.* 피처와 중복 표시되나 무해.
            List<Permanent> field = side.GetFieldPermanents();

            if (field.Count > MaxField) overflow($"field {field.Count}>{MaxField}");

            for (int j = 0; j < MaxField; j++)
            {
                if (j < field.Count && field[j].TopCard is { } top)
                {
                    obs[k++] = vocab.IdOf(top.CardID);
                    obs[k++] = field[j].Level;
                    obs[k++] = field[j].DP / 1000.0;
                    obs[k++] = field[j].IsSuspended ? 1 : 0;
                    obs[k++] = field[j].DigivolutionCards.Count;
                }
                else
                {
                    k += 5;
                }
            }
        }

        for (int i = 0; i < MaxChoice; i++)
        {
            obs[k++] = i < point.ChoiceCardIds.Count ? point.ChoiceCardIds[i] : CardVocabulary.PadId;
        }

        obs[k++] = point.ChoiceCount;
        obs[k++] = point.SelectedCount;

        return obs;
    }

    /// <summary>The action mask for a decision point — 1 exactly on the lanes its materialised candidates
    /// occupy (관측 슬롯↔액션 레인 정렬 불변식: hand/field lane j == observation slot j).</summary>
    public static int[] Mask(DecisionPoint point)
    {
        int[] mask = new int[ActionSize];

        if (point.NullLegal) mask[LaneNull] = 1;
        if (point.YesLegal) mask[LaneYes] = 1;
        foreach (int i in point.HandSlots) if (i < MaxHand) mask[LaneHand + i] = 1;
        foreach (int j in point.MyFieldSlots) if (j < MaxField) mask[LaneMyField + j] = 1;
        foreach (int j in point.FoeFieldSlots) if (j < MaxField) mask[LaneFoeField + j] = 1;
        if (point.FoePlayerLegal) mask[LaneFoePlayer] = 1;
        for (int i = 0; i < point.ChoiceCount && i < MaxChoice; i++) mask[LaneChoice + i] = 1;

        return mask;
    }
}
