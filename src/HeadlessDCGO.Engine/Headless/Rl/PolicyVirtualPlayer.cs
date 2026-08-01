// ============================================================================================================
// THE SEAT A POLICY DRIVES. Where RandomVirtualPlayer rolls a die, this class STOPS: it captures the
// question as a DecisionPoint (candidates = whatever the engine just materialised — wired clicks, buttons,
// candidate lists) and waits for the host to feed back a lane pick. Applying a pick uses the SAME channels
// the random seat uses: clicks, SetXxx, QueueMainPhaseAction — legality by construction.
//
// Multi-argument moves are decomposed (stage-4 design): picking a hand card whose play needs a frame, or an
// attacker that needs a target, parks an INTERMEDIATE decision point held here (`_pendingPlay`/`_pendingAttack`)
// and only queues the engine action when the second pick arrives. NULL on an intermediate point falls back
// to the AS-IS defaults (empty-frame play via PreferredFrame; abort the attack pick).
//
// Asks this seat does NOT surface (MultipleSkills, DNA inner flows, unforeseen selectors) fall back to the
// minimal SelectionChannels answer, counted in AutoAnswered — visible, never silent (stage-4 v1 scope).
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Rl;

using System.Reflection;
using HeadlessDCGO.Engine.Headless.Choices;
using UnityEngine;

public sealed class PolicyVirtualPlayer : VirtualPlayer
{
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private CardSource? _pendingPlay;
    private Permanent? _pendingAttacker;

    /// <summary>The question awaiting a policy answer, or null while the engine can advance on its own.</summary>
    public DecisionPoint? Pending { get; private set; }

    /// <summary>Selector asks answered by the minimal fallback instead of the policy, for reporting.</summary>
    public List<string> AutoAnswered { get; } = new();

    // --- capture: each ask becomes a DecisionPoint instead of an immediate answer -----------------------

    protected override bool Act(Player actor, ChoicePrompt prompt)
    {
        if (Pending is not null)
        {
            return false;
        }

        GameContext context = GManager.instance!.turnStateMachine.gameContext;

        List<int> playable = new();
        List<int> attackers = new();
        List<Action> activations = new();
        List<int> activationCardIds = new();
        List<string> activationLabels = new();

        // AS-IS 술어는 전이 중의 과도 상태에서 스스로 NRE할 수 있다 — 실측 2026-07-29:
        // `CanPlayFromHandDuringMainPhase`(:143)가 프레임 없는 배틀에어리어 퍼머넌트(파괴/이동 도중)를
        // 밟으면 `CanPlayCardTargetFrame`(:1118)에서 터진다. AS-IS 자체 AI도 같은 호출을 하는 잠재
        // 취약점이라(R7) 미러는 손대지 않고, 이 틱의 포획을 미룬다 — 과도 상태는 다음 틱이면 지나가고,
        // 지속되면 stall/step_cap 안전망이 매치를 정리한다.
        try
        {
            for (int i = 0; i < actor.HandCards.Count && i < RlSchema.MaxHand; i++)
            {
                if (actor.HandCards[i].CanPlayFromHandDuringMainPhase)
                {
                    playable.Add(i);
                }
            }

            List<Permanent> field = actor.GetFieldPermanents();

            for (int j = 0; j < field.Count && j < RlSchema.MaxField; j++)
            {
                if (field[j].CanAttack(null))
                {
                    attackers.Add(j);
                }
            }

            // 주도 기동 열거(결함 확정 2026-08-01: 디지버스트 등 [메인]/[트래시] 기동이 액션 공간에
            // 없었다). AS-IS가 클릭으로 여는 세 진입로를 같은 술어로 연다 —
            //   필드: TSM:1472와 동일 = EffectList(OnDeclaration)의 원시 인덱스 중
            //         ActivateICardEffect && CanUse(null) (SetActSkill:3061이 원시 리스트를 다시 읽음)
            //   손패: TSM:2801 · 트래시: CheckCardPanel:367 = CanDeclareSkillList의 필터된 인덱스
            //         (SetActCardSkill:3078이 그 프로퍼티를 다시 읽음)
            // 인덱스 박제-재독의 결정↔소비 간극은 AS-IS 클릭 경합과 동일 계급 — 큐 소비측 경계검사에 위임.
            for (int j = 0; j < field.Count && j < RlSchema.MaxField; j++)
            {
                List<ICardEffect> declared = field[j].EffectList(EffectTiming.OnDeclaration);

                for (int k = 0; k < declared.Count; k++)
                {
                    if (declared[k] is not ActivateICardEffect || !declared[k].CanUse(null))
                    {
                        continue;
                    }

                    int pj = j, pk = k;
                    activations.Add(() => GManager.instance!.turnStateMachine.QueueMainPhaseAction(
                        actor, new ActivatePermanentAction(pj, pk)));
                    activationCardIds.Add(field[j].TopCard is { } top
                        ? Host!.Vocab.IdOf(top.CardID) : CardVocabulary.PadId);
                    activationLabels.Add($"기동: 내 필드[{j}] {field[j].TopCard?.CardID} — {declared[k].EffectName}");
                }
            }

            void CollectCardActivations(IEnumerable<CardSource> cards, string zoneName)
            {
                foreach (CardSource card in cards)
                {
                    List<ICardEffect> declarable = card.CanDeclareSkillList;

                    for (int k = 0; k < declarable.Count; k++)
                    {
                        CardSource pc = card;
                        int pk = k;
                        activations.Add(() => GManager.instance!.turnStateMachine.QueueMainPhaseAction(
                            actor, new ActivateCardAction(pc.CardIndex, pk)));
                        activationCardIds.Add(Host!.Vocab.IdOf(card.CardID));
                        activationLabels.Add($"기동: {zoneName} {card.CardID} — {declarable[k].EffectName}");
                    }
                }
            }

            CollectCardActivations(actor.HandCards, "손패");
            CollectCardActivations(actor.TrashCards, "트래시");
        }
        catch (Exception ex)
        {
            Host?.Overflows.Add($"transient:{ex.GetType().Name}@MainPhase(다음 틱 재시도)");

            return false;
        }

        if (activations.Count > RlSchema.MaxChoice)
        {
            Host?.Overflows.Add($"activations:{activations.Count}@MainPhase");   // 무음 절사 금지
        }

        Pending = new DecisionPoint
        {
            Kind = DecisionKind.MainPhase,
            Seat = actor,
            NullLegal = true,
            HandSlots = playable,
            MyFieldSlots = attackers,
            ChoiceCount = Math.Min(activations.Count, RlSchema.MaxChoice),
            ChoiceCardIds = activationCardIds.Take(RlSchema.MaxChoice).ToList(),
            ChoiceLabels = activationLabels.Take(RlSchema.MaxChoice).ToList(),
            ChoiceApply = i => activations[i](),
        };

        return true;
    }

