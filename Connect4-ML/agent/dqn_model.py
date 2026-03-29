
import torch
import torch.nn as nn

class DQN(nn.Module):
    def __init__(self, output_size=7):
        super(DQN, self).__init__()

        # ── CNN Layers ──────────────────────────────────
        # Takes 3 channel 6x7 board as input
        self.conv_layers = nn.Sequential(
            # First conv layer
            # 3 input channels → 32 filters, 3×3 kernel
            nn.Conv2d(3, 32, kernel_size=3, padding=1),
            nn.ReLU(),

            # Second conv layer
            # 32 filters → 64 filters, 3×3 kernel
            nn.Conv2d(32, 64, kernel_size=3, padding=1),
            nn.ReLU(),

            # Third conv layer
            # 64 filters → 128 filters, 3×3 kernel
            nn.Conv2d(64, 128, kernel_size=3, padding=1),
            nn.ReLU()
        )

        # ── Calculate flattened size after CNN ──────────
        # Board is 6×7, padding=1 keeps size same
        # So after conv layers: 128 filters × 6 rows × 7 cols
        self.flat_size = 128 * 6 * 7   # = 5376

        # ── Fully Connected Layers ──────────────────────
        self.fc_layers = nn.Sequential(
            nn.Linear(self.flat_size, 256),  # CNN output → 256
            nn.ReLU(),
            nn.Linear(256, 256),             # 256 → 256
            nn.ReLU(),
            nn.Linear(256, output_size)      # 256 → 7 Q-values
        )

    def forward(self, x):
        # x shape: (batch, 3, 6, 7)
        x = self.conv_layers(x)    # → (batch, 128, 6, 7)
        x = x.view(x.size(0), -1) # → (batch, 5376) flatten!
        x = self.fc_layers(x)     # → (batch, 7)
        return x
