using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Which room the user picked on the floor view, and where to go back to.
public static class RoomSession
{
    public static string roomLabel = "";   // "1인실" / "2인실" / "장애인실"
    public static string returnScene = "";
}

