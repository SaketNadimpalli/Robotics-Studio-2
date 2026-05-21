from setuptools import find_packages, setup

package_name = 'connect4_ai'

setup(
    name=package_name,
    version='0.0.1',
    packages=find_packages(exclude=['test']),
    data_files=[
        ('share/ament_index/resource_index/packages',
            ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='Saket',
    maintainer_email='saket@todo.com',
    description='Connect4 AI node — DQN + minimax',
    license='MIT',
    tests_require=['pytest'],
    entry_points={
        'console_scripts': [
            'connect4_ai_node = connect4_ai.connect4_ai_node:main',
        ],
    },
)
