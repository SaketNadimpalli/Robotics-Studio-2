
import torch
import torch.nn as nn

class DQN(nn.Module):
    def __init__(self, output_size=7, dropout_rate=0.3):
        super(DQN, self).__init__()

        # ── CNN Layers ──────────────────────────────────
        # Takes 3 channel 6x7 board as input
        self.conv_layers = nn.Sequential(
            # First conv layer
            # 3 input channels → 64 filters, 3×3 kernel
            nn.Conv2d(3, 64, kernel_size=3, padding=1),
            nn.BatchNorm2d(64),
            nn.ReLU(),

            # Second conv layer
            # 64 filters → 128 filters, 3×3 kernel
            nn.Conv2d(64, 128, kernel_size=3, padding=1),
            nn.BatchNorm2d(128),
            nn.ReLU(),

            # Third conv layer
            # 128 filters → 256 filters, 3×3 kernel
            nn.Conv2d(128, 256, kernel_size=3, padding=1),
            nn.BatchNorm2d(256),
            nn.ReLU()
        )

        # ── Calculate flattened size after CNN ──────────
        # Board is 6×7, padding=1 keeps size same
        # So after conv layers: 256 filters × 6 rows × 7 cols
        self.flat_size = 256 * 6 * 7   # = 10,752

        # ── Fully Connected Layers ──────────────────────
        self.fc_layers = nn.Sequential(
            nn.Linear(self.flat_size, 512),  # 10752 → 512 ← compress!
            nn.ReLU(),
            nn.Dropout(p=dropout_rate),

            nn.Linear(512, 256),             # 512 → 256 ← refine!
            nn.ReLU(),
            nn.Dropout(p=dropout_rate),

            nn.Linear(256, output_size)      # 256 → 7 Q-values ← no dropout!
        )

    def forward(self, x):
        # x shape: (batch, 3, 6, 7)
        x = self.conv_layers(x)    # → (batch, 256, 6, 7)
        x = x.view(x.size(0), -1) # → (batch, 10752) flatten!
        x = self.fc_layers(x)     # → (batch, 7)
        return x
