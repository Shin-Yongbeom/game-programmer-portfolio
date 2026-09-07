# Production content authoring

The live district now connects encounters → one-time training rewards → permanent skills →
checkpoint/rest → profile reload. This is a production foundation increment, not completion of the
entire first milestone. Story/main quests remain closed and human review is deferred in batches.

## Content sources

| Authoring source | Owns | Rebuild behavior |
|---|---|---|
| `Assets/_Project/Data/ProductionProgression.asset` | Stable skill IDs, text, costs, prerequisites and effects; encounter IDs, rewards, world positions, rosters, radii and turn gap | Existing authored values are preserved |
| `Assets/_Project/Data/Enemy_brawler.asset`, `Enemy_bruiser.asset`, `Enemy_warden.asset` | Archetype, health, damage, melee timing, detection, movement and visual scale | Profiles are reused; values become generated enemy components |
| `ProductionSystemsBuilder.Checkpoint` calls | Field station IDs, markers and safe return poses | Scene is regenerated from these calls |
| `ProductionAnimationAuthoring.cs` | Source motion landmarks and hold poses | Regenerates presentation assets; existing animation workflow |

To add an encounter, add a unique ID, display name, position, reward, engagement/leash values and
spawn entries to the catalog. Each spawn references an enemy profile and supplies its offset/yaw.
Profiles can be duplicated for tuning variants of the three supported AI archetypes. New AI patterns
still require C# work. Add skills to the same catalog using supported health, Heavy damage or guard
capacity effects; the live UI builds a scrollable list and resolves prerequisite IDs automatically.
New combat techniques still need their action/animation runtime integration.

IDs are save identity. Preserve released IDs when changing names or positions. A new encounter ID
is a new reward/clear record. Changing `catalogId` deliberately makes the old profile incompatible;
do not change it as routine content editing. The schema stores unspent points, not a recalculated
balance, so later price/reward edits do not retroactively spend or refund existing purchases.

Run `Tools/Greyline-Unity.ps1 -Operation Build`, then `-Operation Play` (single Editor and
preflight are built into the wrapper). The validator rejects duplicate IDs, missing/cyclic
prerequisites, missing profiles and invalid combat values. The full Play suite currently contains
baseline balance/roster expectations for the shipped University/Market/Warden fixture. Intentional
baseline design changes require updating those expectations; don't weaken them to hide regressions.

## Live session contract

- Escape/Start pauses simulation, exposes a focused uGUI menu and blocks player, camera and interaction
  commands. Resume restores the previous timeScale/cursor and consumes the closing input frame.
- Training starts with one point; complete University/Market/Warden for 2/2/3 points, once per profile.
  Purchases enforce cost and prerequisites and persist before applying effects. Training/rest is
  unavailable while airborne, committed to an action, guard-broken or within an active combat pocket.
- E uses the existing interaction input/router at Boulevard or North Lane field stations. Rest heals
  trained health and stores a return checkpoint. Backspace/menu retry returns there at full trained
  health; cleared encounter groups stay cleared. It does not reset the profile for farming.
- The production profile is `production-profile.json` under `Application.persistentDataPath`; `.bak`
  holds a previous valid commit. The legacy `systems-sandbox.json` flag save remains separate.
- Schema 1 records catalog ID, checkpoint ID, points, purchased skill IDs and cleared encounter IDs.
  Health-in-combat, physics/animation state and arbitrary positions are deliberately reconstructed at
  the checkpoint. Atomic replacement, revision/checksum validation and backup recovery protect this
  boundary. Future versions and unrecoverable corrupt files are not overwritten.
- A failed autosave is marked UNSAVED. Scene retry carries that session in memory and later rest can
  retry the write. Closing the process before a successful save still loses those unsaved changes;
  the previous committed file remains intact.

## Verification and remaining boundaries

QA creates a fresh profile directory under `Artifacts/ProductionQA/Profiles` for every run, retains
it across scene reloads, then clears the override when QA exits. It never loads or modifies the human
profile. Separate fixtures exercise corruption, backup repair, duplicate IDs, future schemas and
locked-file writes; the live scenario also forces a real autosave failure and recovery.

The connected baseline has 4 stat-training skills, 3 enemy profiles, 3 persistent encounters and
2 checkpoints. Combat action authoring/weapon styles, technique unlocks, broader feedback/audio,
settings/accessibility, replay/new-profile UX and extended interaction types remain production
work. The older UI/dialogue/minigame sandboxes are not implicitly integrated by this change.
Latest run evidence is in `ASTRA_PRODUCTION_VERIFICATION.md`; visual/feel acceptance is separate.
