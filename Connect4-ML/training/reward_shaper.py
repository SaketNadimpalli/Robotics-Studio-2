
import numpy as np

# ─────────────────────────────────────────
#  REWARD CONSTANTS
# ─────────────────────────────────────────
REWARD_WIN             =  1.0
REWARD_LOSS            = -1.0
REWARD_DRAW            =  0.5
REWARD_THREE_IN_A_ROW  =  0.5
REWARD_TWO_IN_A_ROW    =  0.1
REWARD_CENTRE          =  0.05
PENALTY_OPP_THREE      = -0.4
PENALTY_OPP_TWO        = -0.1


def score_window(window, player):
    """
    Score a window of 4 cells for a given player
    Positive = good for player
    Negative = bad for player (good for opponent)
    """
    opponent = 2 if player == 1 else 1
    reward   = 0

    player_count   = window.count(player)
    opponent_count = window.count(opponent)
    empty_count    = window.count(0)

    # ── Player sequences ──────────────────
    if player_count == 3 and empty_count == 1:
        reward += REWARD_THREE_IN_A_ROW   # 3 in a row setup!

    elif player_count == 2 and empty_count == 2:
        reward += REWARD_TWO_IN_A_ROW     # 2 in a row setup!

    # ── Opponent sequences (punish!) ──────
    if opponent_count == 3 and empty_count == 1:
        reward += PENALTY_OPP_THREE       # opponent has 3 in a row!

    elif opponent_count == 2 and empty_count == 2:
        reward += PENALTY_OPP_TWO         # opponent has 2 in a row!

    return reward


def scan_board(board, player):
    """
    Scan entire board in all 4 directions
    and sum up all window scores
    """
    ROWS    = 6
    COLS    = 7
    reward  = 0

    # ── Horizontal ────────────────────────
    for row in range(ROWS):
        for col in range(COLS - 3):
            window = list(board[row, col:col + 4])
            reward += score_window(window, player)

    # ── Vertical ──────────────────────────
    for row in range(ROWS - 3):
        for col in range(COLS):
            window = [board[row + i][col] for i in range(4)]
            reward += score_window(window, player)

    # ── Diagonal down-right ───────────────
    for row in range(ROWS - 3):
        for col in range(COLS - 3):
            window = [board[row + i][col + i] for i in range(4)]
            reward += score_window(window, player)

    # ── Diagonal up-right ─────────────────
    for row in range(3, ROWS):
        for col in range(COLS - 3):
            window = [board[row - i][col + i] for i in range(4)]
            reward += score_window(window, player)

    return reward


def centre_reward(board, player):
    """
    Reward for playing in the centre columns
    Centre is strategically the strongest position!
    """
    reward      = 0
    centre_cols = [2, 3, 4]   # columns 2, 3, 4 are centre

    for col in centre_cols:
        for row in range(6):
            if board[row][col] == player:
                if col == 3:                  # dead centre
                    reward += REWARD_CENTRE * 2
                else:                         # near centre
                    reward += REWARD_CENTRE

    return reward


def shape_reward(board, player):
    """
    Main reward shaping function
    Called every non-terminal move in train.py

    Returns a shaped reward based on:
    - Board position sequences (2/3 in a row)
    - Centre column control
    """
    reward  = 0
    reward += scan_board(board, player)
    reward += centre_reward(board, player)

    # Clip reward to keep it bounded
    # so it never overshadows win/loss rewards!
    reward  = np.clip(reward, -0.9, 0.9)

    return reward
