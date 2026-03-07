from game.connect4_env import Connect4


def main():
    game = Connect4()

    while not game.game_over:
        game.print_board()
        print(f"\nPlayer {game.current_player}'s turn")
        print("Valid moves:", game.get_valid_moves())

        try:
            col = int(input("Choose a column (0-6): "))
        except ValueError:
            print("Please enter a number from 0 to 6.")
            continue

        if not game.drop_piece(col):
            print("Invalid move. Try again.")

    game.print_board()

    if game.winner == 0:
        print("\nIt's a draw!")
    else:
        print(f"\nPlayer {game.winner} wins!")


if __name__ == "__main__":
    main()