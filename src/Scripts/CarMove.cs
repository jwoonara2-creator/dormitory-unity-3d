using UnityEngine;

public class CarMove : MonoBehaviour
{
    public float rotTime = 2.0f;
    public float rotPassTime = 0.0f;

    void Update()
    {
        transform.Translate(Vector3.forward * 1.45f * Time.deltaTime);

        if (rotPassTime >= rotTime)
        {
            transform.Rotate(Vector3.up * 90.0f);
            rotPassTime = 0.0f;
        }
        else
        {
            rotPassTime += Time.deltaTime;
        }
    }
}
