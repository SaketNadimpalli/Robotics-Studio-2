using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Connect4ColliderBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Board Settings")]
    public int columns = 7;
    public int rows = 6;
    public float cellSize = 0.082f;
    public float boardThickness = 0.02f;
    public float frameThickness = 0.01f;

    [ContextMenu("Build Colliders")]
    void BuildColliders()
    {
        // Remove old generated colliders
        Transform old = transform.Find("GeneratedColliders");
        if (old != null) DestroyImmediate(old.gameObject);

        GameObject root = new GameObject("GeneratedColliders");
        root.transform.SetParent(transform, false);

        float boardW = columns * cellSize;
        float boardH = rows * cellSize;

        // --- 4 frame edges ---
        // Y is now up, X is sideways, Z is thickness (facing camera)
        AddBox(root, "Frame_Bottom", new Vector3(0, -boardH/2, 0),
            new Vector3(boardW, frameThickness, boardThickness));
        AddBox(root, "Frame_Left", new Vector3(-boardW/2, 0, 0),
            new Vector3(frameThickness, boardH, boardThickness));
        AddBox(root, "Frame_Right", new Vector3(boardW/2, 0, 0),
            new Vector3(frameThickness, boardH, boardThickness));

        // --- Vertical column dividers ---
        for (int c = 0; c <= columns; c++)
        {
            float x = -boardW/2 + c * cellSize;
            float stripW = cellSize * 0.25f;
            AddBox(root, $"VDiv_{c}", new Vector3(x, 0, 0),
                new Vector3(stripW, boardH, boardThickness));
        }

        // --- Horizontal row dividers ---
        for (int r = 0; r <= rows; r++)
        {
            float y = -boardH/2 + r * cellSize;
            float stripH = cellSize * 0.25f;
            AddBox(root, $"HDiv_{r}", new Vector3(0, y, 0),
                new Vector3(boardW, stripH, boardThickness));
        }

        Debug.Log("Connect4 colliders built!");
    }

    void AddBox(GameObject parent, string name, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = center;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
    }
#endif
}