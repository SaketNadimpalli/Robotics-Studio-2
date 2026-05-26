using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class GameStateManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------
    public static GameStateManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------
    [Header("Column detectors — drag all 7 in order")]
    public ColumnDetector[] columnDetectors;

    [Header("HUD")]
    public Connect4VRHud hud;

    [Header("Player coin respawning")]
    public PlayerCoinSpawner playerCoinSpawner;

    [Header("ROS topics")]
    public string gameOverTopic  = "/connect4/game_over";
    public string resetTopic     = "/connect4/reset";
    public string aiMoveTopic    = "/connect4/robot_move";

    [Header("Sync detection")]
    public float slotDetectionRadius = 0.05f;
    public float syncCheckDelay      = 2.0f;

    // -------------------------------------------------------------------------
    // Public state
    // -------------------------------------------------------------------------
    public static bool IsGameOver { get; private set; }
    public static bool IsPlayerTurn { get; private set; } = true;
    public static string CurrentGameMode { get; private set; } = "IRL"; // "IRL" or "XR"
    public static string CurrentDifficulty { get; private set; } = "Easy"; // "Easy" or "Hard"

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------
    ROSConnection _ros;
    string        _lastPythonBoard;

    // -------------------------------------------------------------------------
    // Unity messages
    // -------------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.Subscribe<Int32Msg>(gameOverTopic, OnGameOver);
        _ros.Subscribe<StringMsg>("/connect4/board_state", OnBoardState);
        _ros.Subscribe<Int32Msg>(aiMoveTopic, OnAIMove);
        _ros.Subscribe<StringMsg>("/connect4/game_mode", OnGameMode);
        _ros.Subscribe<StringMsg>("/connect4/game_difficulty", OnGameDifficulty);
        _ros.Subscribe<BoolMsg>("/connect4/game_start", OnGameStart);
        _ros.RegisterPublisher<BoolMsg>(resetTopic);
        Debug.Log("GameStateManager ready");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            ResetGame();
    }

    // -------------------------------------------------------------------------
    // Turn tracking — called by ColumnDetector after a player coin lands
    // -------------------------------------------------------------------------
    public void PlayerMoved()
    {
        IsPlayerTurn = false;
        hud?.SetTurn(false);
        hud?.SetRobotStatus("Thinking...");
    }

    // Called when the AI move message arrives (coin is about to be spawned)
    void OnAIMove(Int32Msg msg)
    {
        hud?.SetRobotStatus("Moving");
        // Give the coin a moment to fall and snap, then hand back to player
        Invoke(nameof(HandBackToPlayer), 2f);
    }

    // Called by ColumnDetector when an IRL mirror coin snaps (Mode 3)
    // — no robot_move is published in that mode, so we hand back manually
    public void IRLMirrorCoinLanded()
    {
        CancelInvoke(nameof(HandBackToPlayer));
        Invoke(nameof(HandBackToPlayer), 2f);
    }

    void HandBackToPlayer()
    {
        if (!IsGameOver)
        {
            IsPlayerTurn = true;
            hud?.SetTurn(true);
            hud?.SetRobotStatus("Idle");
        }
    }

    // -------------------------------------------------------------------------
    // Game mode
    // -------------------------------------------------------------------------
    void OnGameMode(StringMsg msg)
    {
        CurrentGameMode = msg.data;
        Debug.Log($"Game mode set to: {CurrentGameMode}");
    }

    void OnGameDifficulty(StringMsg msg)
    {
        CurrentDifficulty = msg.data;
        Debug.Log($"Game difficulty set to: {CurrentDifficulty}");
    }

    void OnGameStart(BoolMsg msg)
    {
        if (!msg.data) return;
        IsGameOver = false;
        CancelInvoke(nameof(HandBackToPlayer));

        // In XR Easy mode the IRL human goes first — VR player must wait
        // In XR Hard mode the VR player goes first
        // In IRL mode VR is spectating so turn lock doesn't matter
        if (CurrentGameMode == "XR")
            IsPlayerTurn = CurrentDifficulty == "Hard";
        else
            IsPlayerTurn = true;

        Debug.Log($"Game started — IsPlayerTurn: {IsPlayerTurn}");
        hud?.ResetHUD();
        hud?.SetTurn(IsPlayerTurn);
    }

    // -------------------------------------------------------------------------
    // Game over
    // -------------------------------------------------------------------------
    void OnGameOver(Int32Msg msg)
    {
        IsGameOver = true;
        int winner = msg.data;

        if (winner == 0) Debug.Log("=== GAME OVER: DRAW ===");
        else if (winner == 1) Debug.Log("=== GAME OVER: You win! ===");
        else if (winner == 2) Debug.Log("=== GAME OVER: AI wins! ===");
        else Debug.LogWarning($"Unknown winner code: {winner}");

        hud?.ShowWinner(winner);

        if (winner != 0)
        {
            int playerCode = (winner == 1) ? 1 : 2;
            List<(int r, int c)> winCells = FindWinningCells(playerCode);
            if (winCells != null)
                hud?.HighlightCoins(CoinObjectsAt(winCells));
        }
    }

    // -------------------------------------------------------------------------
    // Reset
    // -------------------------------------------------------------------------
    public void ResetGame()
    {
        Debug.Log("Resetting game...");
        IsGameOver = false;
        IsPlayerTurn = true;
        CancelInvoke(nameof(HandBackToPlayer));

        // Destroy all coins
        foreach (var coin in GameObject.FindGameObjectsWithTag("Coin"))
            Destroy(coin);

        // Clear column states
        foreach (var d in columnDetectors)
            if (d != null) d.ResetState();

        // Respawn player coins
        if (playerCoinSpawner != null)
            playerCoinSpawner.SpawnCoins();
        else
            Debug.LogWarning("[GameStateManager] PlayerCoinSpawner not set — coins not respawned.");

        hud?.ResetHUD();
        _ros.Publish(resetTopic, new BoolMsg(true));
    }

    // -------------------------------------------------------------------------
    // Board sync
    // -------------------------------------------------------------------------
    void OnBoardState(StringMsg msg)
    {
        _lastPythonBoard = msg.data;
        CancelInvoke(nameof(CheckSync));
        Invoke(nameof(CheckSync), syncCheckDelay);
    }

    void CheckSync()
    {
        if (_lastPythonBoard == null) return;
        string unityBoard = FormatBoard(DeriveBoardFromScene());
        if (unityBoard == _lastPythonBoard)
            Debug.Log("Board (in sync):\n" + _lastPythonBoard);
        else
            Debug.LogWarning("DESYNC DETECTED\nPython:\n" + _lastPythonBoard + "\nUnity:\n" + unityBoard);
    }

    // -------------------------------------------------------------------------
    // Board helpers
    // -------------------------------------------------------------------------
    int[,] DeriveBoardFromScene()
    {
        int[,] board = new int[6, 7];
        foreach (var coin in GameObject.FindGameObjectsWithTag("Coin"))
        {
            var snap = coin.GetComponent<CoinSnap>();
            if (snap == null || !snap.hasSnapped) continue;

            float bestDist = float.MaxValue;
            int bestCol = -1, bestRow = -1;
            for (int col = 0; col < columnDetectors.Length; col++)
            {
                var det = columnDetectors[col];
                if (det == null) continue;
                for (int row = 0; row < det.rows.Length; row++)
                {
                    if (det.rows[row] == null) continue;
                    float d = Vector3.Distance(coin.transform.position, det.rows[row].position);
                    if (d < bestDist) { bestDist = d; bestCol = col; bestRow = row; }
                }
            }
            if (bestCol >= 0 && bestDist < 0.5f)
                board[5 - bestRow, bestCol] = snap.isAICoin ? 2 : 1;
        }
        return board;
    }

    string FormatBoard(int[,] board)
    {
        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < 6; r++)
        {
            if (r > 0) sb.Append('\n');
            for (int c = 0; c < 7; c++)
            {
                if (c > 0) sb.Append(' ');
                sb.Append(board[r, c]);
            }
        }
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Win detection — finds the 4 cells that form the winning line
    // -------------------------------------------------------------------------
    List<(int r, int c)> FindWinningCells(int playerCode)
    {
        int[,] board = DeriveBoardFromScene();

        // Four directions: horizontal, vertical, diagonal down-right, diagonal down-left
        int[] drs = {  0, 1, 1,  1 };
        int[] dcs = {  1, 0, 1, -1 };

        for (int r = 0; r < 6; r++)
        {
            for (int c = 0; c < 7; c++)
            {
                for (int d = 0; d < 4; d++)
                {
                    var cells = new List<(int, int)>();
                    bool win = true;
                    for (int k = 0; k < 4; k++)
                    {
                        int nr = r + drs[d] * k;
                        int nc = c + dcs[d] * k;
                        if (nr < 0 || nr >= 6 || nc < 0 || nc >= 7 || board[nr, nc] != playerCode)
                        { win = false; break; }
                        cells.Add((nr, nc));
                    }
                    if (win) return cells;
                }
            }
        }
        return null;
    }

    // Given board (row,col) pairs, find the matching coin GameObjects in the scene
    List<GameObject> CoinObjectsAt(List<(int r, int c)> cells)
    {
        var result = new List<GameObject>();
        foreach (var (r, c) in cells)
        {
            if (c >= columnDetectors.Length || columnDetectors[c] == null) continue;
            int unityRow = 5 - r;
            var det = columnDetectors[c];
            if (unityRow >= det.rows.Length || det.rows[unityRow] == null) continue;
            Vector3 slotPos = det.rows[unityRow].position;

            // Find coin closest to this slot
            float best = float.MaxValue;
            GameObject bestCoin = null;
            foreach (var coin in GameObject.FindGameObjectsWithTag("Coin"))
            {
                float d = Vector3.Distance(coin.transform.position, slotPos);
                if (d < best) { best = d; bestCoin = coin; }
            }
            if (bestCoin != null && best < 0.5f)
                result.Add(bestCoin);
        }
        return result;
    }
}
