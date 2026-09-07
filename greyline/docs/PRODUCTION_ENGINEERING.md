# PRODUCTION_ENGINEERING.md

Status: Unity Production Baseline  
Engine: Unity 6000.3.23f1 LTS
Pipeline: HDRP

## Responsibility

이 문서는 프로젝트를 생산하고 검증하는 방법을 정의한다. 제품의 범위와 콘텐츠 결정은 `PROJECT_CHARTER.md`와 `CONTENT_DESIGN.md`가 소유하며, 현재 작업 순서는 `Docs/ASTRA_START_HERE.md`와 `WORKLOG.md`가 소유한다.

기존 Unreal 프로젝트는 reference/archive다. Unreal의 framework, configuration, gameplay implementation, asset dependency를 Unity Production에 도입하지 않는다.

## Default Production Loop

기본 생산 단위는 다음과 같다:

**Implement → compile → 관련 validator → 필요 시 Play QA → feel/visual만 human review**

구현 방식(C#, Editor Tool, Prefab/ScriptableObject, CLI 조합)은 작업에 맞게 선택한다. Unity
Editor에서 사람이 반복 클릭하는 작업은 기본값으로 두지 않고, 반복 작업은 자동화한다. 사람은
visual quality와 gameplay feel의 최종 판단 및 필요한 수동 조정에 집중한다.

### Unity launch gate (mandatory)

Unity execution is single-owner and stateful. Before any Editor or batchmode command, inspect
running Unity processes. There must be no stale/duplicate process for the same `main` project.
Do not run GUI Editor and batchmode concurrently, and do not repeatedly start `Unity.exe` directly
to recover from a failed launch. Close the current mode, wait for all Unity processes to exit, then
start exactly one intended mode. A scene/tool operation counts as successful only when its expected
artifact is confirmed on disk and the Unity log contains its success marker. Licensing channel
refusal/timeout must first be investigated as a duplicate-process or stale-client condition; memory
is a separate measured check, not an inferred cause.

### Git/Unity resource gate (mandatory)

Run `Tools/Greyline-ResourcePreflight.ps1` before Git worktree operations or Unity launch. The default gate requires 10 GB free physical memory and 20 GB free disk. Git worktree checkout is forbidden while Unity is running; Unity launch is forbidden while another Unity process is running. Failed or stalled operations are not retried automatically. A `LicensingClient-<user>` timeout must first be checked as a duplicate/stale Unity process or licensing-channel contention issue, not diagnosed as low memory without evidence.

Incident record (2026-09-04): repeated direct Unity launches left multiple Unity processes running;
subsequent batchmode hit a LicensingClient channel refusal/timeout. This was an execution-state
error, not a memory shortage. Follow the checks above on every future Unity operation.

**This is the canonical statement of the resource/launch safety gate.** `AGENTS.md` references it
rather than restating thresholds — update this file first if the gate changes.

## Default Stack

- C# — runtime rules와 gameplay systems
- ScriptableObject / structured data — content definitions
- Prefab — reusable runtime composition
- C# Editor Tools — asset, prefab, scene assembly와 validation automation
- Unity CLI — import, validation, build, repeatable checks
- Codex — implementation, tooling, content assembly, diagnosis
- Git — small commits와 작업 격리
- HDRP — rendering
- Input System — input
- Cinemachine — camera, 필요 시 도입
- Unity AI Navigation — 필요할 때만 도입

대형 third-party gameplay framework는 먼저 도입하지 않는다. 특정 기능의 실제 병목이 입증되고, 유지 비용과 대체 경로를 비교한 뒤에만 검토한다.

## Architecture Rules

- Project C#/data가 gameplay rules를 소유한다. Animation·camera·VFX·audio·UI는 그 결과를
  표현한다.
- cross-system 연결은 직접 참조보다 event, interface, data contract를 우선한다.
- External assets는 content를 제공하며 architecture를 강제하지 않는다.
- 새 콘텐츠는 기존 runtime primitive의 data와 Prefab 조합을 우선한다.
- 한 기능을 위해 framework를 만들거나 조기 추상화하지 않는다.
- 실제 병목이 확인되면 기존 구조를 개선·교체할 수 있다.

## Initial Runtime Boundaries

필요할 때 다음 경계를 시작점으로 사용한다. 이름과 구조는 검증 결과에 따라 바꿀 수 있다.

- Player: motor, combat, health, targeting, animator integration
- Enemy: brain, combat, health, hit reaction
- Data: attack, enemy, boss, quest, character, item definitions

초기 locomotion은 Idle, Walk, Run, Sprint, Jump, Fall, Land, Dodge 정도로 제한한다. 전투 애니메이션 품질과 hit response를 locomotion 확장보다 우선한다.

## Asset Reuse and Integration

기존 에셋과 무료/유료 상용 에셋을 적극 재사용한다. 도입 전 Unity 6/HDRP 호환성, Humanoid 호환성, 상업적 라이선스, 유지 상태, 통합 비용을 확인한다.

에셋은 콘텐츠를 제공할 수 있지만 프로젝트의 gameplay architecture를 소유하지 않는다. 여러 대형 gameplay framework를 조합하지 않는다.

## Work Isolation

Keep unrelated work in focused commits. The Astra production branch may change code, data, prefabs,
scenes, tools, and assets together when that is required for one playable increment. Unity remains a
single-owner process; use one active workspace for import, compile, validation, and visual QA.

## Definition of Done

기능 구현 자체가 완료 기준이 아니다. 모든 의미 있는 변경은 다음을 거쳐야 한다:

1. Editor compiles
2. 자동 검증 또는 Unity CLI check
3. 필요한 경우 자동 Play Mode 시나리오의 로그 및 결과 파일 확인
4. 자동화로 판정하기 어려운 gameplay feel / visual quality / encounter readability만 휴먼 리뷰

사용자 지시(2026-09-06): 반복 화면 캡처와 에이전트의 반복 관찰은 기본 검증에서 제외한다.
Codex는 컴파일·검증기·필요한 자동 Play 로그를 확인하고 발견된 기능 결함을 직접 수정한다.
사람 검증은 전투·애니메이션 feel, 월드 가독성·시각 품질, 보스·주요 set-piece의 재미에
한정하며, 그 외 상태·경로·회귀 검증은 가능한 한 자동화한다. 인간의 시각/조작감 승인을 자동
검증 통과와 혼동하지 않는다. 휴먼 리뷰 대기만을 이유로 안전한 구현·자동 검증·커밋을 중단하지 않는다.

Asset import나 code compile만으로 완료 처리하지 않는다. 생산성은 기능 수가 아니라 playable result와 iteration throughput으로 평가한다.

## Performance and Shipping

ordinary gaming PC를 기준으로 대표 gameplay를 콘텐츠 규모 확대 전에 프로파일링한다. 기술 선택의 우선순위는 shipping reliability, iteration speed, player-visible quality, architecture elegance 순이다.
