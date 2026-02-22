using UnityEngine;
using TMPro;

public class ConnectionStatusDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private PlayerNetworkSync networkSync;

    private void Update()
    {
        if (networkSync == null || statusText == null)
            return;

        var state = networkSync.ConnectionState;

        switch (state)
        {
            case ConnectionState.Disconnected:
                statusText.text = "[X] Disconnected";
                statusText.color = Color.red;
                break;

            case ConnectionState.Connecting:
                statusText.text = "[...] Connecting...";
                statusText.color = Color.yellow;
                break;

            case ConnectionState.Connected:
                statusText.text = "[OK] Connected";
                statusText.color = Color.green;
                break;

            case ConnectionState.Reconnecting:
                int attempt = networkSync.ReconnectAttempt;
                statusText.text = $"[!] Reconnecting... (attempt {attempt})";
                statusText.color = new Color(1f, 0.5f, 0f); // Orange
                break;
        }
    }
}
