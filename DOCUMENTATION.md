# Connect 4 Bot — Technical Documentation

**Course:** 41069 Robotics Studio 2 — University of Technology Sydney
**Client:** Robert Fitch | **Coach:** Kate Powell
**Team:** Nam Nguyen · Saket Nadimpalli · Enrique Santos · Zain Khan

---

## System Overview

This project uses a Universal Robots UR3e arm to play Connect 4 against a human in two modes:

- **IRL Mode** — The robot plays autonomously using a trained AI. A human interacts via a GUI.
- **XR Mode** — A remote human wearing a Meta Quest 2 headset teleoperate the robot using hand tracking, playing against an in-person opponent.

The system is composed of four subsystems:

| # | Subsystem | Lead | Folder |
|---|---|---|---|
| 1 | Perception | Nam Nguyen | *(separate repo)* |
| 2 | Motion Planning & Control | Zain Khan | *(separate repo)* |
| 3 | GUI / UX | Enrique Santos | *(separate repo)* |
| 4 | **Algorithm + VR** | Saket Nadimpalli | `Connect4-ML/` and `VR/` |

This document covers **Subsystem 4** only: the AI algorithm and the XR/VR environment.

---

## Dependencies — Shared Hardware

The following hardware is required to run either subsystem in a full-system context:

- Universal Robots **UR3e** arm
- **Meta Quest 2** headset
- 2× laptops dual-booted with **Ubuntu 22.04** and **ROS2 Humble**
- A local WiFi router (all devices must be on the same network)
- Web camera (used by Perception subsystem)

---
---

# Subsystem A — Connect 4 Algorithm

## Purpose

The Connect 4 Algorithm subsystem provides the AI decision-making engine for the robot. It trains and runs a **Deep Q-Network (DQN)** agent combined with a **4-ply minimax search** to determine the optimal column to play each turn. In the integrated system, this subsystem is intended to act as a ROS2 node — receiving board state updates and player moves, then publishing its chosen column so the Motion Planning subsystem and VR subsystem can respond.

> **Note:** The full ROS2 node wrapper is partially implemented. The core AI logic (training, inference, minimax) is complete and standalone. Integration into the live ROS2 pipeline requires a wrapper script to be finalised.

---

## Key Files and Scripts

```
Connect4-ML/
├── play.py                          Main entry point: Human vs AI (pygame GUI)
├── main.py                          Human vs Human (pygame GUI, no AI)
├── test.py                          Training entry point — calls training loop
├── pvp_test.py                      Player-vs-player illegal move tester
│
├── game/
│   └── connect4_env.py              Core game logic: 6×7 board, move validation, win detection
│
├── agent/
│   ├── dqn_agent.py                 DQN agent: epsilon-greedy, experience replay, model save/load
│   ├── dqn_model.py                 CNN model: 3-channel input → 3 conv layers → 7 Q-values
│   ├── Prioritised_replay_buffer.py Prioritized experience replay (PER) with importance sampling
│   └── replay_buffer.py             Standard replay buffer (used in earlier versions)
│
├── training/
│   ├── train.py                     Full training loop: curriculum learning, opponent cache, 1M episodes
│   ├── reward_shaper.py             Reward function: pattern detection, fork threats, blocking bonuses
│   └── opponent_cache.py            Self-play opponent management: stores frozen agent snapshots
│
├── search/
│   └── minimax.py                   4-ply alpha-beta minimax using DQN Q-values at leaf nodes
│
├── visuals/
│   └── renderer.py                  Pygame renderer: blue board, red/yellow coins, status text
│
└── models/
    ├── connect4_best.pth            Best model checkpoint — use this for playing
    ├── connect4_latest.pth          Most recent checkpoint — use this to resume training
    ├── models_V1/                   Version 1 model checkpoints (archived)
    └── checkpoints/                 Periodic training checkpoints (every 10,000 episodes)
```

---

## Inputs and Outputs

