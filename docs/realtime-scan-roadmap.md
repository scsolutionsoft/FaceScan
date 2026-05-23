# Realtime Face Scan Roadmap

## Current Baseline

- Student scan currently runs from the browser camera in the scan page.
- The UI can only toggle `facingMode` between front and rear camera.
- There is no device picker for selecting a specific webcam by `deviceId`.
- Recent scan results are loaded by HTTP pull, not server push.
- Attendance persistence already has a usable core in `AttendanceService.ProcessScanAsync`.

## Gap Summary

### Camera selection gap

- Desktop users often have more than one webcam.
- The current switch button is sufficient for mobile front/back cameras but not for USB webcam scenarios.
- Operators need to know which camera is active and be able to lock the preferred device.

### Real-time monitoring gap

- The current scan page shows latest results, but updates are not pushed in real time.
- There is no dedicated operator dashboard for live gate monitoring.
- There is no health status for camera source, ingest queue, latency, or recognition errors.

### IP camera gap

- The current web page reads browser camera only.
- IP camera ingest needs an additional capture pipeline and should not overload MVC request handling.

## Product Direction

Build the system in layers:

1. Keep the current attendance save flow as the core business path.
2. Add real-time event delivery so scan results appear instantly.
3. Add explicit webcam selection for desktop and kiosk use.
4. Add an external ingest path for IP cameras.
5. Add a dedicated live monitor UX for operators.

## UX Direction

### 1. Scan station page

Extend the existing scan page with a camera selector block above the preview area.

Recommended controls:

- Camera source type: Browser webcam / IP camera lane
- Webcam selector: dropdown populated from `navigator.mediaDevices.enumerateDevices()`
- Resolution preset: Auto / 720p / 1080p
- Mirror toggle for front camera
- Remember selected camera on this device
- Camera health chip: Ready / No permission / Offline / Busy

Recommended behavior:

- On first load, request camera permission, then enumerate devices again so real labels appear.
- When there is only one webcam, auto-select it.
- When the saved webcam is unavailable, fall back to the first available device and show a warning.
- Keep the existing switch button for mobile, but hide or demote it when a full webcam dropdown is available.

### 2. Live monitor page

Create a dedicated operator page for real-time monitoring.

Recommended layout:

- Left: live camera panel or lane placeholder
- Right top: last matched student card with large name and timestamp
- Right middle: recent scan feed with status colors
- Right bottom: counters for pass, duplicate, low confidence, unknown face, offline lane

### 3. Student confirmation display

Keep the student-facing feedback very large and immediate.

Recommended states:

- Green: scanned successfully
- Yellow: duplicate or already scanned
- Red: unknown face or failed match
- Blue: matching in progress

## Delivery Phases

## Phase 1: Webcam Selection On Existing Scan Flow

Goal:
Improve the current browser-camera scan page without changing the recognition core.

Scope:

- Add webcam dropdown to the student scan page.
- Add device enumeration and selected `deviceId` persistence in local storage.
- Keep `facingMode` switch as a mobile fallback.
- Show the active camera name in UI.
- Add graceful fallback when camera permission is denied or the selected camera disappears.

Implementation direction:

- Update the scan page script to use `navigator.mediaDevices.enumerateDevices()`.
- Store `preferredCameraDeviceId` in local storage.
- Use `video: { deviceId: { exact: selectedId } }` when a webcam is selected.
- Fall back to `facingMode` when no specific device is selected.

Suggested acceptance criteria:

- Operators can choose a specific webcam from a dropdown.
- The selected webcam remains selected on reload on the same machine.
- Mobile devices still work with the current front/back flow.
- The scan flow still saves attendance through the existing service path.

## Phase 2: Real-time Push For Scan Results

Goal:
Make scan results appear instantly without manual refresh.

Scope:

- Add SignalR hub for scan events.
- Broadcast successful scan, duplicate scan, and failed recognition events.
- Update scan page and monitor page to subscribe to live events.
- Keep the existing `/Scan/Recent` endpoint as fallback and initial page hydration.

Implementation direction:

- Add `AddSignalR()` and map a scan hub in startup.
- Publish a scan event after `ProcessScanAsync` completes.
- Push lane/station metadata with each event.

Suggested acceptance criteria:

- Latest scan feed updates within about one second after recognition completes.
- Operator pages do not require manual refresh during normal operation.
- If SignalR disconnects, the page can recover automatically.

## Phase 3: Dedicated Live Monitor UX

Goal:
Separate operator monitoring from the scan capture experience.

Scope:

- Add a live monitor page for gate staff.
- Add lane cards, live status badges, counters, and event feed.
- Add filtering by gate, classroom, scan type, and confidence state.

Implementation direction:

- Reuse the visual language from the existing scan result cards.
- Optimize for large screens and wall display mode.
- Add a compact mobile operator layout only if required by actual usage.

Suggested acceptance criteria:

- An operator can see the last successful student and recent queue at a glance.
- Lane health and event flow are visible without opening developer tools.

## Phase 4: IP Camera Ingest

Goal:
Support students walking past an IP camera without manually opening a browser camera.

Scope:

- Add an ingest agent to read RTSP/HLS from IP cameras.
- Detect faces or motion before sending frames for recognition.
- Submit cropped face images to a new machine-to-machine API.
- Reuse the same attendance core and real-time event pipeline.

Implementation direction:

- Do not put RTSP decoding directly in the MVC request path.
- Run an edge agent near the camera source when possible.
- Send only candidate crops or sampled frames, not the full video stream.

Suggested acceptance criteria:

- A configured IP camera can produce scan events without browser interaction.
- Recognition throughput remains stable under multiple passersby.
- Duplicate suppression works per lane and time window.

## Phase 5: Operations, Health, and Audit

Goal:
Make the system stable enough for continuous school operation.

Scope:

- Camera online/offline health reporting
- Queue depth and recognition latency metrics
- Unknown face review queue
- Audit trail for camera config changes
- Optional snapshots for incident review

Suggested acceptance criteria:

- Staff can identify whether a problem is camera, network, recognition, or business rule.
- The system can be monitored during the school rush period without guesswork.

## Technical Notes

### Reuse existing code

- Keep the current attendance write path as the source of truth.
- Keep the existing scan page as the fallback and pilot surface.
- Keep the recent scan endpoint for initial load and degraded mode.

### New components to add

- Scan hub for SignalR events
- Scan event publisher service
- Webcam selector UI state on scan pages
- Camera configuration entities for future IP camera lanes
- Optional ingest worker or external edge agent

## Recommended Order Of Implementation

1. Phase 1 first because it solves the webcam requirement immediately and is low risk.
2. Phase 2 next because it upgrades the user experience without changing recognition capture.
3. Phase 3 after that because the live operator UX becomes meaningful once events are pushed.
4. Phase 4 only after the event pipeline is proven stable.
5. Phase 5 in parallel with pilot rollout if the school will run this in production.

## Immediate Next Build Slice

If implementation starts now, the smallest valuable slice is:

1. Add webcam dropdown on the current scan page.
2. Persist selected `deviceId` in local storage.
3. Add SignalR event broadcast for completed scans.
4. Replace manual refresh of recent scans with live updates.

That slice gives desktop webcam selection and a visible real-time result loop without waiting for IP camera ingest.