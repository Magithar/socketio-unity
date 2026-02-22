using TMPro;
using UnityEngine;

public class RTTDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text rttText;
    [SerializeField] private PlayerNetworkSync networkSync;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < 1f) return;
        timer = 0f;

        if (networkSync == null || networkSync.Socket == null || !networkSync.Socket.IsConnected)
        {
            rttText.text = "";
            return;
        }

#pragma warning disable CS0618
        rttText.text = $"RTT: {networkSync.Socket.PingRttMs:F0} ms";
#pragma warning restore CS0618
    }
}