    protected override bool DecideHatch(Player seat, ChoicePrompt prompt)
    {
        Pending ??= new DecisionPoint
        {
            Kind = DecisionKind.Breeding, Seat = seat, NullLegal = true, YesLegal = true,
        };

        return true;
    }

    protected override bool DecideMove(Player seat, FieldPermanentCard card, ChoicePrompt prompt)
    {
        Pending ??= new DecisionPoint
        {
            Kind = DecisionKind.Breeding, Seat = seat, NullLegal = true, YesLegal = true,
        };

        return true;
    }

    protected override bool Decide(SelectCardPanel panel, ChoicePrompt prompt)
    {
        if (Pending is not null)
        {
            return true;
        }

        // 씬 기본 상태의 패널은 activeSelf=true지만 열린 게 아니다. 이전 게이트(NotSelect 콜백 배선 여부)는
        // 틀렸다: 표준 오버로드(SelectCardPanel.cs:81→:83)가 콜백을 null로 넘기므로, 그 경로의 진짜 ask를
        // 영원히 스킵해 스톨이 됐다(실측 2026-07-30, bridge_smoke 시드 2 `stall:Selection`). 진짜 ask의
        // 신호는 패널 자신의 OpenSelectCardPanel 코루틴이 `WaitWhile(() => !_isEndSelection)`(:169)에
        // 파킹된 것이다 — 그 람다는 this만 캡처하므로 Predicate.Target == panel. 빌드 중의
        // `() => scrollRect.content.childCount > 0`(:251, OpenSelectCardPanelCoroutine)도 this를 캡처하므로
        // 컴파일러 생성 메서드명(<선언 메서드>b__…)으로 가른다 — SelectionChannels.Identify와 같은 방식.
        bool awaitingEndSelection = Waits.Any(wait => wait is WaitWhile waitWhile
            && ReferenceEquals(waitWhile.Predicate.Target, panel)
            && waitWhile.Predicate.Method.Name.StartsWith("<OpenSelectCardPanel>b__", StringComparison.Ordinal));

        if (!awaitingEndSelection || GManager.instance?.turnStateMachine?.gameContext is null)
        {
            return true;
        }

        // 패널은 좌석 무소속(ask 1개) — 다른 좌석 인스턴스가 이미 이 패널을 서빙 중이면 포획하지
        // 않는다. 기본 VirtualPlayer는 포획·클릭이 한 호출 안에서 원자적이라 안전하지만, 정책 좌석은
        // 포획→적용이 분리돼 같은 틱에 양석이 같은 ask를 잡을 수 있다: 첫 클릭이 답을 등록·패널을
        // 닫은 뒤 두 번째가 죽은 패널에 유령 클릭 → 스킬 인덱스 오염(실측 2026-07-30 m-18-18:
        // MultipleSkills 순서 패널 이중 서빙 → [어택 시] 트리거 2건 전부 소실).
        if (Host?.PanelServedElsewhere(this, panel) == true)
        {
            return true;
        }

        // The panel's own wired card copies are the candidates; NotSelect/EndSelect are NULL/YES.
        // 후보는 패널 자신의 장부(_handCards)에서 읽는다 — AS-IS의 클릭·판정 루프(CheckSelection·
        // OnClickHandCard)가 걷는 그 목록이다. GetComponentsInChildren은 헤드리스에서 후보를 놓친다:
        // ScrollRect 심의 content가 독립 루트라 패널 계층 탐색이 닿지 않는다(UnityEngineUI.cs:391) —
        // 실측 2026-07-30 bridge_smoke 시드 3·5 stall(진화원 선택 패널, 후보 1인데 wired=0).
        List<HandCard> wired = (Field(panel, "_handCards") as List<HandCard> ?? new())
            .Where(card => card.OnClickAction is not null).ToList();

        if (Seat is null)
        {
            return true;    // seatless instances never serve panels in policy mode
        }

        // 패널 ask의 주인 귀속: 후보 카드의 소유자가 단일이면 그 좌석의 질문이다(멀리건=그 손패,
        // MultipleSkills 순서=그 스킬 소유 카드). 내 좌석 것이 아니면 서빙하지 않는다 — 아레나에서
        // 결정이 그 좌석의 소비자(참가자)로 라우팅되기 위한 근거. 혼성/무소유 후보는 종전대로
        // 선착 서빙(PanelServedElsewhere가 이중 서빙만 차단).
        List<Player> owners = wired.Select(card => card.cardSource?.Owner)
            .Where(owner => owner is not null).Distinct().ToList()!;

        if (owners.Count == 1 && !ReferenceEquals(owners[0], Seat))
        {
            return true;
        }

        NoteOverflow("panel", wired.Count);

        // NULL/YES 합법성은 패널 자신의 버튼 게이트에서 읽는다 — AS-IS가 이미 SetActive로 판정해 뒀다
        // (NoSelectButton :232 = _canNoSelect(), EndSelectButton :506 = CanEndSelection()). 룰 복제 0.
        bool nullLegal = panel.NoSelectButton?.activeSelf == true;
        bool yesLegal = panel.EndSelectButton?.activeSelf == true;

        if (!nullLegal && !yesLegal && wired.Count == 0)
        {
            return true;    // 패널 구축 중 과도기 틱 — 빈 마스크를 만들지 말고 다음 틱 재포획
        }

        int selectedCount = (Field(panel, "_preSelectedHandCardList") as System.Collections.ICollection)?.Count ?? 0;

        Pending = new DecisionPoint
        {
            Kind = DecisionKind.Selection,
            Seat = Seat,
            NullLegal = nullLegal,
            YesLegal = yesLegal,
            SelectedCount = selectedCount,
            ChoiceCount = Math.Min(wired.Count, RlSchema.MaxChoice),
            ChoiceCardIds = wired.Take(RlSchema.MaxChoice)
                .Select(card => card.cardSource is { } src ? Host!.Vocab.IdOf(src.CardID) : CardVocabulary.PadId)
                .ToList(),
            ChoiceApply = i => wired[i].OnClickAction!.Invoke(wired[i]),
        };

        _panel = panel;

        return true;
    }

