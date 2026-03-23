
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
PENALTY_MISS_BLOCK     = -0.9
BONUS_BLOCK_WIN        =  0.6
BONUS_WIN_MOVE         =  0.8


def score_window(window, player):
    opponent       = 2 if player == 1 else 1
    reward         = 0
    player_count   = window.count(player)
    opponent_count = window.count(opponent)
    empty_count    = window.count(0)

    if player_count == 3 and empty_count == 1:
        reward += REWARD_THREE_IN_A_ROW
    elif player_count == 2 and empty_count == 2:
        reward += REWARD_TWO_IN_A_ROW

    if opponent_count == 3 and empty_count == 1:
        reward += PENALTY_OPP_THREE
    elif opponent_count == 2 and empty_count == 2:
        reward += PENALTY_OPP_TWO

    return reward


def scan_board(board, player):
    ROWS   = 6
    COLS   = 7
    reward = 0

    for row in range(ROWS):
        for col in range(COLS - 3):
            window = list(board[row, col:col + 4])
            reward += score_window(window, player)

    for row in range(ROWS - 3):
        for col in range(COLS):
            window = [board[row + i][col] for i in range(4)]
            reward += score_window(window, player)

    for row in range(ROWS - 3):
        for col in range(COLS - 3):
            window = [board[row + i][col + i] for i in range(4)]
            reward += score_window(window, player)

    for row in range(3, ROWS):
        for col in range(COLS - 3):
            window = [board[row - i][col + i] for i in range(4)]
            reward += score_window(window, player)

    return reward


def centre_reward(board, player):
    reward      = 0
    centre_cols = [2, 3, 4]

    for col in centre_cols:
        for row in range(6):
            if board[row][col] == player:
                if col == 3:
                    reward += REWARD_CENTRE * 2
                else:
                    reward += REWARD_CENTRE

    return reward


def check_win_on_board(board, player):      # ✅ moved up!
    ROWS = 6
    COLS = 7

    for row in range(ROWS):
        for col in range(COLS - 3):
            if all(board[row][col + i] == player for i in range(4)):
                return True

    for row in range(ROWS - 3):
        for col in range(COLS):
            if all(board[row + i][col] == player for i in range(4)):
                return True

    for row in range(ROWS - 3):
        for col in range(COLS - 3):
            if all(board[row + i][col + i] == player for i in range(4)):
                return True

    for row in range(3, ROWS):
        for col in range(COLS - 3):
            if all(board[row - i][col + i] == player for i in range(4)):
                return True

    return False


def get_winning_moves(board, player):       # ✅ moved up!
    winning_cols = []
    ROWS         = 6
    COLS         = 7

    for col in range(COLS):
        if board[0][col] != 0:
            continue

        row = None
        for r in range(ROWS - 1, -1, -1):
            if board[r][col] == 0:
                row = r
                break

        if row is None:
            continue

        temp_board           = board.copy()
        temp_board[row][col] = player

        if check_win_on_board(temp_board, player):
            winning_cols.append(col)

    return winning_cols


def defensive_reward(board, action, player):    # ✅ moved up!
    opponent = 2 if player == 1 else 1
    reward   = 0

    opp_wins = get_winning_moves(board, opponent)

    if len(opp_wins) > 0:
        if action in opp_wins:
            reward += BONUS_BLOCK_WIN
        else:
            reward += PENALTY_MISS_BLOCK

    agent_wins = get_winning_moves(board, player)
    if action in agent_wins:
        reward += BONUS_WIN_MOVE

    return reward


def shape_reward(board, action, player):    # ✅ always last!
    reward  = 0
    reward += scan_board(board, player)
    reward += centre_reward(board, player)
    reward += defensive_reward(board, action, player)
    reward  = np.clip(reward, -0.9, 0.9)
    return reward
