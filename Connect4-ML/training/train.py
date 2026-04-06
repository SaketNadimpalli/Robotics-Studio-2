
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import copy
import json
import numpy as np
from game.connect4_env import Connect4
from agent.dqn_agent import DQNAgent
from training.reward_shaper import get_winning_moves, scan_board, centre_reward, detect_fork_threat
from training.opponent_cache import OpponentCache

# ─────────────────────────────────────────
#  HYPERPARAMETERS
# ─────────────────────────────────────────

EPISODES         = 2000  
TARGET_UPDATE    = 10    
PRINT_EVERY      = 500    
CHECKPOINT_EVERY = 10000   
REPLAY_EVERY     = 2000     
CYCLE_EPISODES   = 500    
WIN_THRESHOLD    = 40.0     
CACHE_SIZE       = 10       
GAMMA            = 0.95     

# ─────────────────────────────────────────
#  PATHS
# ─────────────────────────────────────────
BASE_DIR       = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS_DIR     = os.path.join(BASE_DIR, "models")
LATEST_PATH    = os.path.join(MODELS_DIR, "connect4_latest.pth")
BEST_PATH      = os.path.join(MODELS_DIR, "connect4_best.pth")
CHECKPOINT_DIR = os.path.join(MODELS_DIR, "checkpoints")
REPLAY_DIR     = os.path.join(MODELS_DIR, "replays")