| Direction | Data | Format | Source / Destination |
|---|---|---|---|
| **Input** | Board state | 6×7 numpy array (0=empty, 1=player1, 2=player2) | `connect4_env.py` or ROS `/connect4/board_state` |
| **Input** | Player move | Column index integer (0–6) | Mouse click in `play.py`, or ROS `/connect4/player_move` |
| **Output** | AI move | Column index integer (0–6) | Pygame window in `play.py`, or ROS `/connect4/ai_move` |
| **Output** | Game result | Winner code (1=player, 2=AI, 0=draw) | Pygame window, or ROS `/connect4/game_over` |

**Intended ROS2 topics (integration in progress):**

| Topic | Type | Direction |
|---|---|---|
| `/connect4/board_state` | `std_msgs/String` | Subscribe |
| `/connect4/player_move` | `std_msgs/Int32` | Subscribe |
| `/connect4/ai_move` | `std_msgs/Int32` | **Publish** |
| `/connect4/game_over` | `std_msgs/Int32` | Publish |
| `/connect4/reset` | `std_msgs/Bool` | Subscribe |

---

## Dependencies

### Hardware
- A laptop or PC running **Ubuntu 22.04** (or Windows with WSL2 for standalone testing)
- GPU with CUDA support is **optional but recommended** for training. CPU works but is slow.

### Software

| Package | Version | Notes |
|---|---|---|
| Python | **3.10.8** | Must match exactly |
| torch | **2.5.1+cu121** | CUDA 12.1 build — use CPU variant if no NVIDIA GPU |
| numpy | **2.2.6** | Array operations |
| pygame | **2.6.1** | Game window and renderer |
| ROS2 | Humble | Only needed for full-system integration |

---

## Installation

Follow these steps exactly on a fresh Ubuntu 22.04 machine.

### Step 1 — Install Python 3.10.8

```bash
sudo apt update
sudo apt install -y software-properties-common
sudo add-apt-repository ppa:deadsnakes/ppa
sudo apt update
sudo apt install -y python3.10 python3.10-venv python3.10-dev
```

Verify:
```bash
python3.10 --version
# Expected output: Python 3.10.8
```

### Step 2 — Clone the repository

```bash
gh repo clone SaketNadimpalli/Robotics-Studio-2
cd Robotics-Studio-2/Connect4-ML
```

### Step 3 — Create and activate a virtual environment

```bash
python3.10 -m venv venv
source venv/bin/activate
```

Your terminal prompt should now start with `(venv)`.

### Step 4 — Install PyTorch

**If you have an NVIDIA GPU (recommended for training):**
```bash
pip install torch==2.5.1+cu121 --index-url https://download.pytorch.org/whl/cu121
```

**If you only have CPU:**
```bash
pip install torch==2.5.1 --index-url https://download.pytorch.org/whl/cpu
```

### Step 5 — Install remaining dependencies

```bash
pip install numpy==2.2.6 pygame==2.6.1
```

### Step 6 — Verify the model file exists

```bash
ls models/connect4_best.pth
```

If the file is missing, you must train the model first (see Training section below). The trained checkpoint file is not automatically included when cloning — it must either be downloaded separately or trained from scratch.

### Step 7 — Verify everything works

```bash
python play.py
```

A pygame window should open showing a blue Connect 4 board. If you see an error about a missing `.pth` file, go to the Training section first.

---

## How to Run or Test Independently

### Play against the AI

```bash
cd Connect4-ML
source venv/bin/activate
python play.py
```

- **Red coins** = Human (Player 1) — click a column to drop a piece
- **Yellow coins** = AI (Player 2) — uses DQN + 4-ply minimax automatically
- The AI loads `models/connect4_best.pth` on startup
- Close the window to quit

### Human vs Human (no AI)

```bash
python main.py
```

Both players click the column they want to play. Useful for testing the game logic independently.

### Train the AI from scratch

```bash
python test.py
```

