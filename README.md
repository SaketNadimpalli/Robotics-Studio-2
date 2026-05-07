# Robotics-Studio-2
Project with UR3e





































## TO-DO

# UR3e Mirror Integration Test
### For when you meet your teammate in person
**Context:** Test 1 (static joint publishing) already passed solo. This is the live integration test with the real arm and MoveIt.

---

## Before You Start — Both Machines

Make sure you're on the **same network** (same WiFi or direct ethernet).

On your machine (Unity/Windows), check the IP in your `ROSConnection` settings in Unity matches your teammate's Ubuntu machine IP:
```bash
# Your teammate runs this to find their IP
ip addr show | grep "inet "
```

On your teammate's machine, confirm the endpoint is running:
```bash
ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=0.0.0.0
```
---

## Step 1 — Confirm `/joint_states` is publishing

On **your** machine (WSL2 terminal), run:
```bash
ros2 topic echo /joint_states
```
You should see a stream of messages like:
```
name: ['shoulder_pan_joint', 'shoulder_lift_joint', 'elbow_joint', 'wrist_1_joint', 'wrist_2_joint', 'wrist_3_joint']
position: [-0.02, -1.57, 0.03, -1.57, -0.01, 0.0]
```

**Check:**
- [ ] Data is flowing (not silent)
- [ ] Joint names match exactly what's in `UR3JointStateMirror.cs` — if they differ, update the `UR3JointNames` array in the script
- [ ] 6 joints present

Also check publish rate:
```bash
ros2 topic hz /joint_states
```
Ideally 50Hz. If it's below 10Hz, lower `smoothingSpeed` in the Inspector to around 5.

---

## Step 2 — Static pose check

Ask your teammate to move the real arm to its **home pose** (all zeros / straight up) and hold it still.

In Unity (Play mode), check the virtual arm visually. It should match.

**If a joint is going the wrong direction:** find that joint's index (0–5) in `ROSToUnityAngle()` in `UR3JointStateMirror.cs` and flip its sign.

---

## Step 3 — Single joint sweep

Ask your teammate to slowly rotate **only shoulder_pan** (joint 0) left and right by about 45°.

Watch the virtual arm — shoulder_pan only should move, in the same direction.

Repeat for each joint one at a time if you have time.

---

## Step 4 — Free motion test

Ask your teammate to run a simple MoveIt motion (e.g. move to a pick position above the Connect 4 board).

Watch the virtual arm follow in real-time.

**What good looks like:**
- Virtual arm follows with ~50–100ms lag (normal and fine)
- No joints snapping or jumping
- End effector ends up in roughly the same position relative to the virtual board as the real arm is to the real board

---

## Step 5 — Base anchor check

This is the one thing that might need a quick physical measurement.

With the real arm at home pose, look at where the virtual arm's end effector is relative to the virtual Connect 4 board — does it match the real setup?

If it's offset, measure the real-world distance (metres) from the **centre of the robot base** to the **corner of the Connect 4 board** and update `rosOffsetMetres` in `RobotBaseAnchor.cs`.

---

## Quick Fixes Cheat Sheet

| Problem | Fix |
|---|---|
| Joint names don't match | Update `UR3JointNames[]` in `UR3JointStateMirror.cs` |
| A joint rotates wrong direction | Flip sign in `ROSToUnityAngle()` for that joint index |
| Arm is jittery | Lower `smoothingSpeed` in Inspector |
| Arm lags too much | Raise `smoothingSpeed` in Inspector |
| Virtual arm is offset from board | Adjust `rosOffsetMetres` in `RobotBaseAnchor.cs` |
| No data flowing at all | Check IP address in Unity ROSConnection settings |




## Friday class — meeting with Zain: lock ROS topic contract

**Goal:** agree on topic names + message shapes between Unity (VR) and ROS2 (motion) before either side writes more code on the current setup. Currently only `/joint_states` is informally agreed.

### Topics to lock in (proposed names + message types — to confirm/edit with Zain)

- [ ] `/c4/vr/move_command` — VR → ROS, fired when the player drops a coin in VR
  - Proposed: `int32 column` (0–6), `string colour` ("red" / "yellow")
- [ ] `/c4/board_state` — ground-truth board state (Nam publishes from perception, Unity subscribes)
  - Proposed: 6×7 array of cell states (empty / red / yellow) + a sequence number for drift detection
- [ ] `/c4/robot_status` — Zain publishes, Unity subscribes
  - Proposed values: `idle` / `executing` / `faulted` / `estopped`
- [x] `/joint_states` — already wired ✅
- [ ] `/c4/teleop_pose` — VR hand pose → UR3 end-effector target (Phase 9 Stage B)
  - Proposed: `geometry_msgs/PoseStamped` at 30–60 Hz
- [ ] `/c4/estop` — bidirectional safety
  - Proposed: `std_msgs/Bool` (true = stop)

### Non-topic things to nail down

- [ ] **UR3e URDF version** — which one is he using? Make sure my Unity import matches exactly so virtual ↔ real mirroring stays accurate.
- [ ] **Coordinate frames** — confirm Unity world ↔ ROS `base_link` alignment, especially for teleop pose targets. Unity is left-handed Y-up, ROS is right-handed Z-up — agree on which side does the conversion (my plan: Unity does it before publishing).
- [ ] **"Robot busy" lock** — Unity must not send new move commands while UR3 is mid-motion. Does Zain expose this as a service, a topic flag, or part of `/c4/robot_status`?
- [ ] **Teleop safety limits (Phase 9 Stage B)** — agree split: Unity smooths the pose stream (velocity scaling, jitter filter); ROS side enforces hard kinematic / velocity limits. Confirm what those limits are.
- [ ] **Custom message package** — whose ROS package owns the custom `.msg` files? Both sides need it generated.
- [ ] **Single launch command** — agree on one launch file (or tmuxp script) that brings up: ROS-TCP-Endpoint, motion node, AI node, joint state pub. Demo day will be ugly if we're juggling six terminals.

### Outputs from the meeting

- [ ] Write the agreed contract into the team Discord / shared doc so Nam and Enrique see it too
- [ ] Update VR checklist Phase 6 with the locked-in names
- [ ] Both sides update placeholder topic strings in code to match