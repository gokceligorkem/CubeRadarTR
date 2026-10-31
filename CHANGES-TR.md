# Modified Cube Radar TR — 2026-10-29

## Unreleased — automatic hunt foundation (2026-10-31)
- Preserve full autonomous hunting; expose live input as BLOCKED and LIVE_VERIFIED=false
  until accepted automatic selection is proved. Do not add manual/assisted hunt.
- Split SelectingTarget and DeathVerification into explicit transitions/functions;
  retain a bounded state trace and test two complete autonomous cycles including patrol
  movement. Reject physical/foreign key/button takeover as automatic selection proof.
- Add an isolated, one-pulse M diagnostic for the observed Map binding. Both normal
  M encodings and TAB under the official AION 1 control mode failed in the live client;
  they cannot unlock hunting. No unsupported input path was added.
- Diagnose TAB through bounded scan-code/virtual-key probes, exact SendInput counts,
  foreground HWND/PID, token integrity/elevation and marked low-level hook observations.
  Require automatic TAB + matching new selection, then stationary observation proof,
  before live hunting can start. Physical TAB cannot satisfy the automatic proof.
- Separate last pose sample, last position change and last observation. Only an
  explicit matched stop can renew observation from validated same-stream world clocks;
  keep the 2.5 s observation limit and independent stuck timeout. Unknown movement,
  transport reset or malformed observations invalidate the retained pose.
- Fix route recording from the app after movement becomes stale: wait up to 30 s
  for the first fresh fix, record no stale anchor, preserve real points during a
  healthy stationary pause, and still reject capture loss/world changes/teleports.
- Use unfinished, verified Pet Avı goals as the hunt source. Match persistent
  C2S 33 38 selection to an exact registered NPC lifetime; correlate explicit
  reason-3 removal with that lifetime. Never count disappearance as death.
- Add read-only profile movement calibration and a bounded focus/keys-release
  start wait. A/D strafing does not satisfy the turn-control model. Live readiness
  requires actual session observations; preview/settings cannot enable input.
- Add an explicit strafing-key navigation mode with a backward binding. It moves
  relative to measured forward-control direction with one cardinal key and requires all four directions
  to be measured; existing turning-key profiles retain their behavior.
- Keep avatar facing separate from the calibrated control basis. Stop and release
  owned input when observed travel deviates from the selected key direction.
- Add left click, Mouse 4 (XBUTTON1) and Mouse 5 (XBUTTON2) to the basic-attack
  and skill selectors, including editable Ranger profiles. Preserve keyboard
  bindings and the settings schema. Mouse down/up ownership and physical-user
  takeover use the same tested policy; a separate mouse hook tracks user input.
  Mouse support alone cannot satisfy the target-verification gate.
- Add class-independent editable profiles, bounded per-map route recording and a
  Turkish automatic-hunt page. Settings use a separate versioned `hunt.json`;
  unreadable/future files are preserved. Existing radar/pet/boss settings are untouched.
- Add an explicit Core state machine with separate candidate/verified target types.
  Selection input and proximity never prove selection. Exact target-scoped death
  evidence is required; disappearance and generic despawn never count as death.
- Keep Core hunting free of Windows/WinForms dependencies. Generated-key ownership,
  physical-user takeover, retryable failed releases, channel waits and stop/fault
  cleanup are tested with a recording keyboard. Add a separate Windows adapter.
- Live hunting requires selected-target identity/type, correlated spawn/death
  observations and game-control calibration in the current world.
  Neither configuration nor preview can override that gate; native input is not
  initialized while the gate is closed. No live hunt or class rotation is claimed verified.
- Add local numeric/status-only `hunt-status.json` diagnostics. Demo/self-test does
  not write user settings/diagnostics or create a native input binding.
- Validation: 491 .NET tests, 22 motion-helper tests, native synthetic UI
  checks at 680/1080 px and 125/150% scaling. See `docs/IMPLEMENTATION-AUTO-HUNT-TR.md`.
