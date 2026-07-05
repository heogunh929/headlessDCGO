---
name: digimon-card-porting
description: Port Digimon TCG Unity C# card effects to the HeadlessDCGO .NET card effect framework.
compatibility: opencode
---

# Digimon Card Porting Skill

## 핵심 원칙

- 레퍼런스 카드의 AS-IS → 헤드리스 변환 구조를 유지한다.
- 대상 AS-IS의 DP, 매수, 조건 술어, 타이밍만 바꾼다.
- 없는 프리미티브, enum, 속성, 메서드를 발명하지 않는다.
- 출력은 완성된 .cs 파일 하나다.
- 설명, 주석, 마크다운 설명을 추가하지 않는다.

## 역할 분리

- Planner/Reviewer: Gemma 계열. 포팅 전략, 실패 원인 분석, 의미 검수.
- Coder: Qwen Coder 계열. 실제 .cs 생성과 컴파일 오류 수정.
- Orchestrator: Python 하네스. 카드 선택, 레퍼런스 선택, 컴파일 게이트, 재시도, 로그 기록.

## 권장 운영

- exact: Qwen 단독 생성 → 실패 2회 이상 시 Gemma 진단.
- family: Gemma 기획 → Qwen 생성 → Gemma 검수.
- cold: 자동 포팅 스킵. 강모델/수동 시딩 후 레퍼런스로 승격.
