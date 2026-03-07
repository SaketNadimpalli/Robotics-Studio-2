import pygame


class Connect4Renderer:
    def __init__(self, game):
        self.game = game

        self.SQUARE_SIZE = 100
        self.RADIUS = self.SQUARE_SIZE // 2 - 8

        self.COLS = self.game.COLS
        self.ROWS = self.game.ROWS

        self.width = self.COLS * self.SQUARE_SIZE
        self.height = (self.ROWS + 1) * self.SQUARE_SIZE

        self.BLUE = (0, 0, 255)
        self.BLACK = (0, 0, 0)
        self.RED = (255, 0, 0)
        self.YELLOW = (255, 255, 0)
        self.WHITE = (255, 255, 255)

        pygame.init()
        self.screen = pygame.display.set_mode((self.width, self.height))
        pygame.display.set_caption("Connect 4")
        self.font = pygame.font.SysFont("arial", 40)

    def draw_board(self):
        self.screen.fill(self.BLACK)

        if not self.game.game_over:
            label = self.font.render(f"Player {self.game.current_player}'s turn", True, self.WHITE)
        else:
            if self.game.winner == 0:
                label = self.font.render("Draw!", True, self.WHITE)
            else:
                label = self.font.render(f"Player {self.game.winner} wins!", True, self.WHITE)

        self.screen.blit(label, (20, 20))

        for col in range(self.COLS):
            for row in range(self.ROWS):
                x = col * self.SQUARE_SIZE
                y = (row + 1) * self.SQUARE_SIZE

                pygame.draw.rect(
                    self.screen,
                    self.BLUE,
                    (x, y, self.SQUARE_SIZE, self.SQUARE_SIZE)
                )

                piece = self.game.board[row][col]
                if piece == self.game.PLAYER_1:
                    colour = self.RED
                elif piece == self.game.PLAYER_2:
                    colour = self.YELLOW
                else:
                    colour = self.BLACK

                pygame.draw.circle(
                    self.screen,
                    colour,
                    (
                        x + self.SQUARE_SIZE // 2,
                        y + self.SQUARE_SIZE // 2
                    ),
                    self.RADIUS
                )

        pygame.display.update()

    def get_column_from_mouse(self, pos_x):
        return pos_x // self.SQUARE_SIZE