- Runs up to **1,000,000 episodes** of self-play with curriculum learning
- Progress is printed every 2,000 episodes to the terminal
- Checkpoints save to `models/checkpoints/` every 10,000 episodes
- The best model is saved to `models/connect4_best.pth` automatically
- The most recent model is saved to `models/connect4_latest.pth`
- Stop training at any time with `Ctrl+C` — progress is not lost
- To resume training, the script automatically loads `connect4_latest.pth` if it exists

### Test for illegal moves (player-vs-player)

```bash
python pvp_test.py
```

Runs a scrollable summary screen showing move legality statistics across a test match.

---

## Configurable Settings and Parameters

### Training hyperparameters — `training/train.py` (lines 14–27)

| Parameter | Default | What it controls |
|---|---|---|
| `EPISODES` | `1,000,000` | Total training episodes |
| `EPISODE_OFFSET` | `90,000` | Offset for resuming — add to printed episode count to get true total |
| `TARGET_UPDATE` | `10` | Episodes between copying policy net weights to target net |
| `PRINT_EVERY` | `2,000` | Console log frequency |
| `CHECKPOINT_EVERY` | `10,000` | How often to save a checkpoint file |
| `WIN_THRESHOLD` | `40.0` | Win rate (%) required before adding an agent snapshot to the opponent cache |
| `CACHE_SIZE` | `10` | Max number of past opponents stored for self-play |
| `GAMMA` | `0.95` | Reward discount factor |

### DQN agent hyperparameters — `agent/dqn_agent.py` (lines 11–22)

| Parameter | Default | What it controls |
|---|---|---|
| `lr` | `0.000005` | Adam optimiser learning rate |
| `epsilon` | `1.0` | Starting exploration rate (1.0 = fully random) |
| `epsilon_min` | `0.01` | Minimum exploration rate |
| `epsilon_decay` | `0.99999963` | Multiplied each step — controls how fast randomness decreases |
| `batch_size` | `64` | Number of experiences sampled per training step |
| `buffer_capacity` | `100,000` | Maximum experiences stored in replay buffer |
| `alpha` | `0.6` | PER prioritisation exponent (higher = stronger priority bias) |
| `beta` | `0.4` | Importance sampling correction starting weight |

### Curriculum phases — `training/train.py` (lines 42–49)

The training curriculum changes reward shaping based on the current `epsilon` value:

| Phase | Epsilon range | Behaviour |
|---|---|---|
| 1 | `> 0.7` | Mostly random — early exploration |
| 2 | `0.4 – 0.7` | Starts rewarding blocking moves |
| 3 | `0.2 – 0.4` | Stronger fork detection and blocking penalties |
| 4 | `< 0.2` | Near-greedy — full strategic reward shaping active |

---

## Known Limitations and Assumptions

- **Package versions must match** — if you install different versions and the model fails to load, check that your PyTorch version matches `2.5.1+cu121` (or the CPU equivalent). The exact versions are listed in the Dependencies section above.
- **ROS2 integration is in progress** — the AI currently runs as a standalone Python process. A ROS2 node wrapper that subscribes to player moves and publishes AI moves needs to be completed before the robot can respond to the AI's decisions.
- **Training is slow on CPU** — 1 million episodes takes many hours without a GPU. For testing, load `connect4_best.pth` and run `play.py` directly instead of training.
- **Minimax depth is hardcoded at 4** in `search/minimax.py`. Increasing depth improves play quality but increases compute time per move significantly.
- **Player 1 = Human, Player 2 = AI** is assumed throughout. Swapping roles requires modifying `play.py`.
- **pygame requires a display** — `pvp_test.py` and `play.py` cannot run headlessly (e.g. over SSH without X forwarding). Use `ssh -X` or run on the local machine.
- The `models_V1/` checkpoints are an older architecture and are not compatible with the current `dqn_model.py` without modification.

---
---

# Subsystem B — XR / VR Environment

## Purpose

