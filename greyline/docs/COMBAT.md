# COMBAT.md

Status: Combat Production Baseline
Product: Greyline

플레이어가 느끼는 전투 경험과, 그것을 런타임 동작으로 만드는 규칙을 함께 소유한다.
공용 데이터 계약(인터페이스, `DamageInfo` 메타데이터)은 `Docs/SHARED_COMBAT_CONTRACTS.md`,
전투가 콘텐츠에 배치되는 방식은 `CONTENT_DESIGN.md`가 소유한다. 세부 수치는 Play 튜닝으로
정하고, 플레이 경험의 의미가 바뀔 때만 이 문서를 갱신한다.

## Target

Sleeping Dogs + Sifu. 강한 contact, 빠른 flow, readable enemy attacks, 통제된 다대1.
God of War식 고비용 cinematic action과 Yakuza식 대량 환경 상호작용은 우선순위가 낮다.

핵심 감각: 강한 타격감, 끊기지 않는 공격/회피 flow, 명확한 hit reaction, 적절한 cancel,
작은 기술 세트에서 조합이 늘어나는 성장감.

## Player Vocabulary

Light Combo / Heavy / Charged Heavy / Guard / Dodge / Dodge Attack / Sprint Attack·Combo /
Tackle / Jump Attack / Sliding Attack / Finisher / Gauge Ultimate / Weapons / Limited pistol.

Parry / Perfect Guard는 Guard 안정화 이후 후보이며 1.0 필수가 아니다.
Grab / Throw / 대규모 환경 전투는 후순위다.

## Progression

모든 기술을 처음부터 주지 않는다. 스토리 진행이 새 technique tier를 열고, 플레이어는 일부
기술의 해금 순서를 선택한다.

- Chapter 1: Light 3-hit / Heavy / Guard / Dodge로 시작 → Charged Heavy, Dodge Attack,
  Sprint Attack, Jump Attack, 선택 combo route 1개.
- Chapter 2: Tackle, Sliding Attack, Sprint Combo, Finisher, 추가 combo route.
- Chapter 3: Gauge Ultimate, advanced ender, finisher variation, 선택한 combat style 강화.

기본 기술만으로도 메인 진행이 가능해야 한다.

### Technique Board

대형 RPG skill tree를 만들지 않는다. 3계통(Flow / Power / Mobility), 총 약 12 node 상한.
검증된 combat primitive를 unlock/variation으로 확장하는 용도다.

## Cancel Rules

flow를 위해 cancel을 적극 쓰되 모든 행동을 무제한 cancel 가능하게 만들지 않는다.

- Light: hit 이후 Dodge / Guard cancel, combo는 input buffer 허용.
- Heavy: startup commitment 유지, contact 이후 Dodge 후보.
- Charged Heavy: charge 도중 Dodge cancel 허용.
- Dodge Attack: combo flow로 복귀 가능.
- Finisher / Gauge Ultimate: cancel 불가.

## Guard / Gauge / Finisher

- Guard: hold, 전방 melee 완화 또는 차단, guard reaction, guard break 가능한 강공. Guard
  구현 때문에 현재 Dodge/combo flow를 크게 재설계하지 않는다. timed parry/counter는 후순위.
- Gauge: Chapter 2~3 해금. 공격 적중·combo 유지·위험한 Dodge·stagger/finisher로 획득,
  Gauge Ultimate 한 곳에만 사용. 복수 resource gauge를 만들지 않는다.
- Finisher: enemy low HP 또는 stagger/vulnerable 조건. 짧고 강하고 readable하며 반복 사용
  가능한 제작비. 고비용 bespoke cinematic finisher를 대량 제작하지 않는다.

## Runtime States

### Player

`Locomotion`, `AttackStartup`, `AttackActive`, `AttackRecovery`, `Charge`, `Dodge`, `Slide`,
`Tackle`, `Stagger`, `Guard`, `Dead`는 gameplay 상태다. Animator layer 순서가 아니라 gameplay
우선순위이며, damage 판정은 gameplay 상태를 먼저 본다.

