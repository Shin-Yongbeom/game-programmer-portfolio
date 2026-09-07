# Greyline — Combat & Systems (portfolio subset)

가상의 도시를 배경으로 한 3인칭 근접 액션 게임의 **게임플레이 프로그래밍 큐레이션 소스**입니다.
Unity 6 / HDRP 기반으로 1인 개발 중이며, 이 저장소는 열람용으로 정리한 부분집합입니다.

> 코드네임 "Greyline". 배경 도시와 등장 요소는 전부 허구이며 특정 국가·인물에 대한
> 정치적 주장을 담지 않습니다. 아트·오디오·모션·씬·스토리 문서는 저작권/기획 보호를 위해
> 제외했고, C# 게임플레이 코드와 시스템 설계 문서만 포함합니다.

- **엔진**: Unity 6.3 LTS (`6000.3.23f1`) / HDRP 17.3 / Input System
- **언어**: C#
- **범위**: 런타임 C# 약 100개 파일 / 약 15,800 LOC
- **역할**: 1인 개발 — 게임플레이 시스템 아키텍처·설계 직접 결정, 구현은 AI 도구(Claude/Codex)를 페어로 활용, 검증 자동화 파이프라인 직접 구축

---

## 담당 및 기여

전투·적 AI·세이브 등 게임플레이 시스템의 아키텍처와 판정 규칙, 권한 경계를 직접 설계했습니다.
구현은 Claude/Codex를 페어 프로그래밍 도구로 활용해 1인 개발 속도를 확보했고, 모든 코드를
이해하고 직접 유지보수·리팩터링하고 있습니다. 아래 설계 결정과 자동 검증 체계는 제 몫입니다.

| 영역 | 내용 |
|---|---|
| 전투 시스템 아키텍처 | 상태 우선순위 기반 히트 판정, 애니메이션 contact + 타이머 fallback 이중 경로, dedupe, 캔슬/버퍼 규칙 |
| 적 AI | Telegraph→Active→Recovery 상태 루프, 아키타입, 로컬 추격·리시, 데이터 기반 2페이즈 보스 |
| 1대다 인카운터 | `AttackSlotReservation` — 범용 crowd AI 없이 공격 턴을 예약·회수하는 조정자 |
| 데이터 주도 콘텐츠 | `AttackDefinition` / `EnemyContentProfile` / 퀘스트·캐릭터 정의를 ScriptableObject로 |
| 방어·카운터 | 가드/가드브레이크/타이밍 카운터/자세 붕괴, 우선순위 resolver |
| 환경 처형 | World가 지점 소유, Combat이 거리·각도·예약·결과 판정, 파트너 IK 정렬 |
| 세션·진행·세이브 | 일시정지 UI(입력·카메라 격리), 선행/비용 기반 스킬, 인카운터 1회 보상, 체크포인트/휴식, 버전·체크섬 세이브 프로파일(원자적 교체·백업 복구·미저장 상태 추적) |
| 데이터 오서링 | 스킬·비용·선행·인카운터 ID·보상·로스터를 ScriptableObject 카탈로그로 정의, validator가 중복/순환 ID·잘못된 전투값 거부, ID는 세이브 식별자로 안정 유지 |
| 미니게임 | 상태머신 4종 (다트/장기/스텔스/리듬), 설정 검증 테스트 |
| 프로덕션 툴 | Editor 씬 빌더·검증기, Unity CLI 래퍼(PowerShell), 로그 기반 Play QA |

---

## 전투 아키텍처 요점

### 1. gameplay 상태가 데미지 판정의 1차 근거

Animator 레이어 순서가 아니라 gameplay 상태 우선순위로 판정합니다.

```
Dead → Stagger → Dodge(이동·무적 소유) → AttackActive(contact 해결)
     → AttackStartup/Recovery(선언된 cancel만) → Locomotion
```

feedback(카메라·VFX·SFX·UI) 실패가 데미지 결과를 바꾸지 않습니다. 권한 경계가 코드로 분리되어
있습니다 — `Combat`은 타이밍·히트 쿼리·카운터 창·처형 규칙·데미지 결과를 소유하고, 표현 계층은
그 결과를 소비만 합니다.

### 2. 애니메이션 contact + 타이머 fallback 이중 경로

