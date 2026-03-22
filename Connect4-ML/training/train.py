
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import numpy as np
from game.connect4_env import Connect4
from agent.dqn_agent import DQNAgent

# ─────────────────────────────────────────
#  HYPERPARAMETERS
# ─────────────────────────────────────────
EPISODES        = 1000   # total games to train on
TARGET_UPDATE   = 10     # sync target network every N episodes
SAVE_PATH       = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "models", "connect4_dqn.pth")
PRINT_EVERY     = 50     # print stats every N episodes

def train():
    game  = Connect4()
    agent = DQNAgent()

    # Tracking stats
    episode_rewards = []
    episode_losses  = []
    win_count       = 0
    draw_count      = 0
    loss_count      = 0

    print("🚀 Starting Training...")
    print(f"{'Episode':<10} {'Winner':<10} {'Reward':<10} {'Epsilon':<10} {'Loss':<10}")
    print("-" * 55)

    for episode in range(1, EPISODES + 1):
        state       = game.reset()
        total_reward = 0
        total_loss   = []
        done         = False

        while not done:
            # ── Agent picks a move ──────────────────────────
            valid_moves = game.get_valid_moves()
            action      = agent.select_action(state, valid_moves)

            # ── Game executes move ──────────────────────────
            next_state, reward, done = game.step(action)

            # ── Flip reward for opponent's perspective ──────
            # When it's opponent's turn, a loss for them = win for agent
            if done and game.winner != 0:
                if game.winner == game.current_player:
                    reward = -1.0   # current player just lost
                else:
                    reward = 1.0    # current player just won
            else:
                reward = 0.0    # non-terminal move

            # ── Store experience ────────────────────────────
            agent.remember(state, action, reward, next_state, done)

            # ── Train on a batch ────────────────────────────
            loss = agent.train()
            if loss is not None:
                total_loss.append(loss)

            total_reward += reward
            state         = next_state

        # ── Track results ───────────────────────────────────
        if game.winner == 1:
            win_count += 1
        elif game.winner == 0:
            draw_count += 1
        else:
            loss_count += 1

        episode_rewards.append(total_reward)

        avg_loss = np.mean(total_loss) if total_loss else 0
        episode_losses.append(avg_loss)

        # ── Sync target network ─────────────────────────────
        if episode % TARGET_UPDATE == 0:
            agent.update_target_network()

        # ── Print stats ─────────────────────────────────────
        if episode % PRINT_EVERY == 0:
            avg_reward = np.mean(episode_rewards[-PRINT_EVERY:])
            avg_loss   = np.mean(episode_losses[-PRINT_EVERY:])
            winner_str = "Player 1" if game.winner == 1 else "Draw" if game.winner == 0 else "Player 2"

            print(
                f"{episode:<10} "
                f"{winner_str:<10} "
                f"{avg_reward:<10.3f} "
                f"{agent.epsilon:<10.3f} "
                f"{avg_loss:<10.4f}"
            )

    # ── Save model ──────────────────────────────────────────
    os.makedirs("models", exist_ok=True)
    agent.save(SAVE_PATH)
    print("\n✅ Training Complete!")
    print(f"📊 Results over {EPISODES} episodes:")
    print(f"   Wins:   {win_count}  ({win_count/EPISODES*100:.1f}%)")
    print(f"   Draws:  {draw_count} ({draw_count/EPISODES*100:.1f}%)")
    print(f"   Losses: {loss_count} ({loss_count/EPISODES*100:.1f}%)")
    print(f"💾 Model saved to {SAVE_PATH}")

if __name__ == "__main__":
    train()
