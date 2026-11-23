using UnityEngine;

// Fresh-save starting level and first-run tutorial options. Existing progress takes priority.
[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Game Settings")]
    [Tooltip("仅在没有关卡存档时生效；已有存档优先恢复未通关关卡。")]
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
