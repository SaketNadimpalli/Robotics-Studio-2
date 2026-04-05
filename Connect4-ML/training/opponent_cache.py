
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import numpy as np
import random
import copy
from collections import deque
from agent.dqn_agent import DQNAgent

class RandomBot:
    """Completely random opponent for bootstrapping"""
    epsilon = 0.0

    def select_action(self, board, player, valid_moves):
        return np.random.choice(valid_moves)


class OpponentCache:
    def __init__(self, max_size=10, win_threshold=55.0):
        self.cache         = deque(maxlen=max_size)
        self.win_threshold = win_threshold
        self.max_size      = max_size

        # Always start with random bot!
        self.cache.append(RandomBot())
        print(f"🎮 Cache initialised with RandomBot!")

    def select_opponent(self):
        """Randomly select opponent from cache"""
        return random.choice(list(self.cache))

    def evaluate_vs_cache(self, agent, game, num_games=200):
        """
        Evaluate agent against ALL opponents in cache
        Returns average win rate
        """
        if len(self.cache) == 0:
            return 0.0

        total_wins          = 0
        games_per_opponent  = max(1, num_games // len(self.cache))

        for opponent in self.cache:
            for _ in range(games_per_opponent):
                board = game.reset()
                done  = False

                while not done:
                    valid_moves = game.get_valid_moves()

                    if game.current_player == 1:
                        # Our agent plays as Player 1
                        old_epsilon   = agent.epsilon
                        agent.epsilon = 0.0
                        action        = agent.select_action(board, 1, valid_moves)
                        agent.epsilon = old_epsilon
                    else:
                        # Opponent plays as Player 2
                        action = opponent.select_action(board, 2, valid_moves)

                    board, _, done = game.step(action)

                if game.winner == 1:
                    total_wins += 1

        total_games = games_per_opponent * len(self.cache)
        return total_wins / total_games * 100

    def try_add_to_cache(self, agent, win_rate, epsilon):
        """
        Add snapshot of agent to cache if win rate > threshold
        Remove weakest if cache full
        """
        if epsilon > 0.7:
            threshold = 40.0
        elif epsilon > 0.4:
            threshold = 50.0
        else:
            threshold = 55.0
        if win_rate >= threshold:
            # Create frozen snapshot of current agent
            snapshot         = DQNAgent()
            snapshot.policy_net.load_state_dict(
                copy.deepcopy(agent.policy_net.state_dict())
            )
            snapshot.epsilon = 0.0          # ← always best moves!
            snapshot.policy_net.eval()      # ← no dropout!
            snapshot.device  = agent.device
            snapshot.policy_net.to(agent.device)

            self.cache.append(snapshot)
            print(f"  ✅ Added to cache! Size: {len(self.cache)}/{self.max_size}")
            return True
        else:
            print(f"  ❌ Win rate {win_rate:.1f}% < {threshold}% threshold")
            return False

    def __len__(self):
        return len(self.cache)
