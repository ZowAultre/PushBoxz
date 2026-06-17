mergeInto(LibraryManager.library, {
  PushBoxzCopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);
    var fallback = function () {
      window.prompt("Copy level code:", text);
    };

    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).catch(fallback);
      return;
    }

    fallback();
  },

  PushBoxzPromptLevelCode: function (objectNamePtr, methodNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);
    var code = window.prompt("Paste level code:", "");
    if (code !== null && code.length > 0) {
      SendMessage(objectName, methodName, code);
    }
  }
});
