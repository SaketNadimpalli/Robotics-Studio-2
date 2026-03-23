
import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
from agent.dqn_model import DQN
from agent.replay_buffer import ReplayBuffer

class DQNAgent:
    def __init__(
        self,
        input_size=42,
        hidden_size=128,
        output_size=7,
        lr=0.00005,
        gamma=0.95,
        epsilon=1.0,
        epsilon_min=0.01,
        epsilon_decay=0.9999,
        batch_size=64,
        buffer_capacity=10000
    ):
        self.policy_net = DQN(input_size, hidden_size, output_size)
        self.target_net = DQN(input_size, hidden_size, output_size)
        self.target_net.load_state_dict(self.policy_net.state_dict())
        self.target_net.eval()

        self.gamma         = gamma
        self.epsilon       = epsilon
        self.epsilon_min   = epsilon_min
        self.epsilon_decay = epsilon_decay
        self.batch_size    = batch_size

        self.optimizer = optim.Adam(self.policy_net.parameters(), lr=lr)
        self.memory    = ReplayBuffer(buffer_capacity)
        self.loss_fn   = nn.HuberLoss()    # ✅ changed from MSELoss!

    def normalise_state(self, state):
        return (state.flatten() / 2.0) - 0.5

    def select_action(self, state, valid_moves):
        if np.random.rand() < self.epsilon:
            return np.random.choice(valid_moves)

        state_tensor = torch.FloatTensor(self.normalise_state(state)).unsqueeze(0)
        with torch.no_grad():
            q_values = self.policy_net(state_tensor).squeeze()

        valid_q = {col: q_values[col].item() for col in valid_moves}
        return max(valid_q, key=valid_q.get)

    def remember(self, state, action, reward, next_state, done):
        self.memory.push(
            self.normalise_state(state),        # ✅ no double flatten!
            action,
            reward,
            self.normalise_state(next_state),   # ✅ no double flatten!
            done
        )

    def train(self):
        if len(self.memory) < self.batch_size:
            return None

        states, actions, rewards, next_states, dones = self.memory.sample(self.batch_size)

        states      = torch.FloatTensor(states)
        actions     = torch.LongTensor(actions)
        rewards     = torch.FloatTensor(rewards)
        next_states = torch.FloatTensor(next_states)
        dones       = torch.FloatTensor(dones)

        current_q = self.policy_net(states).gather(1, actions.unsqueeze(1)).squeeze(1)

        with torch.no_grad():
            next_q   = self.target_net(next_states).max(1)[0]
            target_q = rewards + (1 - dones) * self.gamma * next_q

        loss = self.loss_fn(current_q, target_q)
        self.optimizer.zero_grad()
        loss.backward()
        torch.nn.utils.clip_grad_norm_(self.policy_net.parameters(), 1.0)
        self.optimizer.step()

        if self.epsilon > self.epsilon_min:
            self.epsilon *= self.epsilon_decay

        return loss.item()

    def update_target_network(self):
        self.target_net.load_state_dict(self.policy_net.state_dict())

    def save(self, path):
        torch.save({
            'policy_net': self.policy_net.state_dict(),
            'target_net': self.target_net.state_dict(),
            'optimizer' : self.optimizer.state_dict(),
            'epsilon'   : self.epsilon
        }, path)

    def load(self, path):
        checkpoint = torch.load(path)
        self.policy_net.load_state_dict(checkpoint['policy_net'])
        self.target_net.load_state_dict(checkpoint['target_net'])
        self.optimizer.load_state_dict(checkpoint['optimizer'])
        self.epsilon = checkpoint['epsilon']
