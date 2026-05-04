using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LogTabSwitcher : MonoBehaviour
{
    [Header("Panels (the GameObjects holding each TMP text)")]
    public GameObject rosPanel;
    public GameObject unityPanel;

    [Header("Tab buttons (optional — for highlighting active tab)")]
    public Image rosTabButton;
    public Image unityTabButton;

    [Header("Colors")]
    public Color activeTabColor = new Color(0.2f, 0.6f, 0.9f, 1f);
    public Color inactiveTabColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    void Start() => ShowRos();   // default to ROS view

    public void ShowRos()
    {
        rosPanel.SetActive(true);
        unityPanel.SetActive(false);
        if (rosTabButton != null) rosTabButton.color = activeTabColor;
        if (unityTabButton != null) unityTabButton.color = inactiveTabColor;
    }

    public void ShowUnity()
    {
        rosPanel.SetActive(false);
        unityPanel.SetActive(true);
        if (rosTabButton != null) rosTabButton.color = inactiveTabColor;
        if (unityTabButton != null) unityTabButton.color = activeTabColor;
    }
}