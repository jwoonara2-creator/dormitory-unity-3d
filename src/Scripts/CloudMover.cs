using UnityEngine;

public class CloudMover : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f; // 구름이 이동하는 속도
    public float destroyX = 4f; // 이 X 좌표를 넘어가면 구름이 사라짐

    void Update()
    {
        // 오른쪽(X축 방향)으로 이동
        transform.Translate(Vector3.right * moveSpeed * Time.deltaTime, Space.World);

        // 지정한 범위를 벗어나면 오브젝트 파괴
        if (transform.position.x > destroyX)
        {
            Destroy(gameObject);
        }
    }
}