    private SelectCardPanel? _panel;

    /// <summary>이 좌석이 지금 서빙 중(포획했고 아직 적용 전)인 패널 — 호스트의 이중 서빙 중재용.</summary>
    internal SelectCardPanel? ServingPanel => _panel;

    protected override bool AnswerSelection(PendingSelection pending)
    {
        if (Pending is not null)
        {
            return true;
        }

        DecisionPoint? point = pending.Selector switch
        {
            nameof(SelectCountEffect) => CaptureCount(pending),
            nameof(OptionalSkill) => new DecisionPoint
            {
                Kind = DecisionKind.Optional, Seat = pending.Seat, NullLegal = true, YesLegal = true,
            },
            nameof(UserSelectionManager) or nameof(SelectDigiXrosClass) => CaptureCommand(pending),
            nameof(SelectHandEffect) => CaptureWiredHand(pending),
            nameof(SelectPermanentEffect) => CaptureWiredPermanents(pending),
            nameof(SelectAttackEffect) => CaptureAttackTargets(pending),
            _ => null,
        };

        if (point is null)
        {
            // v1 미노출 질문면(MultipleSkills·DNA 내부 등): 최소 응답 폴백 — 보이게 집계.
            AutoAnswered.Add(pending.Selector);

            return SelectionChannels.Answer(pending);
        }

        Pending = point;
        _pendingSelection = pending;

        return true;
    }