The XR subsystem provides the immersive interface for the Connect 4 game. It runs in Unity on a Meta Quest 2 headset and serves three roles: (1) it renders a **digital twin** of the physical Connect 4 board and the real UR3e arm so a remote VR player can see the live game state; (2) it lets that player **drop virtual coins using hand tracking**, which are published as player moves over ROS2; and (3) it implements a **teleoperation pipeline** that maps the player's hand pose through an analytical IK solver into UR3e joint commands, enabling the VR player to directly drive the physical robot arm.

---

## Key Files, Scripts, and Topics

### Unity C# Scripts — `VR/Assets/Script/`

| File | Purpose |
|---|---|
| `GameStateManager.cs` | Master game controller. Subscribes to `/connect4/board_state` and `/connect4/game_over`. Detects desync between Unity coin positions and the ROS board state. Handles game reset. |
| `ColumnDetector.cs` | Trigger zone on each of the 7 columns. Detects when a coin lands and snaps it to the correct row slot. Publishes the column index to `/connect4/player_move` for human coins. Skips publishing for AI coins. |
| `AICoinSpawner.cs` | Subscribes to `/connect4/ai_move`. Spawns a yellow coin above the correct column, applies gravity, disables XR grabbing, and marks it as an AI coin so `ColumnDetector` does not re-publish it. |
| `CoinSnap.cs` | After a 0.5 s delay following a coin landing: zeroes velocity, makes the coin kinematic, disables the XR grab interactable. Stores whether the coin was AI-spawned. |
| `UR3JointStateMirror.cs` | Subscribes to `/joint_states` (from the real UR3e). Converts ROS radians to Unity degrees, applies axis-flip compensation, and drives the virtual articulation body chain in real time with exponential smoothing to reduce jitter. |
| `UR3TeleopDriver.cs` | Reads hand pose from `HandTracker`, converts from Unity world space to ROS robot base frame, calls `UR3eIKSolver` to compute 6 joint angles, and rate-limits joint velocity to prevent jumps. Exposes `SolvedAnglesRadians[]` for ROS publishing (publishing step is partially implemented). Can be armed/disarmed to switch between teleop and mirror modes. |
| `UR3PoseController.cs` | Stores a list of named preset arm poses (as joint angle arrays). Interpolates the virtual arm smoothly between poses. Triggerable via number keys 1–6 or the public `GoToPose()` method. Temporarily disables the URDF Controller during transitions to avoid conflicts. |
| `UR3eIKSolver.cs` | Static analytical inverse kinematics for the UR3e. Takes end-effector position + rotation in robot base frame (ROS right-handed coordinates). Returns 6 joint angles in radians. Uses DH parameters verified from Universal Robots documentation (May 2026). No MonoBehaviour — call as a static utility. |
| `RobotBaseAnchor.cs` | Applies a 263° Y-rotation offset to align the virtual arm's base frame with the scene. Exposes `rosOffsetMetres` to shift the arm's XY position relative to the board. Adjust these values to match the physical robot placement. |
| `Hands/HandTracker.cs` | Interfaces with Unity XR Hands package. Tracks the selected hand's position, rotation, linear velocity, and angular velocity in world space. Reports tracking status. Outputs are consumed by `UR3TeleopDriver`. |
| `ROSInit.cs` | Demonstrates how to register ROS publishers and subscribers. Not part of gameplay — reference/example only. |

### ROS2 Topics

| Topic | Message Type | Direction (Unity) | Description |
|---|---|---|---|
| `/connect4/board_state` | `std_msgs/String` | **Subscribe** | Full board state from the Python game engine |
| `/connect4/ai_move` | `std_msgs/Int32` | **Subscribe** | Column index (0–6) chosen by the AI |
| `/connect4/player_move` | `std_msgs/Int32` | **Publish** | Column the VR player dropped their coin in |
| `/connect4/game_over` | `std_msgs/Int32` | **Subscribe** | Winner code: 1=player, 2=AI, 0=draw |
| `/connect4/reset` | `std_msgs/Bool` | **Subscribe** | Trigger a board reset in Unity |
| `/joint_states` | `sensor_msgs/JointState` | **Subscribe** | Real UR3e joint angles for arm mirroring |
| `/c4/teleop_pose` | `geometry_msgs/PoseStamped` | **Publish** *(in progress)* | Hand-derived EE target pose for UR3e teleoperation |

