using UnityEngine;
using SocketIOUnity.Runtime;
using SocketIOUnity.Transport;
using System.Threading;
using System;

/// <summary>
/// Test script to verify that all Socket.IO callbacks execute on Unity's main thread.
/// Attach to any GameObject to run verification.
/// </summary>
public class MainThreadDispatcherTest : MonoBehaviour
{
    private SocketIOClient _socket;
    private int _mainThreadId;

    void Start()
    {
        // Capture the main thread ID
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        Debug.Log($"[MainThreadTest] Main thread ID: {_mainThreadId}");

        // Connect to test server
        _socket = new SocketIOClient(TransportFactoryHelper.CreateDefault());

        // Test 1: Regular event callback (explicit cast to avoid ambiguity)
        _socket.On("test-thread", (Action<string>)((data) =>
        {
            VerifyMainThread("Regular Event");
            Debug.Log($"[MainThreadTest] ✓ Regular event data: {data}");
        }));

        // Test 2: Binary event callback
        _socket.On("test-binary-thread", (byte[] data) =>
        {
            VerifyMainThread("Binary Event");
            Debug.Log($"[MainThreadTest] ✓ Binary event received {data.Length} bytes");
        });

        // Test 3: ACK callback (not ambiguous, no cast needed)
        _socket.Emit("test-ack-thread", null, (response) =>
        {
            VerifyMainThread("ACK Callback");
            Debug.Log($"[MainThreadTest] ✓ ACK response: {response}");
        });

        _socket.OnConnected += () =>
        {
            VerifyMainThread("OnConnected Event");
            Debug.Log("[MainThreadTest] ✓ Connected to server");
        };

        _socket.Connect("http://localhost:3000");
    }

    void OnDestroy()
    {
        _socket?.Dispose();
    }

    private void VerifyMainThread(string callbackName)
    {
        int currentThreadId = Thread.CurrentThread.ManagedThreadId;
        
        if (currentThreadId == _mainThreadId)
        {
            Debug.Log($"<color=green>[MainThreadTest] ✓ {callbackName} executed on main thread (ID: {currentThreadId})</color>");
        }
        else
        {
            Debug.LogError($"<color=red>[MainThreadTest] ✗ {callbackName} executed on WRONG thread! Main: {_mainThreadId}, Current: {currentThreadId}</color>");
        }

        // Also verify Unity API access works (should not throw exception)
        try
        {
            var position = transform.position;
            Debug.Log($"[MainThreadTest] ✓ Unity API call successful (transform.position accessible)");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MainThreadTest] ✗ Unity API call failed: {ex.Message}");
        }
    }
}