# ─────────────────────────────────────────
#  CURRICULUM PHASES
# ─────────────────────────────────────────
def get_curriculum_phase(epsilon):
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
    """
    Test agent as BOTH P1 and P2
    Count wins regardless of which player!
    Simply: did the agent win?
    """
    wins = 0
    for i in range(num_games):
        board       = game.reset()
        done        = False
        agent_is_p1 = (i < num_games // 2)
        agent_player = 1 if agent_is_p1 else 2

        while not done:
            valid_moves = game.get_valid_moves()

            if game.current_player == agent_player:
                # Agent's turn
                old_eps       = agent.epsilon
                agent.epsilon = 0.0
                action        = agent.select_action(board, agent_player, valid_moves)
                agent.epsilon = old_eps
            else:
                # Random opponent's turn
                action = np.random.choice(valid_moves)

            board, _, done = game.step(action)

        # Did AGENT win? Don't care which player!
        if game.winner == agent_player:
            wins += 1

    return wins / num_games * 100

# ─────────────────────────────────────────
#  SAVE REPLAY FUNCTION
# ─────────────────────────────────────────
def save_replay(agent, game, episode):
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

    if done:
        if game.winner == 0:
            return 0.5, blocks_made, blocks_missed
        elif game.winner == current_player:
            return -1.0, blocks_made, blocks_missed
        else:
            return 1.0, blocks_made, blocks_missed

    reward = 0.0
    reward += centre_reward(board, current_player) * 2
    reward += scan_board(board, current_player) * 0.05

    if phase >= 2:
        if len(opp_win_moves) > 0:
            if action in opp_win_moves:
                reward      += 0.8
                blocks_made += 1
            else:
                if phase == 2:
                    reward        = -0.3
                elif phase == 3:
                    reward        = -0.6
                else:
                    reward        = -0.9
                blocks_missed += 1

        if action in agent_win_moves:
            reward = 0.9

    if phase >= 3:
        fork_threats = detect_fork_threat(board, opponent)
        if len(fork_threats) > 0:
            if action in fork_threats:
                reward += 0.7
            else:
                if phase == 3:
                    reward -= 0.5
                else:
                    reward -= 0.7

    if phase >= 4:
        reward += scan_board(board, opponent) * -0.2

    reward = float(np.clip(reward, -0.9, 0.9))
    return reward, blocks_made, blocks_missed

# ─────────────────────────────────────────
#  4-STEP RETURN COMPUTATION
# ─────────────────────────────────────────
def compute_4step_return(
    board,
    action,
    r1,
    done1,
    game,
    agent,
    current_opponent,
    gamma,
    current_player    # ← knows whose perspective!
):
    opponent_player = 2 if current_player == 1 else 1

    if done1:
        return r1, game.board.copy(), done1

    game_copy = copy.deepcopy(game)

    # ── Move 2 — OTHER player ──────────────────────
    valid_moves_2 = game_copy.get_valid_moves()
    if not valid_moves_2:
        return r1, game_copy.board.copy(), True

    # Other player moves
    if game_copy.current_player == current_player:
        action_2 = agent.select_action(game_copy.board, current_player, valid_moves_2)
    else:
        action_2 = current_opponent.select_action(game_copy.board, opponent_player, valid_moves_2)

    opp_win_2   = get_winning_moves(game_copy.board, opponent_player)
    agent_win_2 = get_winning_moves(game_copy.board, current_player)
    _, _, done2 = game_copy.step(action_2)

    r2, _, _ = compute_reward(
        game_copy.board, action_2, opponent_player,
        opp_win_2, agent_win_2,
        game_copy, done2, agent.epsilon,
        0, 0
    )

    if done2:
        four_step = r1 - gamma * r2
        return float(np.clip(four_step, -2.0, 2.0)), game_copy.board.copy(), done2

    # ── Move 3 — AGENT again ───────────────────────
    valid_moves_3 = game_copy.get_valid_moves()
    if not valid_moves_3:
        four_step = r1 - gamma * r2
        return float(np.clip(four_step, -2.0, 2.0)), game_copy.board.copy(), True

    action_3    = agent.select_action(game_copy.board, current_player, valid_moves_3)
    opp_win_3   = get_winning_moves(game_copy.board, opponent_player)
    agent_win_3 = get_winning_moves(game_copy.board, current_player)
    _, _, done3 = game_copy.step(action_3)

    r3, _, _ = compute_reward(
        game_copy.board, action_3, current_player,
        opp_win_3, agent_win_3,
        game_copy, done3, agent.epsilon,
        0, 0
    )

    if done3:
        four_step = r1 - gamma * r2 + gamma**2 * r3
        return float(np.clip(four_step, -2.0, 2.0)), game_copy.board.copy(), done3

    # ── Move 4 — OTHER player again ────────────────
    valid_moves_4 = game_copy.get_valid_moves()
    if not valid_moves_4:
        four_step = r1 - gamma * r2 + gamma**2 * r3
        return float(np.clip(four_step, -2.0, 2.0)), game_copy.board.copy(), True

    if game_copy.current_player == current_player:
        action_4 = agent.select_action(game_copy.board, current_player, valid_moves_4)
    else:
        action_4 = current_opponent.select_action(game_copy.board, opponent_player, valid_moves_4)

    opp_win_4   = get_winning_moves(game_copy.board, opponent_player)
    agent_win_4 = get_winning_moves(game_copy.board, current_player)
    _, _, done4 = game_copy.step(action_4)

    r4, _, _ = compute_reward(
        game_copy.board, action_4, opponent_player,
        opp_win_4, agent_win_4,
        game_copy, done4, agent.epsilon,
        0, 0
    )

    four_step = (
          r1
        - gamma    * r2
        + gamma**2 * r3
        - gamma**3 * r4
    )

    return float(np.clip(four_step, -2.0, 2.0)), game_copy.board.copy(), done4

# ─────────────────────────────────────────
#  TRAIN FUNCTION
# ─────────────────────────────────────────
def train():
    game           = Connect4()
    agent          = DQNAgent()
    opponent_cache = OpponentCache(
        max_size      = CACHE_SIZE,
        win_threshold = WIN_THRESHOLD
    )

    if os.path.exists(LATEST_PATH):
        agent.load(LATEST_PATH)
        print(f"📂 Resuming from {LATEST_PATH}")
        print(f"   Epsilon: {agent.epsilon:.4f}")
        print(f"   Phase:   {get_curriculum_phase(agent.epsilon)}")
    else:
        print("🆕 Starting fresh training!")

    # ── Tracking stats ────────────────────
    best_win_rate         = 0.0
    episode_rewards       = []
    episode_losses        = []
    win_count             = 0
    draw_count            = 0
    loss_count            = 0
    blocks_made           = 0
    blocks_missed         = 0
    illegal_move_count    = 0
    illegal_move_examples = []
    total_moves           = 0
    total_episodes        = 0
    current_phase         = get_curriculum_phase(agent.epsilon)
    last_phase            = current_phase

    os.makedirs(MODELS_DIR, exist_ok=True)

    print("🚀 Starting Training...")
    print(f"{'Episode':<10} {'Phase':<8} {'Winner':<10} {'Reward':<10} {'Epsilon':<10} {'Loss':<10} {'WinRate':<10} {'BlockRate':<10} {'CacheSize':<10}")
    print("-" * 110)

    while total_episodes < EPISODES:

        current_opponent = opponent_cache.select_opponent()
        opponent_name    = type(current_opponent).__name__
        print(f"\n🎮 New cycle vs {opponent_name} | Cache size: {len(opponent_cache)}")

        cycle_rewards = []
        cycle_losses  = []

        for cycle_ep in range(1, CYCLE_EPISODES + 1):
            board        = game.reset()
            total_reward = 0
            total_loss   = []
            done         = False

            # ── CHANGE 1 — Alternate who agent plays as! ──
            agent_is_p1     = (cycle_ep % 2 == 1)  # odd=P1, even=P2
            agent_player    = 1 if agent_is_p1 else 2
            opponent_player = 2 if agent_is_p1 else 1

            while not done:
                valid_moves    = game.get_valid_moves()
                current_player = game.current_player

                # ── CHANGE 2 — Agent plays as correct player!
                if current_player == agent_player:
                    action = agent.select_action(board, agent_player, valid_moves)
                else:
                    action = current_opponent.select_action(board, opponent_player, valid_moves)

                # ── Illegal move check ─────────────────
                if action not in valid_moves:
                    illegal_move_count += 1
                    illegal_move_examples.append({
                        'episode'    : total_episodes,
                        'player'     : current_player,
                        'action'     : int(action),
                        'valid_moves': valid_moves
                    })
                    action = np.random.choice(valid_moves)

                # ── Check threats BEFORE move ──────────
                opp_num         = opponent_player if current_player == agent_player else agent_player
                opp_win_moves   = get_winning_moves(game.board, opp_num)
                agent_win_moves = get_winning_moves(game.board, current_player)

                # ── Execute move ───────────────────────
                next_board, _, done = game.step(action)
                total_moves        += 1

                # ── Compute reward ─────────────────────
                reward, blocks_made, blocks_missed = compute_reward(
                    game.board, action, current_player,
                    opp_win_moves, agent_win_moves,
                    game, done, agent.epsilon,
                    blocks_made, blocks_missed
                )

                # ── CHANGE 3 — Train when AGENT moves! ────
                if current_player == agent_player:
                    four_step_return, board_4, done_4 = compute_4step_return(
                        board, action, reward, done,
                        game, agent, current_opponent,
                        gamma          = GAMMA,
                        current_player = agent_player  # ← correct perspective!
                    )

                    agent.remember(board, agent_player, action, four_step_return, board_4, done_4)
                    loss = agent.train()
                    if loss is not None:
                        total_loss.append(loss)

                total_reward += reward
                board         = next_board

            # ── Track results ──────────────────────────
            # Track from agent's perspective!
            if game.winner == agent_player:
                win_count += 1
            elif game.winner == 0:
                draw_count += 1
            else:
                loss_count += 1

            total_episodes  += 1
            episode_rewards.append(total_reward)
            avg_loss = np.mean(total_loss) if total_loss else 0
            episode_losses.append(avg_loss)
            cycle_rewards.append(total_reward)
            cycle_losses.append(avg_loss)

            if total_episodes % TARGET_UPDATE == 0:
                agent.update_target_network()

            if total_episodes % CHECKPOINT_EVERY == 0:
                os.makedirs(CHECKPOINT_DIR, exist_ok=True)
                checkpoint_path = os.path.join(CHECKPOINT_DIR, f"checkpoint_ep{total_episodes}.pth")
                agent.save(checkpoint_path)
                print(f"  📌 Checkpoint saved at episode {total_episodes}")

            if total_episodes % REPLAY_EVERY == 0:
                save_replay(agent, game, total_episodes)

            if total_episodes % PRINT_EVERY == 0:
                avg_reward    = np.mean(episode_rewards[-PRINT_EVERY:])
                avg_loss      = np.mean(episode_losses[-PRINT_EVERY:])
                winner_str    = "Player 1" if game.winner == 1 else "Draw" if game.winner == 0 else "Player 2"
                win_rate      = evaluate(agent, game)
                block_rate    = blocks_made / max(1, blocks_made + blocks_missed) * 100
                current_phase = get_curriculum_phase(agent.epsilon)

                print(
                    f"{total_episodes:<10} "
                    f"Phase {current_phase:<3} "
                    f"{winner_str:<10} "
                    f"{avg_reward:<10.3f} "
                    f"{agent.epsilon:<10.3f} "
                    f"{avg_loss:<10.4f} "
                    f"{win_rate:<10.2f}% "
                    f"{block_rate:<10.1f}% "
                    f"{len(opponent_cache):<10}"
                )

                agent.save(LATEST_PATH)
                print(f"  💾 Latest model saved!")

                if win_rate > best_win_rate:
                    best_win_rate = win_rate
                    agent.save(BEST_PATH)
                    print(f"  🏆 New best model! Win rate: {best_win_rate:.2f}%")

                if current_phase != last_phase:
                    print(f"\n  🎓 Entering Phase {current_phase}!")
                    last_phase = current_phase

                blocks_made   = 0
                blocks_missed = 0
                total_moves   = 0

        print(f"\n📊 Cycle complete! Evaluating vs cache...")
        cache_win_rate = opponent_cache.evaluate_vs_cache(agent, game)
        print(f"   Win rate vs cache: {cache_win_rate:.1f}%")
        opponent_cache.try_add_to_cache(agent, cache_win_rate, agent.epsilon)

    total_ep = win_count + draw_count + loss_count
    print("\n✅ Training Complete!")
    print(f"📊 Results over {total_ep} episodes:")
    print(f"   Wins:   {win_count}  ({win_count/total_ep*100:.1f}%)")
    print(f"   Draws:  {draw_count} ({draw_count/total_ep*100:.1f}%)")
    print(f"   Losses: {loss_count} ({loss_count/total_ep*100:.1f}%)")
    print(f"💾 Latest model: {LATEST_PATH}")
    print(f"🏆 Best model:   {BEST_PATH}")

if __name__ == "__main__":
    train()
