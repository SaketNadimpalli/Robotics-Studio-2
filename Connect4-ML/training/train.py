
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import numpy as np
from game.connect4_env import Connect4
from agent.dqn_agent import DQNAgent
from training.reward_shaper import shape_reward, get_winning_moves, scan_board, centre_reward

# ─────────────────────────────────────────
#  HYPERPARAMETERS
# ─────────────────────────────────────────
EPISODES      = 2000
TARGET_UPDATE = 10
SAVE_PATH     = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "models", "connect4_dqn.pth")
PRINT_EVERY   = 100

def evaluate(agent, game, num_games=200):
    wins = 0
    for _ in range(num_games):
        state = game.reset()
        done  = False
        while not done:
            valid_moves = game.get_valid_moves()
            if game.current_player == 1:
                old_epsilon   = agent.epsilon
                agent.epsilon = 0.0
                action        = agent.select_action(state, valid_moves)
                agent.epsilon = old_epsilon
            else:
                action = np.random.choice(valid_moves)
            state, _, done = game.step(action)
        if game.winner == 1:
            wins += 1
    return wins / num_games * 100

def train():
    game  = Connect4()
    agent = DQNAgent()

    best_win_rate = 0.0
    patience      = 10
    no_improve    = 0

    episode_rewards = []
    episode_losses  = []
    win_count       = 0
    draw_count      = 0
    loss_count      = 0

    print("🚀 Starting Training...")
    print(f"{'Episode':<10} {'Winner':<10} {'Reward':<10} {'Epsilon':<10} {'Loss':<10} {'WinRate':<10}")
    print("-" * 70)

    for episode in range(1, EPISODES + 1):
        state        = game.reset()
        total_reward = 0
        total_loss   = []
        done         = False

        while not done:
            valid_moves    = game.get_valid_moves()
            current_player = game.current_player  # ✅ store BEFORE step!

            if current_player == 1:
                action = agent.select_action(state, valid_moves)
            else:
                if np.random.rand() < 0.3:
                    action = np.random.choice(valid_moves)
                else:
                    action = agent.select_action(state, valid_moves)

            # ── Check threats BEFORE move executes ────────
            opponent        = 2 if current_player == 1 else 1
            opp_win_moves   = get_winning_moves(game.board, opponent)
            agent_win_moves = get_winning_moves(game.board, current_player)

            next_state, reward, done = game.step(action)

            # ── Reward Shaping ─────────────────────────────
            if done:
                if game.winner == 0:
                    reward = 0.5
                elif game.winner == current_player:
                    reward = -1.0
                else:
                    reward = 1.0
            else:
                # Base shaped reward
                reward  = scan_board(game.board, current_player) * 0.1
                reward += centre_reward(game.board, current_player)

                # Defensive reward using PRE-MOVE threats ✅
                if len(opp_win_moves) > 0:
                    if action in opp_win_moves:
                        reward += 0.6    # blocked winning move!
                    else:
                        reward -= 0.9    # missed block!

                # Offensive reward
                if action in agent_win_moves:
                    reward += 0.8        # took winning move!

                reward = float(np.clip(reward, -0.9, 0.9))

            agent.remember(state, action, reward, next_state, done)
            loss = agent.train()
            if loss is not None:
                total_loss.append(loss)

            total_reward += reward
            state         = next_state

        if game.winner == 1:
            win_count += 1
        elif game.winner == 0:
            draw_count += 1
        else:
            loss_count += 1

        episode_rewards.append(total_reward)
        avg_loss = np.mean(total_loss) if total_loss else 0
        episode_losses.append(avg_loss)

        if episode % TARGET_UPDATE == 0:
            agent.update_target_network()

        if episode % PRINT_EVERY == 0:
            avg_reward = np.mean(episode_rewards[-PRINT_EVERY:])
            avg_loss   = np.mean(episode_losses[-PRINT_EVERY:])
            winner_str = "Player 1" if game.winner == 1 else "Draw" if game.winner == 0 else "Player 2"
            win_rate   = evaluate(agent, game)

            print(
                f"{episode:<10} "
                f"{winner_str:<10} "
                f"{avg_reward:<10.3f} "
                f"{agent.epsilon:<10.3f} "
                f"{avg_loss:<10.4f} "
                f"{win_rate:.2f}%"
            )

            if win_rate > best_win_rate:
                best_win_rate = win_rate
                no_improve    = 0
                os.makedirs(os.path.dirname(SAVE_PATH), exist_ok=True)
                agent.save(SAVE_PATH)
                print(f"  💾 New best model! Win rate: {best_win_rate:.2f}%")
            else:
                no_improve += 1
                print(f"  ⚠️ No improvement {no_improve}/{patience}")

            if no_improve >= patience and episode >= 1000:
                print(f"\n🛑 Early stopping at episode {episode} — best win rate: {best_win_rate:.2f}%")
                break

    total_episodes = win_count + draw_count + loss_count
    print("\n✅ Training Complete!")
    print(f"📊 Results over {total_episodes} episodes:")
    print(f"   Wins:   {win_count}  ({win_count/total_episodes*100:.1f}%)")
    print(f"   Draws:  {draw_count} ({draw_count/total_episodes*100:.1f}%)")
    print(f"   Losses: {loss_count} ({loss_count/total_episodes*100:.1f}%)")
    print(f"💾 Best model saved to {SAVE_PATH}")

if __name__ == "__main__":
    train()
