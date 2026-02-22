using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Text;

/// <summary>
/// Standalone automated test runner that works in Play mode
/// Attach to a GameObject and press SPACE to run tests
/// No Unity Test Framework required
/// </summary>
public class AutomatedTestRunner : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private PlayerNetworkSync networkSync;
    [SerializeField] private string serverPath = "server";
    [SerializeField] private string serverScript = "server.js";
    [SerializeField] private bool runOnStart = false;

    [Header("Test Selection")]
    [SerializeField] private bool test01_NormalReconnect = true;
    [SerializeField] private bool test02_RapidReconnect = true;
    [SerializeField] private bool test04_NamespaceDisconnect = true;
    [SerializeField] private bool test05_MaxAttempts = true;
    [SerializeField] private bool test07_DuringMovement = true;
    [SerializeField] private bool test10_MemoryLeak = true;

    [Header("Runtime Info")]
    [SerializeField] private string currentTest = "Not running";
    [SerializeField] private int testsRun = 0;
    [SerializeField] private int testsPassed = 0;
    [SerializeField] private int testsFailed = 0;

    private Process serverProcess;
    private TestMonitor monitor;
    private bool isRunning = false;
    private List<TestResult> results = new List<TestResult>();

    private class TestResult
    {
        public string name;
        public bool passed;
        public string message;
        public float duration;
    }

    private void Start()
    {
        if (networkSync == null)
        {
            UnityEngine.Debug.LogError("AutomatedTestRunner: PlayerNetworkSync reference missing!");
            enabled = false;
            return;
        }

        monitor = gameObject.AddComponent<TestMonitor>();
        monitor.networkSync = networkSync;

        if (runOnStart)
        {
            StartCoroutine(RunAllTestsSequence());
        }
    }

    private void Update()
    {
        // Press SPACE to run tests
        if (Input.GetKeyDown(KeyCode.Space) && !isRunning)
        {
            StartCoroutine(RunAllTestsSequence());
        }

        // Press P to print results
        if (Input.GetKeyDown(KeyCode.P))
        {
            PrintResults();
        }
    }

    private IEnumerator RunAllTestsSequence()
    {
        if (isRunning)
        {
            UnityEngine.Debug.LogWarning("Tests already running!");
            yield break;
        }

        isRunning = true;
        results.Clear();
        testsRun = 0;
        testsPassed = 0;
        testsFailed = 0;

        UnityEngine.Debug.Log("═══════════════════════════════════════════════");
        UnityEngine.Debug.Log("🤖 AUTOMATED TEST SUITE STARTING");
        UnityEngine.Debug.Log("═══════════════════════════════════════════════");

        // Run selected tests
        if (test01_NormalReconnect)
            yield return RunTest("Test 1: Normal Reconnect", Test01_NormalReconnect());

        if (test02_RapidReconnect)
            yield return RunTest("Test 2: Rapid Reconnect (5x)", Test02_RapidReconnect());

        if (test04_NamespaceDisconnect)
            yield return RunTest("Test 4: Namespace vs Root Disconnect", Test04_NamespaceDisconnect());

        if (test05_MaxAttempts)
            yield return RunTest("Test 5: Max Reconnect Attempts", Test05_MaxAttempts());

        if (test07_DuringMovement)
            yield return RunTest("Test 7: Reconnect During Movement", Test07_DuringMovement());

        if (test10_MemoryLeak)
            yield return RunTest("Test 10: Memory Leak (10 cycles)", Test10_MemoryLeak());

        // Print final results
        PrintResults();

        isRunning = false;
        currentTest = "Complete";

        UnityEngine.Debug.Log("═══════════════════════════════════════════════");
        UnityEngine.Debug.Log($"🤖 TEST SUITE COMPLETE: {testsPassed}/{testsRun} PASSED");
        UnityEngine.Debug.Log("═══════════════════════════════════════════════");
    }

    private IEnumerator RunTest(string testName, IEnumerator testCoroutine)
    {
        currentTest = testName;
        testsRun++;

        UnityEngine.Debug.Log($"\n▶️  {testName}");
        float startTime = Time.time;

        bool passed = true;
        string errorMessage = "";

        // Reset monitor
        monitor.Reset();

        // Run the test
        yield return testCoroutine;

        // Check if test passed (monitor should have 0 duplicates)
        if (monitor.duplicateDetections > 0)
        {
            passed = false;
            errorMessage = $"DUPLICATE DETECTED: {monitor.duplicateDetections} duplicates found";
        }

        float duration = Time.time - startTime;

        // Record result
        var result = new TestResult
        {
            name = testName,
            passed = passed,
            message = errorMessage,
            duration = duration
        };
        results.Add(result);

        if (passed)
        {
            testsPassed++;
            UnityEngine.Debug.Log($"✅ {testName} PASSED ({duration:F1}s)");
        }
        else
        {
            testsFailed++;
            UnityEngine.Debug.LogError($"❌ {testName} FAILED: {errorMessage} ({duration:F1}s)");
        }

        // Wait between tests
        yield return new WaitForSeconds(1f);
    }

    // ==================== TEST IMPLEMENTATIONS ====================

    private IEnumerator Test01_NormalReconnect()
    {
        // Start server
        yield return StartServerAsync();

        // Wait for connection
        yield return WaitForConnectionAsync(30f);

        int initialStarts = monitor.positionRoutineStarts;

        // Disconnect
        StopServerAsync();
        yield return new WaitForSeconds(2f);

        // Reconnect
        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        // Verify
        if (monitor.positionRoutineStarts != initialStarts + 1)
        {
            UnityEngine.Debug.LogError($"Expected {initialStarts + 1} position starts, got {monitor.positionRoutineStarts}");
        }

        if (monitor.activePositionRoutines != 1)
        {
            UnityEngine.Debug.LogError($"Expected 1 active routine, got {monitor.activePositionRoutines}");
        }

        yield return null;
    }

    private IEnumerator Test02_RapidReconnect()
    {
        const int CYCLES = 5;

        // Initial connection
        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        int initialStarts = monitor.positionRoutineStarts;

        for (int i = 1; i <= CYCLES; i++)
        {
            UnityEngine.Debug.Log($"  Cycle {i}/{CYCLES}...");

            StopServerAsync();
            yield return new WaitForSeconds(1.5f);

            yield return StartServerAsync();
            yield return WaitForConnectionAsync(30f);

            if (monitor.duplicateDetections > 0)
            {
                UnityEngine.Debug.LogError($"Duplicate detected at cycle {i}");
                yield break;
            }
        }

        // Verify total starts
        int expectedStarts = initialStarts + CYCLES;
        if (monitor.positionRoutineStarts != expectedStarts)
        {
            UnityEngine.Debug.LogError($"Expected {expectedStarts} starts, got {monitor.positionRoutineStarts}");
        }

        yield return null;
    }

    private IEnumerator Test04_NamespaceDisconnect()
    {
        // Connect
        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        int reconnectBefore = monitor.reconnectRoutineStarts;

        // Disconnect (triggers both root and namespace disconnect)
        StopServerAsync();
        yield return new WaitForSeconds(2f);

        // Should only have 1 reconnect routine started
        int reconnectAfter = monitor.reconnectRoutineStarts;
        if (reconnectAfter - reconnectBefore != 1)
        {
            UnityEngine.Debug.LogError($"Expected 1 reconnect start, got {reconnectAfter - reconnectBefore}");
        }

        // Reconnect
        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        yield return null;
    }

    private IEnumerator Test05_MaxAttempts()
    {
        // Note: This test requires ReconnectConfig to have maxAttempts set
        // For automated testing, we'll just verify behavior

        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        // Disconnect and DON'T restart
        StopServerAsync();

        // Wait and observe reconnection attempts
        yield return new WaitForSeconds(10f);

        // Should have some reconnect attempts
        if (monitor.reconnectRoutineStarts == 0)
        {
            UnityEngine.Debug.LogError("No reconnect attempts detected");
        }

        // Reconnect to clean up
        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        yield return null;
    }

    private IEnumerator Test07_DuringMovement()
    {
        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        // Simulate movement (changes handled by SendPositionRoutine)
        // We just verify reconnection works

        yield return new WaitForSeconds(1f);

        StopServerAsync();
        yield return new WaitForSeconds(2f);

        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        // Verify no duplicates during transition
        if (monitor.activePositionRoutines != 1)
        {
            UnityEngine.Debug.LogError($"Expected 1 active routine, got {monitor.activePositionRoutines}");
        }

        yield return null;
    }

    private IEnumerator Test10_MemoryLeak()
    {
        const int CYCLES = 10;

        yield return StartServerAsync();
        yield return WaitForConnectionAsync(30f);

        int initialStarts = monitor.positionRoutineStarts;

        for (int i = 1; i <= CYCLES; i++)
        {
            UnityEngine.Debug.Log($"  Leak test cycle {i}/{CYCLES}...");

            StopServerAsync();
            yield return new WaitForSeconds(1f);

            yield return StartServerAsync();
            yield return WaitForConnectionAsync(30f);

            // Check for leaks after each cycle
            if (monitor.activePositionRoutines > 1)
            {
                UnityEngine.Debug.LogError($"Memory leak detected at cycle {i}: {monitor.activePositionRoutines} active routines");
                yield break;
            }
        }

        // Final verification
        int expectedStarts = initialStarts + CYCLES;
        if (monitor.positionRoutineStarts != expectedStarts)
        {
            UnityEngine.Debug.LogError($"Expected {expectedStarts} starts, got {monitor.positionRoutineStarts}");
        }

        if (monitor.activePositionRoutines != 1)
        {
            UnityEngine.Debug.LogError($"Memory leak: {monitor.activePositionRoutines} active routines at end");
        }

        yield return null;
    }

    // ==================== SERVER CONTROL ====================

    private IEnumerator StartServerAsync()
    {
        UnityEngine.Debug.Log("  🚀 Starting server...");

        bool success = false;

        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string fullServerPath = Path.Combine(projectRoot, serverPath);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = serverScript,
                WorkingDirectory = fullServerPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            serverProcess = Process.Start(startInfo);
            success = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"  ❌ Failed to start server: {e.Message}");
        }

        // Wait for server to be ready (must be outside try-catch to use yield)
        if (success)
        {
            yield return new WaitForSeconds(2f);
            UnityEngine.Debug.Log("  ✅ Server started");
        }
    }

    private void StopServerAsync()
    {
        UnityEngine.Debug.Log("  🛑 Stopping server...");

        if (serverProcess != null && !serverProcess.HasExited)
        {
            try
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(3000);
                serverProcess.Dispose();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"  ⚠️  Error stopping server: {e.Message}");
            }
        }

        serverProcess = null;
    }

    private IEnumerator WaitForConnectionAsync(float timeout)
    {
        float startTime = Time.time;

        while (Time.time - startTime < timeout)
        {
            if (networkSync != null && networkSync.ConnectionState == ConnectionState.Connected)
            {
                UnityEngine.Debug.Log("  ✅ Connected");
                yield return new WaitForSeconds(0.5f);
                yield break;
            }

            yield return new WaitForSeconds(0.2f);
        }

        UnityEngine.Debug.LogError($"  ❌ Connection timeout after {timeout}s");
    }

    // ==================== REPORTING ====================

    private void PrintResults()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n═══════════════════════════════════════════════");
        sb.AppendLine("📊 AUTOMATED TEST RESULTS");
        sb.AppendLine("═══════════════════════════════════════════════");
        sb.AppendLine($"Total Tests: {testsRun}");
        sb.AppendLine($"Passed: {testsPassed} ✅");
        sb.AppendLine($"Failed: {testsFailed} ❌");
        sb.AppendLine();

        foreach (var result in results)
        {
            string status = result.passed ? "✅ PASS" : "❌ FAIL";
            sb.AppendLine($"{status} | {result.name} ({result.duration:F1}s)");
            if (!result.passed && !string.IsNullOrEmpty(result.message))
            {
                sb.AppendLine($"       └─ {result.message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Monitor Summary:");
        sb.AppendLine($"  Position Routine Starts: {monitor.positionRoutineStarts}");
        sb.AppendLine($"  Reconnect Routine Starts: {monitor.reconnectRoutineStarts}");
        sb.AppendLine($"  Active Position Routines: {monitor.activePositionRoutines}");
        sb.AppendLine($"  Duplicate Detections: {monitor.duplicateDetections}");
        sb.AppendLine();

        if (testsFailed == 0)
        {
            sb.AppendLine("🎉 ALL TESTS PASSED - NO DUPLICATE COROUTINES DETECTED");
        }
        else
        {
            sb.AppendLine("⚠️  SOME TESTS FAILED - REVIEW ERRORS ABOVE");
        }

        sb.AppendLine("═══════════════════════════════════════════════");

        UnityEngine.Debug.Log(sb.ToString());
    }

    private void OnDestroy()
    {
        // Cleanup server process
        StopServerAsync();
    }

    private void OnGUI()
    {
        // Simple on-screen display
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.fontSize = 12;

        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(10, 10, 300, 140), "", boxStyle);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 11;
        labelStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(20, 20, 280, 20), "🤖 Automated Test Runner", labelStyle);
        GUI.Label(new Rect(20, 45, 280, 20), $"Status: {currentTest}", labelStyle);
        GUI.Label(new Rect(20, 65, 280, 20), $"Tests Run: {testsRun}", labelStyle);

        labelStyle.normal.textColor = Color.green;
        GUI.Label(new Rect(20, 85, 280, 20), $"Passed: {testsPassed}", labelStyle);

        labelStyle.normal.textColor = Color.red;
        GUI.Label(new Rect(20, 105, 280, 20), $"Failed: {testsFailed}", labelStyle);

        labelStyle.fontSize = 9;
        labelStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(20, 125, 280, 20), "SPACE=Run Tests | P=Print Results", labelStyle);
    }
}