---

## Inputs and Outputs

**Inputs to Unity:**
- Hand tracking data from Meta Quest 2 via Unity XR Hands package
- ROS2 topic messages received over ROS-TCP-Connector: AI moves, board state, game over events, real robot joint states

**Outputs from Unity:**
- Human player coin drops → `/connect4/player_move`
- Teleop end-effector target → `/c4/teleop_pose` *(partially implemented)*
- Visual digital twin: virtual board updates, virtual UR3e arm mirroring the real arm in real time

---

## Dependencies

### Hardware

| Component | Specification |
|---|---|
| VR Headset | Meta Quest 2 |
| Connection method | Air Link (wireless, same WiFi network as PC) |
| Development PC | Windows 10/11, capable of running Unity 6 |
| ROS2 Machine | Ubuntu 22.04 laptop on the same local network |

### Software — Unity (Windows PC)

| Package | Version |
|---|---|
| **Unity Editor** | **6000.3.9f1** (must match exactly) |
| ROS-TCP-Connector | Latest from GitHub (`Unity-Technologies/ROS-TCP-Connector`) |
| URDF Importer | v0.5.2 from GitHub |
| XR Hands | 1.7.3 |
| XR Interaction Toolkit | 3.3.1 |
| OpenXR Plugin | 1.16.1 |
| XR Plugin Management | 4.5.4 |
| Universal Render Pipeline | 17.3.0 |
| Input System | 1.18.0 |
| ProBuilder | 6.0.9 |

All packages are declared in `VR/Packages/manifest.json` and install automatically when the project is opened in Unity.

### Software — ROS2 (Ubuntu 22.04)

| Package | Notes |
|---|---|
| ROS2 Humble | Base install |
| `ros_tcp_endpoint` | Bridge between Unity and ROS2 |

---

## Installation

### Part 1 — ROS2 Machine (Ubuntu 22.04)

#### Step 1 — Install ROS2 Humble

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y software-properties-common curl
sudo curl -sSL https://raw.githubusercontent.com/ros/rosdistro/master/ros.key \
    -o /usr/share/keyrings/ros-archive-keyring.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/ros-archive-keyring.gpg] \
    http://packages.ros.org/ros2/ubuntu $(. /etc/os-release && echo $UBUNTU_CODENAME) main" \
    | sudo tee /etc/apt/sources.list.d/ros2.list > /dev/null
