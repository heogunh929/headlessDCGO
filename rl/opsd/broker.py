"""아레나 매치 브로커 — ws 좌석 라우팅 (설계 v1 §3.2·§3.2.5).

판 1개 = runner가 세운 RlBridgeHost 1프로세스. 브로커는 turn을 좌석의 참가자 ws로 밀고
(관측 필터 적용), 착수를 릴레이하며, 타임아웃·끊김 유예·투항을 판정한다. 실시간 관전 없음.
"""

from __future__ import annotations

import asyncio
import json
import random
import time

from aiohttp import ClientSession, ClientTimeout, WSMsgType, web

from . import arena, db


class Seat:
    def __init__(self, participant, deck: dict):
        self.participant = participant          # sqlite Row
        self.deck = deck
        self.actions: asyncio.Queue = asyncio.Queue()


class Match:
    def __init__(self, match_id: str, seats: dict[int, Seat], house_seat: int = 0,
                 verification: bool = False, practice: bool = False):
        self.id = match_id
        self.seats = seats                      # 1|2 -> Seat
        self.house_seat = house_seat            # 하우스 봇 좌석(0=없음) — 브로커가 무작위 합법 수로 대행
        self.verification = verification        # 검증 에피소드(온보딩 ③): 비랭킹, 통과 시 verified=1
        self.practice = practice                # 연습판(botPlay 2026-08-01): 레이팅·전적 미반영(이력 배지)
        self.log_run = ""
        self.log_path = ""
        self.started = time.time()              # 현황 탭·이력 시작 시각(2026-08-01)
        self.step = 0                           # 마지막 관측 스텝 — 스톨 감지용


