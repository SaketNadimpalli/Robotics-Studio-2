
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
        total_wins         = 0
        games_per_opponent = max(1, num_games // len(self.cache))

        for opponent in self.cache:
            for i in range(games_per_opponent):
                board        = game.reset()
                done         = False
                agent_is_p1  = (i < games_per_opponent // 2)
                agent_player = 1 if agent_is_p1 else 2
                opp_player   = 2 if agent_is_p1 else 1

                while not done:
                    valid_moves = game.get_valid_moves()

                    if game.current_player == agent_player:
                        old_epsilon   = agent.epsilon
                        agent.epsilon = 0.0
                        action        = agent.select_action(board, agent_player, valid_moves)
                        agent.epsilon = old_epsilon
                    else:
                        action = opponent.select_action(board, opp_player, valid_moves)

                    board, _, done = game.step(action)

                # Did AGENT win? Don't care which player!
                if game.winner == agent_player:
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
            snapshot.target_net.load_state_dict(
                copy.deepcopy(agent.target_net.state_dict())
            )
            snapshot.epsilon = 0.0          # ← always best moves!
            snapshot.policy_net.eval()
            snapshot.target_net.eval()
            snapshot.device  = agent.device
            snapshot.policy_net.to(agent.device)
            snapshot.target_net.to(agent.device)
            self.cache.append(snapshot)
            print(f"  ✅ Added to cache! Size: {len(self.cache)}/{self.max_size}")
            return True
        else:
            print(f"  ❌ Win rate {win_rate:.1f}% < {threshold}% threshold")
            return False

    def __len__(self):
        return len(self.cache)