    private PendingSelection? _pendingSelection;

    // --- candidate capture per selector (wiring census — no rule predicates) ---------------------------

    private DecisionPoint? CaptureCount(PendingSelection pending)
    {
        SelectCountEffect? selector = GManager.instance!.GetComponent<SelectCountEffect>();

        if (Field(selector, "_candidates") is not List<int> { Count: > 0 } candidates)
        {
            return null;
        }

        NoteOverflow("count", candidates.Count);

        return new DecisionPoint
        {
            Kind = DecisionKind.Count,
            Seat = pending.Seat,
            ChoiceCount = Math.Min(candidates.Count, RlSchema.MaxChoice),
            // Count 질문의 choice 채널은 카드가 아니라 후보 숫자값을 나른다(스키마 문서 명시).
            ChoiceCardIds = candidates.Take(RlSchema.MaxChoice).ToList(),
            ChoiceApply = i => selector!.SetCount(pending.Seat.PlayerID, candidates[i]),
        };
    }

    /// <summary>Active command buttons on the shared SelectCommandPanel — the surface where wired
    /// selections put their "End Selection" affordance (SelectPermanentEffect.cs:486-489,
    /// SelectHandEffect.cs:372-375) and UserSelectionManager its commands.</summary>
    private static List<SelectCommand> CommandButtons()
    {
        SelectCommandPanel? panel = GManager.instance?.selectCommandPanel;

        if (panel is null || !panel.gameObject.activeSelf)
        {
            return new List<SelectCommand>();
        }

        List<SelectCommand> buttons = new();

        foreach (Transform child in panel.transform)
        {
            if (child.gameObject.activeSelf && child.gameObject.GetComponent<SelectCommand>() is { } button)
            {
                buttons.Add(button);
            }
        }

        return buttons;
    }

    private DecisionPoint? CaptureCommand(PendingSelection pending)
    {
        List<SelectCommand> buttons = CommandButtons();

        if (buttons.Count == 0)
        {
            return null;    // 패널 비활성 또는 버튼이 아직 안 만들어진 틱 — 다음 틱 재시도
        }

        NoteOverflow("command", buttons.Count);

        return new DecisionPoint
        {
            Kind = DecisionKind.Command,
            Seat = pending.Seat,
            ChoiceCount = Math.Min(buttons.Count, RlSchema.MaxChoice),
            ChoiceApply = i => buttons[i].OnClick(),
        };
    }