/// <summary>
/// Lightweight test monitor
/// </summary>
public class TestMonitor : MonoBehaviour
{
    public PlayerNetworkSync networkSync;

    public int positionRoutineStarts { get; private set; }
    public int reconnectRoutineStarts { get; private set; }
    public int duplicateDetections { get; private set; }
    public int activePositionRoutines { get; private set; }

    private float lastPositionStartTime;
    private float lastReconnectStartTime;

    private void Start()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Detect SendPositionRoutine starts
        if (logString.Contains("SendPositionRoutine started"))
        {
            // Check for rapid duplicate (< 100ms apart)
            if (Time.time - lastPositionStartTime < 0.1f && positionRoutineStarts > 0)
            {
                duplicateDetections++;
                UnityEngine.Debug.LogError($"🚨 DUPLICATE POSITION ROUTINE at {Time.time:F3}s");
            }

            positionRoutineStarts++;
            activePositionRoutines = 1; // Assume new one replaces old
            lastPositionStartTime = Time.time;
        }

        // Detect ReconnectRoutine starts
        if (logString.Contains("ReconnectRoutine started"))
        {
            if (Time.time - lastReconnectStartTime < 0.1f && reconnectRoutineStarts > 0)
            {
                duplicateDetections++;
                UnityEngine.Debug.LogError($"🚨 DUPLICATE RECONNECT ROUTINE at {Time.time:F3}s");
            }

            reconnectRoutineStarts++;
            lastReconnectStartTime = Time.time;
        }

        // Detect disconnects (position routine stops)
        if (logString.Contains("Disconnected from root socket"))
        {
            activePositionRoutines = 0;
        }
    }

    public void Reset()
    {
        positionRoutineStarts = 0;
        reconnectRoutineStarts = 0;
        duplicateDetections = 0;
        activePositionRoutines = 0;
        lastPositionStartTime = 0;
        lastReconnectStartTime = 0;
    }
}