우선순위(높음→낮음): `Dead` → `Stagger` → `Dodge`(이동·무적 소유) → `AttackActive`(contact 해결) →
`AttackStartup`/`AttackRecovery`(선언된 cancel만) → `Locomotion`.

### Enemy

`Idle → Detect → Approach → Telegraph → Active → Recovery → Cooldown` 기본 루프.

- Telegraph가 공격 의도를 고정하고 플레이어에게 읽을 정보를 준다.
- Active는 attack slot을 소유한 경우 한 번의 contact를 해결한다.
- Recovery/Cooldown이 여유를 주고 slot을 반환한다.
- Stagger/Dead는 즉시 공격을 멈추고 slot을 놓는다.
- Active 공격은 attack profile이 명시적으로 허용하지 않는 한 타깃을 추적하지 않는다.

## Attack Profiles

모든 공격은 중복 구현이 아니라 기존 `AttackDefinition` 계열을 사용한다. 승인된 profile은
안정적 `attackId` + presentation `animationState`, startup/active/recovery 의미, damage·reaction·
knockback·hit shape(range/radius/forward offset), combo route, `CombatHitFlags`와 contact source,
cancel/buffer 정책을 정의한다.

mobility 스크립트의 프로토타입 직접 hit 로직은 임시다. production 승인 전에 승인된 profile을
참조해 timing·radius·flags·dedupe·feedback이 한 경로를 따르게 한다. Charged Heavy는 release
시점의 `chargeNormalized`를 snapshot하며, 매 프레임 damage나 다중 인스턴스를 만들지 않는다.

## Contact and Timer Fallback

애니메이션 contact가 최종 근거다. 승인된 클립은 물리 contact 프레임에서 `AnimationContact`를
호출할 수 있다. 이벤트가 없는 sandbox/debug 클립은 timer fallback을 쓰며, profile이 `Event`인지
`TimerFallback`인지 기록한다. 런타임은 attack instance + hit index로 두 경로를 dedupe한다.
애니메이션 이벤트는 기존 profile의 contact를 요청할 뿐 damage 규칙을 담지 않는다.

## Hit Resolution

각 contact마다:

1. null·dead·self·owner·invalid 타깃 거부;
2. 중복 `AttackId + HitIndex + target instance` 거부;
3. invulnerability 확인;
4. `Counterable`이면 Counter 해결;
5. Guard / GuardBreak 해결;
6. damage 적용;
7. reaction·knockback 적용;
8. `CombatHitResolved` emit;
9. camera·VFX·SFX·UI가 결과를 소비.

우선순위: `Dead/Invulnerable → Counter → GuardBreak → Guard → Damage`.

### Counter

Counter는 별도 damage 지름길이 아니라 resolver다. `Counterable` 공격만 대상이고, 입력 창은
Telegraph의 읽히는 끝에서 열려 Active 전에 닫힌다. 성공 시 들어오는 damage를 취소하고 공격자를
`CounterStagger`로 보낸다. 같은 프레임에 Counter와 Dodge가 성립하면 Dodge(생존)가 이긴다.

## One-versus-many

encounter는 범용 AI 프레임워크가 아니라 `AttackSlotReservation`을 쓴다. 역할은
`PrimaryAttacker`, `Pressure`, `Support`, `Reserve`.

- 기본은 활성 일반 공격자 1명.
- 2명은 boss/elite profile이 명시적으로 허용할 때만.
- Telegraph 진입에 예약하고 Active 실패·stagger·death·timeout에 반환.
- 후보 정렬: 정면 각도, 거리, 시야, 마지막 공격 이후 시간.
- 한 프레임에 여러 slot이 같은 타깃에 damage를 해결하지 않는다.
- 복잡한 crowd steering 전에 authored spacing과 reservation을 먼저 쓴다.

`EnemyAttack`이 개별 상태기계를 소유하고, encounter coordinator는 attack 권리만 소유한다
(이동·damage·quest·dialogue 아님).

## Environmental Takedown / Weapons / Boss Phases

