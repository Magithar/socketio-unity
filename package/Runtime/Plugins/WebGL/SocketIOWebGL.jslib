var NativeWebSocket = {
  // Storage for WebSocket instances
  $webSocketInstances: [],
  $webSocketNextId: 1,

  WebSocketAllocate: function(urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = webSocketNextId++;
    
    var ws = {
      socket: null,
      url: url,
      subprotocols: [],
      error: null,
      messages: []
    };
    
    webSocketInstances[id] = ws;
    return id;
  },

  WebSocketAddSubProtocol: function(instanceId, protocolPtr) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return -1;
    
    var protocol = UTF8ToString(protocolPtr);
    instance.subprotocols.push(protocol);
    return 0;
  },

  WebSocketFree: function(instanceId) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return;
    
    if (instance.socket && instance.socket.readyState < 2) {
      instance.socket.close();
    }
    
    delete webSocketInstances[instanceId];
  },

  WebSocketConnect: function(instanceId) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return -1;
    if (instance.socket) return -2;
    
    try {
      if (instance.subprotocols.length > 0) {
        instance.socket = new WebSocket(instance.url, instance.subprotocols);
      } else {
        instance.socket = new WebSocket(instance.url);
      }
      instance.socket.binaryType = 'arraybuffer';
    } catch (e) {
      instance.error = e.message;
      return -1;
    }
    
    instance.socket.onopen = function() {
      if (NativeWebSocket.onOpenCallback) {
        {{{ makeDynCall('vi', 'NativeWebSocket.onOpenCallback') }}}(instanceId);
      }
    };
    
    instance.socket.onmessage = function(e) {
      if (NativeWebSocket.onMessageCallback) {
        if (typeof e.data === 'string') {
          var length = lengthBytesUTF8(e.data) + 1;
          var buffer = _malloc(length);
          stringToUTF8(e.data, buffer, length);
          {{{ makeDynCall('viii', 'NativeWebSocket.onMessageCallback') }}}(instanceId, buffer, length - 1);
          _free(buffer);
        } else {
          var dataBuffer = new Uint8Array(e.data);
          var buffer = _malloc(dataBuffer.length);
          HEAPU8.set(dataBuffer, buffer);
          {{{ makeDynCall('viii', 'NativeWebSocket.onMessageCallback') }}}(instanceId, buffer, dataBuffer.length);
          _free(buffer);
        }
      }
    };
    
    instance.socket.onerror = function(e) {
      instance.error = "WebSocket error";
      if (NativeWebSocket.onErrorCallback) {
        var errorPtr = allocateUTF8("WebSocket error");
        {{{ makeDynCall('vii', 'NativeWebSocket.onErrorCallback') }}}(instanceId, errorPtr);
        _free(errorPtr);
      }
    };
    
    instance.socket.onclose = function(e) {
      if (NativeWebSocket.onCloseCallback) {
        {{{ makeDynCall('vii', 'NativeWebSocket.onCloseCallback') }}}(instanceId, e.code);
      }
    };
    
    return 0;
  },

  WebSocketClose: function(instanceId, code, reasonPtr) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return -1;
    if (!instance.socket) return -3;
    if (instance.socket.readyState === 2) return -4;
    if (instance.socket.readyState === 3) return -5;
    
    var reason = reasonPtr ? UTF8ToString(reasonPtr) : "";
    
    try {
      instance.socket.close(code, reason);
    } catch (e) {
      return -7;
    }
    
    return 0;
  },

  WebSocketSend: function(instanceId, dataPtr, dataLength) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return -1;
    if (!instance.socket) return -3;
    if (instance.socket.readyState !== 1) return -6;
    
    var data = HEAPU8.subarray(dataPtr, dataPtr + dataLength);
    instance.socket.send(data);
    
    return 0;
  },

  WebSocketSendText: function(instanceId, messagePtr) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return -1;
    if (!instance.socket) return -3;
    if (instance.socket.readyState !== 1) return -6;
    
    var message = UTF8ToString(messagePtr);
    instance.socket.send(message);
    
    return 0;
  },

  WebSocketGetState: function(instanceId) {
    var instance = webSocketInstances[instanceId];
    if (!instance) return -1;
    if (!instance.socket) return 3; // Closed
    
    return instance.socket.readyState;
  },

  WebSocketSetOnOpen: function(callback) {
    NativeWebSocket.onOpenCallback = callback;
  },

  WebSocketSetOnMessage: function(callback) {
    NativeWebSocket.onMessageCallback = callback;
  },

  WebSocketSetOnError: function(callback) {
    NativeWebSocket.onErrorCallback = callback;
  },

  WebSocketSetOnClose: function(callback) {
    NativeWebSocket.onCloseCallback = callback;
  }
};

autoAddDeps(NativeWebSocket, '$webSocketInstances');
autoAddDeps(NativeWebSocket, '$webSocketNextId');
mergeInto(LibraryManager.library, NativeWebSocket);

// Also keep the SocketIO-specific functions for WebGLWebSocketTransport
mergeInto(LibraryManager.library, {
  SocketIO_WebSocket_Create: function(idPtr, urlPtr) {
    var id = UTF8ToString(idPtr);
    var url = UTF8ToString(urlPtr);

    if (!window.__socketioSockets)
      window.__socketioSockets = {};

    var ws = new WebSocket(url);
    ws.binaryType = "arraybuffer";

    ws.onopen = function() {
      SendMessage("WebGLSocketBridge", "JSOnOpen", id);
    };

    ws.onclose = function() {
      SendMessage("WebGLSocketBridge", "JSOnClose", id);
    };

    ws.onerror = function() {
      SendMessage("WebGLSocketBridge", "JSOnError", id);
    };

    ws.onmessage = function(e) {
      if (typeof e.data === "string") {
        // Include socket ID prefix for routing
        SendMessage("WebGLSocketBridge", "JSOnText", id + ":" + e.data);
      } else {
        var bytes = new Uint8Array(e.data);
        var ptr = _malloc(bytes.length);
        HEAPU8.set(bytes, ptr);
        // Include socket ID for routing
        SendMessage("WebGLSocketBridge", "JSOnBinary", id + "," + ptr + "," + bytes.length);
        _free(ptr);
      }
    };

    window.__socketioSockets[id] = ws;
  },

  SocketIO_WebSocket_SendText: function(idPtr, msgPtr) {
    var id = UTF8ToString(idPtr);
    var msg = UTF8ToString(msgPtr);
    if (window.__socketioSockets[id]) {
      window.__socketioSockets[id].send(msg);
    }
  },

  SocketIO_WebSocket_SendBinary: function(idPtr, ptr, len) {
    var id = UTF8ToString(idPtr);
    var data = HEAPU8.slice(ptr, ptr + len);
    if (window.__socketioSockets[id]) {
      window.__socketioSockets[id].send(data);
    }
  },

  SocketIO_WebSocket_Close: function(idPtr) {
    var id = UTF8ToString(idPtr);
    if (window.__socketioSockets[id]) {
      window.__socketioSockets[id].close();
      delete window.__socketioSockets[id];
    }
  }
});
