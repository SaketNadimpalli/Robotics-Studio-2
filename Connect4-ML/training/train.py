
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import json
import numpy as np
from game.connect4_env import Connect4
from agent.dqn_agent import DQNAgent
from training.reward_shaper import get_winning_moves, scan_board, centre_reward, detect_fork_threat

# ─────────────────────────────────────────
#  HYPERPARAMETERS
# ─────────────────────────────────────────
EPISODES         = 100000
TARGET_UPDATE    = 10
PRINT_EVERY      = 500
CHECKPOINT_EVERY = 5000
REPLAY_EVERY     = 1000

# ─────────────────────────────────────────
#  PATHS
# ─────────────────────────────────────────
BASE_DIR       = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS_DIR     = os.path.join(BASE_DIR, "models")
LATEST_PATH    = os.path.join(MODELS_DIR, "connect4_latest.pth")   # ← always latest
BEST_PATH      = os.path.join(MODELS_DIR, "connect4_best.pth")     # ← best win rate
CHECKPOINT_DIR = os.path.join(MODELS_DIR, "checkpoints")
REPLAY_DIR     = os.path.join(MODELS_DIR, "replays")

# ─────────────────────────────────────────
#  CURRICULUM PHASES
# ─────────────────────────────────────────
def get_curriculum_phase(epsilon):
    """
    Returns current curriculum phase based on epsilon
    Phase 1: Basic survival        (ε > 0.7)
    Phase 2: Tactical awareness    (0.4 < ε < 0.7)
    Phase 3: Strategic thinking    (0.2 < ε < 0.4)
    Phase 4: Advanced strategy     (ε < 0.2)
    """
    if epsilon > 0.7:
        return 1
    elif epsilon > 0.4:
        return 2
    elif epsilon > 0.2:
        return 3
    else:
        return 4

# ─────────────────────────────────────────
#  EVALUATE FUNCTION
# ─────────────────────────────────────────
def evaluate(agent, game, num_games=200):
    wins = 0
    for _ in range(num_games):
        board = game.reset()
        done  = False
        while not done:
            valid_moves = game.get_valid_moves()
            if game.current_player == 1:
                old_epsilon   = agent.epsilon
                agent.epsilon = 0.0
                action        = agent.select_action(board, 1, valid_moves)
                agent.epsilon = old_epsilon
            else:
                action = np.random.choice(valid_moves)
            board, _, done = game.step(action)
        if game.winner == 1:
            wins += 1
    return wins / num_games * 100

# ─────────────────────────────────────────
#  SAVE REPLAY FUNCTION
# ─────────────────────────────────────────
def save_replay(agent, game, episode):
    """Play one game and save moves for later playback"""
    moves = []
    board = game.reset()
    done  = False

    while not done:
        valid_moves = game.get_valid_moves()

        if game.current_player == 1:
            old_epsilon   = agent.epsilon
            agent.epsilon = 0.0
            action        = agent.select_action(board, 1, valid_moves)
            agent.epsilon = old_epsilon
        else:
            action = np.random.choice(valid_moves)

        moves.append({
            'player': int(game.current_player),
            'action': int(action)
        })

        board, _, done = game.step(action)

    os.makedirs(REPLAY_DIR, exist_ok=True)
    replay_path = os.path.join(REPLAY_DIR, f"replay_ep{episode}.json")
    with open(replay_path, 'w') as f:
        json.dump({
            'episode': int(episode),
            'winner' : int(game.winner),
            'moves'  : moves
        }, f)

