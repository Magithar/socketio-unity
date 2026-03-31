using System.Collections.Generic;
using UnityEngine;
using SocketIOUnity.Runtime;

/// <summary>
/// Runtime diagnostics overlay. Shows connection state, RTT, throughput, pending ACKs,
/// and a live event log. Works in both Editor Play Mode and standalone builds.
///
/// Usage (simplest):
///   SocketIOManager.Instance.ShowDiagnostics = true;
///
/// Usage (standalone, without SocketIOManager):
///   var overlay = gameObject.AddComponent<SocketIODiagnosticsOverlay>();
///   overlay.Socket = mySocketIOClient;
///
/// DiagnosticsPanel prefab:
///   Add this component to an empty GameObject and save it as a prefab named
///   "DiagnosticsPanel". Toggle visibility via SetActive or ShowDiagnostics.
/// </summary>
[AddComponentMenu("SocketIO/Diagnostics Overlay")]
public sealed class SocketIODiagnosticsOverlay : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] private Vector2 _position = new Vector2(10f, 10f);

    [Header("Display")]
    [SerializeField] private bool _showEventLog = true;
    [SerializeField] private int _maxLogEntries = 15;

    // Optional direct assignment — auto-detects via SocketIOManager otherwise.
    public SocketIOClient Socket
    {
        get => _socket;
        set
        {
            if (_socket == value) return;
            UnbindSocket();
            _socket = value;
            TryBindSocket();
        }
    }

    private SocketIOClient _socket;
    private bool _subscribed;

    private struct LogEntry { public string text; public Color color; }
    private readonly List<LogEntry> _log = new List<LogEntry>();
    private Vector2 _logScroll;

    // IMGUI styles — lazily initialized on first OnGUI call.
    private GUIStyle _boxStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _metricStyle;
    private GUIStyle _logStyle;

    private const int PanelWidth = 330;
    private const int LogLineHeight = 16;

    // ------------------------------------------------------------------
    // Lifecycle

    private void Update()
    {
        if (!_subscribed)
            TryBindSocket();
    }

    private void OnDisable()
    {
        UnbindSocket();
    }

    // ------------------------------------------------------------------
    // Socket binding

    private void TryBindSocket()
    {
        if (_subscribed) return;

        // Use explicit assignment first, then fall back to SocketIOManager.
        if (_socket == null && SocketIOManager.Instance != null)
            _socket = SocketIOManager.Instance.Socket;

        if (_socket == null) return;

        _socket.OnConnected    += HandleConnected;
        _socket.OnDisconnected += HandleDisconnected;
        _socket.OnError        += HandleError;
        _socket.OnStateChanged += HandleStateChanged;
        _subscribed = true;
    }

    private void UnbindSocket()
    {
        if (!_subscribed || _socket == null) return;

        _socket.OnConnected    -= HandleConnected;
        _socket.OnDisconnected -= HandleDisconnected;
        _socket.OnError        -= HandleError;
        _socket.OnStateChanged -= HandleStateChanged;
        _subscribed = false;
    }

    // ------------------------------------------------------------------
    // Event log population

    private void HandleConnected()             => AppendLog("Connected",                         Color.green);
    private void HandleDisconnected()          => AppendLog("Disconnected",                      new Color(0.9f, 0.4f, 0.4f));
    private void HandleError(SocketError err)  => AppendLog($"Error [{err.Type}]: {err.Message}", Color.red);
    private void HandleStateChanged(ConnectionState s) => AppendLog($"State → {s}",              Color.yellow);

    private void AppendLog(string text, Color color)
    {
        if (_log.Count >= _maxLogEntries)
            _log.RemoveAt(0);
        _log.Add(new LogEntry { text = $"[{System.DateTime.Now:HH:mm:ss}] {text}", color = color });
        _logScroll.y = float.MaxValue; // auto-scroll to bottom
    }

    // ------------------------------------------------------------------
    // Rendering

    private void InitStyles()
    {
        if (_boxStyle != null) return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6)
        };

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
            normal    = { textColor = Color.white }
        };

        _metricStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal   = { textColor = Color.white }
        };

        _logStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 10,
            wordWrap  = false
        };
    }

    private void OnGUI()
    {
        InitStyles();

        float panelHeight = 155f + (_showEventLog ? 26f + _maxLogEntries * LogLineHeight : 0f);
        var rect = new Rect(_position.x, _position.y, PanelWidth, panelHeight);

        GUI.Box(rect, GUIContent.none, _boxStyle);
        GUILayout.BeginArea(rect);
        GUILayout.Space(4);

        GUILayout.Label("  Socket.IO Diagnostics", _titleStyle);
        DrawDivider();
        DrawStateRow();
        DrawDivider();
        DrawMetrics();

        if (_showEventLog)
        {
            DrawDivider();
            GUILayout.Label("  Event Log", _titleStyle);
            DrawEventLog();
        }

        GUILayout.EndArea();
    }

    private void DrawStateRow()
    {
        var state = _socket?.State ?? ConnectionState.Disconnected;
        Color stateColor = state switch
        {
            ConnectionState.Connected    => Color.green,
            ConnectionState.Connecting   => Color.yellow,
            ConnectionState.Reconnecting => new Color(1f, 0.6f, 0f),
            _                            => new Color(0.8f, 0.35f, 0.35f)
        };
        var prev = GUI.color;
        GUI.color = stateColor;
        GUILayout.Label($"  ● {state}", _metricStyle);
        GUI.color = prev;
    }

    private void DrawMetrics()
    {
        bool connected = _socket?.IsConnected == true;

#pragma warning disable CS0618 // Obsolete telemetry — accepted for diagnostics overlay
        string rtt     = connected ? $"{_socket.PingRttMs:0.0} ms"             : "--";
        string ns      = connected ? _socket.NamespaceCount.ToString()          : "--";
        string acks    = connected ? _socket.PendingAckCount.ToString()         : "--";
#pragma warning restore CS0618

        GUILayout.Label($"  RTT:          {rtt}",  _metricStyle);
        GUILayout.Label($"  Namespaces:   {ns}",   _metricStyle);
        GUILayout.Label($"  Pending ACKs: {acks}", _metricStyle);

#if SOCKETIO_PROFILER_COUNTERS
        float sent = SocketIOUnity.Debugging.SocketIOThroughputTracker.SentBytesPerSec;
        float recv = SocketIOUnity.Debugging.SocketIOThroughputTracker.ReceivedBytesPerSec;
        GUILayout.Label($"  \u2b06 Sent:       {sent:0} B/s", _metricStyle);
        GUILayout.Label($"  \u2b07 Recv:       {recv:0} B/s", _metricStyle);
#else
        GUILayout.Label("  \u2b06 Sent:       (define SOCKETIO_PROFILER_COUNTERS)", _metricStyle);
        GUILayout.Label("  \u2b07 Recv:       (define SOCKETIO_PROFILER_COUNTERS)", _metricStyle);
#endif
    }

    private void DrawEventLog()
    {
        _logScroll = GUILayout.BeginScrollView(_logScroll,
            GUILayout.Height(_maxLogEntries * LogLineHeight));

        foreach (var entry in _log)
        {
            _logStyle.normal.textColor = entry.color;
            GUILayout.Label(entry.text, _logStyle);
        }

        GUILayout.EndScrollView();
    }

    private static void DrawDivider()
    {
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
    }
}