sudo apt update
sudo apt install -y ros-humble-desktop
echo "source /opt/ros/humble/setup.bash" >> ~/.bashrc
source ~/.bashrc
```

Verify:
```bash
ros2 --version
# Expected: ros2 cli libraries version X.X.X
```

#### Step 2 — Build the ROS-TCP-Endpoint

```bash
mkdir -p ~/ros2_ws/src
cd ~/ros2_ws/src
git clone https://github.com/Unity-Technologies/ROS-TCP-Endpoint.git
cd ~/ros2_ws
colcon build
echo "source ~/ros2_ws/install/setup.bash" >> ~/.bashrc
source ~/.bashrc
```

#### Step 3 — Note your machine's IP address

```bash
ip addr show
```

Find the `inet` address on your WiFi interface (usually `wlan0` or similar). Write it down — you will need it in Unity. Example: `192.168.1.42`.

---

### Part 2 — Unity (Windows PC)

#### Step 4 — Install Unity Hub

Download Unity Hub from [https://unity.com/download](https://unity.com/download) and install it.

#### Step 5 — Install Unity Editor 6000.3.9f1

1. Open Unity Hub.
2. Click **Installs** → **Install Editor**.
3. Select version **6000.3.9f1** from the list (use the archive tab if it does not appear).
4. Under **Add modules**, check:
   - **Android Build Support** (required for future standalone Quest build)
   - **Android SDK & NDK Tools**
   - **OpenJDK**
5. Click **Install** and wait for it to complete.

#### Step 6 — Clone the repository

```bash
gh repo clone SaketNadimpalli/Robotics-Studio-2
```

#### Step 7 — Open the VR project in Unity Hub

1. In Unity Hub, click **Projects** → **Add** → **Add project from disk**.
2. Navigate to and select the `VR/` folder inside the cloned repository.
3. Confirm the Editor version is **6000.3.9f1**. If Unity Hub prompts to upgrade, click **Continue with current version** — do not upgrade.
4. Click to open the project. Unity will import all packages from `manifest.json` — this may take **5–15 minutes** on first open.

#### Step 8 — Configure the ROS connection

1. In the Unity menu bar, click **Robotics** → **ROS Settings**.
2. Set **Protocol** to `ROS2`.
3. Set **ROS IP Address** to the IP address of your Ubuntu machine from Step 3 (e.g. `192.168.1.42`).
4. Set **ROS Port** to `10000`.
5. Click **Apply** and close the settings window.

#### Step 9 — Set up Meta Quest 2 for Air Link

On the **Quest 2 headset:**
1. Put on the headset and go to **Settings** → **Experimental** → enable **Meta Quest Air Link**.

On the **Windows PC:**
1. Install the [Meta Quest Link app](https://www.meta.com/en-gb/help/quest/articles/headsets-and-accessories/oculus-link/meta-quest-link-app/).
2. Open the app and pair your Quest 2 following the on-screen steps.
3. In the headset, open the Air Link panel and select your PC to connect.

#### Step 10 — Verify XR settings in Unity

1. In Unity: **Edit** → **Project Settings** → **XR Plug-in Management**.
2. Under the **PC** tab, confirm **OpenXR** is checked.
3. Under the **Android** tab, confirm **OpenXR** is checked (for future standalone build).
4. No further XR configuration should be needed — the project is pre-configured.

---

## How to Run or Test Independently

### Step 1 — Start the ROS-TCP bridge (Ubuntu machine)

Open a terminal on the Ubuntu machine:

```bash
source /opt/ros/humble/setup.bash
source ~/ros2_ws/install/setup.bash
ros2 run ros_tcp_endpoint default_server_endpoint \
    --ros-args -p ROS_IP:=0.0.0.0 -p ROS_PORT:=10000
```

You should see:
```
[INFO] [...] [ROS_TCP_ENDPOINT]: Starting server on 0.0.0.0:10000
```

Leave this terminal open for the entire session.

### Step 2 — Open the scene in Unity

1. In the Unity **Project** panel, navigate to `Assets/Scenes/`.
2. Double-click the main game scene(`Connect4_Robot_arm_Scene`)
3. The scene should load showing the Connect 4 board, column trigger zones, and the virtual UR3e arm.

### Step 3 — Press Play

Click the **Play** button (▶) in the Unity Editor toolbar. In the Game view you should see the scene render. The console should print a message confirming connection to the ROS-TCP-Endpoint.

### Step 4 — Put on the headset

Connect the Quest 2 via Air Link (see Installation Step 9). The Unity scene will now render inside the headset.

**Expected behaviour in VR:**
- The Connect 4 board is visible in the scene.
- You can reach out and grab a coin using hand tracking, then drop it into a column.
- A successful drop triggers a publish on `/connect4/player_move`.
- If the full system is running, an AI coin appears automatically in response.
- The virtual UR3e arm mirrors the real robot's joint positions in real time.

### Testing without a physical robot

You can test the VR board interaction and coin mechanics without the UR3e by simulating messages from the Ubuntu machine:

```bash
# Simulate an AI coin drop in column 3 (0-indexed)
ros2 topic pub --once /connect4/ai_move std_msgs/msg/Int32 "data: 3"

# Simulate a game over (player 2 / AI wins)
ros2 topic pub --once /connect4/game_over std_msgs/msg/Int32 "data: 2"