- Live A probe failed: both SendInput forms returned 1/event and were correctly
  marked/observed at equal integrity with game foreground, but no new selection
  event followed. Physical TAB produced a matched target event. Stop at A; no attack.
  See `docs/INPUT-MOTION-VALIDATION-TR.md` for the exact acceptance table.

## 0.8.11 (2026-10-31)
- The end of the teleport measurement captured two real holes (11 and 247 bytes)
  about 2 s apart, with no Npcap buffer drops and no reordered retransmissions.
  The receive statistic reset while the adapter stayed open. This matches kernel
  filter replacement on the 2 s connection refresh: Npcap BIOCSETF purges its queue.
- Prefer exact owned world sockets on port 13328 in strict capture. Auxiliary
  connections opening/closing no longer change a continuing world filter or flush
  queued world records. All owned world sockets remain included; no broader
  traffic filter is introduced. Proxy connections without that port retain the
  previous exact-owned-socket fallback.
- Includes the full-collection recovery and local status diagnostics from 0.8.10.
  Real missing data still invalidates balances until an exact full collection arrives.
- Regression proof fails before the filter fix and covers auxiliary churn, multiple
  world sockets and the proxy fallback; all 323 .NET tests pass.
- Npcap filter semantics: https://github.com/nmap/npcap/blob/master/packetWin7/npf/npf/ioctls.h

## 0.8.10 (2026-10-31)
- Fix the partial-count warning remaining after a complete pet collection has
  restored exact soul balances. After loss, that collection starts a fresh farming
  session instead of treating incomplete historical kills/rewards as complete.
  Refreshes without loss preserve the existing complete session counts.
- Make the mini status explicitly request soul sync when totals are unverified;
  live synchronized state takes priority over a historical partial-count note.
- Add a bounded local pet-status.json snapshot every two seconds under LocalAppData:
  run/version/time, reset reason, verified-balance count, Npcap drops, TCP gaps,
  filter-update count and decoder errors. No names, locations, payloads, keys or
  network addresses are written. Preview tests never write the live snapshot.
- Regression fails before the recovery fix; all 320 .NET tests pass. Repeated live
  teleports preserved all 305 balance records in the observation reader. The cause
  of the original intermittent loss was not reproduced; true loss still requires
  a full collection and never silently reuses guessed balances.

## 0.8.9 (2026-10-30)
- Keep Pet Hunt's tracked view and mini list limited to current nearby pet
  sources within 150 metres in 3D. Moving away or receiving entity removal drops
  the rows; returning shows the same goals and soul/session counts again.
- Filter cached sightings using the latest player position on every update.
  Without positions, a server sighting expires from the nearby UI after 10 s.
  Saved goals still receive delayed soul rewards; explicit opt-outs stay saved.
- Rename the tracked view to Yakındaki takiplerim and count only nearby tracking
  in the summary. The all-sources and missing-souls collection views remain available.
- Native regression fails before the fix and passes afterward for horizontal and
  vertical departure, return, removal and stale positionless sightings.
- Includes the 0.8.8 login collection fix, validated in a live relog: sync remained
  verified for over one minute after the actor identity changed.

## 0.8.8 (2026-10-30)
- Fix a valid login pet collection being erased when the new world actor identity
  arrives afterward. The collection flow owns soul balances; a transient actor
  change only replaces combat identity and discards its pending hit associations.
- Preserve verified balances and farming session counters across actor recreation.
  A new collection still replaces balances, and real TCP gaps still invalidate them.
- Local measurement received two valid 0x0090 collections while the old reader
  cleared every balance. Regression proof reproduces the collection-before-actor
  ordering and covers either login order plus stale-hit isolation.

