# District production verification — 2026-09-07

Rolling latest-verification snapshot. 오래된 실행 이력은 누적하지 않고 최신 검증 상태로
교체한다. 과거 기록은 Git history와 `Logs/`가 보존한다.

Unity 6000.3.23f1 / HDRP 17.3.0, branch `astra/production-push`.
Both runs used the checked-in wrapper, passed resource preflight with zero existing Unity processes,
ran in user context and exited normally with code 0. No external asset, package, purchase or
credential use was needed for this session/progression/content-tooling increment.

| Check | Result | Local evidence |
|---|---|---|
| Generator + validators | PASS; `ASTRA_PRODUCTION_BUILD_OK` | `Logs/astra-Build-20260907-153030.log` |
| Humanoid/controller/combat validation | 0 errors, 0 warnings; 48 configured entries | Same build log |
| Presentation validation | Eleven motion profiles, valid landmarks/holds, native playback rates, character-definition references | Same build log; `PRODUCTION_PRESENTATION_VALIDATION_OK` |
| Environmental combat content | 19 counter/bench/crate/monument surfaces | Same build log; `DISTRICT_FINISHER_SURFACES` |
| Connected standing-capsule route | 828 samples, six places | Same build log |
| Enemy/defense regression | FIFO turn/release, counter/posture/death, one contact, allies, walls, leash, boss phase; dead attacker cannot acquire a turn or apply a late contact | Same build log |
| Systems/content validation | 4 skills, 3 data-authored encounters/profiles, 2 checkpoints; IDs/prerequisites/profile values valid | Same build log; `PRODUCTION_SYSTEMS_VALIDATION_OK` |
| Full Play Mode scenario + final script compilation | PASS; `PRODUCTION_DISTRICT_PLAY_QA_OK`, 18/18 waypoints, 520.49m, no captured runtime errors | `Logs/astra-Play-20260907-153408.log` |
| Production save fixtures | Roundtrip, backup rotation/recovery/repair, locked-file failure, duplicate IDs, future schema, unrecoverable-file protection | Same Play log; `PRODUCTION_SAVE_VALIDATION_OK` |
| Unity wrapper fixtures (unchanged, prior increment) | PASS 16/16, without launching Unity | `Tools/Test-Greyline-UnityWrapper.ps1` |

The Play report is `Artifacts/ProductionQA/district-play-qa.json` (overwritten by the next run).
Logs/artifacts are local outputs, not required source assets. Reproduce with
`Tools/Greyline-Unity.ps1 -Operation Play`; regenerate with `-Operation Build`.

## Runtime evidence

Play uses real scene colliders and CharacterController; the route drives that controller directly
with AI temporarily paused, then enables runtime motor/AI for combat. Public action entry points
and injected keyboard input exercise the following:

- Tapped HeavyPunchCombo applies base damage, full ChargedUppercut applies 1.6x exactly once.
  Actual Animator time at damage must be within 0.12 seconds of the authored source contact.
- Low-ceiling crouch blocks standing combat. Live C + W lowers the rendered head and camera;
  foot IK preserves a planted foot within 22cm of the ground. Releasing C restores standing.
- All four dodge states accompany 2.5–3.4m displacement, keep hips within 75cm horizontally of
  the controller during the dodge, preserve invulnerability and recover.
- Timed R counters actual enemy contact; finisher selection/animation resolves lethal contact.
  The corpse holds one death pose, disables standing collision, settles within 18cm of ground
  and is excluded from finisher targeting.
- Real F starts a 2.4m approach into a facing pair and a clinch knee. Native animation reaches its
  authored contact within 0.12s of damage, and a live knee is within 65cm of the victim's hips.
  Hand IK supports the clinch. The environmental variant pushes the victim to a solid prop before
  damage; its impact counter increments exactly once and its forward-fall corpse reaches the ground.
- Low obstacles reject approach even with a clear chest-height ray. A newly inserted obstacle aborts
  safely. Disabling mid-clinch separates the living pair and restores collision, AI and player control.
  Side props cannot grant an environmental bonus; airborne and nonlethal high-health starts are rejected.
- Three enemies share one turn, at least two connect, escape ends pursuit; phase-two Warden
  presents distinct jab, heavy and sweep states at actual contact.
- Camera wall retraction and player death cancellation remain valid. Real Backspace input reloads
  the scene after defeat with full health, working guard input, one HUD, six enemies and unchanged timeScale.
- Real Escape opens the focused production menu and freezes player/camera/simulation. Combat commands
  are rejected; uGUI Submit resumes and consumes its input frame. Catalog-bound training buttons
  enforce one purchase, point balance and prerequisites. Trained health/posture reach 190/84 and
  both Heavy upgrades produce a real tapped Heavy contact of 62.1 damage (46 × 1.35).
- Defeating each encounter awards once and autosaves. Real E uses the interaction router to rest/heal
  at North Lane. Scene reload restores all four skills, points, checkpoint and cleared groups with one
  session and one EventSystem. Unfinished encounters respawn; cleared encounters remain cleared.
- A real locked profile forces autosave failure. Unsaved rewards survive scene retry in memory,
  are visibly marked UNSAVED, and commit when a later rest retries after the lock is released.

Each automated run owns a fresh profile under `Artifacts/ProductionQA/Profiles`; the override survives
scene reloads and is removed when QA exits. Human `Application.persistentDataPath` saves are untouched.
The initial station marker obstructed the right-dodge lane; relocating it diagonally restored the
full dodge regression. UI tests explicitly focus Resume before Submit after exercising navigation.

Content sources and boundaries: `Docs/PRODUCTION_CONTENT_AUTHORING.md`. The first production
foundation milestone is still in progress; the connected save/menu/training stack does not imply
weapon/action authoring, settings/accessibility or all feedback work is complete.

## Presentation implementation and limits

`ProductionAnimationAuthoring.cs` generates `ProductionPresentation.asset`, four hold-pose clips
and controller references. Its source-frame landmarks are the canonical recipe: change the recipe
before regenerating. Runtime playback changes Animator speed to reach those landmarks under C#
phase clocks; no production state uses time-parameter pose scrubbing. Character definitions carry
the same set into existing sandbox scenes. Gameplay damage, displacement and invulnerability rules
remain authoritative. Selected Mixamo source provenance and derivative-pose terms are recorded in
`Assets/Art/Source/Mixamo/SOURCE.md`.

Live bone checks exposed animation culling and baked XZ travel that state-name checks had missed.
Runtime characters now evaluate bones offscreen, and relevant motion imports discard extracted root
travel. Crouch foot IK reconciles the acquired high-hip stride with its idle pose. Death has one owner
and holds its final pose rather than restarting. The wrapper caches its process handle and requires
both exit 0 and the success marker; explicit error output still fails, including errors after a marker.

Finisher QA caught a knee/body gap before acceptance. Authoring now samples the knee source for
its forward, body-height apex (frame 22 / 0.733s); the clinch uses a 65cm root separation and temporary
collision suppression only between its two participants. World collision remains active throughout.
Approach rotation is eased, victim AI is explicitly held, and cancellation restores the pair. This
composes single-actor sources with code/IK; it is not acceptance of polished paired choreography.

Source-pose probes diagnose clip geometry; they are not live-controller proof. The Play checks above
evaluate live Animator/bone state but do not judge visual polish. Batch simulation uses a fixed 1/60
second step; wall-clock timings are not gameplay duration or GPU benchmarks. This does not prove
all combinations, gamepad acceptance, landing feel, shipping performance or three hours of content.
Screenshots were disabled. Human feel/art acceptance: `Docs/ASTRA_HUMAN_PLAY_REVIEW.md`.
