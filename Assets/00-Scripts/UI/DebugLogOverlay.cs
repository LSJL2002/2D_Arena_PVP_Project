using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows Debug.Log / warnings / errors on screen, so you can see them in a build
/// (builds have no Console window). Creates itself automatically when the game starts;
/// nothing needs to be added to a scene. Click the "Logs" button (top-left) to show/hide.
/// </summary>
public class DebugLogOverlay : MonoBehaviour
{
    private const int MaxLines = 40;

    private struct Entry
    {
        public string Text;
        public LogType Type;
    }

    // Fusion can log from background threads, so all access to this list is locked.
    private readonly List<Entry> entries = new();
    private readonly object entriesLock = new();

    private bool visible = true;
    private volatile bool scrollToBottom;
    private Vector2 scroll;
    private GUIStyle lineStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnStartup()
    {
        var go = new GameObject("DebugLogOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugLogOverlay>();
    }

    private void OnEnable()
    {
        Application.logMessageReceivedThreaded += OnLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceivedThreaded -= OnLog;
    }

    private void OnLog(string message, string stackTrace, LogType type)
    {
        string time = System.DateTime.Now.ToString("HH:mm:ss");
        lock (entriesLock)
        {
            entries.Add(new Entry { Text = $"[{time}] {message}", Type = type });
            if (entries.Count > MaxLines)
            {
                entries.RemoveAt(0);
            }
        }
        scrollToBottom = true;
    }

    private void OnGUI()
    {
        lineStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = false };

        if (GUI.Button(new Rect(10, 10, 80, 30), visible ? "Hide Logs" : "Logs"))
        {
            visible = !visible;
        }
        if (!visible) return;

        if (GUI.Button(new Rect(95, 10, 60, 30), "Clear"))
        {
            lock (entriesLock) entries.Clear();
        }

        float width = Mathf.Min(Screen.width - 20, 700);
        float height = Screen.height * 0.45f;
        var area = new Rect(10, 45, width, height);

        GUI.Box(area, GUIContent.none);
        GUI.Box(area, GUIContent.none); // drawn twice for a darker background

        GUILayout.BeginArea(new Rect(area.x + 5, area.y + 5, area.width - 10, area.height - 10));
        if (scrollToBottom)
        {
            scroll.y = float.MaxValue; // follow the newest message
            scrollToBottom = false;
        }
        scroll = GUILayout.BeginScrollView(scroll);

        lock (entriesLock)
        {
            foreach (Entry e in entries)
            {
                lineStyle.normal.textColor = e.Type switch
                {
                    LogType.Warning => Color.yellow,
                    LogType.Error or LogType.Exception or LogType.Assert => new Color(1f, 0.4f, 0.4f),
                    _ => Color.white,
                };
                GUILayout.Label(e.Text, lineStyle);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
