using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(NPCController))] // NPCController 컴포넌트 필요
public class NPCPathfinder : MonoBehaviour
{
    private const string npcTag = "NPC"; // NPC 게임 오브젝트에 할당된 태그

    // --- Movement Settings Removed ---
    // public float moveSpeed = 2f;
    // public float rotationSpeed = 5f;
    // public float arrivalThreshold = 0.1f;

    [Header("경로 탐색 설정")]
    // public float pathUpdateInterval = 0.5f; // Removed - Updates are triggered now
    public float pathfindingRetryDelay = 1f; // 경로 탐색 실패 시 재시도 지연 시간
    public int maxPathfindingAttempts = 3;    // 최대 경로 탐색 시도 횟수
    // public bool allowDiagonalMovement = true; // Removed - Forcing Orthogonal Only
    public bool preventCornerCutting = true;   // 코너 가로지르기 방지 여부

    [Header("목표물 설정")]
    public TargetType targetTypePreference = TargetType.None; // 선호하는 목표물 타입
    public string targetTag = "Interactable"; // 목표물(가구 등)에 사용할 태그
    public float minTargetDistance = 1f;     // 목표물로 간주할 최소 거리
    // public float maxTargetDistance = 10f; // Removed - Searching entire map now
    public float targetSearchInterval = 2f;  // 목표물 재탐색 주기

    [Header("디버그 설정")]
    public bool showDebug = true;            // 디버그 기즈모 표시 여부
    public Color pathColor = Color.green;   // 계산된 경로 색상
    public Color destinationColor = Color.red; // 최종 목적지 노드 색상
    public Color forbiddenColor = Color.gray; // 금지된 노드 색상

    // 내부 상태 변수
    private GameObject currentTargetObject;       // 현재 추적 중인 목표물 게임 오브젝트
    private Vector2Int? currentDestinationNode; // 현재 목표물 근처의 최종 도착 지점 노드
    private List<Vector2Int> currentFoundPath;  // 마지막으로 계산된 경로
    private float lastPathRequestTime;          // 마지막 경로 요청 시간 (재계산 쿨다운용)
    private float lastTargetSearchTime;         // 마지막 목표물 탐색 시간
    private int pathfindingAttempts;            // 현재 목표물에 대한 경로 탐색 시도 횟수
    // private bool isMoving = true; // Movement state is now handled by NPCController
    // private Vector2Int? nextWaypoint = null; // Next waypoint info is handled by NPCController

    private NPCController npcController; // 이동을 담당하는 컨트롤러 컴포넌트 참조
    private HashSet<Vector2Int> currentForbiddenNodes = new HashSet<Vector2Int>(); // 현재 금지된 노드 목록 (경로 탐색 및 기즈모용)

    private void Awake()
    {
        npcController = GetComponent<NPCController>();
        if (npcController == null)
        {
            Debug.LogError("NPCController 컴포넌트를 찾을 수 없습니다!", this);
        }
    }

    private void Start()
    {
        StartCoroutine(UpdateTargetRoutine()); // 목표물 탐색 코루틴 시작
        // lastPathRequestTime = -pathUpdateInterval; // No longer needed for initial forced update
        lastTargetSearchTime = -targetSearchInterval; // 즉시 첫 탐색 실행하도록 초기화
    }

    private void Update()
    {
        // 목표물 탐색은 주로 코루틴에서 처리
        // Can potentially remove this block if coroutine handles all cases
        /*
        if ((currentTargetObject == null || !currentDestinationNode.HasValue) && npcController.IsIdle)
        {
             if (Time.time - lastTargetSearchTime >= targetSearchInterval)
             {
                 FindNewTargetAndDestination();
             }
        }
        */
    }

    // 주기적으로 목표물 상태를 확인하고 경로 갱신을 요청하는 코루틴
    private IEnumerator UpdateTargetRoutine()
    {
        while (true)
        {
            // 목표물이 없거나, 있더라도 주기적으로 재확인 및 경로 갱신 시도
            if (currentTargetObject == null || !currentDestinationNode.HasValue) 
            {
                 FindNewTargetAndDestination(); // Attempts to find a target if none exists
            }
            else
            {
                 // If already has a target, still periodically re-evaluate
                 // FindNewTargetAndDestination handles checking if the target changed
                 FindNewTargetAndDestination(); 
            }

            yield return new WaitForSeconds(targetSearchInterval);
        }
    }

