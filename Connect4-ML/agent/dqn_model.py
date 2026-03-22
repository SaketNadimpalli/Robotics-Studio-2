
import torch
import torch.nn as nn

class DQN(nn.Module):
    def __init__(self, input_size=42, hidden_size=128, output_size=7):
        super(DQN, self).__init__()

        self.network = nn.Sequential(
            nn.Linear(input_size, hidden_size),   # Input layer → Hidden layer 1
            nn.ReLU(),                             # Activation function
            nn.Linear(hidden_size, hidden_size),  # Hidden layer 1 → Hidden layer 2
            nn.ReLU(),                             # Activation function
            nn.Linear(hidden_size, output_size)   # Hidden layer 2 → Output layer
        )

    def forward(self, x):
        return self.network(x)