    private DecisionPoint? CaptureWiredHand(PendingSelection pending)
    {
        List<int> slots = new();

        for (int i = 0; i < pending.Seat.HandCards.Count && i < RlSchema.MaxHand; i++)
        {
            if (FindHandView(pending.Seat.HandCards[i])?.OnClickAction is not null)
            {
                slots.Add(i);
            }
        }

        if (slots.Count == 0)
        {
            return null;
        }

        SelectHandEffect? selector = GManager.instance!.GetComponent<SelectHandEffect>();
        bool canNoSelect = Field(selector, "_canNoSelect") is true;
        int selected = (Field(selector, "_targetCards") as System.Collections.ICollection)?.Count ?? 0;

        return new DecisionPoint
        {
            Kind = DecisionKind.Selection,
            Seat = pending.Seat,
            // 부분 선택이 이미 쌓였으면 빈-답(no-select)은 선택기 내부 상태와 어긋난다 — 시작 전에만 허용.
            NullLegal = canNoSelect && selected == 0,
            // AS-IS의 완료 경로는 둘이다: 자동 종료(checkBeforeEndingSelection=false && !_canNoSelect &&
            // !_canEndNotMax && count==max, SelectHandEffect.cs:312-317) 또는 selectCommandPanel의
            // "End Selection" 버튼(:372-375). 후자를 YES로 노출하지 않으면 자동 종료가 안 걸리는 선택은
            // 완료 채널이 없어 토글(재클릭=해제) 무한루프가 된다 — 실측 2026-07-30, step_cap 34%의 원인.
            // 판정은 버튼 실체가 아니라 패널 activeSelf로 한다: CheckEndSelect가 CanEndSelect면
            // SetUpCommandButton(동기 SetActive(true), SelectCommandPanel.cs:24-25), 아니면 Off(동기
            // SetActive(false), :101) — 버튼 Instantiate는 코루틴이 1~2틱 뒤에 하므로 버튼 유무로 재면
            // 재포획이 항상 앞서 YES가 영원히 닫힌다(실측: 수리 전후 트래젝토리 동일).
            YesLegal = GManager.instance.selectCommandPanel?.gameObject.activeSelf == true,
            SelectedCount = selected,
            HandSlots = slots,
        };
    }

    private DecisionPoint? CaptureWiredPermanents(PendingSelection pending)
    {
        GameContext context = GManager.instance!.turnStateMachine.gameContext;
        Player foe = pending.Seat == context.You ? context.Opponent : context.You;

        // 후보의 정본은 현재 질문의 AS-IS 술어다 — 클릭 배선은 과거 질문(같은 선택기)의 잔존이 섞여
        // 공격자가 블로커 후보로 오인됐다(실측 2026-07-30 시드 1055 자기전투). 술어를 직접 실행해
        // 거르고, 클릭 배선은 적용 수단으로만 쓴다.
        SelectPermanentEffect? spe = GManager.instance.GetComponent<SelectPermanentEffect>();
        var canTarget = Field(spe, "_canTargetCondition") as Func<Permanent, bool>;
        List<int> mine = WiredFieldSlots(pending.Seat, typeof(SelectPermanentEffect), canTarget);
        List<int> theirs = WiredFieldSlots(foe, typeof(SelectPermanentEffect), canTarget);

        if (mine.Count == 0 && theirs.Count == 0)
        {
            return null;
        }

        SelectPermanentEffect? selector = GManager.instance.GetComponent<SelectPermanentEffect>();
        bool canNoSelect = Field(selector, "_canNoSelect") is true;
        int selected = (Field(selector, "_targetPermanents") as System.Collections.ICollection)?.Count ?? 0;

        return new DecisionPoint
        {
            Kind = DecisionKind.Selection,
            Seat = pending.Seat,
            NullLegal = canNoSelect && selected == 0,
            // "End Selection" 버튼 = 완료 채널, 판정은 패널 activeSelf — CaptureWiredHand의 YES 주석 참조
            // (같은 AS-IS 구조, SelectPermanentEffect.cs:466-473 자동 종료 · :486-489 버튼).
            YesLegal = GManager.instance.selectCommandPanel?.gameObject.activeSelf == true,
            SelectedCount = selected,
            MyFieldSlots = mine,
            FoeFieldSlots = theirs,
        };
    }

    private DecisionPoint? CaptureAttackTargets(PendingSelection pending)
    {
        GameContext context = GManager.instance!.turnStateMachine.gameContext;
        Player foe = pending.Seat == context.You ? context.Opponent : context.You;

        bool security = Field(foe.securityObject, "OnClickAction") is not null;
        List<int> defenders = WiredFieldSlots(foe, typeof(SelectAttackEffect));

        if (!security && defenders.Count == 0)
        {
            return null;
        }

        return new DecisionPoint
        {
            Kind = DecisionKind.AttackTarget,
            Seat = pending.Seat,
            FoePlayerLegal = security,
            FoeFieldSlots = defenders,
        };
    }

    /// <summary>클릭 배선의 소유자 판정 — 델리게이트의 선언 타입 사슬이 <paramref name="owner"/>인
    /// 것만 후보로 인정한다. "배선 존재=후보"는 낡은 배선(예: 공격 선언 UI가 어태커에 걸어둔 클릭)을
    /// 후보로 오인해, 블로커 선택에서 공격자를 클릭 → SwitchDefender(공격자) → 자기전투가 됐다
    /// (실측 2026-07-30 시드 1055: Birdramon 자기전투 → 중복 파괴 NRE — 사용자 룰 지적으로 발견).</summary>
    private static bool WiredBy(Delegate? action, Type owner)
    {
        for (Type? t = action?.Method.DeclaringType; t is not null; t = t.DeclaringType)
        {
            if (t == owner)
            {
                return true;
            }
        }

        return false;
    }

