using UnityEngine;

// Scene-wide starting level and tutorial options. HueGame starts immediately.
[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Game Settings")]
    [Range(1, 100)] public int startingLevel = 1;
    public bool showTutorial = true;

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
