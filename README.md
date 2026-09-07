# 취업 포트폴리오 — 게임 프로그래머 (신입/인턴)

게임 프로그래머 직군 지원용 포트폴리오입니다.

<!-- 필요하면 이름·연락처를 여기에 추가하세요 -->

---

두 개의 1인 프로젝트로, 게임플레이 시스템을 직접 설계하고 구현하는 능력과, 만든 것을 배포해
끝까지 운영해 본 생산·학습 능력을 함께 정리했습니다.

## 1. Greyline — Unity 6 / HDRP 근접 액션 게임 (개발 중)

→ [`greyline/`](greyline/README.md)

가상의 대도시를 배경으로 한 3인칭 근접 액션 게임입니다. 1인 개발이며, 시스템 아키텍처와 설계는
직접 결정하고 구현은 AI 도구를 페어로 활용해 진행 중입니다. 이 저장소에는 게임플레이 C#(약
15,800 LOC)와 시스템 설계 문서만 큐레이션해 담았습니다. 아트, 오디오, 모션, 씬, 스토리 문서는
제외했습니다.

- 전투 아키텍처: gameplay 상태 우선 히트 판정, 애니메이션 contact + 타이머 fallback 이중 경로
- 1대다 인카운터: 슬롯 예약으로 동시 공격자 제어
- 데이터 주도 적 프로필과 보스 페이즈, 세션·진행·버전 세이브
- Editor 툴 + Unity CLI + 로그 기반 Play QA로 검증 자동화

스택: Unity 6.3 LTS, C#, HDRP, Input System, ScriptableObject, Unity Test Framework, PowerShell CLI

## 2. 캠퍼스 마법지도 (More Adore Campus Map) — 배포·운영 중

→ [`campus-magic-map/`](campus-magic-map/README.md)

대학 캠퍼스 지도 기반 모바일 서비스입니다. 앱과 웹, 백엔드, 인프라를 혼자 만들고 6개월 이상
직접 운영했으며, 유료 마케팅 없이 이용자 1,000명을 넘겼습니다. 운영 중인 서비스라 소스는
공개하지 않고 구조와 역할만 문서로 정리했습니다.

- 클라이언트: React Native + MapLibre 벡터타일, 오프라인 자산, 소셜 로그인, 실시간 교통
- 백엔드: Express / TypeScript + PostgreSQL + Redis, OCR, 오프라인 자산 파이프라인
- 인프라: VPC 서브넷 분리 + Bastion, 배포 절차·보안 문서, 복구 리허설

스택: React Native, TypeScript, MapLibre, TanStack Query, Express, PostgreSQL, Redis, Socket.IO, FCM

---

*본 저장소는 채용 포트폴리오 열람 목적으로 공개됩니다. 코드의 상업적 재사용을 허가하지 않습니다.*
