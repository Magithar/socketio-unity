mergeInto(LibraryManager.library, {
    WebGL_CopyToClipboard: function (textPtr) {
        var text = UTF8ToString(textPtr);
        window.prompt('Room code (Ctrl+C to copy):', text);
    },

    WebGL_InitPasteListener: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        // Only register once
        if (window._unityPasteListenerActive) return;
        window._unityPasteListenerActive = true;
        document.addEventListener('paste', function (e) {
            var text = (e.clipboardData || window.clipboardData).getData('text');
            if (text && text.length > 0) {
                // Forward pasted text to Unity
                SendMessage(goName, 'OnBrowserPaste', text);
            }
        });
    }
});
