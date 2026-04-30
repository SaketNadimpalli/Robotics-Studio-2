using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class GameStateManager : MonoBehaviour
{
    [Header("Drag all 7 ColumnDetector components here")]
    public ColumnDetector[] columnDetectors;

    [Header("ROS topics")]
    public string gameOverTopic = "/connect4/game_over";
    public string resetTopic = "/connect4/reset";

    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<Int32Msg>(gameOverTopic, OnGameOver);
        ros.Subscribe<StringMsg>("/connect4/board_state", OnBoardState);
        ros.RegisterPublisher<BoolMsg>(resetTopic);
        Debug.Log("GameStateManager ready");
    }

    void Update()
    {
        // Press R to reset (Editor / keyboard testing)
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetGame();
        }
    }

    void OnGameOver(Int32Msg msg)
    {
        int winner = msg.data;
        if (winner == 0) Debug.Log("=== GAME OVER: DRAW ===");
        else if (winner == 1) Debug.Log("=== GAME OVER: You win! ===");
        else if (winner == 2) Debug.Log("=== GAME OVER: AI wins! ===");
        else Debug.LogWarning($"Unknown winner code: {winner}");
    }

    [Header("Sync detection")]
    public float slotDetectionRadius = 0.05f;  // tune to your coin/slot scale

    int[,] DeriveBoardFromScene()
    {
        // Python convention: board[0] = top row, board[5] = bottom
        // Unity convention: rows[0] = bottom, rows[5] = top
        // So pythonRow = 5 - unityRowIdx
        int[,] board = new int[6, 7];

        for (int col = 0; col < columnDetectors.Length; col++)
        {
            var detector = columnDetectors[col];
            if (detector == null) continue;

            for (int unityRow = 0; unityRow < detector.rows.Length; unityRow++)
            {
                int pyRow = 5 - unityRow;
                Vector3 slotPos = detector.rows[unityRow].position;
                Collider[] hits = Physics.OverlapSphere(slotPos, slotDetectionRadius);

                foreach (var hit in hits)
                {
                    if (!hit.CompareTag("Coin")) continue;
                    var snap = hit.GetComponent<CoinSnap>();
                    if (snap != null && snap.hasSnapped)
                    {
                        board[pyRow, col] = snap.isAICoin ? 2 : 1;
                        break;
                    }
                }
            }
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

    void OnBoardState(StringMsg msg)
    {
        Debug.Log("OnBoardState fired!");
        string unityBoard = FormatBoard(DeriveBoardFromScene());

        if (unityBoard == msg.data)
        {
            Debug.Log("Board (in sync):\n" + msg.data);
        }
        else
        {
            Debug.LogWarning(
                "DESYNC DETECTED\nPython:\n" + msg.data + "\nUnity:\n" + unityBoard
            );
        }
    }

    public void ResetGame()
    {
        Debug.Log("Resetting game...");

        // Destroy all coins in scene (player + AI alike)
        GameObject[] coins = GameObject.FindGameObjectsWithTag("Coin");
        foreach (var coin in coins)
        {
            Destroy(coin);
        }

        // Clear each column's row state
        foreach (var detector in columnDetectors)
        {
            if (detector != null) detector.ResetState();
        }

        // Tell Python to reset its game state
        ros.Publish(resetTopic, new BoolMsg(true));
    }
}