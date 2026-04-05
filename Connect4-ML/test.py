
import sys
import os
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from game.connect4_env import Connect4
from agent.dqn_agent import DQNAgent
from search.minimax import minimax_search

# ── Full path to model ──────────────────
BASE_DIR   = os.path.dirname(os.path.abspath(__file__))
MODEL_PATH = os.path.join(BASE_DIR, "models", "connect4_best.pth")

# Load trained agent
agent = DQNAgent()
agent.load(MODEL_PATH)  # ← full path! ✅
agent.epsilon = 0.0

game  = Connect4()
board = game.reset()

print("Testing minimax search on empty board...")
best_col = minimax_search(board, agent, ai_player=1, depth=4)
print(f"\n✅ Best column on empty board: {best_col + 1}")
print("Should prefer centre columns (3, 4, 5)!")