    // 목표물 주변의 점유 공간을 금지 노드로 설정하고, 다른 NPC 위치도 포함합니다.
    private void GenerateForbiddenNodes()
    {
        if (GridManager.Instance == null) return;

        currentForbiddenNodes.Clear();
        // Vector2 gridCellSize = GridManager.Instance.gridCellSize; // 캐싱 -> GridManager에 없음. 1x1 셀 크기로 가정.
        Vector2 gridCellSize = Vector2.one; // 1x1 셀 크기 사용

        // 1. 다른 모든 NPC들의 현재 위치를 금지 노드로 추가합니다.
        GameObject[] npcs = GameObject.FindGameObjectsWithTag(npcTag);
        foreach (var npc in npcs)
        {
            // 자기 자신은 제외
            if (npc == gameObject) continue;

            Vector2Int npcGridPos = GridManager.Instance.WorldToGrid(npc.transform.position);
            if (GridManager.Instance.GetNode(npcGridPos) != null) // 그리드 내 유효한 위치인지 확인
            {
                currentForbiddenNodes.Add(npcGridPos);
            }
        }

        // 2. 현재 목표물이 있다면, 목표물의 콜라이더와 실제로 겹치는 그리드 셀들을 금지 노드로 추가합니다.
        if (currentTargetObject != null)
        {
            var targetCollider = currentTargetObject.GetComponent<Collider2D>();
            if (targetCollider != null)
            {
                Bounds bounds = targetCollider.bounds;
                // Bounds를 기반으로 검사할 그리드 영역 계산 (약간의 여유 포함 가능성 고려)
                Vector2Int minGrid = GridManager.Instance.WorldToGrid(bounds.min);
                Vector2Int maxGrid = GridManager.Instance.WorldToGrid(bounds.max);

                // 물리 충돌 검사를 위한 설정
                // ContactFilter2D contactFilter = new ContactFilter2D().NoFilter(); // 제거 - LayerMask 오버로드 사용
                // 충돌 결과를 저장할 배열 (크기 1이면 충분, 단일 타겟 확인용)
                Collider2D[] overlapResults = new Collider2D[1];

                // Bounds에 포함될 가능성이 있는 모든 그리드 셀 검사
                for (int x = minGrid.x; x <= maxGrid.x; x++)
                {
                    for (int y = minGrid.y; y <= maxGrid.y; y++)
                    {
                        Vector2Int cellPos = new Vector2Int(x, y);

                        // 그리드 범위 밖 노드는 검사할 필요 없음
                        if (GridManager.Instance.GetNode(cellPos) == null) continue;

                        // 현재 셀의 월드 중심 좌표 계산 (GridToWorld가 중심을 반환한다고 가정)
                        Vector2 cellCenterWorldPos = GridManager.Instance.GridToWorld(cellPos);

                        // 현재 그리드 셀 영역과 겹치는 콜라이더 검사 (LayerMask 사용하는 오버로드 사용)
                        // Physics2D.OverlapBoxNonAlloc(Vector2 point, Vector2 size, float angle, Collider2D[] results, int layerMask = DefaultRaycastLayers)
                        int overlapCount = Physics2D.OverlapBoxNonAlloc(cellCenterWorldPos, gridCellSize, 0f, overlapResults, Physics2D.AllLayers); // 기본 레이어 마스크 명시적 전달 (Physics2D 네임스페이스 필요)

                        if (overlapCount > 0)
                        {
                            // 어떤 콜라이더가 감지되는지 로그 추가
                            for (int i = 0; i < overlapCount; i++)
                            {
                                Debug.Log($"[Pathfinder][GetOccupiedNodes] Cell {cellPos} overlapped with: {overlapResults[i].gameObject.name} (Layer: {LayerMask.LayerToName(overlapResults[i].gameObject.layer)})");
                                if (overlapResults[i] == targetCollider)
                                {
                                    currentForbiddenNodes.Add(cellPos);
                                    break; // 이 셀은 금지됨, 다음 셀 검사
                                }
                            }
                        }
                    }
                }
            }
            else // 콜라이더가 없는 목표물 (중심점만 금지)
            {
                Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(currentTargetObject.transform.position);
                if (GridManager.Instance.GetNode(targetGridPos) != null)
                {
                   currentForbiddenNodes.Add(targetGridPos);
                }
            }
        }
        // 필요하다면 여기에 추가적인 금지 규칙(예: 특정 타일 타입)을 넣을 수 있습니다.
    }