- Environmental takedown: World가 authored point(위치·표면·접근각·snap·outcome·cooldown)를
  소유하고, Combat이 거리·각도·타깃 능력·예약·결과를 판정한다. 전역 ragdoll이나 모든 오브젝트
  자동 인식은 1.0 범위가 아니다.
- Weapons: 두 번째 전투 프레임워크가 아니라 attack-profile 세트다. 우선순위는 맨손 → 막대 →
  병 → 칼. pickup·durability·persistence는 Systems, 공격/hit 동작은 Combat.
- Boss phase는 거대한 bespoke 상태기계가 아니라 data다: phase 조건(health/구조/시간/arena
  event), 허용 attack profile, slot 수, telegraph/recovery 배수, counterable/guard-break flag,
  transition presentation id. phase 전환은 Recovery/Stagger 경계에서 일어나고 Timeline은
  transition을 연출할 뿐 damage authority를 갖지 않는다.

## Authority Boundaries

- Combat(project C#/data)이 attack timing, hit query, counter window, takedown 규칙, damage
  결과를 소유한다.
- Animation·camera·VFX·audio·UI는 전투 결과를 표현할 뿐 damage 규칙을 소유하지 않는다.
  feedback 실패가 damage 결과를 바꾸지 않는다.
- CharacterController가 이동 authority다. presentation은 이 캡슐을 따른다.
- Systems는 결과 signal(quest·flag·save)을 소비하고, Combat을 직접 참조하지 않는다.

`CombatHitResolved`(attackId/hitIndex, attacker/victim, point/direction, damage/reaction/flags,
countered/guarded/killed, cameraProfileId/feedbackProfileId)가 production feedback 계약이다.
공용 이벤트가 생기기 전의 lane별 feedback은 adapter일 뿐이며 두 번째 damage authority가 되면 안 된다.

## Performance and Reliability

- NonAlloc physics 쿼리와 안정적 버퍼를 쓴다.
- child collider는 target instance + hit index로 dedupe한다.
- attack·AI·feedback 경로에서 프레임당 할당을 피한다.
- one-shot VFX/audio는 풀링한다.
- 재현을 위해 안정적 attack/state id를 로그로 남긴다.
- 검증되는 가장 단순한 도구/패키지/구현을 고른다.

## Chapter 1 Boss Candidate

Arena 미확정. 키가 매우 크고 호리호리한 중년 남성, 과장된 비율, 진지한 톤. 긴 reach로 거리와
타이밍을 지배해 플레이어가 spacing을 의식하게 한다. 핵심 패턴 5개: Long Jab→Cross, Delayed
Straight, Long Front Kick, Wide Hook, Rush Lunge. phase 변화는 새 패턴 대량 추가보다 기존
패턴 연결과 timing variation을 우선한다. 첫 보스는 현재 combat vocabulary를 시험하고 가르친다.

## Acceptance

자동(메인 게이트): compile 0, combat validator 0 error/warning, DevCombat builder ×2 byte-stable,
고유 attack id와 acyclic combo graph, 승인 profile마다 유효한 hit shape와 contact source,
누락된 승인 Animator state 없음.

사람(Play 게이트, 자동으로 판정 어려운 것만): 공격 연결감과 무게, Heavy tap vs charged 대비와
charge-to-Dodge cancel, Telegraph 가독성과 Active contact, Counter/Guard/GuardBreak 우선순위,
다대1 압박과 턴 순서의 공정함, 보스의 identity와 재미.

## Production Rule

새 action은 combat decision / flow / enemy readability / progression / spectacle 중 하나를
명확히 개선해야 한다. 단순히 animation 수를 늘리기 위한 action은 추가하지 않는다. 실제 병목이
확인되면 기존 구조를 개선·교체할 수 있다.

## References

- Sifu 개발자 전투 개요: https://blog.playstation.com/?p=356872
- Sleeping Dogs 개발자 인터뷰: https://techau.com.au/interview-sleeping-dogs-developer-mike-skupa-and-jeff-oconnell/
- Unity imported Animation Events: https://docs.unity3d.com/6000.0/Manual/AnimationEventsOnImportedClips.html