- 승인된 클립은 물리 contact 프레임에서 `AnimationContact`를 호출
- 이벤트가 없는 sandbox/debug 클립은 timer fallback을 사용
- 런타임은 `AttackId + HitIndex + target instance` 조합으로 두 경로를 dedupe
- 저프레임레이트 contact 윈도우와 고프레임레이트 접근 이동을 함께 처리

관련 파일: `Assets/_Project/Scripts/Combat/CombatContracts.cs`,
`Assets/_Project/Scripts/Combat/AttackDefinition.cs`, `docs/COMBAT_CONTRACTS.md`

### 3. 1대다 — 슬롯 예약

동시 공격자를 기본 1명으로 제한합니다(보스/엘리트 profile이 명시 허용 시 2명). 후보 정렬은
정면 각도 → 거리 → 시야 → 마지막 공격 경과시간. Telegraph 진입에 예약하고 Active 실패·stagger·
death·timeout에 반환합니다. 죽은 공격자는 턴을 얻거나 늦은 contact를 적용할 수 없습니다.

관련 파일: `Assets/_Project/Scripts/Enemies/EnemyEncounterCoordinator.cs`,
`Assets/_Project/Scripts/Enemies/EnemyAttack.cs`

### 4. 보스 페이즈 = 데이터

거대한 bespoke 상태머신 대신 phase 조건(health/시간/arena event), 허용 attack profile, slot 수,
telegraph/recovery 배수, counterable/guard-break flag를 데이터로 정의합니다. 페이즈 전환은
Recovery/Stagger 경계에서만 일어납니다.

---

## 검증 자동화

기본 루프: **구현 → 컴파일 → validator → 로그 기반 Play QA → feel·visual만 사람 판단**

- `Assets/Editor/`의 씬 빌더와 validator가 데이터 무결성(고유 attack id, acyclic combo graph,
  유효한 hit shape/contact source, 스킬 선행·순환 ID, 잘못된 전투값)을 컴파일 단계에서 거부
- `Tools/*.ps1`가 Unity를 batchmode로 띄워 빌드·Play QA를 실행하고, 종료 코드와 로그 성공
  마커를 함께 확인 (마커 없이 종료 0이면 실패로 판정). 이 래퍼는 Unity 없이 도는 회귀 픽스처로
  자체 테스트
- Play QA는 실제 씬 콜라이더/CharacterController 위에서 주입 입력으로 히트 타이밍·회피 변위·
  카운터·세이브 롤백까지 로그로 검증. 사람은 타격감·가독성·보스 재미만 판단

---

## 저장소 구조

```
Assets/_Project/Scripts/
  Combat/        공격 정의, 계약, 방어/카운터, 회피, 환경 처형, 모빌리티
  Enemies/       체력, 히트 리액션, 결정론적 넉백, 공격 상태기계, 인카운터 조정자
  Player/        3인칭 모터, 플레이어 전투
  Core/          캐릭터 정의, 애니메이션 드라이버, 크라우치 IK, 파트너 IK, 게임 플래그
  Interaction/   상호작용, NPC 대화 트리거, 체크포인트, 미니게임 진입
  Minigames/     상태머신 4종 + 설정
  Progression/    스킬 카탈로그, 지속 인카운터, 프로덕션 세션 루프
  Save/           버전·체크섬 세이브 스토어, 게임 플래그 세이브
  Quest / Dialogue / World / UI / Slice / Camera
Assets/_Project/Tests/    미니게임 로직·설정 검증 (Unity Test Framework)
Assets/Editor/            씬 빌더, 검증기, Play QA 하네스
Tools/                    Unity CLI 래퍼, 리소스 프리플라이트, 래퍼 픽스처 테스트 (PowerShell)
docs/
  COMBAT.md               전투 경험·런타임 규칙·상태 우선순위
  COMBAT_CONTRACTS.md     시스템 간 public 데이터 계약
```

## 빌드에 대해

열람용 소스 부분집합이므로 그대로 빌드되지 않습니다(아트·씬·패키지 캐시 제외). 의존 패키지는
`Packages/manifest.json` 참조.

---

*본 저장소는 채용 포트폴리오 열람 목적으로 공개되며, 상업적 재사용을 허가하지 않습니다.*
