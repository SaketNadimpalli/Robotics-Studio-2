
import sys
import os
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pygame
from game.connect4_env import Connect4
from visuals.renderer import Connect4Renderer

# ─────────────────────────────────────────
#  COLOURS
# ─────────────────────────────────────────
WHITE  = (255, 255, 255)
BLACK  = (0,   0,   0  )
RED    = (220, 50,  50 )
GREEN  = (50,  200, 50 )
YELLOW = (255, 220, 0  )
ORANGE = (255, 140, 0  )
GREY   = (40,  40,  40 )

# ─────────────────────────────────────────
#  DRAW TEST SUMMARY SCREEN
# ─────────────────────────────────────────
def draw_summary(screen, font_big, font_med, font_small,
                 total_attempts, illegal_count, illegal_examples, winner, scroll_y=0):
    screen.fill(GREY)
    legal_count = total_attempts - illegal_count

    # ── Apply scroll offset to all y positions ──
    y_offset = -scroll_y

    # ── Title ──────────────────────────────
    title = font_big.render("SUBSYSTEM TEST RESULT", True, WHITE)
    screen.blit(title, (screen.get_width() // 2 - title.get_width() // 2, 30 + y_offset))

    # ── Winner ─────────────────────────────
    if winner == 0:
        winner_text = "Game Result: Draw"
        wc = YELLOW
    else:
        winner_text = f"Game Result: Player {winner} Wins!"
        wc = RED if winner == 1 else YELLOW
    wlabel = font_med.render(winner_text, True, wc)
    screen.blit(wlabel, (screen.get_width() // 2 - wlabel.get_width() // 2, 90 + y_offset))

    # ── Divider ────────────────────────────
    pygame.draw.line(screen, WHITE, (50, 140 + y_offset), (screen.get_width() - 50, 140 + y_offset), 2)

    # ── Stats ──────────────────────────────
    stats = [
        f"Total move attempts :  {total_attempts}",
        f"Legal moves made    :  {legal_count}",
        f"Illegal moves caught:  {illegal_count}",
    ]
    y = 160 + y_offset
    for stat in stats:
        label = font_med.render(stat, True, WHITE)
        screen.blit(label, (80, y))
        y += 45

    # ── Divider ────────────────────────────
    pygame.draw.line(screen, WHITE, (50, y + 5), (screen.get_width() - 50, y + 5), 2)
    y += 25

    # ── Pass result ────────────────────────
    if illegal_count == 0:
        result_lines = ["TEST PASS", "No illegal moves were attempted."]
    else:
        result_lines = ["TEST PASS", f"All {illegal_count} illegal move(s) were detected and rejected."]
    for line in result_lines:
        rl = font_med.render(line, True, GREEN)
        screen.blit(rl, (screen.get_width() // 2 - rl.get_width() // 2, y))
        y += 45

    # ── Illegal move log ───────────────────
    if illegal_examples:
        pygame.draw.line(screen, ORANGE, (50, y + 5), (screen.get_width() - 50, y + 5), 1)
        y += 20
        hdr = font_small.render("Illegal Move Log:", True, ORANGE)
        screen.blit(hdr, (80, y))
        y += 30
        for i, ex in enumerate(illegal_examples):
            txt = (f"  #{i+1}  Player {ex['player']} tried column {ex['action']}"
                   f"   |   Valid were: {ex['valid_moves']}")
            lbl = font_small.render(txt, True, ORANGE)
            screen.blit(lbl, (80, y))
            y += 28

    # ── Scroll hint pinned to bottom ───────
    hint = font_small.render("Scroll to see more  |  Close window to exit", True, (150, 150, 150))
    screen.blit(hint, (screen.get_width() // 2 - hint.get_width() // 2,
                        screen.get_height() - 30))

    pygame.display.update()

# ─────────────────────────────────────────
#  MAIN PVP FUNCTION
# ─────────────────────────────────────────
def pvp_test():
    game     = Connect4()
    board    = game.reset()
    renderer = Connect4Renderer(game)

    # ── Fonts ──────────────────────────────
    font_big   = pygame.font.SysFont("arial", 36, bold=True)
    font_med   = pygame.font.SysFont("arial", 26)
    font_small = pygame.font.SysFont("arial", 20)

    # ── Test counters ──────────────────────
    illegal_move_count  = 0
    total_move_attempts = 0
    illegal_examples    = []

    renderer.draw_board()

    # ─────────────────────────────────────
    #  GAME LOOP
    # ─────────────────────────────────────
    running = True
    while running:
        for event in pygame.event.get():

            # ── Quit ──────────────────────
            if event.type == pygame.QUIT:
                pygame.quit()
                sys.exit()

            # ── Mouse click = move ─────────
            if event.type == pygame.MOUSEBUTTONDOWN and not game.game_over:
                pos_x       = event.pos[0]
                action      = renderer.get_column_from_mouse(pos_x)
                valid_moves = game.get_valid_moves()

                total_move_attempts += 1

                # ── ILLEGAL MOVE DETECTION (THE TEST) ──
                if action not in valid_moves:
                    illegal_move_count += 1
                    illegal_examples.append({
                        "player"     : game.current_player,
                        "action"     : action,
                        "valid_moves": list(valid_moves)
                    })
                    print()
                    print("  ┌─────────────────────────────────────────┐")
                    print(f"  │  🚫 ILLEGAL MOVE DETECTED!              │")
                    print(f"  │  Column {action} is not a valid move.          │")
                    print(f"  │  Valid columns : {list(valid_moves)}     │")
                    print(f"  │  Illegal moves caught so far: {illegal_move_count}         │")
                    print("  └─────────────────────────────────────────┘")
                    print()
                    continue

                # ── Legal move — execute it ────────────
                board, _, done = game.step(action)
                renderer.draw_board()

                # ── Game over → show summary ───────────
                if done:
                    scroll_y     = 0
                    SCROLL_SPEED = 20

                    draw_summary(
                        renderer.screen,
                        font_big, font_med, font_small,
                        total_move_attempts,
                        illegal_move_count,
                        illegal_examples,
                        game.winner,
                        scroll_y
                    )

                    # ── Summary screen loop with scrolling ──
                    while True:
                        for e in pygame.event.get():

                            if e.type == pygame.QUIT:
                                pygame.quit()
                                sys.exit()

                            if e.type == pygame.MOUSEWHEEL:
                                scroll_y -= e.y * SCROLL_SPEED
                                scroll_y  = max(0, scroll_y)
                                draw_summary(
                                    renderer.screen,
                                    font_big, font_med, font_small,
                                    total_move_attempts,
                                    illegal_move_count,
                                    illegal_examples,
                                    game.winner,
                                    scroll_y
                                )

if __name__ == "__main__":
    pvp_test()
