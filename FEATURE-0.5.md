# Cube Radar TR 0.5 — accepted feature scope

User authorized the proposed radar improvements and requested boss/event alerts.

| Module | Responsibility | Depends on |
|---|---|---|
| target-tracking | Lock object identity, filter height, show freshness | existing cube snapshots |
| compact-view | Small always-on-top radar with selected target | target-tracking |
| event-clock | Next scheduled/one-off occurrence, timezone conversion, alert deduplication | — |
| event-ui | Boss/map/location list, pulsing imminent rows, sound, local editing | event-clock |
| reconnect | Explicit one-click reconnect after a stopped motion reader | existing capture lifecycle |

Build order: target-tracking → compact-view; event-clock → event-ui; reconnect.
No dependency additions. Existing WinForms, Core and xUnit seams are reused.

Acceptance:
- Locked target remains the same object after reordering; missing/filtered target
  is explicitly reported, never replaced silently. Map/session change clears lock.
- Height filter applies consistently to radar/list/nearest card; stale geometry
  never generates a direction or proximity alert.
- Compact window shares capture state, mute and target; closing it leaves the main
  app running. Main app shutdown disposes it and all timers.
- Event list includes boss, map, location, server time, local time and countdown.
  Selected timezone changes display, never the underlying instant. Server schedule
  timezone is distinct and explicitly configured; DST is handled by TimeZoneInfo.
- Imminent events glow gently without rapid flashing and alert once at each chosen
  threshold/start. Alerts survive restart without duplicate delivery. Mute prevents
  catch-up sound. Expired one-off events do not repeat.
- Weekly schedules and manual countdowns can be saved/edited locally. Imported
  community schedules show their provenance and require explicit clock confirmation.
  No schedule is represented as a live boss detection or a verified kill timer.
- Window location/timezone and schedule config are local; no data is uploaded.
- Existing .NET/Python gates plus focused temporal/target tests, normal/minimum/mini/
  events/unknown-state renders and a runtime smoke check prove completion.

Open data question: user's service/server timezone. Build independent portions
while awaiting their reply; unconfirmed presets remain inactive.