## 0.8.7 (2026-10-30)
- Preserve capture/decoder/pet state when the owned connection list changes on
  the same adapter. Previously strict-filter refresh closed/reopened Npcap and
  reset every pet to Waiting for sync even if the world socket continued.
  Apply the new exact BPF in place; real TCP loss remains subject to gap checks.
- Scope pet invalidation and actor/map updates to the bound collection flow.
  Other connections can no longer clear the collection through their identity,
  map, TCP gap or failed compressed record. True loss on the world flow still
  invalidates totals; a full collection safely restores them.
- Add local pet-sync diagnostics: capture-reset count/reason, sync state and
  verified balance counts. No character names, identifiers or packet data added.
- Regression tests reproduce the foreign-flow reset and cover own death,
  teleport/map changes, reward completion after revival, and scoped loss/recovery.
  Live death/revival has not been remeasured; an already discarded old collection
  still needs one full in-game sync. This release does not restore guessed totals.
- All 315 .NET tests and the native WinForms interaction/preview gate pass.

## 0.8.6 (2026-10-30)
- Fix Boss events > Actions > Presets opening behind the topmost main window.
  Modal forms, messages and the diagnostic save dialog now use the actual parent
  Form as their native owner. Other topmost app windows yield while a modal is
  open; their previous topmost state is restored when it closes or fails.
- Match the preset dialog to the app theme, add Cancel/Escape, and retain timezone
  selection and existing import semantics. Independent boss editors remain
  modeless and now follow changes to the main Topmost toggle while open.
- Native regression proof fails before the fix; it checks the preset dialog's
  HWND owner, native modal disabling, both topmost states, usable timezone picker,
  Cancel without event changes, overlay restoration, and common-dialog ownership.
  Existing pet/mini/editor proofs continue to run. The 0.8.5 motion helper is kept.

## 0.8.5 (2026-10-30)
- Fix a reproducible motion-reader loss of cipher alignment when TCP fragments
  arrive out of order. Wait up to 1.5 s for missing fragments in a bounded
  256 KiB / 256-segment queue, trim retransmissions, and preserve the capture time.
  Persistent gaps still invalidate alignment and require fresh clock validation.
- Add the actual motion-helper version, health/clock/motion age, last movement
  opcode, TCP counters and at most 24 opcode/length counters to local diagnostics.
  No raw payloads, addresses or cryptographic material are included. Old helpers
  remain compatible and are identified as version unknown.
- 14 Python transport tests include the failing-before-fix encrypted ground-motion
  reproduction, wraparound/overlap, true-loss timeout and queue pressure. The
  friend's reported dash/jump-only symptom has not yet been validated on that PC;
  the new diagnostics distinguish alignment loss from unsupported movement forms.
- All 312 .NET tests and the native WinForms preview/interaction gate also pass.

## 0.8.4 (2026-10-30)
- Automatically track nearby pets with a verified remaining soul balance toward
  MAX. Wait for collection sync; MAX, unknown and unobserved distant pets are not
  automatically added. Added goals remain after the last nearby mob dies so the
  later soul reward/completion is retained.
- Add persistent per-pet opt-out. Uncheck Track in the main list or right-click
  a mini row > Untrack. Repeated game updates and app restarts respect the saved
  exclusion. Explicitly rechecking removes the exclusion and resumes tracking.
- Add Missing souls (MAX) view. Untracking leaves the collection/balance visible
  there. The target picker is disabled while untracked, avoiding accidental re-add.
- All 312 .NET tests and native proofs pass. Native proof exercises automatic
  add, the actual mini menu callback, serialized exclusion, no re-add on refresh,
  retained unchecked main row, explicit reselection and distant balance viewing.

## 0.8.3 (2026-10-30)
- Tracked pets advance their target after a confirmed rank-up: rank 2 with zero
  current souls now shows 75 remaining to rank 3, including old saved rank-2 goals.
  Pending/unverified upgrades and merely reaching a soul threshold do not advance.
