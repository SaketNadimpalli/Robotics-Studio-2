import rclpy
from rclpy.node import Node
from std_msgs.msg import String


class TalkerNode(Node):
    def __init__(self):
        super().__init__('connect4_talker')
        self.publisher = self.create_publisher(String, '/from_python', 10)
        self.timer = self.create_timer(1.0, self.publish_message)
        self.counter = 0
        self.get_logger().info('Talker node started, publishing to /from_python')

    def publish_message(self):
        msg = String()
        msg.data = f'hello from python #{self.counter}'
        self.publisher.publish(msg)
        self.get_logger().info(f'Published: {msg.data}')
        self.counter += 1


def main():
    rclpy.init()
    node = TalkerNode()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