# ─────────────────────────────────────────
#  REWARD FUNCTION
# ─────────────────────────────────────────
def compute_reward(
    board, action, current_player,
    opp_win_moves, agent_win_moves,
    game, done, epsilon,
    blocks_made, blocks_missed
):
    phase    = get_curriculum_phase(epsilon)
    opponent = 2 if current_player == 1 else 1

    # ── Terminal rewards (always active) ──────────
    if done:
        if game.winner == 0:
            return 0.5, blocks_made, blocks_missed
        elif game.winner == current_player:
            return -1.0, blocks_made, blocks_missed
        else:
            return 1.0, blocks_made, blocks_missed

    reward = 0.0

    # ── Phase 1+ — Basic rewards ───────────────────
    # Centre control (always active)
    reward += centre_reward(board, current_player)

    # 2 in a row setup
    reward += scan_board(board, current_player) * 0.01

    # ── Phase 2+ — Tactical rewards ───────────────
    if phase >= 2:

        # Block immediate winning move
        if len(opp_win_moves) > 0:
            if action in opp_win_moves:
                reward      += 0.8
                blocks_made += 1
            else:
                if phase == 2:
                    reward        = -0.3   # soft penalty
                elif phase == 3:
                    reward        = -0.6   # medium penalty
                else:
                    reward        = -0.9   # heavy penalty
                blocks_missed += 1

        # Take winning move
        if action in agent_win_moves:
            reward = 0.9

    # ── Phase 3+ — Strategic rewards ──────────────
    if phase >= 3:

        # Fork threat detection
        fork_threats = detect_fork_threat(board, opponent)
        if len(fork_threats) > 0:
            if action in fork_threats:
                reward += 0.7    # ✅ blocked fork setup!
            else:
                if phase == 3:
                    reward -= 0.5    # medium fork penalty
                else:
                    reward -= 0.7    # heavy fork penalty

    # ── Phase 4 — Advanced rewards ─────────────────
    if phase >= 4:
        # Heavily penalise opponent 3 in a row
        reward += scan_board(board, opponent) * -0.2

    reward = float(np.clip(reward, -0.9, 0.9))
    return reward, blocks_made, blocks_missed