    private static List<int> WiredFieldSlots(Player side, Type owner, Func<Permanent, bool>? canTarget = null)
    {
        List<Permanent> field = side.GetFieldPermanents();
        List<int> slots = new();

        for (int j = 0; j < field.Count && j < RlSchema.MaxField; j++)
        {
            if (field[j].ShowingPermanentCard is { OnClickAction: not null } card && WiredBy(card.OnClickAction, owner))
            {
                bool eligible;
                try { eligible = canTarget?.Invoke(field[j]) ?? true; }   // AS-IS 술어 직접 실행(현재 질문 기준)
                catch (Exception) { eligible = false; }                   // 과도 상태 NRE는 후보 제외

                if (eligible)
                {
                    slots.Add(j);
                }
            }
        }

        return slots;
    }

    // --- apply: one lane pick, routed back through the same surfaces the clicks use ---------------------

    /// <summary>Applies a masked-legal lane to the pending decision. May immediately produce a FOLLOW-UP
    /// decision point (sequential decomposition); the caller keeps serving until Pending is null.</summary>
    public void Apply(int lane)
    {
        DecisionPoint point = Pending ?? throw new InvalidOperationException("no pending decision");
        Pending = null;

        switch (point.Kind)
        {
            case DecisionKind.MainPhase:
                ApplyMainPhase(point, lane);
                break;

            case DecisionKind.PlayTarget:
                ApplyPlayTarget(point, lane);
                break;

            case DecisionKind.AttackTarget when _pendingAttacker is not null:
                ApplyAttackLaunch(point, lane);
                break;

            case DecisionKind.AttackTarget:
                ApplyAttackAnswer(point, lane);
                break;

            case DecisionKind.Breeding:
                ApplyBreeding(point, lane);
                break;

            case DecisionKind.Optional:
                GManager.instance!.GetComponent<OptionalSkill>()!
                    .SetUseOptional(point.Seat.PlayerID, lane == RlSchema.LaneYes);
                break;

            case DecisionKind.Selection when _panel is not null:
                ApplyPanel(point, lane);
                break;

            case DecisionKind.Selection:
                ApplyWiredSelection(point, lane);
                break;

            case DecisionKind.Command:
            case DecisionKind.Count:
                point.ChoiceApply!(lane - RlSchema.LaneChoice);
                _pendingSelection = null;
                break;
        }
    }

