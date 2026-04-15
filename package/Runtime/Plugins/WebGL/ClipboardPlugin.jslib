mergeInto(LibraryManager.library, {
    WebGL_CopyToClipboard: function (textPtr) {
        var text = UTF8ToString(textPtr);
        window.prompt('Room code (Ctrl+C to copy):', text);
    }
});
