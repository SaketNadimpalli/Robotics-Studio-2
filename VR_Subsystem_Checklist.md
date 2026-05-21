# Connect 4 Bot — VR Subsystem Checklist (Saket)

**Subsystem 4 — VR + Algorithm.** Target grade: **HD**. Comms pipeline: **ROS-TCP-Connector**. Headset: **Meta Quest** (Air Link for now; standalone APK build deferred until late).

This list is ordered by build progression. Tick what's done, then look at what's left below the last tick to see what comes next.

> Connect 4 model/algorithm: ✅ already done (per your message — not tracked here).

---

## Phase 0 — Unity / XR foundation

- [x] Unity LTS version locked in
- [x] Project switched to Android build target
- [x] XR Plugin Management installed
- [x] Meta XR All-in-One SDK / OpenXR + Oculus feature group installed
- [x] XR Interaction Toolkit (or Meta Interaction SDK) installed
- [x] Quest dev mode enabled, ADB recognises headset
- [x] First scene deploys to headset
- [x] Project under version control with Unity `.gitignore`

## Phase 1 — Basic VR scene

- [x] XR Origin / camera rig
- [x] Connect 4 board model in scene at correct scale
- [x] Game piece prefab (red + yellow)
- [x] Floor / room reference
- [x] Lighting reads in headset
- [x] Builds and runs on Quest with board visible

## Phase 2 — Hand-based piece interaction

> **Decision:** hands-only for the VR player. Controllers deferred — physical units have severe stick drift, and uni testing showed hand tracking is reliable for grab/drop. Joystick fallback dropped from scope.

- [x] Hand tracking enabled and visible in scene
- [x] Pinch grabs a coin
- [x] Release drops a coin
- [x] Coin snaps into the lowest empty row of whichever column it was dropped over
- [ ] **Bug (cosmetic, low priority):** if dropped over a full column, coin currently bounces under physics — should return to spawn instead

## Phase 2.5 — In-VR player-facing UI ✅

> All items complete. HUD implemented as world-space canvas (`Connect4VRHud.cs`) positioned near the board. Right-wrist reset button added separately; existing wrist menu restricted to left hand only.

- [x] Turn indicator visible in-VR ("Your turn" / "Robot's turn")
- [x] Robot status visible in-VR ("Idle" / "Thinking" / "Moving")
- [x] Win banner appears in-VR when a game ends, naming the winner
- [x] Winning four-in-a-row highlighted on the virtual board
- [x] Reset / new game accessible from in-VR (`PlayerCoinSpawner` + right-wrist button)
- [x] Wrist-menu debug overlay showing ROS / console output (kept as developer panel — bonus observability)

## Phase 3 — VR-side game state

- [x] Internal 6×7 board state in Unity
- [x] Turn manager
- [x] Win detection logic (terminal correctly logs winner)
- [x] Connect 4 algorithm hooked into Unity via Python ROS node — full bidirectional sync, with sync-error detection
- [x] **Bug fixed:** game continued accepting coin drops after win — `GameStateManager.IsGameOver` flag checked in `ColumnDetector`
- [x] **Bug fixed:** R-key reset cleared coins but didn't respawn them — `PlayerCoinSpawner` script spawns 10 player coins on Start and after reset
- [x] Winning line highlight in VR *(also completed under Phase 2.5)*
- [ ] Difficulty selectable (easy / medium / hard) — *deferred*

## Phase 4 — Wireless + hand tracking

- [x] Air Link wireless verified at acceptable latency
- [x] Hand tracking enabled and visuals working
- [x] Pinch / release gestures reliable (uni-tested)
- [ ] N/A — hand vs controller mode switch (controllers out of scope)
- [ ] Latency formally measured for the report (informally feels good — record a real number, e.g. drop-to-AI-response in ms)
- [ ] Standalone APK build replacing Air Link — *deferred to near-end*

## Phase 5 — UR3 representation in VR