- Apply Hide completed to the mini list as well. Only verified MAX pets are hidden;
  a rank-1 pet ready to upgrade remains visible. Selections remain saved, so turning
  the filter off can show MAX pets again. Automatic height follows visible rows.
- 306 .NET tests pass. Native controls verify Ready remains visible, rank-2 promotion
  displays 75 in both lists, and confirmed MAX disappears from both. The compact
  borderless list, opacity and previous HUD proofs remain in place.

## 0.8.2 (2026-10-30)
- Replace the large pet mini cards with a borderless flat list. Each pet uses
  a 46 px two-line row; one tracked pet fits roughly 320 x 100 px at 100% DPI.
  Keep the name, remaining souls, balance, observed kills/gains and direction.
- Add the top-right options menu and right-click menu: translucent toggle,
  40–100% visibility slider, Topmost, fit height, edit tracking and close.
  Opacity applies to the whole window and is saved with the other mini settings.
- Drag the header or rows to move; resize from the edges/corners. Height fits
  the selected pets by default; manual vertical resizing disables auto-fit.
- Migrate old mini dimensions once while retaining pet goals, position and
  Topmost. Existing pet/boss/cube data and capture/audio behavior are unchanged.
- All 300 .NET tests and native proofs pass. Verify native layered alpha at 55%,
  restore full opacity, save appearance, test corner hit codes, compact height
  for one/three pets and safe migration. Existing minimize/cleanup proof remains.

## 0.8.1 (2026-10-30)
- Add Pet Hunt > Mini pet: independent draggable/resizable Topmost window.
  It remains visible and keeps updating when main is minimized. Position, size
  and Topmost preference persist; main shutdown disposes the mini, while closing
  the mini leaves tracking running. Each selected pet has its own scrolling card.
- Show observed session kill participation separately from pet souls gained and
  souls remaining to the chosen goal. A trusted own attack/hit followed by an
  NPC death within 30 seconds counts once. Corpse/despawn and others' attacks
  never count; kills do not add souls or promise server reward credit.
- Session total works even when the NPC's initial appearance preceded capture.
  Unmapped deaths are not guessed into a pet: its card shows unknown/lower-bound
  participation. Same-stream collection refresh retains session kills/souls;
  capture gaps mark the session incomplete.
- Support respawn appearances in 42 36 and verified 04 38 layouts 4/6 including
  per-hit UID and chained records; 03 38 hit notices provide attack participation.
  See licenses/PetTracker/COMBAT-SOURCE.txt for the primary GPL protocol source.
- 300 .NET tests pass. Native proof minimizes main, checks independent visibility
  and Topmost, observes kill/remaining labels update, then checks shutdown cleanup.
  Live diagnostic observed own hit, NPC death and a soul gain; it revealed missing
  initial NPC identity. The final source-independent numeric total is covered by
  regression tests; it was not compared with a new live kill after that correction.

## 0.8.0 (2026-10-30)
- Add Pet Hunt beside Radar and Boss & Events: nearby source creatures, pet
  level/current souls, selected target rank, souls remaining, search, tracked
  list and completed-goal filter. Pet/NPC names match the supplied game catalog.
- Read login collection, soul gains/spends and rank upgrades through the same
  passive capture. Unknown/gapped data stays unverified, complete snapshots
  resync it, retransmitted frames do not add points twice. NPC deaths do not
  count as souls. Rank/spend events can arrive in either order.
- NPC appearance and existing position records provide nearby source matching.
  Map changes, removals and entity reuse clear old contacts. Distances/directions
  require fresh NPC/player positions; recent sightings expire after 90 seconds.
- Per-pet goals and filters save to a separate pets.json. Collection balances
  remain session-local. Real rewards reaching a selected goal notify once;
  muted completion remains visible and does not replay when unmuted.
- EU/BABA live check decoded the collection and observed 5 soul points for
  Superior Water Spirit (rank 2, 11/75, 64 remaining to MAX). Live distance and
  rank-up were not visually verified. Redacted observation in fixtures/pets.
