using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Allows users to configure server URL at runtime (production-standard approach).
/// This component provides a UI for entering custom server URLs without rebuilding.
/// </summary>
public class ServerUrlInput : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField urlInputField;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Network Reference")]
    [SerializeField] private PlayerNetworkSync networkSync;

    private void Start()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        // Show current server URL
        if (urlInputField != null && networkSync != null)
        {
            urlInputField.text = networkSync.CurrentServerUrl;

            // Disable UI if runtime changes are not allowed
            if (!networkSync.AllowRuntimeUrlChange)
            {
                urlInputField.interactable = false;
                if (saveButton != null) saveButton.interactable = false;
                if (resetButton != null) resetButton.interactable = false;
                UpdateStatusText("URL changes disabled by configuration", Color.gray);
                return;
            }
        }

        UpdateStatusText("1. Enter server URL  2. Tap Save  3. Restart app", Color.red);
    }

    private void OnSaveClicked()
    {
        if (urlInputField == null || networkSync == null)
        {
            UpdateStatusText("Missing references!", Color.red);
            return;
        }

        string url = urlInputField.text.Trim();

        if (string.IsNullOrEmpty(url))
        {
            UpdateStatusText("URL cannot be empty!", Color.red);
            return;
        }

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            UpdateStatusText("URL must start with http:// or https://", Color.red);
            return;
        }

        if (networkSync.SetCustomServerUrl(url))
        {
            UpdateStatusText("Saved! Restart app to apply.", Color.green);
        }
        else
        {
            UpdateStatusText("Failed to save URL", Color.red);
        }
    }

    private void OnResetClicked()
    {
        if (networkSync == null)
        {
            UpdateStatusText("Missing network reference!", Color.red);
            return;
        }

        networkSync.ClearCustomServerUrl();

        if (urlInputField != null)
        {
            urlInputField.text = "";
        }

        UpdateStatusText("Reset! Restart app to use default.", Color.yellow);
    }

    private void UpdateStatusText(string message, Color? color = null)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color ?? Color.white;
        }

        Debug.Log($"[ServerUrlInput] {message}");
    }

    private void OnDestroy()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(OnResetClicked);
    }
}
