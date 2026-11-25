// Unity (C#) -> hosting page. The page decides how to reach the app:
//   Flutter WebView (phone): FlutterBridge.postMessage(msg)
//   Flutter web (iframe)   : parent.postMessage(JSON {type:'unity', msg}, '*')
mergeInto(LibraryManager.library, {
  AppBridge_Send: function (ptr) {
    var msg = UTF8ToString(ptr);
    if (window.appSend) window.appSend(msg);
  }
});
