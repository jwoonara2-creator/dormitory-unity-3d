using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Room view: "go to the bathroom" / "back to the room" buttons glide the camera through the bathroom door.
public class RoomViewController : MonoBehaviour
{
    public RoomLookCamera look;
    public Text titleLabel;
    public Button bathButton, roomButton, backButton;
    public Vector3[] pathToBath, pathToRoom;
    public float roomYaw, roomPitch, bathYaw, bathPitch;

     void Awake()
    {
        UiUtil.EnsureEventSystem();
         // if (titleLabel != null) titleLabel.text = string.IsNullOrEmpty(RoomSession.roomLabel) ? "원룸" : RoomSession.roomLabel;
        if (bathButton != null) bathButton.onClick.AddListener(GoBath);
        if (roomButton != null) roomButton.onClick.AddListener(GoRoom);
        // scenes without one get a button on its own canvas (same look as every other scene's back button)
        if (backButton == null) backButton = AppUiFixes.CreateBackButton("층 보기", null);
        else AppUiFixes.StyleAsRoundIcon(backButton, UiIcon.Back);
        // short text pills: "화장실" / "방으로"
        AppUiFixes.StyleActionButton(bathButton, "화장실");
        AppUiFixes.StyleActionButton(roomButton, "방으로");
        if (backButton != null)
        {
            backButton.onClick.AddListener(Back);
            // entered from the Main map (no floor view in between) -> go back to Main
            var target = BackTarget();
            backButton.gameObject.SetActive(target != null);
            var label = backButton.GetComponentInChildren<Text>(true);
            if (label != null && target == MainScene) label.text = "메인";
        }
        SetButtons(false);
    }

    public void GoBath() { if (look != null) look.MoveAlong(pathToBath, bathYaw, bathPitch); SetButtons(true); }
    public void GoRoom() { if (look != null) look.MoveAlong(pathToRoom, roomYaw, roomPitch); SetButtons(false); }

    const string MainScene = "Main";

    static string BackTarget()
    {
        if (!string.IsNullOrEmpty(RoomSession.returnScene)) return RoomSession.returnScene;
        return Application.CanStreamedLevelBeLoaded(MainScene) ? MainScene : null;
    }

    public void Back()
    {
        var target = BackTarget();
        if (target != null) SceneManager.LoadScene(target);
    }

    bool inBathNow, buttonsHidden;

    void SetButtons(bool inBath)
    {
        inBathNow = inBath;
        if (bathButton != null) bathButton.gameObject.SetActive(!inBath && !buttonsHidden);
        if (roomButton != null) roomButton.gameObject.SetActive(inBath && !buttonsHidden);
    }

    // hides the bathroom / room buttons while the top view is shown (see RoomTopView)
    public void SetButtonsHidden(bool hidden)
    {
        buttonsHidden = hidden;
        SetButtons(inBathNow);
    }
}
