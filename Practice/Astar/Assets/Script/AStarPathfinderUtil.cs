using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for potential optimizations if needed

// A* 경로 탐색 알고리즘을 제공하는 정적 유틸리티 클래스
public static class AStarPathfinderUtil
{
    private const int MOVE_STRAIGHT_COST = 10; // 직선 이동 비용
    private const int MOVE_DIAGONAL_COST = 14; // 대각선 이동 비용 (Pythagorean approx.)
    private const float TURN_PENALTY = 0.01f; // 회전 시 추가 비용 (매우 작게 설정하여 직선 우선)

    /// <summary>
    /// 지정된 시작점에서 목표점까지의 경로를 계산합니다.
    /// </summary>
    /// <param name="startPos">시작 그리드 좌표</param>
    /// <param name="targetPos">목표 그리드 좌표</param>
    /// <param name="allowDiagonal">대각선 이동 허용 여부</param>
    /// <param name="dontCrossCorner">코너를 가로지르는 이동 방지 여부 (엄격한 대각선)</param>
    /// <param name="forbiddenNodes">경로 계산 시 피해야 할 추가적인 동적 장애물 노드 집합</param>
    /// <returns>경로 노드 좌표 리스트 (시작점 포함). 경로가 없으면 null 반환.</returns>
    public static List<Vector2Int> FindPath(Vector2Int startPos, Vector2Int targetPos, bool allowDiagonal, bool dontCrossCorner, HashSet<Vector2Int> forbiddenNodes)
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError("경로 탐색 오류: GridManager 인스턴스를 찾을 수 없습니다!");
            return null;
        }

        Node startNode = gridManager.GetNode(startPos);
        Node targetNode = gridManager.GetNode(targetPos);

        // 기본 유효성 검사
        if (startNode == null || targetNode == null)
        {
            Debug.LogWarning("경로 탐색 오류: 시작 또는 목표 노드가 그리드 범위를 벗어났습니다.");
            return null;
        }
        // 시작 노드가 막혔는지 확인 (벽 또는 금지 노드)
        if (!gridManager.IsWalkable(startPos) || (forbiddenNodes != null && forbiddenNodes.Contains(startPos)))
        {
            Debug.LogWarning("경로 탐색 오류: 시작 노드가 막혀 있습니다.");
            return null;
        }
         // 목표 노드가 막혔는지 확인 (벽 또는 금지 노드)
         if (!gridManager.IsWalkable(targetPos) || (forbiddenNodes != null && forbiddenNodes.Contains(targetPos)))
        {
            // 목표 지점이 금지된 노드(예: 다른 NPC)라도 그 근처까지는 경로를 찾아야 하므로 경고만 출력하고 진행
            Debug.LogWarning("경로 탐색 경고: 목표 노드가 현재 막혀 있습니다. 접근 가능한 가장 가까운 이웃으로 경로를 탐색합니다.");
            // A* 알고리즘이 막힌 목표 노드를 처리하고 가장 가까운 지점을 찾도록 함
        }

        // A* 탐색을 위한 리스트 및 집합 초기화
        List<Node> openSet = new List<Node>();    // 평가할 노드 리스트
        HashSet<Node> closedSet = new HashSet<Node>(); // 이미 평가된 노드 집합

        // 주의: 여러 경로 탐색 요청 간에 노드 객체를 재사용하는 경우, 비용/부모 정보를 반드시 초기화해야 함.
        // 가장 확실한 방법: GridManager에서 노드 풀 관리 또는 사용된 노드 추적 후 리셋.
        // 여기서는 간단하게 neighbour 처리 시 비용 확인 로직에 의존.

        // 시작 노드 초기 설정
        startNode.GCost = 0;
        startNode.HCost = CalculateHeuristic(startNode.Position, targetNode.Position);
        startNode.ParentNode = null; 
        openSet.Add(startNode);

        // A* 알고리즘 메인 루프
        while (openSet.Count > 0)
        {
            // Open Set에서 F 비용이 가장 낮은 노드 찾기 (동일 F 비용 시 H 비용 비교)
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost || (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            // 현재 노드를 Open Set에서 제거하고 Closed Set에 추가
            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // 목표 노드에 도달했으면 경로 생성 후 반환
            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            // 이웃 노드 처리 (금지 노드 정보 전달)
            ProcessNeighbours(currentNode, targetNode, openSet, closedSet, gridManager, allowDiagonal, dontCrossCorner, forbiddenNodes);
        }

        // Open Set이 비었는데 목표를 찾지 못함 -> 경로 없음
        Debug.LogWarning($"경로 탐색 실패: {startPos} -> {targetPos} 경로 없음 (금지 노드 고려됨).");
        return null;
    }

    // 현재 노드의 이웃 노드들을 평가하고 업데이트합니다.
    private static void ProcessNeighbours(Node currentNode, Node targetNode, List<Node> openSet, HashSet<Node> closedSet, GridManager gridManager, bool allowDiagonal, bool dontCrossCorner, HashSet<Vector2Int> forbiddenNodes)
    {
        // 8방향 이웃 확인 (또는 4방향)
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue; // 자기 자신 제외

                bool isDiagonal = Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1;
                if (!allowDiagonal && isDiagonal) continue; // 대각선 이동 불가 시 스킵

                Vector2Int neighbourPos = currentNode.Position + new Vector2Int(x, y);
                Node neighbourNode = gridManager.GetNode(neighbourPos);

                // --- 유효성 및 장애물 통합 확인 --- 
                // 1. 그리드 범위 내 존재 (GetNode != null)
                // 2. GridManager가 이동 가능하다고 판단 (벽 레이어 확인)
                // 3. Closed Set에 없음 (이미 평가 완료)
                // 4. 금지된 노드 목록(NPC, 가구 등)에 없음
                if (neighbourNode == null || 
                    !gridManager.IsWalkable(neighbourPos) || 
                    closedSet.Contains(neighbourNode) || 
                    (forbiddenNodes != null && forbiddenNodes.Contains(neighbourPos)))
                {
                    continue; // 다음 이웃으로
                }

                // 대각선 이동 시 코너 통과 불가 옵션 확인
                if (isDiagonal && !CanMoveDiagonally(currentNode, neighbourNode, gridManager, dontCrossCorner))
                {
                    continue;
                }

                // 현재 노드를 거쳐 이웃 노드로 가는 새로운 G 비용 계산
                float baseMoveCost = CalculateDistanceCost(currentNode.Position, neighbourNode.Position); // 직선/대각선 기본 비용
                float turnCostPenalty = 0f;

                // 부모 노드가 있고, 방향이 꺾이는 경우 회전 페널티 추가
                if (currentNode.ParentNode != null)
                {
                    Vector2Int directionFromParent = currentNode.Position - currentNode.ParentNode.Position;
                    Vector2Int directionToNeighbour = neighbourNode.Position - currentNode.Position;
                    if (directionFromParent != directionToNeighbour)
                    {
                        turnCostPenalty = TURN_PENALTY;
                    }
                }

                // 최종 이동 비용 (G 코스트) 계산 (회전 페널티 포함)
                float newMovementCostToNeighbour = currentNode.GCost + baseMoveCost + turnCostPenalty;

                // 새로운 경로가 더 저렴하거나 Open Set에 없는 경우
                if (newMovementCostToNeighbour < neighbourNode.GCost || !openSet.Contains(neighbourNode))
                {
                    // G, H 비용 업데이트 및 부모 노드 설정
                    neighbourNode.GCost = newMovementCostToNeighbour;
                    neighbourNode.HCost = CalculateHeuristic(neighbourNode.Position, targetNode.Position);
                    neighbourNode.ParentNode = currentNode;

                    // Open Set에 없으면 추가
                    if (!openSet.Contains(neighbourNode))
                    {
                        openSet.Add(neighbourNode);
                    }
                    // 참고: List<T>.Contains는 O(n) 연산. 성능 최적화가 필요하면 OpenSet을 HashSet<Node> 등으로 관리하고 비용 업데이트 로직 추가.
                }
            }
        }
    }

     // 대각선 이동 시 코너 통과 가능 여부를 확인합니다.
    private static bool CanMoveDiagonally(Node currentNode, Node diagonalNeighbour, GridManager gridManager, bool dontCrossCorner)
    {
        Vector2Int offset = diagonalNeighbour.Position - currentNode.Position;
        Node adjacentNode1 = gridManager.GetNode(currentNode.Position + new Vector2Int(offset.x, 0));
        Node adjacentNode2 = gridManager.GetNode(currentNode.Position + new Vector2Int(0, offset.y));

        // 인접 노드의 이동 가능성을 동적으로 확인
        bool adjacent1IsBlocked = adjacentNode1 == null || !gridManager.IsWalkable(adjacentNode1.Position); 
        bool adjacent2IsBlocked = adjacentNode2 == null || !gridManager.IsWalkable(adjacentNode2.Position);

        if (dontCrossCorner) // 엄격: 양쪽 인접 노드가 모두 이동 가능해야 함
        {
            return !adjacent1IsBlocked && !adjacent2IsBlocked;
        }
        else // 완화: 양쪽 인접 노드가 *동시에* 막힌 경우만 통과 불가
        {
            return !adjacent1IsBlocked || !adjacent2IsBlocked;
        }
    }

    // 목표 노드부터 시작 노드까지 부모 노드를 따라가며 최종 경로를 생성합니다.
    private static List<Vector2Int> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node currentNode = endNode;

        // 목표 노드부터 시작 노드까지 거슬러 올라감
        while (currentNode != null && currentNode != startNode)
        {
            path.Add(currentNode.Position);
            currentNode = currentNode.ParentNode;
        }
        // 시작 노드 추가 (만약 도달했다면)
         if (currentNode == startNode)
        {
            path.Add(startNode.Position); 
        }
        // 경로 순서를 시작 -> 목표로 뒤집기
        path.Reverse(); 
        return path;
    }

    // 두 노드 간의 휴리스틱 비용(예상 거리)을 계산합니다. (여기서는 대각선 거리 사용)
    private static float CalculateHeuristic(Vector2Int posA, Vector2Int posB)
    {
        int dstX = Mathf.Abs(posA.x - posB.x);
        int dstY = Mathf.Abs(posA.y - posB.y);

        // 맨해튼 거리보다 대각선 이동을 고려한 휴리스틱이 더 정확할 수 있음
        if (dstX > dstY)
            return MOVE_DIAGONAL_COST * dstY + MOVE_STRAIGHT_COST * (dstX - dstY);
        return MOVE_DIAGONAL_COST * dstX + MOVE_STRAIGHT_COST * (dstY - dstX);
    }

    // 두 노드 간의 실제 이동 비용(G 코스트 증가분)을 계산합니다.
    private static float CalculateDistanceCost(Vector2Int posA, Vector2Int posB)
    {
        int dstX = Mathf.Abs(posA.x - posB.x);
        int dstY = Mathf.Abs(posA.y - posB.y);

        // 직선 또는 대각선 이동 확인
        if (dstX == 1 && dstY == 1)
        {
            return MOVE_DIAGONAL_COST;
        }
        else if (dstX == 1 || dstY == 1) // dstX + dstY == 1 로도 가능
        {
            return MOVE_STRAIGHT_COST;
        }
        else
        {
            // 일반적으로 직접 이웃만 고려하므로 이 경우는 드묾. 예외 처리 또는 기본값.
            Debug.LogWarning($"CalculateDistanceCost: 비-이웃 노드 간 비용 계산 시도? ({posA} -> {posB})");
            // 근사치로 휴리스틱 값 반환 또는 0 반환
            return CalculateHeuristic(posA, posB); 
        }
    }
} 