
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import numpy as np
import torch
import copy
from game.connect4_env import Connect4

# ─────────────────────────────────────────
#  MINIMAX WITH ALPHA-BETA PRUNING
# ─────────────────────────────────────────

def evaluate_board(board, player, agent):
    """
    Use trained neural network to evaluate
    how good a board position is for player!

    Returns value between -1 and +1:
    +1.0 = great for player
    -1.0 = terrible for player
     0.0 = neutral
    """
    state        = agent.prepare_state(board, player)
    state_tensor = torch.FloatTensor(state).unsqueeze(0).to(agent.device)

    agent.policy_net.eval()
    with torch.no_grad():
        q_values = agent.policy_net(state_tensor).squeeze()
    agent.policy_net.train()

    # Return max Q value as position evaluation
    # Higher = better position for this player!
    return q_values.max().item()


def get_valid_moves(board):
    """Get list of valid columns"""
    COLS = 7
    return [col for col in range(COLS) if board[0][col] == 0]


def simulate_move(board, col, player):
    """
    Simulate dropping a piece without
    modifying the original board!
    Returns new board after move
    """
    ROWS = 6
    new_board = board.copy()

    # Find lowest empty row in column
    for row in range(ROWS - 1, -1, -1):
        if new_board[row][col] == 0:
            new_board[row][col] = player
            return new_board

    return None  # column full


def check_winner(board, player):
    """
    Check if player has won on this board
    Same logic as connect4_env but standalone
    """
    ROWS = 6
    COLS = 7

    # Horizontal
    for row in range(ROWS):
        for col in range(COLS - 3):
            if all(board[row][col + i] == player for i in range(4)):
                return True

    # Vertical
    for row in range(ROWS - 3):
        for col in range(COLS):
            if all(board[row + i][col] == player for i in range(4)):
                return True

    # Diagonal down-right
    for row in range(ROWS - 3):
        for col in range(COLS - 3):
            if all(board[row + i][col + i] == player for i in range(4)):
                return True

    # Diagonal up-right
    for row in range(3, ROWS):
        for col in range(COLS - 3):
            if all(board[row - i][col + i] == player for i in range(4)):
                return True

    return False


def minimax(board, depth, is_maximising, alpha, beta, agent, ai_player):
    """
    Minimax with Alpha-Beta Pruning!

    board:          current board state
    depth:          how many more plies to search
    is_maximising:  True = AI's turn (maximise)
                    False = opponent's turn (minimise)
    alpha:          best score AI can guarantee
    beta:           best score opponent can guarantee
    agent:          our trained DQNAgent
    ai_player:      which player number is the AI (1 or 2)

    Returns: best score for this position
    """
    opponent = 2 if ai_player == 1 else 1

    # ── Terminal conditions ────────────────────────

    # Check if AI won
    if check_winner(board, ai_player):
        return 1.0 * (depth + 1)  # ← depth bonus rewards faster wins!

    # Check if opponent won
    if check_winner(board, opponent):
        return -1.0 * (depth + 1)  # ← depth penalty for slower losses!

    # Check draw (no valid moves)
    valid_moves = get_valid_moves(board)
    if len(valid_moves) == 0:
        return 0.0  # draw

    # ── Base case — evaluate with neural network ───
    if depth == 0:
        if is_maximising:
            return evaluate_board(board, ai_player, agent)
        else:
            return -evaluate_board(board, opponent, agent)

    # ── Recursive case ─────────────────────────────

    if is_maximising:
        # AI's turn → maximise score
        best_score = float('-inf')

        for col in valid_moves:
            # Simulate AI playing this column
            new_board = simulate_move(board, col, ai_player)
            if new_board is None:
                continue

            # Recurse — now opponent's turn
            score = minimax(
                new_board, depth - 1,
                False,          # ← opponent's turn next
                alpha, beta,
                agent, ai_player
            )

            best_score = max(best_score, score)
            alpha      = max(alpha, score)

            # ── Alpha-Beta Pruning ─────────────────
            if beta <= alpha:
                break  # prune! opponent won't allow this ✂️

        return best_score

    else:
        # Opponent's turn → minimise AI's score
        best_score = float('inf')

        for col in valid_moves:
            # Simulate opponent playing this column
            new_board = simulate_move(board, col, opponent)
            if new_board is None:
                continue

            # Recurse — now AI's turn again
            score = minimax(
                new_board, depth - 1,
                True,           # ← AI's turn next
                alpha, beta,
                agent, ai_player
            )

            best_score = min(best_score, score)
            beta       = min(beta, score)

            # ── Alpha-Beta Pruning ─────────────────
            if beta <= alpha:
                break  # prune! AI won't allow this ✂️

        return best_score


def minimax_search(board, agent, ai_player, depth=4):
    """
    Main function called from play.py!

    Finds the best column to play using
    4-ply minimax search with neural network evaluation

    Returns: best column (0-6)
    """
    valid_moves = get_valid_moves(board)

    # Edge case — only one move available
    if len(valid_moves) == 1:
        return valid_moves[0]

    best_score  = float('-inf')
    best_col    = valid_moves[0]
    alpha       = float('-inf')
    beta        = float('inf')

    print(f"\n🔍 Minimax search (depth={depth}):")

    for col in valid_moves:
        # Simulate AI playing this column
        new_board = simulate_move(board, col, ai_player)
        if new_board is None:
            continue

        # Evaluate this move with minimax
        score = minimax(
            new_board, depth - 1,
            False,      # ← opponent's turn after AI plays
            alpha, beta,
            agent, ai_player
        )

        print(f"  Column {col + 1}: score = {score:.4f}")

        if score > best_score:
            best_score = score
            best_col   = col

        alpha = max(alpha, score)

    print(f"  → Best column: {best_col + 1} (score={best_score:.4f})")
    return best_col