class Broker:
    def __init__(self, runner_url: str, token: str):
        self.runner_url = runner_url
        self.token = token
        self.connections: dict[int, web.WebSocketResponse] = {}   # participant id -> ws
        self.queue: list[int] = []                                # 래더 큐 (participant id)
        self.queue_since: dict[int, float] = {}                   # 큐 진입 시각 — 봇 백필 판정
        self._policy_cache: dict = {}                             # policy_path -> 로드된 모델
        self.rooms: dict[str, dict] = {}                          # 방 코드 -> {pid, deck} (룸 매치)
        self.matches: dict[str, Match] = {}                       # matchId -> Match
        self.by_participant: dict[int, str] = {}                  # participant id -> matchId
        self._seq = 0
        self._pump_started = False

    # ---------- ws 수명 ----------

    async def handle_ws(self, request: web.Request) -> web.WebSocketResponse:
        # 키 인증은 HTTP와 같은 실패 제한을 통과시킨다(보안 정리 2026-08-01: ws가 우회로였다)
        from .server import arena_participant
        participant = arena_participant(request)
        ws = web.WebSocketResponse(heartbeat=30)
        await ws.prepare(request)
        if participant is None:
            await ws.send_json({"type": "error", "code": "auth", "message": "유효한 API 키가 아님"})
            await ws.close()
            return ws

        if not self._pump_started:
            # 주기 매치메이크(온보딩 ④): enqueue 이벤트 외에도 10초마다 큐를 페어링 —
            # 검증 직후 재큐잉·시차 큐잉이 다음 이벤트 없이도 성립하게.
            self._pump_started = True
            asyncio.create_task(self._pump())

        pid = participant["id"]
        old = self.connections.get(pid)
        self.connections[pid] = ws
        if old is not None and not old.closed:
            await old.close()   # 재접속 = 기존 연결 대체(끊김 유예 재접속 경로)

        try:
            await ws.send_json({"type": "hello", "handle": participant["handle"],
                                "season": db.active_season(),
                                "inMatch": self.by_participant.get(pid)})
        except (ConnectionError, RuntimeError):
            return ws   # 접속 직후 이탈(재접속 경합) — finally가 정리
        try:
            async for msg in ws:
                if msg.type != WSMsgType.TEXT:
                    continue
                try:
                    data = json.loads(msg.data)
                except json.JSONDecodeError:
                    await ws.send_json({"type": "error", "code": "protocol", "message": "JSON 아님"})
                    continue
                await self._on_message(participant, ws, data)
        finally:
            # 재접속이 이 연결을 대체했으면(connections[pid]가 남) 큐·방은 새 연결의 것 — 건드리지
            # 않는다. 안 그러면 구 연결의 청소가 새 연결이 방금 만든 방을 지운다(실측 2026-07-30).
            if self.connections.get(pid) is ws:
                del self.connections[pid]
                if pid in self.queue:
                    self.queue.remove(pid)
                self.queue_since.pop(pid, None)
                for code, room in list(self.rooms.items()):
                    if room["pid"] == pid:
                        del self.rooms[code]    # 방 주인 이탈 = 방 소멸
        return ws

    async def _on_message(self, participant, ws, data: dict) -> None:
        pid = participant["id"]
        kind = data.get("type")

        if kind == "enqueue":
            if pid in self.by_participant:
                return await ws.send_json({"type": "error", "code": "busy", "message": "이미 대전 중"})
            if self._at_capacity():
                return await ws.send_json({"type": "error", "code": "busy",
                                           "message": "서버 동시 대전 상한 — 잠시 후 다시 시도"})
            deck = self._active_deck(pid)   # 덱 선택은 웹 전용(2026-07-31 확정) — 항상 활성 덱
            if deck is None:
                return await ws.send_json({"type": "error", "code": "no_deck", "message": "활성 덱 없음 — 먼저 덱을 등록/지정"})
            # 검증 에피소드(온보딩 ③): 미검증 참가자의 첫 큐잉 = 하우스 봇과 비랭킹 1판.
            # 통과(완주)해야 래더 진입 — 연결·타임아웃 문제를 랭킹 반영 전에 걸러낸다.
            if not self._is_verified(pid):
                await ws.send_json({"type": "queued", "position": 0,
                                    "notice": "검증 에피소드 — 하우스 봇과 비랭킹 1판(완주 시 래더 진입)"})
                await self._start_verification(participant, deck)
                return
            if pid not in self.queue:
                self.queue.append(pid)
                self.queue_since[pid] = time.time()
            await ws.send_json({"type": "queued", "position": self.queue.index(pid) + 1})
            await self._matchmake()

        elif kind == "practice":
            # 연습판(botPlay 2026-08-01): 하우스 봇과 즉시 1판 — 레이팅·이력 완전 미반영
            if pid in self.by_participant:
                return await ws.send_json({"type": "error", "code": "busy", "message": "이미 대전 중"})
            if self._at_capacity():
                return await ws.send_json({"type": "error", "code": "busy",
                                           "message": "서버 동시 대전 상한 — 잠시 후 다시 시도"})
            deck = self._active_deck(pid)
            if deck is None:
                return await ws.send_json({"type": "error", "code": "no_deck", "message": "활성 덱 없음 — 먼저 덱을 등록/지정"})
            await ws.send_json({"type": "queued", "position": 0,
                                "notice": "연습판 — 하우스 봇과 1판(레이팅·전적 미반영)"})
            await self._start_verification(participant, deck, practice=True)

        elif kind == "create_room":
            # 룸 매치(사용자 확정 2026-07-30 — 지정 도전 대체): 코드를 만들어 대외 채널로 공유,
            # 상대가 join_room으로 들어오면 즉시 성립.
            if pid in self.by_participant:
                return await ws.send_json({"type": "error", "code": "busy", "message": "이미 대전 중"})
            if self._at_capacity():
                return await ws.send_json({"type": "error", "code": "busy",
                                           "message": "서버 동시 대전 상한 — 잠시 후 다시 시도"})
            deck = self._active_deck(pid)   # 덱 선택은 웹 전용(2026-07-31 확정) — 항상 활성 덱
            if deck is None:
                return await ws.send_json({"type": "error", "code": "no_deck", "message": "활성 덱 없음"})
            for code, room in list(self.rooms.items()):
                if room["pid"] == pid:
                    del self.rooms[code]        # 참가자당 방 1개
            code = "".join(random.choices("ABCDEFGHJKMNPQRSTUVWXYZ23456789", k=6))
            self.rooms[code] = {"pid": pid, "deck": deck}
            await ws.send_json({"type": "room_created", "code": code})

        elif kind == "join_room":
            code = str(data.get("code", "")).strip().upper()
            room = self.rooms.get(code)
            if room is None:
                return await ws.send_json({"type": "error", "code": "no_room", "message": "없는 방 코드"})
            host_pid = room["pid"]
            if host_pid == pid:
                return await ws.send_json({"type": "error", "code": "self", "message": "자기 방에는 참가 불가"})
            if host_pid not in self.connections or host_pid in self.by_participant:
                del self.rooms[code]
                return await ws.send_json({"type": "error", "code": "host_unavailable", "message": "방 주인이 접속 중이 아님"})
            deck2 = self._active_deck(pid)  # 덱 선택은 웹 전용 — 항상 활성 덱
            if deck2 is None:
                return await ws.send_json({"type": "error", "code": "no_deck", "message": "활성 덱 없음"})
            del self.rooms[code]
            for waiting in (host_pid, pid):
                if waiting in self.queue:
                    self.queue.remove(waiting)
            host_row = db.conn().execute("SELECT * FROM participants WHERE id=?", (host_pid,)).fetchone()
            await self._start_match(host_row, participant, room["deck"], deck2)

        elif kind == "action":
            match_id = self.by_participant.get(pid)
            match = self.matches.get(match_id or "")
            if match is None:
                return await ws.send_json({"type": "error", "code": "no_match", "message": "진행 중 대전 없음"})
            for seat_no, seat in match.seats.items():
                if seat.participant["id"] == pid:
                    await seat.actions.put(("action", int(data.get("index", -1))))

        elif kind == "resign":
            match_id = self.by_participant.get(pid)
            match = self.matches.get(match_id or "")
            if match is not None:
                for seat_no, seat in match.seats.items():
                    if seat.participant["id"] == pid:
                        await seat.actions.put(("resign", 0))

        else:
            await ws.send_json({"type": "error", "code": "protocol", "message": f"unknown type '{kind}'"})

    def _active_deck(self, pid: int) -> dict | None:
        return arena.active_deck(pid)

    @staticmethod
    def _is_verified(pid: int) -> bool:
        row = db.conn().execute("SELECT verified FROM participants WHERE id=?", (pid,)).fetchone()
        return bool(row and row["verified"])

    async def _start_verification(self, participant, deck: dict, practice: bool = False) -> None:
        """검증 에피소드: 상대 좌석(2) = 하우스 봇(브로커가 무작위 합법 수 대행), 미러 덱, 비랭킹.
        practice=True면 연습판(botPlay) — 같은 하우스 봇 구조, 기록은 완전 미반영."""
        self._seq += 1
        match_id = f"{'pm' if practice else 'vm'}-{time.strftime('%Y%m%d%H%M%S')}-{self._seq}"
        house = db.conn().execute("SELECT * FROM participants WHERE id=?", (participant["id"],)).fetchone()
        match = Match(match_id, {1: Seat(participant, deck), 2: Seat(house, deck)},
                      house_seat=2, verification=not practice, practice=practice)
        async with ClientSession() as session:
            async with session.post(f"{self.runner_url}/arena/match",
                                    headers={"X-Ops-Token": self.token},
                                    json={"matchId": match_id, "seed": random.randrange(1, 2 ** 30),
                                          "decks": {"1": deck, "2": deck}, "maxSteps": 2000}) as response:
                body = await response.json()
                if response.status != 201:
                    if ws := self.connections.get(participant["id"]):
                        await ws.send_json({"type": "error", "code": "engine", "message": str(body)})
                    return
        match.log_run, match.log_path = body.get("run", ""), body.get("log", "")
        self.matches[match_id] = match
        self.by_participant[participant["id"]] = match_id
        if ws := self.connections.get(participant["id"]):
            await ws.send_json({"type": "match_start", "matchId": match_id, "seat": 1,
                                "verification": not practice, "practice": practice, "yourDeck": deck,
                                "opponent": {"handle": "하우스 봇(연습)" if practice else "하우스 봇(검증)",
                                             "rating": 0}})
        asyncio.create_task(self._drive(match))

    async def _pump(self) -> None:
        while True:
            await asyncio.sleep(10)
            try:
                await self._matchmake()
                await self._bot_backfill()
            except Exception:
                pass

    # ---------- 매치 성립·진행 ----------

    def _at_capacity(self) -> bool:
        """동시 대전 상한(보안 정리 2026-08-01) — 판 1개 = 엔진 프로세스 1개, OOM 방어선."""
        try:
            cap = int(db.setting("max_concurrent_matches"))
        except (TypeError, ValueError):
            cap = 6
        return cap > 0 and len(self.matches) >= cap

    async def _matchmake(self) -> None:
        while len(self.queue) >= 2:
            if self._at_capacity():
                return   # 큐는 유지 — 다음 펌프(10초)에서 재시도
            pid1, pid2 = self.queue[0], self.queue[1]
            row = lambda pid: db.conn().execute("SELECT * FROM participants WHERE id=?", (pid,)).fetchone()
            deck1, deck2 = self._active_deck(pid1), self._active_deck(pid2)
            self.queue = self.queue[2:]
            self.queue_since.pop(pid1, None)
            self.queue_since.pop(pid2, None)
            if deck1 is None or deck2 is None:
                continue
            await self._start_match(row(pid1), row(pid2), deck1, deck2)

    async def _bot_backfill(self) -> None:
        """상시 RL 봇(사용자 확정 2026-08-01): 매칭 우선도 최하위 — 사람끼리 짝이 안 잡히고
        bot_wait_sec 이상 기다린 참가자에게만 붙는다. 봇 = policy 참가자(정책 결속+활성 덱) 첫 행,
        동시 1판. bot_wait_sec 0 = 비활성."""
        try:
            wait = float(db.setting("bot_wait_sec"))
        except (TypeError, ValueError):
            wait = 60.0
        if wait <= 0 or not self.queue or self._at_capacity():
            return
        bot = db.conn().execute(
            "SELECT * FROM participants WHERE kind='policy' AND status='active' AND policy_path != ''"
            " ORDER BY id LIMIT 1").fetchone()
        if bot is None or bot["id"] in self.by_participant:
            return
        bot_deck = self._active_deck(bot["id"])
        if bot_deck is None:
            return
        now = time.time()
        for pid in list(self.queue):
            if now - self.queue_since.get(pid, now) < wait:
                continue
            row = db.conn().execute("SELECT * FROM participants WHERE id=?", (pid,)).fetchone()
            deck = self._active_deck(pid)
            self.queue.remove(pid)
            self.queue_since.pop(pid, None)
            if row is None or deck is None:
                continue
            await self._start_bot_match(row, deck, bot, bot_deck)
            return   # 봇은 동시에 1판만

    def _policy_index(self, match, msg: dict, legal: list[int]) -> int:
        """상시 봇 착수 — MaskablePPO 추론(지연 로드·캐시), 실패는 무작위 폴백(판 보전 우선).
        관측 = 호스트 벡터 + 좌석 원핫 2(DcgoSeatEnv._observation과 동일 구성)."""
        try:
            path = _resolve_policy(match.house_policy_path)
            if path is None:
                raise FileNotFoundError(match.house_policy_path)
            model = self._policy_cache.get(str(path))
            if model is None:
                from sb3_contrib import MaskablePPO
                model = MaskablePPO.load(str(path), device="cpu")
                self._policy_cache[str(path)] = model
                print(f"[bot] 정책 로드: {path}", flush=True)
            import numpy as np
            seat_onehot = np.zeros(2, dtype=np.float32)
            seat_onehot[match.house_seat - 1] = 1.0
            obs = np.concatenate([np.asarray(msg["observation"], dtype=np.float32), seat_onehot])
            mask = np.asarray(msg["actionMask"], dtype=bool)
            # 리그 PolicyOpponent와 동일하게 샘플링(deterministic=False) — 결정론 추론은
            # 루프성 플레이로 step_cap 무승부를 만들었다(실측 2026-08-01 첫 백필 판)
            action, _ = model.predict(obs, action_masks=mask, deterministic=False)
            index = int(action)
            if index in legal:
                return index
        except Exception as ex:
            print(f"[bot] 정책 추론 실패({type(ex).__name__}: {ex}) — 무작위 폴백", flush=True)
        return random.choice(legal)

    async def _start_bot_match(self, human, deck: dict, bot, bot_deck: dict) -> None:
        """봇 백필 랭킹전 — 좌석 2 = 봇(브로커가 정책 추론 대행, ws 없음). 종료 처리는 일반
        랭킹전 경로(record_match) 그대로."""
        self._seq += 1
        match_id = f"am-{time.strftime('%Y%m%d%H%M%S')}-{self._seq}"
        match = Match(match_id, {1: Seat(human, deck), 2: Seat(bot, bot_deck)}, house_seat=2)
        match.house_policy_path = bot["policy_path"]

        async with ClientSession() as session:
            async with session.post(f"{self.runner_url}/arena/match",
                                    headers={"X-Ops-Token": self.token},
                                    json={"matchId": match_id, "seed": random.randrange(1, 2 ** 30),
                                          "decks": {"1": deck, "2": bot_deck}, "maxSteps": 2000}) as response:
                body = await response.json()
                if response.status != 201:
                    if ws := self.connections.get(human["id"]):
                        await ws.send_json({"type": "error", "code": "engine", "message": str(body)})
                    return
        match.log_run, match.log_path = body.get("run", ""), body.get("log", "")
        self.matches[match_id] = match
        self.by_participant[human["id"]] = match_id
        self.by_participant[bot["id"]] = match_id
        bot_rating = arena.rating_row(bot["id"], db.active_season())["rating"]
        if ws := self.connections.get(human["id"]):
            await ws.send_json({"type": "match_start", "matchId": match_id, "seat": 1,
                                "yourDeck": deck,
                                "opponent": {"handle": bot["handle"], "rating": round(bot_rating)},
                                "notice": "대기 시간 초과 — 상시 봇과 랭킹전"})
        asyncio.create_task(self._drive(match))

    async def _start_match(self, p1, p2, deck1: dict, deck2: dict) -> None:
        self._seq += 1
        match_id = f"am-{time.strftime('%Y%m%d%H%M%S')}-{self._seq}"
        seed = random.randrange(1, 2 ** 30)
        match = Match(match_id, {1: Seat(p1, deck1), 2: Seat(p2, deck2)})

        async with ClientSession() as session:
            async with session.post(f"{self.runner_url}/arena/match",
                                    headers={"X-Ops-Token": self.token},
                                    json={"matchId": match_id, "seed": seed,
                                          "decks": {"1": deck1, "2": deck2}, "maxSteps": 2000}) as response:
                body = await response.json()
                if response.status != 201:
                    for pid in (p1["id"], p2["id"]):
                        if ws := self.connections.get(pid):
                            await ws.send_json({"type": "error", "code": "engine", "message": str(body)})
                    return
        match.log_run, match.log_path = body.get("run", ""), body.get("log", "")

        self.matches[match_id] = match
        for pid in (p1["id"], p2["id"]):
            self.by_participant[pid] = match_id
        for seat_no, seat in match.seats.items():
            if ws := self.connections.get(seat.participant["id"]):
                opponent = match.seats[3 - seat_no].participant
                elo = arena.rating_row(opponent["id"], db.active_season())["rating"]
                await ws.send_json({"type": "match_start", "matchId": match_id, "seat": seat_no,
                                    "seed": seed, "yourDeck": seat.deck,
                                    "opponent": {"handle": opponent["handle"], "rating": round(elo, 1)}})
        asyncio.create_task(self._drive(match))

    async def _drive(self, match: Match) -> None:
        """판 하나의 라우팅 루프: runner 롱폴 → 좌석 ws로 push → 착수 회수 → 릴레이."""
        move_timeout = float(db.setting("move_timeout_sec"))
        grace = float(db.setting("disconnect_grace_sec"))
        headers = {"X-Ops-Token": self.token}
        result: dict | None = None
        forced: tuple[int, str] | None = None     # (패배 좌석, 사유)

        try:
            async with ClientSession() as session:
                while result is None and forced is None:
                    async with session.get(f"{self.runner_url}/arena/match/{match.id}/turn?wait=25",
                                           headers=headers, timeout=ClientTimeout(total=60)) as response:
                        msg = await response.json()
                    kind = msg.get("type")

                    if kind == "none":
                        continue

                    if kind == "result":
                        result = msg
                        break

                    if kind == "host_exit":
                        forced = (0, "engine_abort")
                        break

                    if kind == "error":
                        # illegal_action 등 — 해당 좌석에 전달만, 다음 turn 재발행이 따라온다
                        continue

                    if kind != "turn":
                        continue

                    seat_no = int(msg["seat"])
                    if seat_no == match.house_seat:
                        # 하우스 봇 좌석: 브로커가 대행 — ws 없음. 검증/연습 = 무작위,
                        # 상시 봇(house_policy_path) = RL 정책 추론(2026-08-01).
                        legal = [i for i, v in enumerate(msg.get("actionMask") or []) if v == 1]
                        index = (self._policy_index(match, msg, legal)
                                 if getattr(match, "house_policy_path", "") else random.choice(legal))
                        async with session.post(f"{self.runner_url}/arena/match/{match.id}/act",
                                                headers=headers,
                                                json={"seat": seat_no, "index": index}) as response:
                            await response.json()
                        continue
                    seat = match.seats[seat_no]
                    match.step = msg.get("stepIndex") or match.step   # 현황 탭 스톨 감지용
                    describe = msg.get("describe") or {}
                    payload = {
                        "type": "your_turn", "matchId": match.id, "seat": seat_no,
                        "stepIndex": msg.get("stepIndex"), "kind": describe.get("kind"),
                        "selectedCount": describe.get("selectedCount"),
                        "state": filter_state(describe.get("state"), seat_no),
                        "legalActions": describe.get("legal") or
                            [{"index": i, "desc": f"lane {i}"} for i, v in enumerate(msg.get("actionMask") or []) if v],
                        "deadline": time.time() + move_timeout,
                    }
                    ws = self.connections.get(seat.participant["id"])
                    if ws is not None and not ws.closed:
                        await ws.send_json(payload)
                        wait = move_timeout
                    else:
                        wait = move_timeout + grace   # 끊김 유예 — 재접속하면 남은 시간 내 착수 가능

                    try:
                        verb, index = await asyncio.wait_for(seat.actions.get(), timeout=wait)
                    except asyncio.TimeoutError:
                        forced = (seat_no, "timeout")
                        break

                    if verb == "resign":
                        forced = (seat_no, "resign")
                        break

                    mask = msg.get("actionMask") or []
                    if not (0 <= index < len(mask) and mask[index] == 1):
                        # 불법 수 = 즉시 반칙패가 아니라 재요청 1회성 — 호스트에 보내면 error+재발행이 오므로
                        # 브로커가 직접 거절하고 같은 turn을 다시 기다린다.
                        if ws is not None and not ws.closed:
                            await ws.send_json({"type": "error", "code": "illegal_action",
                                                "message": f"index {index}", "retry": True})
                        retry_deadline = time.time() + move_timeout
                        legal_index = None
                        while time.time() < retry_deadline:
                            try:
                                verb2, index2 = await asyncio.wait_for(seat.actions.get(),
                                                                      timeout=max(0.1, retry_deadline - time.time()))
                            except asyncio.TimeoutError:
                                break
                            if verb2 == "resign":
                                forced = (seat_no, "resign")
                                break
                            if verb2 == "action" and 0 <= index2 < len(mask) and mask[index2] == 1:
                                legal_index = index2
                                break
                        if forced is not None:
                            break
                        if legal_index is None:
                            forced = (seat_no, "timeout")
                            break
                        index = legal_index

                    async with session.post(f"{self.runner_url}/arena/match/{match.id}/act",
                                            headers=headers, json={"seat": seat_no, "index": index}) as response:
                        await response.json()

                # 정리: 호스트 종료
                async with session.post(f"{self.runner_url}/arena/match/{match.id}/end", headers=headers) as response:
                    await response.json()
        except Exception as ex:                                   # 브로커 사고 = 무효판(양측 통지, 기록 없음)
            for seat in match.seats.values():
                if ws := self.connections.get(seat.participant["id"]):
                    try:
                        await ws.send_json({"type": "error", "code": "broker", "message": str(ex)})
                    except ConnectionError:
                        pass
            self._cleanup(match)
            return

        if forced is not None and forced[0] == 0:
            for seat in match.seats.values():
                if ws := self.connections.get(seat.participant["id"]):
                    await ws.send_json({"type": "match_end", "matchId": match.id, "result": "aborted"})
            self._cleanup(match)
            return

        if forced is not None:
            loser, reason = forced
            winner = 3 - loser
        else:
            winner = result.get("winnerSeat")
            reason = result.get("reason", "game_end")

        if match.practice:
            # 연습판(botPlay): 레이팅 무반영 — 이력 행은 [연습] 배지로 남긴다(뷰어 접근용, 2026-08-01)
            pid = match.seats[1].participant["id"]
            arena.record_verification(match.id, pid, match.seats[1].deck,
                                      winner if winner in (1, 2) else None, reason,
                                      match.log_run, match.log_path, practice=True,
                                      started_ts=_iso(match.started))
            if ws := self.connections.get(pid):
                try:
                    await ws.send_json({"type": "match_end", "matchId": match.id, "practice": True,
                                        "winnerSeat": winner, "reason": reason,
                                        "notice": "연습판 — 레이팅·전적 미반영"})
                except ConnectionError:
                    pass
            self._cleanup(match)
            return

        if match.verification:
            # 검증 에피소드: 비랭킹 — Elo/이력 무기록. 참가자 귀책 실패(타임아웃·투항)만 불합격.
            pid = match.seats[1].participant["id"]
            passed = not (forced is not None and forced[0] == 1)
            if passed:
                db.conn().execute("UPDATE participants SET verified=1 WHERE id=?", (pid,))
                db.conn().commit()
            # 검증판도 이력에 노출(사용자 지시 2026-08-01) — 레이팅만 무반영
            arena.record_verification(match.id, pid, match.seats[1].deck,
                                      winner if winner in (1, 2) else None, reason,
                                      match.log_run, match.log_path,
                                      started_ts=_iso(match.started))
            if ws := self.connections.get(pid):
                try:
                    await ws.send_json({"type": "match_end", "matchId": match.id, "verification": True,
                                        "passed": passed, "winnerSeat": winner, "reason": reason,
                                        "notice": "검증 통과 — 다시 큐잉하면 래더 진입" if passed
                                        else "검증 실패 — 착수 시간·연결을 점검 후 재시도"})
                except ConnectionError:
                    pass
            self._cleanup(match)
            return

        deltas = arena.record_match(
            match.id, match.seats[1].participant["id"], match.seats[2].participant["id"],
            match.seats[1].deck, match.seats[2].deck,
            winner if winner in (1, 2) else None, reason, match.log_run, match.log_path,
            started_ts=_iso(match.started))

        for seat_no, seat in match.seats.items():
            if ws := self.connections.get(seat.participant["id"]):
                try:
                    await ws.send_json({"type": "match_end", "matchId": match.id,
                                        "winnerSeat": winner, "reason": reason,
                                        "ratingDelta": deltas.get(str(seat_no))})
                except ConnectionError:
                    pass
        self._cleanup(match)

    def _cleanup(self, match: Match) -> None:
        self.matches.pop(match.id, None)
        for seat in match.seats.values():
            if self.by_participant.get(seat.participant["id"]) == match.id:
                del self.by_participant[seat.participant["id"]]


_REPO = __import__("pathlib").Path(__file__).resolve().parents[2]


def _resolve_policy(path: str):
    """policy_path 해석 — 절대/저장소 상대/runs 상대 순으로 시도."""
    from pathlib import Path
    for candidate in (Path(path), _REPO / path, _REPO / "runs" / path):
        if candidate.exists():
            return candidate
    return None


def _iso(epoch: float) -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S%z", time.localtime(epoch))


def filter_state(state, viewer_seat: int):
    """관측 필터(설계 §2): 자기 손패만 보이고, 양측 시큐리티 내용과 상대 손패는 장수로 대체.
    실게임 비공개 정보 기준 — 판 로그(전지적)와 달리 ws로는 가리고 내보낸다."""
    if not isinstance(state, dict):
        return state
    import copy
    s = copy.deepcopy(state)
    me, foe = ("p1", "p2") if viewer_seat == 1 else ("p2", "p1")
    for side, hide_hand in ((me, False), (foe, True)):
        if side not in s:
            continue
        block = s[side]
        block["securityCount"] = len(block.get("security") or [])
        block.pop("security", None)
        if hide_hand:
            block["handCount"] = len(block.get("hand") or [])
            block.pop("hand", None)
    return s
