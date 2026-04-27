import pygame
from game.connect4_env import Connect4
from visuals.renderer import Connect4Renderer


def main():
    game = Connect4()
    renderer = Connect4Renderer(game)

    running = True

    while running:
        renderer.draw_board()

        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False

            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_r:
                    game.reset()

            if event.type == pygame.MOUSEBUTTONDOWN and not game.game_over:
                mouse_x = event.pos[0]
                col = renderer.get_column_from_mouse(mouse_x)

                if 0 <= col < game.COLS:
                    game.drop_piece(col)

    pygame.quit()


if __name__ == "__main__":
    main()