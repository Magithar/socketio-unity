using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;
using Newtonsoft.Json;

public class PlayerNetworkSync : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField]
    [Tooltip("Default server URL for Unity Editor (development)")]
    private string editorServerUrl = "http://localhost:3000";

#pragma warning disable CS0414 // Field assigned but never used (false positive - used in non-Editor builds)
    [SerializeField]
    [Tooltip("Default server URL for production builds (will be overridden if user sets custom URL). Use localhost for local testing.")]
    private string productionServerUrl = "http://localhost:3000";
#pragma warning restore CS0414

    // Public API for UI components to check if URL changes are allowed
    public bool AllowRuntimeUrlChange => true; // Always true for now, can be made configurable later

    private string serverUrl; // Actual URL to use (computed at runtime)

    // PlayerPrefs key for storing custom server URL
    private const string CUSTOM_SERVER_URL_KEY = "PlayerSync_CustomServerUrl";

    // Public API to get/set server URL at runtime
    public string CurrentServerUrl => serverUrl;

    public bool SetCustomServerUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("Cannot set empty server URL");
            return false;
        }

        PlayerPrefs.SetString(CUSTOM_SERVER_URL_KEY, url);
        PlayerPrefs.Save();
        Debug.Log($"Custom server URL saved: {url}. Restart required to apply.");
        return true;
    }

    public void ClearCustomServerUrl()
    {
        PlayerPrefs.DeleteKey(CUSTOM_SERVER_URL_KEY);
        PlayerPrefs.Save();
        Debug.Log("Custom server URL cleared. Restart to use default.");
    }

    [SerializeField] private Transform localPlayerTransform;  // Reference to LocalPlayer
    public PlayerController controller;
    public PlayerSpawner spawner;

    [Header("Reconnection Settings")]
    [SerializeField]
    [Tooltip("Custom reconnection configuration. Leave default for standard exponential backoff (1s → 2s → 4s → 8s → 16s → 30s max)")]
    private ReconnectConfig reconnectConfig = new ReconnectConfig();

    private SocketIOClient rootSocket;
    private NamespaceSocket namespaceSocket;  // The /playersync namespace

    // Public API - maintain compatibility with RTTDisplay and other scripts
    public SocketIOClient Socket => rootSocket;

    [Header("Network Settings")]
    [SerializeField]
    [Range(10f, 1000f)]
    [Tooltip("Network update interval in milliseconds (e.g., 50ms = 20Hz, 100ms = 10Hz, 33ms = 30Hz)")]
    private float updateIntervalMs = 50f;
    private string playerId;
    private bool isNamespaceConnected = false;
    private Coroutine positionRoutine; // Track the coroutine to prevent duplicates
    private Coroutine reconnectRoutine; // Track reconnection attempts

    // Public API for UI
    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;
    public int ReconnectAttempt { get; private set; } = 0;

    private float lastReconnectCheckTime;
    private bool isReconnecting = false;
    private bool isDestroyed = false;

    /// <summary>
    /// Computes the server URL based on platform and user preferences.
    /// Priority: Custom URL (PlayerPrefs) > Platform default
    /// </summary>
    private string GetServerUrl()
    {
        // 1. Check if user has set a custom URL (highest priority)
        if (PlayerPrefs.HasKey(CUSTOM_SERVER_URL_KEY))
        {
            string customUrl = PlayerPrefs.GetString(CUSTOM_SERVER_URL_KEY);
            Debug.Log($"Using custom server URL from PlayerPrefs: {customUrl}");
            return customUrl;
        }

        // 2. Use platform-specific defaults
#if UNITY_EDITOR
        Debug.Log($"Using Editor default URL: {editorServerUrl}");
        return editorServerUrl;
#elif UNITY_ANDROID || UNITY_IOS
        Debug.Log($"Using mobile production URL: {productionServerUrl}");
        return productionServerUrl;
#elif UNITY_WEBGL
        Debug.Log($"Using WebGL production URL: {productionServerUrl}");
        return productionServerUrl;
#else
        Debug.Log($"Using standalone production URL: {productionServerUrl}");
        return productionServerUrl;
#endif
    }

    private void Start()
    {
        Debug.Log("PlayerNetworkSync START - Script is running!");

        // Compute server URL based on platform and user preferences
        serverUrl = GetServerUrl();
        Debug.Log($"📡 Using server URL: {serverUrl}");

        try
        {
            Debug.Log("Creating SocketIOClient...");
            rootSocket = new SocketIOClient(TransportFactoryHelper.CreateDefault());

            // Apply custom reconnection configuration
            rootSocket.ReconnectConfig = reconnectConfig;

            Debug.Log($"SocketIOClient created successfully with reconnect config: " +
                     $"initialDelay={reconnectConfig.initialDelay}s, multiplier={reconnectConfig.multiplier}, " +
                     $"maxDelay={reconnectConfig.maxDelay}s, jitter={reconnectConfig.jitterPercent * 100}%");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create SocketIOClient: {e.Message}\n{e.StackTrace}");
            return;
        }

        // Connect to root first
        Debug.Log($"Connecting to root: {serverUrl}");
        ConnectionState = ConnectionState.Connecting;
        ConnectToServer();

        // Add error handler to root socket
        rootSocket.OnError += (error) =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            Debug.LogError($"❌ Socket Error: {error}");

            // Switch to reconnecting state if we lose connection at any time
            if (ConnectionState == ConnectionState.Connecting || ConnectionState == ConnectionState.Connected)
            {
                ConnectionState = ConnectionState.Reconnecting;
                ReconnectAttempt = ConnectionState == ConnectionState.Connecting ? 1 : ReconnectAttempt + 1;
                isNamespaceConnected = false;
                controller.CanMove = false;

                // Stop position updates
                if (positionRoutine != null)
                {
                    StopCoroutine(positionRoutine);
                    positionRoutine = null;
                }

                // Clean up all remote players
                spawner.RemoveAllRemotePlayers();
            }
        };

        // Add disconnect handler to root socket
        rootSocket.OnDisconnected += () =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            Debug.LogWarning("❌ Disconnected from root socket");
            isNamespaceConnected = false;
            controller.CanMove = false;
            ConnectionState = ConnectionState.Reconnecting;
            ReconnectAttempt = 0;

            // Stop position updates
            if (positionRoutine != null)
            {
                StopCoroutine(positionRoutine);
                positionRoutine = null;
            }

            // Clean up all remote players
            spawner.RemoveAllRemotePlayers();

            // Start reconnection attempts
            if (reconnectRoutine == null && !isReconnecting)
            {
                reconnectRoutine = StartCoroutine(ReconnectRoutine());
            }
        };

        SetupNamespace();
    }

    private void ConnectToServer()
    {
        rootSocket.Connect(serverUrl);
        SetupNamespace();
    }

    private void SetupNamespace()
    {
        // Get the /playersync namespace
        Debug.Log("Getting /playersync namespace...");
        namespaceSocket = rootSocket.Of("/playersync");

        namespaceSocket.OnConnected += () =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            Debug.Log("✅ Connected to /playersync namespace!");
            isNamespaceConnected = true;
            ConnectionState = ConnectionState.Connected;
            ReconnectAttempt = 0; // Reset attempt counter on successful connection
            isReconnecting = false;

            // Stop reconnection attempts if running
            if (reconnectRoutine != null)
            {
                StopCoroutine(reconnectRoutine);
                reconnectRoutine = null;
            }
        };

        namespaceSocket.OnDisconnected += () =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            Debug.LogWarning("❌ Disconnected from /playersync");
            isNamespaceConnected = false;
            controller.CanMove = false;
            ConnectionState = ConnectionState.Reconnecting;
            ReconnectAttempt = 1; // First attempt

            // Stop position updates
            if (positionRoutine != null)
            {
                StopCoroutine(positionRoutine);
                positionRoutine = null;
            }

            // Clean up all remote players
            spawner.RemoveAllRemotePlayers();
        };

        // Server sends authoritative player ID
        namespaceSocket.On("player_id", (string response) =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            playerId = response.Trim('"');  // Remove quotes if present
            Debug.Log($"Received player ID from server: {playerId}");

            controller.CanMove = true;

            // Stop existing coroutine if reconnecting
            if (positionRoutine != null)
            {
                StopCoroutine(positionRoutine);
            }

            positionRoutine = StartCoroutine(SendPositionRoutine());
        });

        // Receive existing players when joining
        namespaceSocket.On("existing_players", (string response) =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            Debug.Log($"📦 Raw existing_players response: {response}");
            var playersDict = JsonConvert.DeserializeObject<Dictionary<string, PositionData>>(response);
            Debug.Log($"Received {playersDict.Count} existing players (my ID: {playerId})");

            foreach (var kvp in playersDict)
            {
                Debug.Log($"  → Player in dict: {kvp.Key} at {kvp.Value.ToVector3()}");

                if (kvp.Key == playerId)
                {
                    Debug.Log($"    Skipping self: {kvp.Key}");
                    continue;
                }

                Debug.Log($"    Spawning remote player: {kvp.Key}");
                spawner.SpawnRemotePlayer(kvp.Key);
                spawner.UpdateRemotePlayer(kvp.Key, kvp.Value.ToVector3());
            }
        });

        // Another player joined
        namespaceSocket.On("player_join", (string response) =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            string id = response.Trim('"');  // Remove quotes if present
            if (id == playerId) return;

            Debug.Log($"Player joined: {id}");
            spawner.SpawnRemotePlayer(id);
        });

        // Another player moved
        namespaceSocket.On("player_move", (string response) =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            var data = JsonConvert.DeserializeObject<MovePacket>(response);

            if (data.id == playerId)
                return;

            // Position updates happen 20x/sec - don't log every one
            spawner.UpdateRemotePlayer(data.id, data.position.ToVector3());
        });

        // Player disconnected
        namespaceSocket.On("player_leave", (string response) =>
        {
            if (isDestroyed) return; // Don't process events after destruction

            string id = response.Trim('"');  // Remove quotes if present
            Debug.Log($"Player left: {id}");
            spawner.RemoveRemotePlayer(id);
        });
    }

    private void Update()
    {
        // Track reconnect attempts while disconnected
        if (ConnectionState == ConnectionState.Reconnecting &&
            rootSocket != null &&
            !isNamespaceConnected)
        {
            // Increment attempt counter every 2 seconds (approximate exponential backoff check)
            if (Time.time - lastReconnectCheckTime > 2f)
            {
                lastReconnectCheckTime = Time.time;
                ReconnectAttempt++;
            }
        }
    }

    private IEnumerator ReconnectRoutine()
    {
        isReconnecting = true;
        ReconnectAttempt = 0;

        // NOTE: This sample demonstrates manual reconnection control for UI state tracking.
        // SocketIOClient also has built-in automatic reconnection (configured via ReconnectConfig).
        // Use the built-in reconnection for production; this is for demonstration only.

        while (!isNamespaceConnected && ConnectionState == ConnectionState.Reconnecting)
        {
            // Check max attempts if configured
            if (reconnectConfig.maxAttempts > 0 && ReconnectAttempt >= reconnectConfig.maxAttempts)
            {
                Debug.LogWarning($"⚠️ Max reconnect attempts ({reconnectConfig.maxAttempts}) reached");
                ConnectionState = ConnectionState.Disconnected;
                isReconnecting = false;
                break;
            }

            ReconnectAttempt++;

            // Calculate delay using configurable exponential backoff
            float baseDelay = reconnectConfig.initialDelay * Mathf.Pow(reconnectConfig.multiplier, ReconnectAttempt - 1);
            float delay = Mathf.Min(baseDelay, reconnectConfig.maxDelay);

            // Apply jitter if configured
            if (reconnectConfig.jitterPercent > 0f)
            {
                float jitterAmount = delay * reconnectConfig.jitterPercent;
                delay += UnityEngine.Random.Range(-jitterAmount, jitterAmount);
                delay = Mathf.Max(delay, 0.1f);
            }

            Debug.Log($"🔄 Reconnection attempt {ReconnectAttempt} in {delay:0.2f} seconds...");
            yield return new WaitForSeconds(delay);

            if (isNamespaceConnected)
            {
                Debug.Log("✅ Already reconnected, stopping reconnection routine");
                break;
            }

            try
            {
                Debug.Log($"🔌 Attempting to reconnect to {serverUrl}...");

                // Dispose old socket and create new one
                if (rootSocket != null)
                {
                    rootSocket = null;
                }

                rootSocket = new SocketIOClient(TransportFactoryHelper.CreateDefault());
                rootSocket.ReconnectConfig = reconnectConfig; // Apply custom config
                ConnectToServer();
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Reconnection attempt {ReconnectAttempt} failed: {e.Message}");
            }
        }

        isReconnecting = false;
        reconnectRoutine = null;
    }

    private void OnDestroy()
    {
        // Set flag FIRST to prevent event handlers from executing
        isDestroyed = true;

        Debug.Log("🔌 PlayerNetworkSync is being destroyed - disconnecting socket");

        // Stop all coroutines
        if (positionRoutine != null)
        {
            StopCoroutine(positionRoutine);
            positionRoutine = null;
        }

        if (reconnectRoutine != null)
        {
            StopCoroutine(reconnectRoutine);
            reconnectRoutine = null;
        }

        // Disconnect socket properly
        if (rootSocket != null)
        {
            try
            {
                rootSocket.Disconnect();
                Debug.Log("✅ Socket disconnected successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error disconnecting socket: {e.Message}");
            }
        }

        // Clean up remote players
        if (spawner != null)
        {
            spawner.RemoveAllRemotePlayers();
        }
    }

    private IEnumerator SendPositionRoutine()
    {
        Debug.Log($"🚀 SendPositionRoutine started for player {playerId}");

        while (true)
        {
            yield return new WaitForSeconds(updateIntervalMs / 1000f);

            if (!isNamespaceConnected) continue;

            if (localPlayerTransform == null)
            {
                Debug.LogError("❌ localPlayerTransform is NULL! Cannot send position.");
                continue;
            }

            var packet = new MovePacket
            {
                id = playerId,
                position = new PositionData(localPlayerTransform.position)
            };

            // Send position updates (20x/sec) - don't log every one
            namespaceSocket.Emit("player_move", packet);
        }
    }

    [Serializable]
    public class MovePacket
    {
        public string id;
        public PositionData position;
    }

    [Serializable]
    public class PositionData
    {
        public float x;
        public float y;
        public float z;

        public PositionData() { }

        public PositionData(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}
