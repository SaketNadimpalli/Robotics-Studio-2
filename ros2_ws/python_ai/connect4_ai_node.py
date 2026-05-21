from email import message
import os
import sys
import random
import io

CONNECT4_ML_DIR = '/mnt/c/Users/saket/Documents/GitHub/Robotics-Studio-2/Connect4-ML'
sys.path.append(CONNECT4_ML_DIR)

import rclpy
from rclpy.node import Node
from std_msgs.msg import Int32, Bool, String   # NEW: Bool, String

from game.connect4_env import Connect4
from agent.dqn_agent import DQNAgent
from search.minimax import minimax_search


MODEL_PATH = os.path.join(CONNECT4_ML_DIR, 'models', 'connect4_latest.pth')

HUMAN = 1
AI = 2
SEARCH_DEPTH = 4

class RosStdoutMirror(io.TextIOBase):
    """Wraps a stream so writes also publish to a ROS topic."""
    def __init__(self, original_stream, publisher):
        self.original = original_stream
        self.publisher = publisher
        self._buffer = ""

    def write(self, text):
        self.original.write(text)
        self._buffer += text
        # flush on newline so we send whole lines, not partial writes
        while "\n" in self._buffer:
            line, self._buffer = self._buffer.split("\n", 1)
            if line.strip():
                msg = String()
                msg.data = line
                try:
                    self.publisher.publish(msg)
                except Exception:
                    pass  # don't crash printing if publisher dies
        return len(text)

    def flush(self):
        self.original.flush()


class Connect4AINode(Node):
    def __init__(self):
        super().__init__('connect4_ai')

        self.game = Connect4()
        self.agent = self._load_agent()

        self.board_pub = self.create_publisher(String, '/connect4/board_state', 10)
        self.debug_log_pub = self.create_publisher(String, '/connect4/debug_log', 10)

        # Mirror stdout (catches all print() calls including minimax)
        sys.stdout = RosStdoutMirror(sys.stdout, self.debug_log_pub)

        self.player_sub = self.create_subscription(
            Int32, '/connect4/player_move', self.on_player_move, 10
        )
        self.ai_pub = self.create_publisher(
            Int32, '/connect4/robot_move', 10
        )
        self.arm_execute_pub = self.create_publisher(
            Int32, '/column_command', 10
        )

        # NEW
        self.game_over_pub = self.create_publisher(
            Int32, '/connect4/game_over', 10
        )
        self.reset_sub = self.create_subscription(
            Bool, '/connect4/reset', self.on_reset, 10
        )
        self.game_mode = 'IRL'  # default — only respond to player moves in IRL mode
        self.mode_sub = self.create_subscription(
            String, '/connect4/game_mode', self.on_game_mode, 10
        )

        self.get_logger().info(
            f'Connect4 AI node ready (DQN + minimax, depth={SEARCH_DEPTH})'
        )
        self._log(f'Initial board:\n{self.game.board}')

    def _load_agent(self):
        agent = DQNAgent()
        agent.load(MODEL_PATH)
        agent.epsilon = 0.0
        self.get_logger().info(f'Model loaded from {MODEL_PATH}')
        return agent

    def _publish_board(self):
        rows = [' '.join(str(int(v)) for v in row) for row in self.game.board]
        msg = String()
        msg.data = '\n'.join(rows)
        self.board_pub.publish(msg)

    def _log(self, message):
        """Log to ROS console AND publish to /connect4/debug_log for VR."""
        self.get_logger().info(message)
        msg = String()
        msg.data = f'[INFO] {message}'
        self.debug_log_pub.publish(msg)

    def on_player_move(self, msg):
        if self.game_mode != 'IRL':
            self.get_logger().info('Ignoring player move — not in IRL mode')
            return
        col = int(msg.data) - 1  # convert from 1-based to 0-based
        self._log(f'Player played column {col}')

        if self.game.game_over:
            self.get_logger().warn('Game already over, ignoring player move')
            return

        if not self.game.drop_piece(col):
            self.get_logger().warn(f'Invalid human move at column {col}')
            return

        if self.game.game_over:
            self._handle_game_over()
            return

        valid = self.game.get_valid_moves()
        if not valid:
            self.get_logger().warn('No valid AI moves')
            return

        try:
            ai_col = minimax_search(
                self.game.board, self.agent, ai_player=AI, depth=SEARCH_DEPTH
            )
        except Exception as e:
            self.get_logger().error(f'Minimax failed ({e}); falling back to random')
            ai_col = random.choice(valid)

        if ai_col not in valid:
            self.get_logger().warn(f'AI returned invalid column {ai_col}, falling back')
            ai_col = random.choice(valid)

        self.game.drop_piece(ai_col)
        self._log(f'AI plays column {ai_col}')

        out = Int32()
        out.data = int(ai_col) + 1  # publish 1-based to match system convention
        self.ai_pub.publish(out)
        self.arm_execute_pub.publish(out)  # also send to motion planner

        self._log(f'Board:\n{self.game.board}')
        self._publish_board()

        if self.game.game_over:
            self._handle_game_over()

    # NEW: combined log + publish
    def _handle_game_over(self):
        winner = int(self.game.winner) if self.game.winner is not None else 0
        if winner == 0:
            self._log('Game over: DRAW')
        else:
            self._log(f'Game over: Player {winner} wins')

        msg = Int32()
        msg.data = winner
        self.game_over_pub.publish(msg)
        self._publish_board() 

    # NEW: reset callback
    def on_game_mode(self, msg):
        self.game_mode = msg.data
        self._log(f'Game mode set to: {self.game_mode}')

    def on_reset(self, msg):
        self.game.reset()
        self._log('=== Game reset ===')
        self._log(f'Initial board:\n{self.game.board}')
        self._publish_board()


def main():
    rclpy.init()
    node = Connect4AINode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()