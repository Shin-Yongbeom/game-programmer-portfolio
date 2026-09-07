# Shared Combat Contracts

## Compatibility rule

이 파일은 시스템 간 public 계약만 정의한다. 전투 동작·상태·판정 규칙과 플레이 경험은
`COMBAT.md`가 소유한다.

Existing consumers of `DamageInfo`, `IDamageable`, and `IHitReactionReceiver` remain valid. New combat features extend these contracts instead of introducing a parallel combat framework.

## Damage metadata

`DamageInfo` now carries optional production metadata:

- `AttackId`: stable content identifier from `AttackDefinition`;
- `HitIndex`: index for multi-hit attacks;
- `Flags`: `Counterable`, `EnvironmentalCandidate`, or `Uninterruptible`.

The original constructor remains available for existing enemies, tests, and tools.

## Capability interfaces

- `ICounterable` is implemented only by actors that can receive a counter. It does not decide input timing or animation.
- `IEnvironmentalTakedownTarget` is implemented only by actors that can receive a contextual takedown. The environment owns valid takedown points; combat owns the request and outcome.

Animation, camera, VFX, audio, and UI consume the resulting event/state. They do not own damage rules.

## System ownership

- Combat owns attack timing, counter windows, hit queries, and takedown rules.
- Character presentation owns animation and pose acceptance.
- World owns authored takedown points, surfaces, and spatial constraints.
- Quest/progression consumes outcome signals rather than direct Combat-to-Quest references.
- UI owns feedback presentation only.

Exact state, priority, slot, and contact/fallback rules belong to `COMBAT.md`; do not duplicate
them here. Current implementation order is owned by `WORKLOG.md` and `Docs/ASTRA_START_HERE.md`.