    // 주어진 게임 오브젝트의 Collider2D를 기반으로 점유하고 있는 그리드 노드 목록을 반환합니다.
    private HashSet<Vector2Int> GetOccupiedNodes(GameObject targetObject)
    {
        HashSet<Vector2Int> occupiedNodes = new HashSet<Vector2Int>();
        if (targetObject == null || GridManager.Instance == null) 
        {
            return occupiedNodes;
        }

        var targetCollider = targetObject.GetComponent<Collider2D>();
        if (targetCollider != null)
        {
            // 추가 로그: 찾은 콜라이더 정보
            Debug.Log($"[Pathfinder][GetOccupiedNodes] Found Collider2D on {targetObject.name}: Type={targetCollider.GetType().Name}, Enabled={targetCollider.enabled}");

            Bounds bounds = targetCollider.bounds;
            // 추가 로그: Bounds 및 그리드 범위
            Debug.Log($"[Pathfinder][GetOccupiedNodes] Target Bounds: Min={bounds.min}, Max={bounds.max}");
            Vector2Int minGrid = GridManager.Instance.WorldToGrid(bounds.min);
            Vector2Int maxGrid = GridManager.Instance.WorldToGrid(bounds.max);
            Debug.Log($"[Pathfinder][GetOccupiedNodes] Grid Bounds Check: MinG={minGrid}, MaxG={maxGrid}");
            Vector2 gridCellSize = Vector2.one; // 1x1 셀 크기 가정
            // overlapResults 크기 증가 (디버깅용)
            Collider2D[] overlapResults = new Collider2D[5]; 

            for (int x = minGrid.x; x <= maxGrid.x; x++)
            {
                for (int y = minGrid.y; y <= maxGrid.y; y++)
                {
                    Vector2Int cellPos = new Vector2Int(x, y);
                    if (GridManager.Instance.GetNode(cellPos) == null) continue;

                    Vector2 cellCenterWorldPos = GridManager.Instance.GridToWorld(cellPos);
                    // 모든 레이어를 대상으로 검사하도록 변경
                    int overlapCount = Physics2D.OverlapBoxNonAlloc(cellCenterWorldPos, gridCellSize, 0f, overlapResults, Physics2D.AllLayers); 

                    if (overlapCount > 0)
                    {
                        // 어떤 콜라이더가 감지되는지 로그 추가
                        for (int i = 0; i < overlapCount; i++)
                        {
                            // 배열 크기보다 overlapCount가 작을 수 있으므로 null 체크 추가
                            if (overlapResults[i] != null)
                            {
                                Debug.Log($"[Pathfinder][GetOccupiedNodes] Cell {cellPos} overlapped with: {overlapResults[i].gameObject.name} (Layer: {LayerMask.LayerToName(overlapResults[i].gameObject.layer)}, Collider: {overlapResults[i].GetType().Name})");
                                
                                // 비교 방식 변경: 컴포넌트 대신 게임 오브젝트 비교
                                if (overlapResults[i].gameObject == targetObject)
                                {
                                    Debug.Log($"[Pathfinder][GetOccupiedNodes] Match! Collider's GameObject {overlapResults[i].gameObject.name} matches targetObject {targetObject.name}. Adding cell {cellPos}.");
                                    occupiedNodes.Add(cellPos);
                                    // 해당 셀에서 타겟 오브젝트를 찾았으면 더 이상 이 셀의 다른 콜라이더는 확인할 필요 없음
                                    break; 
                                }
                            }
                        }
                    }
                }
            }
        }
        else // 콜라이더가 없는 경우
        {
            // 추가 로그: 콜라이더 못 찾음
            Debug.LogWarning($"[Pathfinder][GetOccupiedNodes] No Collider2D found on target object: {targetObject.name}", targetObject);
            // 중심점만 점유 노드로 간주 (기존 로직 유지)
            Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(targetObject.transform.position);
            if (GridManager.Instance.GetNode(targetGridPos) != null)
            {
                occupiedNodes.Add(targetGridPos);
            }
        }
        return occupiedNodes;
    }

