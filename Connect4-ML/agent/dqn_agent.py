
import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
from agent.dqn_model import DQN
from agent.replay_buffer import ReplayBuffer

class DQNAgent:
    def __init__(
        self,
        output_size=7,
        lr=0.000005,
        gamma=0.95,
        epsilon=1.0,
        epsilon_min=0.01,
        epsilon_decay=0.9999986,
        batch_size=64,
        buffer_capacity=100000
    ):
        # ── Device ────────────────────────────────────
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        print(f" Using device: {self.device}")

        # ── Networks ──────────────────────────────────
        self.policy_net = DQN(output_size=output_size).to(self.device)  # ✅ GPU!
        self.target_net = DQN(output_size=output_size).to(self.device)  # ✅ GPU!
        self.target_net.load_state_dict(self.policy_net.state_dict())
        self.target_net.eval()

        # ── Training parameters ────────────────────────
        self.gamma         = gamma
        self.epsilon       = epsilon
        self.epsilon_min   = epsilon_min
        self.epsilon_decay = epsilon_decay
        self.batch_size    = batch_size

        # ── Optimizer and memory ───────────────────────
        self.optimizer = optim.Adam(self.policy_net.parameters(), lr=lr)
        self.memory    = ReplayBuffer(buffer_capacity)
        self.loss_fn   = nn.HuberLoss()

    def prepare_state(self, board, player):
        opponent  = 2 if player == 1 else 1
        ch1       = (board == player).astype(np.float32)
        ch2       = (board == opponent).astype(np.float32)
        ch3       = (board == 0).astype(np.float32)
        state_3ch = np.stack([ch1, ch2, ch3], axis=0)
        return state_3ch

    def select_action(self, board, player, valid_moves):
        if np.random.rand() < self.epsilon:
            return np.random.choice(valid_moves)

        self.policy_net.eval()
        state        = self.prepare_state(board, player)
        state_tensor = torch.FloatTensor(state).unsqueeze(0).to(self.device)  # ✅ GPU!

        with torch.no_grad():
            q_values = self.policy_net(state_tensor).squeeze()

        self.policy_net.train()
        valid_q = {col: q_values[col].item() for col in valid_moves}
        return max(valid_q, key=valid_q.get)

    def remember(self, board, player, action, reward, next_board, done):
        state      = self.prepare_state(board, player)
        next_state = self.prepare_state(next_board, player)
        self.memory.push(state, action, reward, next_state, done)

    def train(self):
        if len(self.memory) < self.batch_size:
            return None

        self.policy_net.train()
        states, actions, rewards, next_states, dones = self.memory.sample(self.batch_size)

        # ── Move everything to GPU ─────────────────────
        states      = torch.FloatTensor(states).to(self.device)       # ✅ GPU!
        actions     = torch.LongTensor(actions).to(self.device)       # ✅ GPU!
        rewards     = torch.FloatTensor(rewards).to(self.device)      # ✅ GPU!
        next_states = torch.FloatTensor(next_states).to(self.device)  # ✅ GPU!
        dones       = torch.FloatTensor(dones).to(self.device)        # ✅ GPU!

        current_q = self.policy_net(states).gather(1, actions.unsqueeze(1)).squeeze(1)

        with torch.no_grad():
            best_actions = self.policy_net(next_states).argmax(dim=1)
            next_q   = self.target_net(next_states).gather(1, best_actions.unsqueeze(1)).squeeze(1)
            target_q = rewards + (1 - dones) * self.gamma * next_q

        loss = self.loss_fn(current_q, target_q)
        self.optimizer.zero_grad()
        loss.backward()
        torch.nn.utils.clip_grad_norm_(self.policy_net.parameters(), 0.5)  # ← stricter!
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
        checkpoint = torch.load(path, map_location=self.device, weights_only=True)  # ✅ loads to correct device!
        self.policy_net.load_state_dict(checkpoint['policy_net'])
        self.target_net.load_state_dict(checkpoint['target_net'])
        self.optimizer.load_state_dict(checkpoint['optimizer'])
        self.epsilon = checkpoint['epsilon']
