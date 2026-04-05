
import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
from agent.dqn_model import DQN
from agent.Prioritised_replay_buffer import PrioritisedReplayBuffer

class DQNAgent:
    def __init__(
        self,
        output_size     = 7,
        lr              = 0.000005,
        gamma           = 0.95,
        epsilon         = 1.0,
        epsilon_min     = 0.01,
        epsilon_decay   = 0.99999986,
        batch_size      = 64,
        buffer_capacity = 100000,
        alpha           = 0.6,    # ← PER prioritisation
        beta            = 0.4,    # ← importance sampling start
        beta_increment  = 0.0000001  # ← beta increases over time
    ):
        # ── Device ────────────────────────────────────
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        print(f" Using device: {self.device}")

        # ── Networks ──────────────────────────────────
        self.policy_net = DQN(output_size=output_size).to(self.device)
        self.target_net = DQN(output_size=output_size).to(self.device)
        self.target_net.load_state_dict(self.policy_net.state_dict())
        self.target_net.eval()

        # ── Training parameters ────────────────────────
        self.gamma         = gamma
        self.epsilon       = epsilon
        self.epsilon_min   = epsilon_min
        self.epsilon_decay = epsilon_decay
        self.batch_size    = batch_size

        # ── PER parameters ─────────────────────────────
        self.beta          = beta
        self.beta_max      = 1.0
        self.beta_increment = beta_increment

        # ── Optimizer and memory ───────────────────────
        self.optimizer = optim.Adam(self.policy_net.parameters(), lr=lr)
        self.memory    = PrioritisedReplayBuffer(
            capacity = buffer_capacity,
            alpha    = alpha
        )
        self.loss_fn   = nn.HuberLoss(reduction='none')  # ← per element!

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
        state_tensor = torch.FloatTensor(state).unsqueeze(0).to(self.device)

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

        # ── Sample with priorities! ────────────────────
        states, actions, rewards, next_states, dones, indices, weights = \
            self.memory.sample(self.batch_size, beta=self.beta)

        # ── Move to GPU ────────────────────────────────
        states      = torch.FloatTensor(states).to(self.device)
        actions     = torch.LongTensor(actions).to(self.device)
        rewards     = torch.FloatTensor(rewards).to(self.device)
        next_states = torch.FloatTensor(next_states).to(self.device)
        dones       = torch.FloatTensor(dones).to(self.device)
        weights     = torch.FloatTensor(weights).to(self.device)

        current_q = self.policy_net(states).gather(1, actions.unsqueeze(1)).squeeze(1)

        with torch.no_grad():
            # DDQN
            best_actions = self.policy_net(next_states).argmax(dim=1)
            next_q       = self.target_net(next_states).gather(
                1, best_actions.unsqueeze(1)
            ).squeeze(1)
            target_q = rewards + (1 - dones) * (self.gamma ** 4) * next_q

        # ── TD errors for priority update ──────────────
        td_errors = (current_q - target_q).abs().detach().cpu().numpy()

        # ── Weighted loss ──────────────────────────────
        elementwise_loss = self.loss_fn(current_q, target_q)
        loss             = (weights * elementwise_loss).mean()

        self.optimizer.zero_grad()
        loss.backward()
        torch.nn.utils.clip_grad_norm_(self.policy_net.parameters(), 0.5)
        self.optimizer.step()

        # ── Update priorities ──────────────────────────
        self.memory.update_priorities(indices, td_errors)

        # ── Increase beta ──────────────────────────────
        self.beta = min(self.beta_max, self.beta + self.beta_increment)

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
            'epsilon'   : self.epsilon,
            'beta'      : self.beta    # ← save beta too!
        }, path)

    def load(self, path):
        checkpoint = torch.load(path, map_location=self.device, weights_only=True)
        self.policy_net.load_state_dict(checkpoint['policy_net'])
        self.target_net.load_state_dict(checkpoint['target_net'])
        self.optimizer.load_state_dict(checkpoint['optimizer'])
        self.epsilon = checkpoint['epsilon']
        # Load beta if it exists in checkpoint
        self.beta    = checkpoint.get('beta', 0.4)  # ← default 0.4 if not found!
