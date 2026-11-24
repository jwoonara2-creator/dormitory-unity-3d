using UnityEngine;
using UnityEngine.SceneManagement; 

public class SceneChanger : MonoBehaviour
{
   
    public void GoToMain()
    {
        SceneManager.LoadScene("Main");
    }


    public void GoToGym()
    {
        
        SceneManager.LoadScene("GymRoom");
    }

    public void GymCameraView()
    {

        SceneManager.LoadScene("GymRoomView");
    }

    public void GoToStudy()
    {
        SceneManager.LoadScene("StudyRoom");
    }

    public void StudyCameraView()
    {
        SceneManager.LoadScene("StudyRoomView");
    }

    public void GoToLaundry()
    {
        SceneManager.LoadScene("LaundryRoom");
    }

    public void LaundryCameraView()
    {
        SceneManager.LoadScene("LaundryRoomView");
    }

    public void GoToRoom1()
    {
        SceneManager.LoadScene("RoomView_1Person");
    }

    public void GoToRoom2()
    {
        SceneManager.LoadScene("RoomView_2Person");
    }

    public void GoToRoomD()
    {
        SceneManager.LoadScene("RoomView_Accessible");
    }

    public void GoToFloor()
    {
        SceneManager.LoadScene("FloorView");
    }
}