    private void ApplyMainPhase(DecisionPoint point, int lane)
    {
        GameContext context = GManager.instance!.turnStateMachine.gameContext;

        if (lane == RlSchema.LaneNull)
        {
            GManager.instance.turnStateMachine.QueueMainPhaseAction(point.Seat, new PassAction());

            return;
        }

        if (lane >= RlSchema.LaneChoice && lane < RlSchema.LaneChoice + RlSchema.MaxChoice)
        {
            point.ChoiceApply!(lane - RlSchema.LaneChoice);   // 주도 기동(2026-08-01) — 큐잉은 클로저가

            return;
        }

        if (lane >= RlSchema.LaneMyField && lane < RlSchema.LaneMyField + RlSchema.MaxField)
        {
            _pendingAttacker = point.Seat.GetFieldPermanents()[lane - RlSchema.LaneMyField];
            Player foe = point.Seat == context.You ? context.Opponent : context.You;
            List<Permanent> enemies = foe.GetFieldPermanents();
            List<int> targets = new();

            for (int j = 0; j < enemies.Count && j < RlSchema.MaxField; j++)
            {
                if (_pendingAttacker.CanAttackTargetDigimon(enemies[j], null))
                {
                    targets.Add(j);
                }
            }

            Pending = new DecisionPoint
            {
                Kind = DecisionKind.AttackTarget,
                Seat = point.Seat,
                FoePlayerLegal = _pendingAttacker.CanAttackTargetDigimon(null, null),
                FoeFieldSlots = targets,
            };

            return;
        }

        CardSource card = point.Seat.HandCards[lane - RlSchema.LaneHand];

        if (!card.IsPermanent)
        {
            QueuePlay(point.Seat, card, targetFrame: -1);

            return;
        }

        List<FieldCardFrame> frames = point.Seat.fieldCardFrames;
        List<int> evolveSlots = new();
        bool emptyLegal = false;
        List<Permanent> field = point.Seat.GetFieldPermanents();

        foreach (FieldCardFrame frame in frames)
        {
            if (!card.CanPlayCardTargetFrame(frame, true, null))
            {
                continue;
            }

            if (frame.IsEmptyFrame())
            {
                emptyLegal = true;
            }
            else
            {
                int slot = field.FindIndex(p => p.PermanentFrame == frame);

                if (slot >= 0 && slot < RlSchema.MaxField && !evolveSlots.Contains(slot))
                {
                    evolveSlots.Add(slot);
                }
            }
        }

        if (!emptyLegal && evolveSlots.Count == 0)
        {
            // 술어상 플레이 가능이나 대상 프레임이 없는 극단 — 패스로 강등.
            GManager.instance.turnStateMachine.QueueMainPhaseAction(point.Seat, new PassAction());

            return;
        }

        if (evolveSlots.Count == 0)
        {
            // 빈-프레임 플레이는 frame을 여기서 정하지 않는다(-1 위임). 큐잉 시점에 빈 프레임을 박제하면
            // 소비 시점까지의 틱 사이에 비동기 이동(브리딩 이동 등)이 그 프레임을 점유해 플레이가 검증
            // 없는 진화로 둔갑한다 — 실측 2026-07-30 00:1x(TOCTOU). TargetFrameID=-1이면 TSM:1206이
            // targetPermanent=null로 넘기고 PlayCardClass가 소비 시점에 PreferredFrame을 재계산한다
            // (CardController.cs:1310) — 사람 드롭과 같은 동기성.
            QueuePlay(point.Seat, card, targetFrame: -1);

            return;
        }

        _pendingPlay = card;
        Pending = new DecisionPoint
        {
            Kind = DecisionKind.PlayTarget,
            Seat = point.Seat,
            NullLegal = emptyLegal,
            MyFieldSlots = evolveSlots,
        };
    }

    private void ApplyPlayTarget(DecisionPoint point, int lane)
    {
        CardSource card = _pendingPlay ?? throw new InvalidOperationException("no pending play");
        _pendingPlay = null;

        // NULL(빈 프레임) 선택도 -1 위임 — ApplyMainPhase의 TOCTOU 주석 참조.
        int frame = lane == RlSchema.LaneNull
            ? -1
            : point.Seat.GetFieldPermanents()[lane - RlSchema.LaneMyField].PermanentFrame.FrameID;

        QueuePlay(point.Seat, card, frame);
    }

    private void ApplyAttackLaunch(DecisionPoint point, int lane)
    {
        Permanent attacker = _pendingAttacker!;
        _pendingAttacker = null;

        int attackerIndex = point.Seat.GetFieldPermanents().IndexOf(attacker);
        int target = lane == RlSchema.LaneFoePlayer ? -1 : lane - RlSchema.LaneFoeField;

        GManager.instance!.turnStateMachine.QueueMainPhaseAction(
            point.Seat, new AttackPermanentAction(attackerIndex, target));
    }

    private void ApplyAttackAnswer(DecisionPoint point, int lane)
    {
        // SelectAttackEffect 질문(효과 유발 공격 대상): 배선된 클릭을 그대로 누른다.
        GameContext context = GManager.instance!.turnStateMachine.gameContext;
        Player foe = point.Seat == context.You ? context.Opponent : context.You;

        if (lane == RlSchema.LaneFoePlayer)
        {
            (Field(foe.securityObject, "OnClickAction") as UnityEngine.Events.UnityAction)?.Invoke();
        }
        else
        {
            FieldPermanentCard card = foe.GetFieldPermanents()[lane - RlSchema.LaneFoeField].ShowingPermanentCard!;
            card.OnClickAction!.Invoke(card);
        }

        _pendingSelection = null;
    }

    private void ApplyBreeding(DecisionPoint point, int lane)
    {
        if (lane == RlSchema.LaneYes)
        {
            if (point.Seat.OnClickHatchObjectAction is not null)
            {
                point.Seat.OnClickHatchObject();
            }
            else if (point.Seat.GetBreedingAreaPermanents() is { Count: > 0 } raised
                && raised[0].ShowingPermanentCard is { OnClickAction: not null } moveCard)
            {
                moveCard.OnClickAction.Invoke(moveCard);
            }

            return;
        }

        GManager.instance!.turnStateMachine.SetBreedingPhase(point.Seat.PlayerID, doBreeding: false);
    }

