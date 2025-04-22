using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(NPCPathfinder))] // NPCPathfinder 컴포넌트 필요
public class NPCController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 3.0f;        // 이동 속도 (초당 월드 유닛)
    public float arrivalThreshold = 0.05f; // 웨이포인트 도착 판정 거리

    private List<Vector2Int> currentPath; // 현재 따라갈 경로 (NPCPathfinder로부터 받음)
    private int currentWaypointIndex;     // 현재 경로에서 목표로 하는 웨이포인트 인덱스
    private bool isMoving = false;        // 현재 이동 중인지 여부

    private NPCPathfinder npcPathfinder; // 경로 탐색 및 재계산 요청을 위한 참조

    // NPC가 현재 유휴 상태인지 확인하는 프로퍼티
    public bool IsIdle => !isMoving;
    // NPC가 현재 이동 중인지 확인하는 프로퍼티 (외부에서 사용 가능)
    public bool IsMoving => isMoving;

    void Awake()
    {
        // NPCPathfinder 컴포넌트 가져오기
        npcPathfinder = GetComponent<NPCPathfinder>();
        if (npcPathfinder == null) 
        {
            Debug.LogError("NPCPathfinder 컴포넌트를 찾을 수 없습니다!", this);
        }
    }

    /// <summary>
    /// 주어진 경로를 따라 이동을 시작합니다.
    /// </summary>
    /// <param name="path">이동할 경로 (Vector2Int 좌표 리스트)</param>
    public void StartMovingAlongPath(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0) // 경로가 유효하지 않으면 중지
        {
            StopMoving();
            return;
        }

        // 참고: 경로 재계산 시 거의 동일한 경로가 들어올 경우,
        //       현재 웨이포인트 인덱스를 유지하는 최적화를 통해 부드러운 연결 가능.
        //       단순화를 위해 여기서는 항상 처음부터 경로를 시작.

        currentPath = path;
        currentWaypointIndex = 0; // 경로의 첫 번째 웨이포인트부터 시작
        isMoving = true;        // 이동 상태로 변경
    }

    /// <summary>
    /// NPC의 현재 이동을 중지하고 경로를 초기화합니다.
    /// </summary>
    public void StopMoving()
    {
        isMoving = false;
        currentPath = null;
        currentWaypointIndex = 0;
    }

    private void Update()
    {
        // 이동 중이 아니거나, 경로가 없거나, 경로 끝에 도달했으면 실행 중지
        if (!isMoving || currentPath == null || currentWaypointIndex >= currentPath.Count)
        {
            if (isMoving) StopMoving(); // 상태 일관성을 위해 확실히 중지
            return;
        }

        // 현재 목표 웨이포인트 좌표 가져오기
        Vector2Int nextWaypointGridPos = currentPath[currentWaypointIndex];
        
        // --- 장애물 확인 --- 
        // 다음 웨이포인트가 현재 이동 불가능한 상태인지 확인 (벽 또는 다른 유닛)
        if (!GridManager.Instance.IsWalkable(nextWaypointGridPos))
        {   
            // 일시적인 장애물(다른 NPC)인지, 아니면 진짜 벽인지 구분하여 다른 처리 가능
            // bool isForbidden = npcPathfinder.IsNodeCurrentlyForbidden(nextWaypointGridPos); 
            // if(isForbidden) { // 잠시 대기하는 로직 추가 가능 } 
            
            // 현재 로직: 다음 지점이 막혔으면 무조건 경로 재계산 요청
            Debug.LogWarning($"다음 웨이포인트 {nextWaypointGridPos}에 장애물 감지! 경로 재계산 요청.", this);
            npcPathfinder.TriggerPathRecalculation(); // 경로 재계산 요청
            return; // 현재 프레임 이동 중지 (새 경로가 오면 StartMovingAlongPath 호출됨)
        }

        // --- 이동 처리 --- 
        // 목표 웨이포인트의 월드 좌표로 변환
        Vector3 targetWorldPosition = GridManager.Instance.GridToWorld(nextWaypointGridPos);
        // 목표 지점을 향해 부드럽게 이동
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, moveSpeed * Time.deltaTime);

        // --- 도착 확인 --- 
        // 목표 웨이포인트에 충분히 가까워졌는지 확인 (제곱 거리 사용으로 성능 향상)
        if ((targetWorldPosition - transform.position).sqrMagnitude < arrivalThreshold * arrivalThreshold)
        {   
            // 웨이포인트 도달
            int previousWaypointIndex = currentWaypointIndex;
            currentWaypointIndex++; // 다음 웨이포인트로 인덱스 증가

            // 경로의 마지막 지점에 도달했는지 확인
            if (currentWaypointIndex >= currentPath.Count)
            {   
                StopMoving(); // 이동 종료
            }
            // --- 최종 접근 시 재계산 --- 
            // 마지막에서 두 번째 노드에 막 도착하여 최종 목적지를 향할 때,
            // 경로를 다시 확인하여 최신 상태 반영 (다른 NPC 도착, 목표물 미세 이동 등)
            else if (currentWaypointIndex == currentPath.Count - 1)
            {
                // Debug.Log("최종 목적지 접근 중, 경로 확인 요청.");
                npcPathfinder.TriggerPathRecalculation(); // 경로 재계산 요청
                // 재계산된 경로가 올 때까지 현재 이동은 멈추지 않음
            }
        }
    }

    // 디버그용 기즈모: 현재 목표 웨이포인트 표시
     void OnDrawGizmosSelected()
     {
         if (isMoving && currentPath != null && currentWaypointIndex < currentPath.Count)
         {
             Gizmos.color = Color.magenta;
             Vector3 targetWorldPos = GridManager.Instance.GridToWorld(currentPath[currentWaypointIndex]);
             Gizmos.DrawWireSphere(targetWorldPos, arrivalThreshold); // 도착 판정 반경 표시
             Gizmos.DrawLine(transform.position, targetWorldPos); // 현재 위치에서 목표 웨이포인트까지 선 표시
         }
     }
} 