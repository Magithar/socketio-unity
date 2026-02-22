using UnityEngine;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Automated coroutine duplicate detection tool
/// Attach to PlayerSyncManager to monitor coroutine lifecycle
/// </summary>
public class CoroutineDebugger : MonoBehaviour
{
    [SerializeField] private PlayerNetworkSync networkSync;

    [Header("Detection Settings")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool detectDuplicates = true;
    [SerializeField] private float detectionWindow = 0.1f; // 100ms window for duplicate detection

    private class CoroutineEvent
    {
        public string name;
        public int instanceId;
        public float timestamp;
        public string type; // "started" or "ended"
    }

    private List<CoroutineEvent> events = new List<CoroutineEvent>();
    private Dictionary<string, int> activeInstances = new Dictionary<string, int>();

    // Stats
    private int totalPositionRoutineStarts = 0;
    private int totalReconnectRoutineStarts = 0;
    private int duplicateDetections = 0;

    private void Start()
    {
        if (networkSync == null)
        {
            Debug.LogError("CoroutineDebugger: PlayerNetworkSync reference missing!");
            enabled = false;
            return;
        }

        Debug.Log("🔬 CoroutineDebugger: Monitoring enabled");
        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (!detectDuplicates) return;

        // Parse Unity logs to detect coroutine lifecycle events
        if (logString.Contains("SendPositionRoutine started"))
        {
            totalPositionRoutineStarts++;
            ParseAndRecordEvent(logString, "SendPosition", "started");
        }
        else if (logString.Contains("ReconnectRoutine started"))
        {
            totalReconnectRoutineStarts++;
            ParseAndRecordEvent(logString, "Reconnect", "started");
        }
        else if (logString.Contains("ReconnectRoutine ended"))
        {
            ParseAndRecordEvent(logString, "Reconnect", "ended");
        }
        else if (logString.Contains("Disconnected from root socket"))
        {
            // Position routine should stop here
            RecordEvent("SendPosition", -1, "ended");
        }
    }

    private void ParseAndRecordEvent(string logString, string coroutineName, string eventType)
    {
        // Extract instance ID from log like: "SendPositionRoutine started (Instance #1)"
        int instanceId = -1;
        int hashIndex = logString.IndexOf("Instance #");
        if (hashIndex != -1)
        {
            int startIndex = hashIndex + "Instance #".Length;
            int endIndex = logString.IndexOf(")", startIndex);
            if (endIndex != -1)
            {
                string idStr = logString.Substring(startIndex, endIndex - startIndex);
                int.TryParse(idStr, out instanceId);
            }
        }

        RecordEvent(coroutineName, instanceId, eventType);
    }

    private void RecordEvent(string coroutineName, int instanceId, string eventType)
    {
        var evt = new CoroutineEvent
        {
            name = coroutineName,
            instanceId = instanceId,
            timestamp = Time.time,
            type = eventType
        };

        events.Add(evt);

        // Update active instance tracking
        string key = coroutineName;

        if (eventType == "started")
        {
            if (activeInstances.ContainsKey(key))
            {
                int prevInstance = activeInstances[key];

                // Check if previous instance is still active
                bool isDuplicate = CheckForDuplicate(coroutineName, prevInstance, detectionWindow);

                if (isDuplicate)
                {
                    duplicateDetections++;
                    Debug.LogError($"🚨 DUPLICATE COROUTINE DETECTED! {coroutineName} Instance #{prevInstance} still active when Instance #{instanceId} started");
                    Debug.LogError($"   Time between starts: {Time.time - GetLastStartTime(coroutineName, prevInstance):F3}s");
                }
            }

            activeInstances[key] = instanceId;

            if (enableLogging)
            {
                Debug.Log($"🔬 [{Time.time:F2}s] {coroutineName} Instance #{instanceId} started (Active instances: {activeInstances.Count})");
            }
        }
        else if (eventType == "ended")
        {
            if (activeInstances.ContainsKey(key))
            {
                activeInstances.Remove(key);

                if (enableLogging)
                {
                    Debug.Log($"🔬 [{Time.time:F2}s] {coroutineName} Instance #{instanceId} ended (Active instances: {activeInstances.Count})");
                }
            }
        }
    }

    private bool CheckForDuplicate(string coroutineName, int previousInstance, float timeWindow)
    {
        // Look backwards through events to see if previous instance was stopped
        float currentTime = Time.time;

        for (int i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];

            // Only check recent events (within time window)
            if (currentTime - evt.timestamp > timeWindow)
                break;

            // Found an end event for the previous instance
            if (evt.name == coroutineName &&
                evt.instanceId == previousInstance &&
                evt.type == "ended")
            {
                return false; // Previous instance was properly stopped
            }
        }

        // No end event found for previous instance within time window = duplicate!
        return true;
    }