- 291 .NET tests pass. Native proof covers pet navigation, stable rows/selection,
  goal/checkbox, search and 125/150% rendering, alongside all earlier HUD proofs.
- Catalog/mapping and body-layout attribution: 0Fox2Give/PetTracker, GPL-3.0,
  pinned revision and original notices in licenses/PetTracker. No artwork copied.

## 0.7.3 (2026-10-30)
- Repair BABA Watcher Kaira by shipped-template identity and server profile,
  including random IDs from a clean installation. The old migration matched
  only the original user's seeded ID, leaving a friend's zero-time packet
  without a usable clock. Custom schedules and existing alert choices remain.
- Add Refresh to Boss & Events. It starts the existing passive receiver if
  stopped, applies queued game records and recalculates clocks without clearing
  alert history. New game times still require opening the in-game Field Boss list.
- On receipt of a supported game list, add missing Altgard/Abyss slots and replace
  the obsolete unconfirmed Nahma template with its first real spawn-point entry.
  Added rows have alerts off; existing per-boss choices are retained.
- All 282 .NET tests pass. Native interaction proof covers a clean-install
  random-ID Kaira, zero packet time, the real Refresh button, all 8 Abyss entries,
  retained alert history, selection and checkbox state.

## 0.7.2 (2026-10-30)
- Fixed-cadence events such as BABA Watcher Kaira now advance to the next
  occurrence after the one-minute start-alert window instead of remaining on
  the elapsed time for 30 minutes. A newer game timestamp still takes priority.
- Clearer field-boss states: Spawned, Last record: spawned, and Waiting for a new
  game record. Overdue rows show elapsed time; no unobserved death or respawn is
  fabricated. Live BABA packet at 15:23 TR confirmed Shujakan was still spawned.
- Boss editor is an independent non-modal window, inherits the main window's
  Topmost state and stays visible when main is minimized. Save retains any newer
  live boss record received while the editor was open. Main close disposes it.
- 279 .NET tests pass, including fixed-cadence rearming and the new live Shujakan
  fixture. Native proof covers editor visibility, non-modal operation, minimize
  independence and saving without discarding incoming game data.

## 0.7.1 (2026-10-30)
- Fix mini radar disappearing when the main window is minimized: show it as an
  independent top-level window. Its own Topmost setting stays effective while
  the main UI is minimized; the existing shared updates and alerts continue.
- Main application shutdown still explicitly closes the mini window. No capture,
  motion, boss-clock or sound changes. User settings and boss selections retained.
- Native regression proof fails on 0.7.0, passes after removing window ownership:
  minimize main, verify mini is native-visible/normal/topmost, continue radar
  updates, restore main, and confirm main shutdown disposes the independent mini.

## 0.7.0 (2026-10-30)
- New restrained dark HUD: custom navigation, rounded surfaces, clear target
  distance/direction/height, single start/stop action and keyboard-accessible
  switches. Responsive cube list keeps object rows and target identity.
- Larger clean radar with centred player and unobstructed compass/range labels.
  Status lives outside the plot. Sonar refresh is capped at 12.5 fps and paused
  when hidden/minimized or no live/demo traffic is available.
- Compact resizable/draggable mini radar with one target blip, large distance,
  concise status and a separate boss clock. Optional click-through can always
  be undone from the main window; mini close leaves main tracking running.
- Three upcoming event cards, separate countdown/status columns, subtle row
  accents and grouped event actions. Existing boss selections/timers preserved.
- Grouped settings with shared sound volume, visual range and render frequency,
  optional capture start when app opens, native PerMonitorV2 support. Technical
  diagnostics and export stay in Advanced settings; Turkish remains the UI language.
- All 276 .NET and 9 motion-reader tests pass. Native interaction proof covers
  stable grids, target lock, height filters, navigation, volume/mute, mini compact
  and native click-through enable/undo. 125/150% control/text scaling simulations
  and empty/unknown/compact/boss/settings/mini/editor views were rendered.

