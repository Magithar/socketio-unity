mergeInto(LibraryManager.library, {
  SocketIO_WebSocket_Create: function (idPtr, urlPtr) {
    const id = UTF8ToString(idPtr);
    const url = UTF8ToString(urlPtr);

    if (!window.__socketioSockets)
      window.__socketioSockets = {};

    const ws = new WebSocket(url);
    ws.binaryType = "arraybuffer";

    ws.onopen = () => {
      SendMessage("WebGLSocketBridge", "JSOnOpen", id);
    };

    ws.onclose = () => {
      SendMessage("WebGLSocketBridge", "JSOnClose", id);
    };

    ws.onerror = () => {
      SendMessage("WebGLSocketBridge", "JSOnError", id);
    };

    ws.onmessage = (e) => {
      if (typeof e.data === "string") {
        SendMessage("WebGLSocketBridge", "JSOnText", e.data);
      } else {
        const bytes = new Uint8Array(e.data);
        const ptr = _malloc(bytes.length);
        HEAPU8.set(bytes, ptr);
        SendMessage("WebGLSocketBridge", "JSOnBinary", ptr + "," + bytes.length);
        _free(ptr);
      }
    };


    window.__socketioSockets[id] = ws;
  },

  SocketIO_WebSocket_SendText: function (idPtr, msgPtr) {
    const id = UTF8ToString(idPtr);
    const msg = UTF8ToString(msgPtr);
    window.__socketioSockets[id]?.send(msg);
  },

  SocketIO_WebSocket_SendBinary: function (idPtr, ptr, len) {
    const id = UTF8ToString(idPtr);
    const data = HEAPU8.slice(ptr, ptr + len);
    window.__socketioSockets[id]?.send(data);
  },

  SocketIO_WebSocket_Close: function (idPtr) {
    const id = UTF8ToString(idPtr);
    window.__socketioSockets[id]?.close();
    delete window.__socketioSockets[id];
  }
});
