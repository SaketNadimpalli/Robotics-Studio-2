
import numpy as np
import random
from collections import deque

class PrioritisedReplayBuffer:
    def __init__(self, capacity=100000, alpha=0.6):
        """
        capacity: max experiences to store
        alpha:    how much to prioritise
                  0.0 = random (like current!)
                  1.0 = fully prioritised
                  0.6 = recommended sweet spot!
        """
        self.capacity   = capacity
        self.alpha      = alpha
        self.buffer     = []        # stores experiences
        self.priorities = []        # stores TD errors
        self.position   = 0         # current write position

    def push(self, state, action, reward, next_state, done):
        """
        Store new experience with MAX priority
        New experiences get highest priority so
        they get sampled at least once! ✅
        """
        # New experience gets max priority
        max_priority = max(self.priorities) if self.priorities else 1.0

        if len(self.buffer) < self.capacity:
            # Buffer not full yet — just append!
            self.buffer.append((state, action, reward, next_state, done))
            self.priorities.append(max_priority)
        else:
            # Buffer full — overwrite oldest!
            self.buffer[self.position]     = (state, action, reward, next_state, done)
            self.priorities[self.position] = max_priority

        # Move write position forward (wraps around!)
        self.position = (self.position + 1) % self.capacity

    def sample(self, batch_size, beta=0.4):
        """
        Sample batch based on priorities!

        beta: importance sampling correction
              0.0 = no correction
              1.0 = full correction
              starts at 0.4, increases over training!
        """
        # Convert priorities to probabilities
        priorities = np.array(self.priorities, dtype=np.float32)
        probs      = priorities ** self.alpha   # ← apply alpha!
        probs      /= probs.sum()               # ← normalise to sum=1!

        # Sample indices based on probabilities!
        indices = np.random.choice(
            len(self.buffer),
            batch_size,
            p     = probs,
            replace = False
        )

        # Get experiences at those indices
        batch       = [self.buffer[i] for i in indices]
        states, actions, rewards, next_states, dones = zip(*batch)

        # ── Importance Sampling Weights ──────────────
        # Correct for sampling bias!
        # Experiences sampled more often get lower weight
        N       = len(self.buffer)
        weights = (N * probs[indices]) ** (-beta)
        weights /= weights.max()   # ← normalise!
        weights  = np.array(weights, dtype=np.float32)

        return (
            np.array(states),
            np.array(actions),
            np.array(rewards,     dtype=np.float32),
            np.array(next_states),
            np.array(dones,       dtype=np.float32),
            indices,    # ← need these to update priorities later!
            weights     # ← importance sampling weights!
        )

    def update_priorities(self, indices, td_errors):
        """
        Update priorities after training!
        Called from dqn_agent.train() with new TD errors
        """
        for idx, error in zip(indices, td_errors):
            # Small epsilon (0.01) prevents priority = 0
            # ensuring every experience can be sampled!
            self.priorities[idx] = abs(float(error)) + 0.01

    def __len__(self):
        return len(self.buffer)