# ─────────────────────────────────────────
#  TRAIN FUNCTION
# ─────────────────────────────────────────
def train():
    game  = Connect4()
    agent = DQNAgent()

    if os.path.exists(LATEST_PATH):
        agent.load(LATEST_PATH)
        print(f"📂 Resuming from {LATEST_PATH}")
        print(f"   Epsilon: {agent.epsilon:.4f}")
        print(f"   Phase:   {get_curriculum_phase(agent.epsilon)}")
    else:
        print ("🆕 Starting fresh training!")


    # ── Tracking stats ────────────────────
    best_win_rate   = 0.0
    episode_rewards = []
    episode_losses  = []
    win_count       = 0
    draw_count      = 0
    loss_count      = 0
    blocks_made     = 0
    blocks_missed   = 0
    illegal_move_count = 0
    illegal_move_examples = []
    total_moves     = 0
    current_phase   = get_curriculum_phase(agent.epsilon)
    last_phase = current_phase

    os.makedirs(MODELS_DIR, exist_ok=True)

    print("🚀 Starting Training...")
    print(f"{'Episode':<10} {'Phase':<8} {'Winner':<10} {'Reward':<10} {'Epsilon':<10} {'Loss':<10} {'WinRate':<10} {'BlockRate':<10}")
    print("-" * 95)

    for episode in range(1, EPISODES + 1):
        board        = game.reset()
        total_reward = 0
        total_loss   = []
        done         = False

        while not done:
            valid_moves    = game.get_valid_moves()
            current_player = game.current_player  # ✅ store BEFORE step!

            if current_player == 1:
                action = agent.select_action(board, current_player, valid_moves)
            else:
                if np.random.rand() < 0.3:
                    action = np.random.choice(valid_moves)
                else:
                    action = agent.select_action(board, current_player, valid_moves)
        
        if action not in valid_moves:
            illegal_move_count += 1
            illegal_move_examples.append({
                'episode': episode,
                'player': current_player,
                'action': int(action),
                'valid_moves': valid_moves
            })
            action = np.random.choice(valid_moves)

            # ── Check threats BEFORE move ──────────
            opponent        = 2 if current_player == 1 else 1
            opp_win_moves   = get_winning_moves(game.board, opponent)
            agent_win_moves = get_winning_moves(game.board, current_player)

            next_board, _, done = game.step(action)
            total_moves        += 1

            # ── Curriculum Reward ──────────────────
            reward, blocks_made, blocks_missed = compute_reward(
                game.board, action, current_player,
                opp_win_moves, agent_win_moves,
                game, done, agent.epsilon,
                blocks_made, blocks_missed
            )

            # ── Store and train ────────────────────
            agent.remember(board, current_player, action, reward, next_board, done)
            loss = agent.train()
            if loss is not None:
                total_loss.append(loss)

            total_reward += reward
            board         = next_board

        # ── Track results ──────────────────────────
        if game.winner == 1:
            win_count += 1
        elif game.winner == 0:
            draw_count += 1
        else:
            loss_count += 1

        episode_rewards.append(total_reward)
        avg_loss = np.mean(total_loss) if total_loss else 0
        episode_losses.append(avg_loss)

        # ── Sync target network ────────────────────
        if episode % TARGET_UPDATE == 0:
            agent.update_target_network()

        # ── Save checkpoint ────────────────────────
        if episode % CHECKPOINT_EVERY == 0:
            os.makedirs(CHECKPOINT_DIR, exist_ok=True)
            checkpoint_path = os.path.join(CHECKPOINT_DIR, f"checkpoint_ep{episode}.pth")
            agent.save(checkpoint_path)
            print(f"  📌 Checkpoint saved at episode {episode}")

        # ── Save replay ────────────────────────────
        if episode % REPLAY_EVERY == 0:
            save_replay(agent, game, episode)

        # ── Print stats ────────────────────────────
        if episode % PRINT_EVERY == 0:
            avg_reward   = np.mean(episode_rewards[-PRINT_EVERY:])
            avg_loss     = np.mean(episode_losses[-PRINT_EVERY:])
            winner_str   = "Player 1" if game.winner == 1 else "Draw" if game.winner == 0 else "Player 2"
            win_rate     = evaluate(agent, game)
            block_rate   = blocks_made / max(1, blocks_made + blocks_missed) * 100
            current_phase = get_curriculum_phase(agent.epsilon)

            print(
                f"{episode:<10} "
                f"Phase {current_phase:<3} "
                f"{winner_str:<10} "
                f"{avg_reward:<10.3f} "
                f"{agent.epsilon:<10.3f} "
                f"{avg_loss:<10.4f} "
                f"{win_rate:<10.2f}% "
                f"{block_rate:<10.1f}%"
            )

            # ── Always save latest ─────────────────
            agent.save(LATEST_PATH)
            print(f"  💾 Latest model saved!")

            # ── Save best if improved ──────────────
            if win_rate > best_win_rate:
                best_win_rate = win_rate
                agent.save(BEST_PATH)
                print(f"  🏆 New best model! Win rate: {best_win_rate:.2f}%")

            # ── Notify phase change ────────────────

            if current_phase != last_phase:
                print(f"\n  🎓 Entering Phase {current_phase}!")
                last_phase = current_phase


            # ── Reset counters ─────────────────────
            blocks_made   = 0
            blocks_missed = 0
            total_moves   = 0

    # ── Final summary ──────────────────────────────
    total_episodes = win_count + draw_count + loss_count
    print("\n✅ Training Complete!")
    print(f"📊 Results over {total_episodes} episodes:")
    print(f"   Wins:   {win_count}  ({win_count/total_episodes*100:.1f}%)")
    print(f"   Draws:  {draw_count} ({draw_count/total_episodes*100:.1f}%)")
    print(f"   Losses: {loss_count} ({loss_count/total_episodes*100:.1f}%)")
    print(f"💾 Latest model: {LATEST_PATH}")
    print(f"🏆 Best model:   {BEST_PATH}")

if __name__ == "__main__":
    train()
