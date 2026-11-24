using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

// Talks to the Flutter app that hosts the WebGL build (see README "앱과 연결").
// Messages are one-line strings: "name" or "name:value".
//   Unity -> app : ready | hotspot:<id> | room:<1person|2person|accessible>
//   app -> Unity : show_floor:<n> | go_room:<1person|2person|accessible>
// The object is created automatically (RuntimeInitializeOnLoadMethod) and survives scene loads,
// so no scene needs to contain it.
public class AppBridge : MonoBehaviour
{
    public const string ObjectName = "AppBridge"; // the web page calls SendMessage("AppBridge", "OnCommand", msg)

    // scene shown for "show_floor" (the floor plan is the same for every floor for now)
    const string FloorScene = "FloorView";

    static AppBridge instance;

    public static int CurrentFloor { get; private set; } = 1;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void AppBridge_Send(string message);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        var go = new GameObject(ObjectName);
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AppBridge>();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // first scene is up -> tell the app it can send commands; entering a room view -> tell the app
    bool announcedReady;
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AppUiFixes.Apply(scene);
        if (!announcedReady) { announcedReady = true; Send("ready"); }
        var room = RoomTypeOfScene(scene.name);
        if (room != null) Send("room:" + room);
    }

    // Unity -> app. Safe to call anywhere (no-op in the editor / standalone builds).
    public static void Send(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        AppBridge_Send(message);
#else
        Debug.Log("[AppBridge] -> app: " + message);
#endif
    }

    // app -> Unity (called from the web page through SendMessage)
    public void OnCommand(string message)
    {
        Debug.Log("[AppBridge] <- app: " + message);
        if (Split(message, "show_floor:", out var floor))
        {
            if (int.TryParse(floor, out var n)) CurrentFloor = n;
            LoadScene(FloorScene);
        }
        else if (Split(message, "go_room:", out var type))
        {
            var scene = SceneOfRoomType(type);
            if (scene == null) { Debug.LogWarning("[AppBridge] unknown room type: " + type); return; }
            RoomSession.roomLabel = LabelOfRoomType(type);
            RoomSession.returnScene = FloorScene;
            LoadScene(scene);
        }
        else Debug.LogWarning("[AppBridge] unknown command: " + message);
    }

    static void LoadScene(string name)
    {
        if (SceneManager.GetActiveScene().name == name) return;
        SceneManager.LoadScene(name);
    }

    static bool Split(string message, string prefix, out string value)
    {
        if (message != null && message.StartsWith(prefix)) { value = message.Substring(prefix.Length); return true; }
        value = null;
        return false;
    }

    static string SceneOfRoomType(string type)
    {
        switch (type)
        {
            case "1person": return "RoomView_1Person";
            case "2person": return "RoomView_2Person";
            case "accessible": return "RoomView_Accessible";
            default: return null;
        }
    }

    static string LabelOfRoomType(string type)
    {
        switch (type)
        {
            case "1person": return "1인실";
            case "2person": return "2인실";
            default: return "장애인실";
        }
    }

    static string RoomTypeOfScene(string scene)
    {
        switch (scene)
        {
            case "RoomView_1Person": return "1person";
            case "RoomView_2Person": return "2person";
            case "RoomView_Accessible": return "accessible";
            default: return null;
        }
    }
}