# Trigger a board reset
ros2 topic pub --once /connect4/reset std_msgs/msg/Bool "data: true"
```

### Verify topics are flowing

On the Ubuntu machine, confirm Unity is publishing player moves:

```bash
ros2 topic echo /connect4/player_move
```

Drop a coin in Unity — you should see the column number print in the terminal.

---

## Configurable Settings and Parameters

| Setting | Where to change | Details |
|---|---|---|
| **ROS IP Address** | Unity menu → **Robotics → ROS Settings** | Set to IP of Ubuntu machine |
| **ROS Port** | Unity menu → **Robotics → ROS Settings** | Default: `10000` |
| **Coin snap delay** | `CoinSnap.cs` — `snapDelay` field | Currently `0.5f` seconds; reduce if coins appear to float too long |
| **Robot base rotation offset** | `RobotBaseAnchor.cs` — hardcoded `263°` Y-rotation | Adjust to match physical orientation of UR3e relative to the board |
| **Robot base XY offset** | `RobotBaseAnchor.cs` — `rosOffsetMetres` | Shift the virtual arm's position to align with the physical board |
| **Joint smoothing** | `UR3JointStateMirror.cs` — exponential smoothing alpha constant | Increase to smooth jitter; decrease for faster mirror response |
| **Preset arm poses** | `UR3PoseController.cs` — `poses[]` array | Edit or add joint angle arrays (values in radians, ROS convention) |
| **IK DH parameters** | `UR3eIKSolver.cs` lines 24–27 | Only modify if swapping to a different UR model. Current values are verified for UR3e. |
| **Teleop arm mode** | `UR3TeleopDriver.cs` — `armed` flag | Set to `true` at runtime (or wire to a UI button) to enable teleop; `false` reverts to mirror mode |

---

## Known Limitations and Assumptions

- **Air Link only** — The headset must be on the same WiFi network as the PC. Deploying a standalone APK to run directly on the Quest 2 (without a PC) is planned but not yet implemented. Building standalone requires additional Android SDK configuration in Unity.

- **ROS IP is not runtime-configurable** — The IP address is set in Unity Project Settings (Robotics → ROS Settings) and is baked in at edit time. If you move to a different network, you must stop Play mode, change the IP, and press Play again. There is no in-headset UI to change this.

- **Teleop pipeline is partially implemented** — `UR3TeleopDriver.cs` correctly computes IK solutions from hand pose and stores them in `SolvedAnglesRadians[]`, but the step that publishes those angles to `/c4/teleop_pose` is not yet wired up. The physical UR3e will not respond to hand movements until this publish step is completed and the Motion Planning subsystem subscribes to that topic.

- **The ROS-TCP-Endpoint must be running before pressing Play** — If Unity starts first, it will attempt to connect and fail silently. Always start the bridge on Ubuntu first, then press Play in Unity.

- **Board desync handling is basic** — `GameStateManager.cs` detects when the Unity coin positions disagree with the ROS board state, but recovery requires sending a `/connect4/reset` message manually. Automatic recovery is not yet implemented.

- **`UR3eIKSolver.cs` is UR3e-specific** — The DH parameters are hardcoded for the UR3e. Using this solver with a UR3, UR5, or UR10 without updating the parameters will produce incorrect joint angles.

- **Hand tracking is Quest 2 controller-free** — The system uses Unity XR Hands (hand tracking), not controller buttons. Ensure hand tracking is enabled in Quest 2 settings: **Settings → Movement Tracking → Hand and Body Tracking → Hand Tracking**.

- The main scene is `Assets/Scenes/Connect4_Robot_arm_Scene.unity`. Do not open the sample or recovery scenes (`SampleScene`, `_Recovery/`) as they are not part of the game.

- **No E-stop from VR** — There is currently no emergency stop button accessible from inside the VR environment. If the robot behaves unexpectedly during teleop, the physical E-stop on the UR3e teach pendant must be used.