- [x] Unity URDF Importer installed
- [x] UR3e URDF imported (matches Zain's URDF)
- [x] UR3 placed in VR world at correct relative position to virtual board
- [x] Joint state subscriber via ROS-TCP-Connector
- [x] Virtual UR3 mirrors joint angles in real time *(verified with manually-published angles — TODO: re-verify against real UR3 in person)*
- [x] **ROS pipeline convention confirmed:** send raw ROS radian values; `UR3JointStateMirror` applies them directly — no sign flips needed
- [ ] End-effector / gripper visualised, animates open/close

## Phase 6 — ROS-TCP-Connector communication pipeline

- [x] ROS-TCP-Connector Unity package installed
- [x] ROS-TCP-Endpoint running on ROS2 side
- [x] Smoke tested locally (WSL ↔ Unity)
- [x] **Cross-machine tested with friend's laptop** — distributed networking verified
- [x] Connection status visible — covered by wrist-mounted ROS terminal overlay (also doubles as live console)
- [ ] Connection config (IP / port) exposed in a Unity scriptable, not hardcoded — easy to switch on demo day
- [ ] **⚠ Topic contract with Zain — OUTSTANDING.** Currently only joint angles informally agreed. Lock down before either side writes more on top:
  - [ ] `/c4/vr/move_command` — VR → ROS, column index + colour
  - [ ] `/c4/board_state` — Nam publishes, you subscribe
  - [ ] `/c4/robot_status` — Zain publishes (idle / moving / faulted)
  - [x] `/joint_states` — already wired ✅
  - [ ] `/c4/arm_flag` — VR sends discrete "move to column X" flag command (replaces teleop pose topic — see Phase 9 note)
  - [ ] `/c4/estop` — bidirectional safety
- [ ] Custom message types generated on both sides, packages match

## Phase 7 — Full turn loop synced (D-tier)

- [ ] VR player move → publish → UR3 picks & places → board state confirms → VR re-syncs
- [ ] Robot opponent move: AI decides → command → UR3 executes → VR animates the same move at the same time
- [ ] Drift handling: perception-reported state vs VR-expected state reconciled
- [ ] Lock VR input while robot is moving
- [ ] Coin-drop animation timing roughly matches real coin fall
- [ ] Tested end-to-end in sim before real UR3
- [ ] Tested end-to-end with real UR3

## Phase 8 — Robustness & error handling (D-tier)

- [x] Post-win input lockout *(fixed — `GameStateManager.IsGameOver` in `ColumnDetector`)*
- [x] Reset respawns coins for second game *(`PlayerCoinSpawner` built and wired)*
- [ ] Full-column drop returns coin to spawn cleanly *(cosmetic, low priority — see Phase 2)*
- [ ] Lost ROS connection → graceful pause, "reconnecting…" prompt, no crash
- [ ] Robot fault state → VR shows alert, locks input
- [ ] User input errors don't deadlock the turn manager
- [ ] Full game playable end-to-end without manual intervention

## Phase 9 — UR3 arm control from VR (HD headline feature)

> **Pivot (session 2):** direct hand teleop shelved. Diagnosed 3 bugs in `UR3TeleopDriver.cs` (world-space vs robot-base-local frame, sign flips on joints 1 & 3, wrong convention in `ReadCurrentAngles`), partially fixed, but remaining spinning issue traced to workspace positioning. Teleop further parked — will revisit with MoveIt Servo if time allows.
>
> **Architecture (session 3):** Flag-based arm control. When a coin is placed in column X, Unity publishes a `sensor_msgs/JointState` with prerecorded column angles to a ROS2 topic. Friend's (Zain's) ROS2 node receives it and forwards to MoveIt. `UR3PoseController` drives the virtual arm to the same pose locally. Convention confirmed: publish raw ROS radian values, no sign flips.

### Stage A — Column angle recording + Unity publish

- [x] `UR3PoseController` confirmed working — smoothly interpolates virtual arm to preset poses via degree arrays, logs radian snapshot on arrival
- [x] Column 1 hover angles recorded: `[1.0897, -0.8727, 0.6021, -1.3086, -1.5962, -1.8889]` rad
- [ ] Columns 2–7 angles recorded (need physical arm session with Zain)
- [ ] `ColumnDetector` (or `GameStateManager`) publishes `sensor_msgs/JointState` with column angles on coin placement
- [ ] `UR3PoseController.GoToAngles()` called locally so virtual arm moves to match
- [ ] VR locks player input while arm is moving (HUD status = "Moving") — **⚠ also needed: `IsPlayerTurn` flag in `GameStateManager` to block drops during robot turn — not yet implemented**
- [ ] VR unlocks input when arm reaches pose (or on `/c4/robot_status` → "Idle")

### Stage B — Real UR3 executes flag commands

