
import sys
import os
import pygame
import numpy as np

sys.stdout.reconfigure(encoding='utf-8')

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from game.connect4_env import Connect4
from agent.dqn_agent import DQNAgent
from visuals.renderer import Connect4Renderer

# ─────────────────────────────────────────
#  SETTINGS
# ─────────────────────────────────────────
BASE_DIR   = os.path.dirname(os.path.abspath(__file__))
MODEL_PATH = os.path.join(BASE_DIR, "models", "connect4_latest.pth")
HUMAN      = 2   # human is player 1
AI         = 1   # ai is player 2

def load_agent():
    agent         = DQNAgent()
    agent.load(MODEL_PATH)
    agent.epsilon = 0.0   # no exploration, pure exploitation!
    print(f"✅ Model loaded from {MODEL_PATH}")
    return agent

def ai_move(agent, game):
    """Get AI's best move directly from DQN agent"""
    valid_moves = game.get_valid_moves()
    return agent.select_action(game.board, AI, valid_moves)

def main():
    # ── Setup ─────────────────────────────
    game     = Connect4()
    renderer = Connect4Renderer(game)
    agent    = load_agent()

    print("\n🎮 Connect 4 — You vs AI!")
    print("You are Player 1 (RED)")
    print("AI  is Player 2 (YELLOW)")
    print("Click a column to make your move!")
    print("Press R to restart\n")

    running           = True
    game_over_printed = False

    while running:
        renderer.draw_board()

        # ── AI's turn ─────────────────────
        if not game.game_over and game.current_player == AI:
            pygame.time.wait(500)
            col = ai_move(agent, game)
            game.drop_piece(col)
            print(f"🤖 AI played column {col + 1}")

        # ── Event handling ─────────────────
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False

            # ── Restart ───────────────────
            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_r:
                    game.reset()
                    game_over_printed = False
                    print("\n🔄 Game restarted!")
                    print("You are Player 1 (RED)")
                    print("AI  is Player 2 (YELLOW)")

            # ── Human move ────────────────
            if event.type == pygame.MOUSEBUTTONDOWN:
                if not game.game_over and game.current_player == HUMAN:
                    mouse_x = event.pos[0]
                    col     = renderer.get_column_from_mouse(mouse_x)

                    if 0 <= col < game.COLS:
                        if game.is_valid_move(col):
                            game.drop_piece(col)
                            print(f"👤 You played column {col + 1}")
                        else:
                            print(f"⚠️ Column {col + 1} is full!")

        # ── Game over message ──────────────
        if game.game_over and not game_over_printed:
            if game.winner == HUMAN:
                print("🎉 You won! Press R to play again!")
            elif game.winner == AI:
                print("🤖 AI won! Press R to play again!")
            else:
                print("🤝 Draw! Press R to play again!")
            game_over_printed = True

    pygame.quit()

if __name__ == "__main__":
    main()