    // 목표물 주변의 이동 가능한 인접 노드 중 NPC에게 가장 가까운 노드를 찾습니다.
    private Vector2Int? FindBestAdjacentWalkableNode(GameObject targetObject, HashSet<Vector2Int> allForbiddenNodes)
    {
        if (targetObject == null || GridManager.Instance == null) return null;
        Debug.Log($"[Pathfinder] Finding best adjacent node for: {targetObject.name}");

        HashSet<Vector2Int> occupiedNodes = GetOccupiedNodes(targetObject);
        Debug.Log($"[Pathfinder] Occupied nodes for {targetObject.name}: {occupiedNodes.Count} nodes. [{string.Join(", ", occupiedNodes)}]");
        if (occupiedNodes.Count == 0) 
        {
             Debug.LogWarning($"[Pathfinder] Target {targetObject.name} occupies 0 nodes. Cannot find adjacent.", targetObject);
             return null; // 점유 노드가 없으면 인접 노드도 없음
        }

        List<Vector2Int> validAdjacentNodes = new List<Vector2Int>();
        Vector3 npcWorldPos = transform.position; // NPC 위치 캐싱

        // 각 점유 노드의 이웃을 검사
        foreach (Vector2Int occupiedPos in occupiedNodes)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // 자기 자신 제외

                    Vector2Int neighbourPos = occupiedPos + new Vector2Int(x, y);

                    // 조건 확인:
                    // 1. 그리드 범위 내
                    // 2. 이동 가능 (벽 아님)
                    // 3. 목표물 점유 노드 아님
                    // 4. 다른 금지 노드 아님
                    // 5. 이미 후보 목록에 없음 (중복 방지)
                    if (GridManager.Instance.GetNode(neighbourPos) != null &&
                        GridManager.Instance.IsWalkable(neighbourPos) &&
                        !occupiedNodes.Contains(neighbourPos) &&
                        !allForbiddenNodes.Contains(neighbourPos) &&
                        !validAdjacentNodes.Contains(neighbourPos)) 
                    {
                        // 6. (추가 조건) 벽 너머에 있는지 확인 (대각선 코너 검사)
                        bool isDiagonal = (x != 0 && y != 0);
                        if (isDiagonal)
                        {
                            Vector2Int adjacentPos1 = occupiedPos + new Vector2Int(x, 0);
                            Vector2Int adjacentPos2 = occupiedPos + new Vector2Int(0, y);
                            
                            // 두 인접 노드가 모두 존재하고 이동 가능해야 코너 통과 가능
                            bool canPassCorner = (GridManager.Instance.GetNode(adjacentPos1) != null && GridManager.Instance.IsWalkable(adjacentPos1)) &&
                                                 (GridManager.Instance.GetNode(adjacentPos2) != null && GridManager.Instance.IsWalkable(adjacentPos2));

                            if (!canPassCorner)
                            {
                                continue; // 벽 코너에 막혔으므로 이 대각선 노드는 도착 지점 후보에서 제외
                            }
                        }

                        // 모든 조건을 통과하면 유효한 인접 노드로 추가
                        validAdjacentNodes.Add(neighbourPos);
                    }
                }
            }
        }

        // 유효한 인접 노드가 없는 경우
        if (validAdjacentNodes.Count == 0)
        {
            Debug.LogWarning($"목표 '{targetObject.name}' 주변에 접근 가능한 인접 노드를 찾지 못했습니다 (점유 노드: {occupiedNodes.Count}개, 금지 노드 고려됨).", targetObject);
            return null;
        }
        Debug.Log($"[Pathfinder] Found {validAdjacentNodes.Count} valid adjacent nodes for {targetObject.name}: [{string.Join(", ", validAdjacentNodes)}]");

        // 가장 가까운 노드 찾기
        Vector2Int bestNode = validAdjacentNodes[0];
        float minDistanceSqr = Vector3.SqrMagnitude(GridManager.Instance.GridToWorld(bestNode) - npcWorldPos);

        for (int i = 1; i < validAdjacentNodes.Count; i++)
        {
            float distanceSqr = Vector3.SqrMagnitude(GridManager.Instance.GridToWorld(validAdjacentNodes[i]) - npcWorldPos);
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                bestNode = validAdjacentNodes[i];
            }
        }

        Debug.Log($"[Pathfinder] Best adjacent node found: {bestNode}");
        return bestNode;
    }

    // 새로운 목표물을 찾고 해당 목표물 근처의 도착 지점을 계산합니다.
    private void FindNewTargetAndDestination()
    {
        Debug.Log("[Pathfinder] Finding new target and destination...");
        GameObject previousTarget = currentTargetObject; // 이전 목표물 저장
        Vector2Int? previousDestination = currentDestinationNode; // 이전 도착 지점 저장

        FindNewTargetObject(); // 현재 조건에 맞는 최적의 목표물 탐색
        Debug.Log($"[Pathfinder] FindNewTargetObject completed. currentTargetObject: {currentTargetObject?.name ?? "NULL"}");

        GenerateForbiddenNodes(); // 목표물 및 NPC 위치 기반 금지 노드 목록 갱신

        if (currentTargetObject != null) // 유효한 목표물을 찾았는지 확인
        {
            Debug.Log($"[Pathfinder] Finding best adjacent node for target: {currentTargetObject.name}");
            // 목표물 주변의 이동 가능하고 '허용된' **인접** 노드 탐색
            currentDestinationNode = FindBestAdjacentWalkableNode(currentTargetObject, currentForbiddenNodes);
            Debug.Log($"[Pathfinder] FindBestAdjacentWalkableNode completed. currentDestinationNode: {(currentDestinationNode.HasValue ? currentDestinationNode.Value.ToString() : "NULL")}");

            // 도착 지점을 찾았고, (목표물이나 도착 지점이 변경되었거나 || 아직 경로가 없으면) 경로 요청
            if (currentDestinationNode.HasValue && (currentTargetObject != previousTarget || currentDestinationNode != previousDestination || currentFoundPath == null))
            {
                RequestNewPath(); 
            }
            else if (!currentDestinationNode.HasValue)
            {   
                 // 목표물은 찾았으나 접근 가능한 인접 노드가 없는 경우 (FindBestAdjacentWalkableNode 내부에서 이미 경고 로그 출력됨)
                 // 이전 목표물과 동일한 목표물에 대해 계속 실패하는 경우 추가 로그 방지 가능
                 if(currentTargetObject == previousTarget) {
                     // 이미 경고 로그가 출력되었으므로 여기서는 조용히 처리
                 }
                 ClearTargetAndPath(); // 목표 및 경로 정보 초기화
            }
        }
        else 
        {
             if(previousTarget != null) {
                ClearTargetAndPath();
             }
        }
        lastTargetSearchTime = Time.time; // 탐색 시간 기록
    }

    // 설정된 기준(태그, 타입, 거리)에 맞는 최적의 목표물 오브젝트를 찾습니다.
    private void FindNewTargetObject()
    {
        Debug.Log($"[Pathfinder] Attempting to find new target object. Preference: {targetTypePreference}, Tag: {targetTag}");
        if (targetTypePreference == TargetType.None) // 선호 타입 없으면 탐색 안함
        {
            currentTargetObject = null;
            Debug.Log("[Pathfinder] Target preference is None. Skipping search.");
            return;
        }

        // 지정된 태그를 가진 모든 잠재적 목표물 검색
        var potentialTargets = GameObject.FindGameObjectsWithTag(targetTag);
        Debug.Log($"[Pathfinder] Found {potentialTargets.Length} potential targets with tag '{targetTag}'.");
        if (potentialTargets.Length == 0) // 해당 태그 오브젝트 없음
        {
            currentTargetObject = null; // 현재 목표물 초기화
            return;
        }

        GameObject bestTarget = null; // 가장 적합한 목표물
        float closestDistSqr = float.MaxValue; // 가장 가까운 거리 제곱값 (초기값: 최대)
        string targetName = targetTypePreference.ToString(); // Enum 값을 문자열 이름으로 변환
        Debug.Log($"[Pathfinder] Searching for target with exact name: '{targetName}'");

        foreach (var target in potentialTargets)
        {
            // 이름이 선호 타입과 일치하는지 확인
            if (target.name != targetName) continue;
            Debug.Log($"[Pathfinder] Potential target '{target.name}' matches name. Calculating distance.");

            // 거리 계산 (제곱 거리 사용으로 성능 향상)
            Vector3 directionToTarget = target.transform.position - transform.position;
            float dSqrToTarget = directionToTarget.sqrMagnitude;

            // 최소 거리 조건을 만족하고, 현재까지 찾은 bestTarget보다 가까우면 갱신
            if (dSqrToTarget >= minTargetDistance * minTargetDistance && dSqrToTarget < closestDistSqr) 
            {
                closestDistSqr = dSqrToTarget;
                bestTarget = target;
            }
        }
        // 최종적으로 찾은 bestTarget을 현재 목표물로 설정 (못 찾았으면 null)
        currentTargetObject = bestTarget;
        Debug.Log($"[Pathfinder] FindNewTargetObject finished. Best target found: {bestTarget?.name ?? "None"}");
    }

    // NPCController 등 외부에서 경로 재계산을 요청할 때 호출되는 함수
    public void TriggerPathRecalculation()
    {
        // 너무 짧은 간격으로 반복 호출되는 것 방지 (쿨다운)
        if (Time.time - lastPathRequestTime < 0.5f) 
        {
             // Debug.Log("[Pathfinder] TriggerPathRecalculation skipped due to cooldown."); // 필요시 로그 추가
            return;
        }
        Debug.Log("[Pathfinder] TriggerPathRecalculation called externally.");
        lastPathRequestTime = Time.time; // 쿨다운 타이머 리셋
        
        // 목표물 재탐색부터 도착 지점 계산, 경로 요청까지 전체 프로세스 실행
        FindNewTargetAndDestination(); 

        // 재계산 직전에 금지 노드 목록 갱신 -> FindNewTargetAndDestination 내부에서 처리됨
        // GenerateForbiddenNodes(); 
        // RequestNewPath(); // 경로 요청 실행 -> FindNewTargetAndDestination 내부에서 처리됨
    }

    // 실제 A* 경로 탐색을 요청하고 결과를 NPCController에 전달하는 함수
    private void RequestNewPath()
    {
        // 경로 요청 전 필수 조건 확인
        if (currentTargetObject == null || !currentDestinationNode.HasValue || GridManager.Instance == null || npcController == null)
        {
            Debug.LogWarning($"[Pathfinder] RequestNewPath aborted. Conditions not met: Target={currentTargetObject?.name ?? "NULL"}, DestNode={currentDestinationNode.HasValue}, Grid={GridManager.Instance != null}, Controller={npcController != null}");
            ClearTargetAndPath(); // 조건 미충족 시 초기화
            return;
        }

        Vector2Int startGridPos = GridManager.Instance.WorldToGrid(transform.position);
        Vector2Int finalTargetGridPos = currentDestinationNode.Value;
        Debug.Log($"[Pathfinder] Requesting new path from {startGridPos} to {finalTargetGridPos} (Target: {currentTargetObject.name})");

        // 금지 노드 목록은 이미 GenerateForbiddenNodes() 호출로 최신 상태임
        // GenerateForbiddenNodes(); // 여기서 또 호출할 필요 없음

        lastPathRequestTime = Time.time; // 경로 요청 시간 기록

        // A* 유틸리티 함수 호출 (금지 노드 목록 전달)
        var path = AStarPathfinderUtil.FindPath(startGridPos, finalTargetGridPos, false, preventCornerCutting, currentForbiddenNodes);

        // 경로 탐색 결과 처리
        if (path != null && path.Count > 0) // 경로 찾기 성공
        {   
            Debug.Log($"[Pathfinder] Path found successfully! Length: {path.Count}");
            // 기존 경로와 비교하여 실제로 변경되었는지 확인 (불필요한 이동 재시작 방지)
            bool isNewPath = currentFoundPath == null || !PathsAreEqual(currentFoundPath, path);
            if (isNewPath) 
            {   
                // 새로운 경로를 NPCController에 전달하여 이동 시작
                currentFoundPath = path;
                npcController.StartMovingAlongPath(path);
                pathfindingAttempts = 0; // 성공 시 시도 횟수 초기화
            }
            // else: 경로는 찾았지만 기존과 동일하므로 아무것도 안 함
        }
        else // 경로 찾기 실패
        {   
            Debug.LogWarning($"[Pathfinder] Pathfinding failed from {startGridPos} to {finalTargetGridPos}.");
            pathfindingAttempts++; // 시도 횟수 증가
            Debug.LogWarning($"경로 탐색 시도 {pathfindingAttempts}/{maxPathfindingAttempts} 실패: 목적지 {finalTargetGridPos} (대상 '{currentTargetObject?.name ?? "알 수 없음"}' 근처) (금지 노드 고려됨).", currentTargetObject);
            // 최대 시도 횟수 초과 시 목표 포기
            if (pathfindingAttempts >= maxPathfindingAttempts)
            {   
                Debug.LogError($"경로 탐색 최종 실패: 대상 '{currentTargetObject?.name ?? "알 수 없음"}' 근처 경로 {maxPathfindingAttempts}회 시도 후 실패. 목표를 초기화합니다.");
                ClearTargetAndPath();
            }
            // 실패했지만 아직 재시도 가능하면, 현재 이동 중인 NPC 정지
            else if (npcController != null && npcController.IsMoving) { 
                npcController.StopMoving(); 
            }
        }
    }

    // 목표물, 도착 지점, 경로 등 관련 데이터를 초기화하는 도우미 함수
    private void ClearTargetAndPath()
    {
        currentTargetObject = null;
        currentDestinationNode = null;
        currentFoundPath = null;
        pathfindingAttempts = 0;
        // NPC가 이동 중이었다면 정지시킴
        if (npcController != null && npcController.IsMoving) 
        { 
            npcController.StopMoving();
        }
    }

    // 두 경로(Vector2Int 리스트)가 동일한지 비교하는 도우미 함수
    private bool PathsAreEqual(List<Vector2Int> path1, List<Vector2Int> path2)
    {
        if (path1 == null || path2 == null || path1.Count != path2.Count) 
            return false;
        for (int i = 0; i < path1.Count; i++)
            if (path1[i] != path2[i]) return false;
        return true;
    }

    // --- Grid/World Conversion Removed (Use GridManager.Instance) ---
    // private Vector2Int WorldToGrid(Vector3 worldPosition) { ... }
    // private Vector3 GridToWorld(Vector2Int gridPosition) { ... }

    // --- GetNextWaypointPosition Removed (Handled by NPCController) ---
    // public Vector2Int? GetNextWaypointPosition() { ... }

    // 외부(예: NPCController)에서 특정 노드가 현재 금지된 상태인지 확인할 수 있는 함수
    public bool IsNodeCurrentlyForbidden(Vector2Int nodePos)
    {
        // 주의: 마지막으로 GenerateForbiddenNodes가 호출된 시점의 정보 기준
        return currentForbiddenNodes.Contains(nodePos);
    }

    // 디버그용 기즈모 그리기
    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        // 계산된 경로 그리기
        if (currentFoundPath != null && currentFoundPath.Count > 0)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < currentFoundPath.Count - 1; i++)
            {
                Vector3 start = GridManager.Instance.GridToWorld(currentFoundPath[i]);
                Vector3 end = GridManager.Instance.GridToWorld(currentFoundPath[i + 1]);
                Gizmos.DrawLine(start, end);
            }
        }
        // 최종 도착 지점 그리기
        if (currentDestinationNode.HasValue)
        {
            Gizmos.color = destinationColor;
            Gizmos.DrawWireSphere(GridManager.Instance.GridToWorld(currentDestinationNode.Value), 0.35f);
        }
        // 현재 목표물과의 연결선 그리기
        if (currentTargetObject != null)
        {   
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTargetObject.transform.position); // NPC -> 목표물
            // 목표물 -> 최종 도착 지점 연결선
            if(currentDestinationNode.HasValue)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(currentTargetObject.transform.position, GridManager.Instance.GridToWorld(currentDestinationNode.Value));
            }
        }
         // 마지막으로 계산된 금지 노드 목록 그리기
        if(currentForbiddenNodes != null)
        {
            Gizmos.color = forbiddenColor;
            foreach(var nodePos in currentForbiddenNodes)
            {
                Vector3 worldPos = GridManager.Instance.GridToWorld(nodePos);
                 Gizmos.DrawCube(worldPos, Vector3.one * 0.5f); // 작은 큐브로 표시
            }
        }
    }
}