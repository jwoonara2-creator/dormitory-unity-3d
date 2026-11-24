using System.Collections;
using UnityEngine;

public class CloudSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject[] cloudPrefabs; // 구름 프리팹 배열 (여러 종류 등록 가능)
    public float spawnInterval = 4f;  // 구름이 생성되는 주기 (초 단위)

    [Header("Spawn Area (Position)")]
    public float spawnX = -4f;       // 생성될 X 좌표 (왼쪽 화면 밖)
    public float minY = 1.8f;          // 생성될 Y 좌표 최소값 (높이)
    public float maxY = 3;          // 생성될 Y 좌표 최대값 (높이)
    public float zPosition = 0f;    // 생성될 Z 좌표 (배경 깊이)

    void Start()
    {
        // 게임 시작과 동시에 구름 생성 루틴 시작
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            SpawnCloud();
            // 지정한 주기(초)만큼 대기
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnCloud()
    {
        if (cloudPrefabs.Length == 0) return;

        // 등록된 구름 프리팹 중 랜덤하게 하나 선택
        int randomIndex = Random.Range(0, cloudPrefabs.Length);
        GameObject selectedCloud = cloudPrefabs[randomIndex];

        // 랜덤한 높이(Y축) 계산
        float randomY = Random.Range(minY, maxY);
        Vector3 spawnPos = new Vector3(spawnX, randomY, zPosition);

        // Y축으로 90도 회전시키기 위한 Quaternion 설정
        Quaternion spawnRotation = Quaternion.Euler(0f, 90f, 0f);

        // 구름 생성 (회전값 적용)
        Instantiate(selectedCloud, spawnPos, spawnRotation);
    }
}