- [ ] Topic + message type agreed with Zain (also tracked Phase 6)
- [ ] Zain's ROS2 node receives `JointState` and forwards to MoveIt
- [ ] Lab-tested with real UR3, hardware E-stop in hand
- [ ] `/joint_states` from real arm continues driving virtual UR3 — closes the visual loop

### Stage C — Teleop (parked)

> `UR3TeleopDriver.cs` kept in repo. Revisit with MoveIt Servo if HD hand-teleop criterion becomes a concern closer to demo day.

## Phase 10 — Safety & UX (HD-tier)

- [ ] VR play zone defined and visualised
- [ ] User exits play zone → robot paused, in-VR prompt
- [ ] In-VR E-stop (wrist menu or floating panel) wired to `/c4/estop`
- [ ] Hardware E-stop / GUI E-stop mirrored in VR (synced with Enrique's GUI)
- [ ] Status prompts in-VR — overlaps with Phase 2.5 *(Phase 2.5 complete — wire E-stop states into existing HUD)*
- [ ] Comfort: locomotion off, fixed standing experience
- [ ] First-run calibration: align virtual board to real board (manual offset or AprilTag-aided)

## Phase 11 — AI quality target (HD rubric requirement)

- [x] AI plays at minimum a depth-limited minimax / heuristic level (model done)
- [ ] Difficulty levels mapped to depth / heuristic strength — *deferred*
- [ ] Win-rate benchmark vs an online Connect 4 engine recorded (HD target: 60–70%)
- [ ] Win rates documented in subsystem report

## Phase 12 — Cross-subsystem integration

- [ ] **With Zain (motion):** lock topic contract (Phase 6), confirm UR3 URDF version matches, agree who owns the "robot busy" lock, agree flag-based arm command topic + safety limits
- [ ] **With Nam (perception):** board state topic format, frequency, reconciliation rule when VR-state vs perceived-state disagree
- [ ] **With Enrique (GUI):** shared game-state source of truth, E-stop wiring, mode/difficulty toggle, how XR mode is engaged from the GUI
- [ ] One full integrated dry run on real hardware ≥1 week before assessment
- [ ] Demo script with fallback paths

## Phase 13 — Documentation & demo prep

- [ ] **Single-command launch** for the full ROS stack (currently nodes are started manually — replace with a launch file or tmuxp script before integration testing)
- [ ] README in VR repo: setup, build, deploy, troubleshooting
- [ ] ROS topic / message reference doc
- [ ] Calibration / first-run procedure documented
- [ ] Known issues + workarounds list
- [ ] Backup build on USB stick for demo day

---

## Where you are right now

Phases 0, 1, 2, 4 done. Phase 2.5 **complete**. Phase 3 **complete**. Phase 5 mostly done (ROS convention confirmed — raw radians, no flips; gripper visual + in-person re-verify still pending). Phase 6 mostly done (topic contract outstanding). Phase 8 known bugs fixed; robustness tail open.

Phase 9: flag-based arm architecture confirmed. `UR3PoseController` working. Column 1 angles recorded. Columns 2–7 need physical arm time with Zain. Unity-side publish + input lock still to be wired.

**⚠ Outstanding gap for D:** `ColumnDetector` checks `IsGameOver` but not `IsPlayerTurn` — player can drop coins during the robot's turn. Needs a `IsPlayerTurn` flag in `GameStateManager`.

**Current rubric position:** solidly C-tier. D requires real arm integration (Phase 7, Zain-dependent) + turn-lock fix + robustness tail. HD requires full Phase 9 end-to-end + Phase 10 safety + Phase 11 benchmark.

### Suggested next priorities

1. **Phase 7 / Phase 9 prep — add `IsPlayerTurn` lock to `ColumnDetector`.** Small, pure Unity, fixes the D robustness gap independently of Zain.
2. **Phase 6 — lock topic contract with Zain.** Blocks Phase 7, 9B, 12.
3. **Phase 9 Stage A — wire `ColumnDetector` → publish `JointState` + call `GoToAngles()` locally.** Can do the Unity side before Zain is ready.
4. **Phase 9 Stage A — record columns 2–7 with real arm** (needs Zain).
5. **Phase 7 — full turn loop** once topic contract locked and Zain's node is live.
6. **Phase 8 — robustness tail** (ROS disconnect, fault state, end-to-end clean game).
7. **Phase 10 — safety/UX** (play zone, E-stop).
8. **Phase 11 — run AI win-rate benchmark** and record the number.
9. **Phase 13 — single-command launch + docs**.
