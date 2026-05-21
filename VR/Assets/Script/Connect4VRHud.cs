using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Connect4VRHud : MonoBehaviour
{
    [Header("Turn / Status labels")]
    public TextMeshProUGUI turnLabel;
    public TextMeshProUGUI robotStatusLabel;

    [Header("Win banner (root GameObject — hidden until game ends)")]
    public GameObject winBanner;
    public TextMeshProUGUI winLabel;

    [Header("New Game button")]
    public Button newGameButton;

    [Header("Material swapped onto winning coins (assign a bright emissive material)")]
    public Material winHighlightMaterial;

    List<MeshRenderer> _highlighted = new List<MeshRenderer>();
    List<Material>     _origMats    = new List<Material>();

    void Start()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(() => GameStateManager.Instance?.ResetGame());

        if (winBanner != null) winBanner.SetActive(false);
        SetTurn(true);
        SetRobotStatus("Idle");
    }

    // -------------------------------------------------------------------------
    // Public API — called by GameStateManager
    // -------------------------------------------------------------------------

    public void SetTurn(bool isPlayerTurn)
    {
        if (turnLabel != null)
            turnLabel.text = isPlayerTurn ? "Your Turn" : "Robot's Turn";
    }

    public void SetRobotStatus(string status)
    {
        if (robotStatusLabel != null)
            robotStatusLabel.text = status;
    }

    public void ShowWinner(int winner)
    {
        if (winBanner != null) winBanner.SetActive(true);
        if (winLabel != null)
            winLabel.text = winner == 0 ? "Draw!"
                          : winner == 1 ? "You Win!"
                                        : "Robot Wins!";
    }

    public void HighlightCoins(List<GameObject> coins)
    {
        ClearHighlight();
        if (winHighlightMaterial == null) return;
        foreach (var coin in coins)
        {
            if (coin == null) continue;
            var r = coin.GetComponent<MeshRenderer>();
            if (r == null) continue;
            _highlighted.Add(r);
            _origMats.Add(r.material);
            r.material = winHighlightMaterial;
        }
    }

    public void ResetHUD()
    {
        if (winBanner != null) winBanner.SetActive(false);
        ClearHighlight();
        SetTurn(true);
        SetRobotStatus("Idle");
    }

    // -------------------------------------------------------------------------
    void ClearHighlight()
    {
        for (int i = 0; i < _highlighted.Count; i++)
            if (_highlighted[i] != null && i < _origMats.Count)
                _highlighted[i].material = _origMats[i];
        _highlighted.Clear();
        _origMats.Clear();
    }
}