    private void ApplyPanel(DecisionPoint point, int lane)
    {
        SelectCardPanel panel = _panel!;

        // 포획과 적용 사이에 패널이 닫혔으면(자동 종료·타 경로 완료) 클릭을 폐기한다 — 닫힌 패널
        // 위 클릭은 AS-IS 핸들러의 부작용만 남긴다(위 이중 서빙 주석의 유령 클릭과 같은 계열).
        if (!panel.gameObject.activeSelf)
        {
            Host?.Overflows.Add("panel-stale:click-dropped");
            _panel = null;

            return;
        }

        if (lane == RlSchema.LaneNull)
        {
            panel.OnClickNotSelectButton();
            _panel = null;
        }
        else if (lane == RlSchema.LaneYes)
        {
            panel.OnClickEndSelectButton();
            _panel = null;
        }
        else
        {
            point.ChoiceApply!(lane - RlSchema.LaneChoice);
            // 카드 픽 뒤에도 패널이 계속 열려 있으면(다중 선택) 다음 틱에 같은 질문이 다시 잡힌다.
            _panel = null;
        }
    }

    private void ApplyWiredSelection(DecisionPoint point, int lane)
    {
        if (lane == RlSchema.LaneYes)
        {
            // 선택 완료: selectCommandPanel의 "End Selection" 버튼 클릭(사람과 같은 표면).
            // 버튼 코루틴 지연으로 사라진 틱이면 no-op — 같은 질문이 다음 틱 재포획된다.
            {
            }
            if (CommandButtons() is { Count: > 0 } buttons)
            {
                buttons[0].OnClick();
            }

            _pendingSelection = null;

            return;
        }

        if (lane == RlSchema.LaneNull)
        {
            // no-select: 해당 선택기의 최소 응답(빈 선택)이 곧 no-select 채널이다.
            if (_pendingSelection is { } pendingSelection)
            {
                SelectionChannels.Answer(pendingSelection);
            }

            _pendingSelection = null;

            return;
        }

        if (lane >= RlSchema.LaneHand && lane < RlSchema.LaneHand + RlSchema.MaxHand)
        {
            HandCard view = FindHandView(point.Seat.HandCards[lane - RlSchema.LaneHand])!;
            view.OnClickAction!.Invoke(view);
        }
        else
        {
            GameContext context = GManager.instance!.turnStateMachine.gameContext;
            Player side = lane < RlSchema.LaneFoeField
                ? point.Seat
                : point.Seat == context.You ? context.Opponent : context.You;
            int slot = lane < RlSchema.LaneFoeField ? lane - RlSchema.LaneMyField : lane - RlSchema.LaneFoeField;
            FieldPermanentCard card = side.GetFieldPermanents()[slot].ShowingPermanentCard!;
            Console.Error.WriteLine($"[CLICK] lane={lane} seat={point.Seat.PlayerName} side={side.PlayerName} card={card.ThisPermanent?.TopCard?.CardID} handler={card.OnClickAction?.Method.DeclaringType?.FullName}"); // TEMP-PROBE-SELF
            card.OnClickAction!.Invoke(card);
        }

        _pendingSelection = null;
    }

    private void QueuePlay(Player seat, CardSource card, int targetFrame)
    {
        GameContext context = GManager.instance!.turnStateMachine.gameContext;
        int cardIndex = context.ActiveCardList.IndexOf(card);
        GManager.instance.turnStateMachine.QueueMainPhaseAction(
            seat, new PlayCardAction(cardIndex, targetFrame, null, -1, null));
    }

    // --- plumbing ---------------------------------------------------------------------------------------

    /// <summary>Set by the host so panel captures can translate card ids through the shared vocabulary.</summary>
    public RlMatchHost? Host { get; set; }

    private void NoteOverflow(string what, int count)
    {
        if (count > RlSchema.MaxChoice)
        {
            Host?.Overflows.Add($"choice:{what} {count}>{RlSchema.MaxChoice}");
        }
    }

    /// <summary>The on-screen HandCard view for a hand CardSource — the AS-IS accessor
    /// (`CardSource.ShowingHandCard`, CardSource.cs:327), the same object SelectHandEffect wires.</summary>
    private static HandCard? FindHandView(CardSource card) => card.ShowingHandCard;

    private static object? Field(object? target, string name)
    {
        for (Type? type = target?.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField(name, AnyInstance) is { } field)
            {
                return field.GetValue(target);
            }
        }

        return null;
    }
}