## 0.6.0 (2026-10-30)
- Read complete field-boss lists directly from the existing passive capture. BABA
  Altgard (24 slots) and Abyss (8 slots) were checked against the in-game list;
  existing named records and per-boss alert choices are retained.
- A new game timestamp replaces the old one-off clock and rearms that occurrence's
  alerts. Living bosses suppress the old timer. Death records of known nearby
  bosses clear the old clock; a measured interval may estimate the next spawn.
  No interval is guessed. Open the in-game list to receive authoritative updates;
  remote kills with the list closed cannot be guaranteed.
- Watcher Kaira uses the 3-hour Global schedule, phased from the BABA in-game
  clock (15:00 TR). Its packet's zero time is not treated as a timestamp.
- Separate original war-horn/drum sound for bosses, with a Boss sound test button;
  cube sounds do not cut off the 2.6-second boss motif.

## 0.5.1 (2026-10-30)
- Update boss rows in place instead of clearing/recreating them every second;
  preserve scrolling, current cell, sorting, column widths and order. Columns can
  be dragged/resized; horizontal and vertical scrollbars remain usable.
- Editable per-boss Alert checkbox persists immediately. Unchecked rows keep their
  countdown but do not glow or alert. Bulk enable/disable controls are included.
- Initial rendering no longer consumes the first due alert before sound is ready.
- 263 .NET tests pass. Native grid proof covers 40 rows over multiple ticks,
  retained scroll/selection/column state, and checkbox updates in both directions.

## 0.5.0 (2026-10-30)
- Persistent object-identity target lock, consistent height filtering, sample age,
  shared-state always-on-top mini window and explicit motion reconnection action.
- Boss/event tab with local CRUD, one-off and weekly schedules, separate schedule
  and display timezones, DST handling, map/location/source links, imminent-row glow.
- Deduplicated 10/5-minute and start sounds respect global/boss mute; keys persist
  atomically. Expired one-off events and late startup do not produce catch-up noise.
- EU/BABA/Asmodian profile; community clock presets require in-game confirmation.
  No boss spawn/death packet detector or guessed respawn interval is added.
- 262 .NET tests pass; recorded protocol, lock, compact, mini, event, settings,
  editor and missing-heading views rendered. Existing flight helper/audio retained.

## 0.4.1 (2026-10-30)
- Fix heading loss during active free flight: decode 0A37 transitions (40/41
  bytes) and 0B37 updates (39 bytes), and accept them in the GUI motion bridge.
- Flight XYZ/velocity/heading offsets follow the upstream documented layouts;
  keep velocity-derived travel direction separate from the heading field.
- Local user-labelled flight/walk capture confirmed both omitted opcode families,
  successful ground decoding and zero TCP gaps. Three-layout regression now passes,
  including malformed records and stale timestamps. Installed flight UI awaits validation.
- Stationary hover can emit no updates; the 2.5-second freshness limit is retained.

## 0.4.0 (2026-10-30)
- Clear nearest-cube card with distance, relative direction and height in Turkish.
- Matching distance-ranked numbers on the radar and table; nearest target uses
  a gold badge and guide. Labels avoid the player, markers, rings and direction text.
- Fixed front/back/left/right labels, forward cone, compact live status and
  collapsed advanced settings/diagnostics reduce main-screen clutter.
- Explicit heading-unavailable presentation; normal/minimum-size and unknown-heading
  renders verified with clearly labelled example data. No live cubes are invented.
- Existing capture, geometry, packet-facing helper, freshness policy and audio retained.

## 0.3.0 (2026-10-30)
- Added an explicitly authorized, read-only AION2 motion helper. Only this helper
  requests elevation; keys remain in its RAM and are never sent to the GUI.
