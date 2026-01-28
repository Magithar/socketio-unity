using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SocketIOUnity.Runtime;

public class BasicChatUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text chatLog;
    public TMP_InputField messageInput;
    public Button sendButton;
    public TMP_Text statusText;

    private SocketIOClient socket;

    private void Awake()
    {
        sendButton.onClick.AddListener(OnSendClicked);
    }

    private void Start()
    {
        socket = SocketIOManager.Instance.Socket;

        // ---- Connection lifecycle
        socket.OnConnected += OnConnected;
        socket.OnDisconnected += OnDisconnected;
        socket.OnError += OnError;

        // ---- Chat event
        socket.On("chat", OnChatMessage);

        statusText.text = "Connecting...";
        socket.Connect("ws://localhost:3000");
    }

    private void OnDestroy()
    {
        if (socket == null) return;

        socket.OnConnected -= OnConnected;
        socket.OnDisconnected -= OnDisconnected;
        socket.OnError -= OnError;

        socket.Off("chat", OnChatMessage);
    }

    // =============================
    // Socket Callbacks
    // =============================

    private void OnConnected()
    {
        AppendSystemMessage("Connected");
        statusText.text = "Connected";
        Debug.Log("[Chat] Connected");
    }

    private void OnDisconnected()
    {
        AppendSystemMessage("Disconnected - reconnecting...");
        statusText.text = "Disconnected";
        Debug.Log("[Chat] Disconnected - reconnecting...");
    }

    private void OnError(string error)
    {
        AppendSystemMessage($"Error: {error}");
        Debug.LogError($"[Chat] Error: {error}");
    }

    private void OnChatMessage(string message)
    {
        AppendChatMessage($"Server: {message}");
        Debug.Log($"[Chat] Server: {message}");
    }

    // =============================
    // UI Actions
    // =============================

    private void OnSendClicked()
    {
        var text = messageInput.text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        AppendChatMessage($"You: {text}");
        Debug.Log($"[Chat] You: {text}");
        socket.Emit("chat", text);

        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    // =============================
    // Helpers
    // =============================

    private void AppendChatMessage(string message)
    {
        chatLog.text += message + "\n";
    }

    private void AppendSystemMessage(string message)
    {
        chatLog.text += $"<color=#AAAAAA>{message}</color>\n";
    }
}
