using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Corridor / floor view: three buttons that open the room views by room type.
public class RoomEntryButtons : MonoBehaviour
{
    public Button onePerson, twoPerson, accessible;

    void Awake()
    {
        UiUtil.EnsureEventSystem();
        if (onePerson != null) onePerson.onClick.AddListener(() => Enter("1인실", "RoomView_1Person"));
        if (twoPerson != null) twoPerson.onClick.AddListener(() => Enter("2인실", "RoomView_2Person"));
        if (accessible != null) accessible.onClick.AddListener(() => Enter("장애인실", "RoomView_Accessible"));
    }

    static void Enter(string label, string scene)
    {
        RoomSession.roomLabel = label;
        RoomSession.returnScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(scene);
    }
}