- Exact outbound world capture, validated RC4 cursor/clock alignment, separate
  position, movement yaw and facing yaw; no image matching, injection or game input.
- Same-account local pipe transports only motion/status. GUI prioritizes fresh
  C2S pose and shows Heading Source Packet; cube geometry is recalculated from it.
- 256 .NET tests and nine Python reader tests pass. Live: 421 motion records,
  1,736 clocks, 0 TCP gaps; median travel-angle error ~0.004 degrees.
  User confirmed Packet appears in the installed GUI while walking.
- Stale (>2.5s) or unsupported motion remains Unknown; local camera-only rotation
  is not invented. Existing cube detection, two-tone sound and sonar are retained.

## 0.2.1 (2026-10-30)
- Fixed self-identity drift reproduced during live heading investigation:
  a candidate selected by own records switched to another entity on a buff.
- SelfInfo now remains authoritative until another SelfInfo. Consecutive
  self-only records supersede buff-only guesses; unrelated buffs cannot replace them.
- Identity confidence is included in the diagnostic snapshot. Tentative buff
  identities no longer produce exact companion geometry or proximity beeps.
- 250 tests pass, including six new identity/geometry regressions.
- Actual facing is still unverified; no cast/aim field is labelled as facing.
  No memory-reading or C2S decryption has been added in this version.

## 0.2.0 (2026-10-30)
- Player-centred, heading-relative projection in a separately tested RadarMath.
  Character facing is UNKNOWN: no unverified packet field is promoted to heading.
  Optional travel-direction fallback expires after 3 seconds, explicitly labelled.
- Larger dark radar, fixed player triangle, 2.5s sonar animation (35ms timer),
  cube pulse, 150m clipping/optional edge clamp, relative sectors and height marks.
- Six independent readiness indicators, selectable targets, hover details,
  collapsed diagnostic panel and explicit local JSON export of bounded candidates.
- Sound backend fix retained; optional proximity cadence respects mute and freshness.
- 244 tests pass (22 new projection/heading/cadence/probe cases over 222 baseline).
- Capture filter, framing, Npcap lifecycle and cube parser retained.
- Remaining limitation: continuous real character facing has not been verified.
  See TEKNIK-RAPOR-TR.md for packet fields, formula and validation limits.

## 0.1.1
- Replaced the inaudible Windows system event with the existing upstream
  waveOut audio backend and generated two-tone PCM alert. User confirmed the
  standalone backend test was audible; no Windows volume settings were changed.
- Added packet/frame/stream counters and more precise readiness messages.
- Live aggregate diagnosis: 848 packets, 591 decoded records, one recognized
  game stream, own identity and position decoded, zero cube spawn records.
  No raw payloads, user coordinates or player identifiers were saved.

Based on dr89mh/aion2-cube-radar, upstream version 0.9.2 (GPL-3.0).
Original authorship and source headers retained. This is a separate, modified
companion, not an official upstream binary or an NCSOFT-approved product.

- Added `src/CubeRadar.Tr`: a Turkish Windows Forms interface with cube-only
  radar, distance/height display, explicit Start/Stop and audio alert.
- No image capture/matching, raw traffic logs, PvP UI, account integration,
  upstream updater or local web server in the new entry point.
- Added exact server-to-client, game-process-owned TCP connection filtering;
  closes the capture device when no recognized game connection exists.
- Corrected asynchronous capture-stop and failed-open cleanup paths.
- Reset decoder/engine state on capture changes. Hide current-presence and exact
  geometry claims when packets or the own position become stale.
- Added five targeted policy/filter tests. Existing 217 core tests retained.
- Added Npcap-free JSON-fixture proof and a capture start/stop diagnostic.

Testing status: 222 unit/fixture tests pass; recorded JSON fixture contains two
detected cubes with position/distance; installed Npcap starts/stops successfully
while AION2 is closed (zero packets). No live current-client cube validation yet.
Verteron templates are verified by upstream; Altgard remains experimental.