    private float GetLastStartTime(string coroutineName, int instanceId)
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];
            if (evt.name == coroutineName &&
                evt.instanceId == instanceId &&
                evt.type == "started")
            {
                return evt.timestamp;
            }
        }

        return 0f;
    }

    private void Update()
    {
        // Press 'L' to print detailed log
        if (Input.GetKeyDown(KeyCode.L))
        {
            PrintDetailedLog();
        }

        // Press 'R' to print report
        if (Input.GetKeyDown(KeyCode.R))
        {
            PrintReport();
        }
    }

    private void PrintDetailedLog()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🔬 ===== COROUTINE EVENT LOG =====");
        sb.AppendLine($"Total Events: {events.Count}");
        sb.AppendLine();

        foreach (var evt in events)
        {
            sb.AppendLine($"[{evt.timestamp:F2}s] {evt.name} #{evt.instanceId} {evt.type}");
        }

        sb.AppendLine("================================");
        Debug.Log(sb.ToString());
    }

    private void PrintReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🔬 ===== COROUTINE TEST REPORT =====");
        sb.AppendLine($"Session Duration: {Time.time:F1}s");
        sb.AppendLine();
        sb.AppendLine("Lifecycle Events:");
        sb.AppendLine($"  SendPositionRoutine starts: {totalPositionRoutineStarts}");
        sb.AppendLine($"  ReconnectRoutine starts:    {totalReconnectRoutineStarts}");
        sb.AppendLine();
        sb.AppendLine("Currently Active:");
        sb.AppendLine($"  Active coroutines: {activeInstances.Count}");
        foreach (var kvp in activeInstances)
        {
            sb.AppendLine($"    - {kvp.Key} Instance #{kvp.Value}");
        }
        sb.AppendLine();

        if (duplicateDetections > 0)
        {
            sb.AppendLine($"❌ DUPLICATE DETECTIONS: {duplicateDetections}");
            sb.AppendLine("   TEST FAILED - Review console for details");
        }
        else
        {
            sb.AppendLine("✅ NO DUPLICATES DETECTED");
            sb.AppendLine("   Test passed");
        }

        sb.AppendLine();
        sb.AppendLine("Connection State:");
        sb.AppendLine($"  Current: {networkSync.ConnectionState}");
        sb.AppendLine($"  Reconnect Attempts: {networkSync.ReconnectAttempt}");
        sb.AppendLine("==================================");

        Debug.Log(sb.ToString());
    }

    private void OnGUI()
    {
        if (!enableLogging) return;

        // Simple HUD in top-right corner
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 12;

        string status = duplicateDetections > 0 ? "❌ DUPLICATES DETECTED" : "✅ No Duplicates";
        Color color = duplicateDetections > 0 ? Color.red : Color.green;

        GUI.backgroundColor = new Color(0, 0, 0, 0.7f);
        GUI.Box(new Rect(Screen.width - 250, 80, 240, 120), "", style);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 11;
        labelStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(Screen.width - 240, 90, 220, 20), "🔬 Coroutine Debugger", labelStyle);
        GUI.Label(new Rect(Screen.width - 240, 110, 220, 20), $"Position starts: {totalPositionRoutineStarts}", labelStyle);
        GUI.Label(new Rect(Screen.width - 240, 130, 220, 20), $"Reconnect starts: {totalReconnectRoutineStarts}", labelStyle);
        GUI.Label(new Rect(Screen.width - 240, 150, 220, 20), $"Active: {activeInstances.Count}", labelStyle);

        labelStyle.normal.textColor = color;
        GUI.Label(new Rect(Screen.width - 240, 170, 220, 20), status, labelStyle);

        labelStyle.fontSize = 9;
        labelStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(Screen.width - 240, 185, 220, 20), "L=Log | R=Report", labelStyle);
    }
}
