using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; } // 싱글톤 인스턴스

    [Header("그리드 설정")]
    public Vector2Int bottomLeft = new Vector2Int(0, 0);    // 그리드 좌하단 기준 좌표
    public Vector2Int topRight = new Vector2Int(10, 10);   // 그리드 우상단 기준 좌표
    public float nodeRadius = 0.4f;                        // 노드의 충돌 감지 반경
    public LayerMask wallLayer;                            // 벽(장애물)으로 인식할 레이어 마스크 << 인스펙터에서 반드시 설정!

    private Node[,] nodeGrid; // 그리드 데이터를 저장할 2차원 배열
    private int gridSizeX, gridSizeY; // 그리드의 X, Y 크기

    void Awake()
    {
        // 싱글톤 패턴 구현
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // --- Wall Layer 설정 확인 --- 
        if (wallLayer == 0) // 레이어 마스크 값이 0이면 'Nothing'이 선택된 상태
        {
            Debug.LogError("GridManager 오류: 인스펙터에서 'Wall Layer'가 설정되지 않았습니다! 벽으로 사용할 레이어를 지정해주세요.", this);
            enabled = false; // 컴포넌트 비활성화
            return;
        }
        // --- 확인 완료 --- 

        CreateGrid(); // 그리드 생성 실행
    }

    // 초기 그리드를 생성하고 정적 벽을 표시합니다.
    void CreateGrid()
    {
        gridSizeX = topRight.x - bottomLeft.x + 1;
        gridSizeY = topRight.y - bottomLeft.y + 1;
        nodeGrid = new Node[gridSizeX, gridSizeY];

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector2Int gridPos = bottomLeft + new Vector2Int(x, y); // 절대 그리드 좌표 계산
                Vector3 worldPoint = GridToWorld(gridPos); // 월드 좌표로 변환
                // 초기 정적 벽 확인
                bool isWall = Physics2D.OverlapCircle(worldPoint, nodeRadius, wallLayer);
                nodeGrid[x, y] = new Node(gridPos, isWall); // 노드 생성 및 저장 (절대 좌표 사용)
            }
        }

        // 유효한 경우 레이어 이름 로그 출력 (더 안전한 방식)
        string layerName = "<다중/유효하지 않음>";
        if (IsSingleLayer(wallLayer)) // 단일 레이어인지 확인
        {
             try { layerName = LayerMask.LayerToName((int)Mathf.Log(wallLayer.value, 2)); } catch {} // 이름 가져오기 시도
        } 
        Debug.Log($"그리드 생성 완료: {gridSizeX}x{gridSizeY} 노드. 초기 벽 레이어: {layerName} (마스크: {wallLayer.value})");
    }

    // 레이어 마스크가 단일 레이어를 나타내는지 확인하는 도우미 함수
    private bool IsSingleLayer(LayerMask mask)
    {
        return mask.value > 0 && (mask.value & (mask.value - 1)) == 0;
    }

    // 특정 그리드 좌표에 해당하는 노드를 반환합니다.
    public Node GetNode(Vector2Int gridPosition)
    {
        // 절대 그리드 좌표를 배열 인덱스로 변환
        int x = gridPosition.x - bottomLeft.x;
        int y = gridPosition.y - bottomLeft.y;

        // 배열 범위 내에 있는지 확인
        if (x >= 0 && x < gridSizeX && y >= 0 && y < gridSizeY)
        {
            return nodeGrid[x, y];
        }
        return null; // 범위를 벗어나면 null 반환
    }

    // 특정 그리드 좌표가 현재 이동 가능한지 동적으로 확인합니다.
    public bool IsWalkable(Vector2Int gridPosition)
    {
        Node node = GetNode(gridPosition);
        if (node == null) 
        {
            return false; // 그리드 밖은 이동 불가
        }

        // 실시간으로 wallLayer에 해당하는 장애물 확인
        Vector3 worldPoint = GridToWorld(gridPosition);
        bool hasObstacle = Physics2D.OverlapCircle(worldPoint, nodeRadius, wallLayer);

        // 노드가 존재하고, 현재 장애물이 없어야 이동 가능
        // CreateGrid 시점의 node.IsWall 정보는 주로 정적 벽 판단에 사용될 수 있음
        return !hasObstacle;
    }

    // 월드 좌표를 가장 가까운 그리드 좌표로 변환합니다.
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        // 월드 좌표를 가장 가까운 정수 그리드 좌표로 반올림
        int x = Mathf.RoundToInt(worldPosition.x);
        int y = Mathf.RoundToInt(worldPosition.y);
        return new Vector2Int(x, y);
    }

    // 그리드 좌표를 해당 그리드 셀의 중심 월드 좌표로 변환합니다.
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        // 그리드 셀 중앙의 월드 좌표 계산
        return new Vector3(gridPosition.x + 0.5f, gridPosition.y + 0.5f, 0);
    }

    // 그리드 크기(X, Y 개수)를 반환합니다.
    public Vector2Int GetGridSize()
    {
        return new Vector2Int(gridSizeX, gridSizeY);
    }

    // 에디터에서 그리드를 시각적으로 표시하기 위한 기즈모
    void OnDrawGizmos()
    {
        // 그리드 노드가 생성되었을 경우
        if (nodeGrid != null)
        {
            Vector3 cubeSize = new Vector3(1, 1, 0.1f); // 기즈모 크기
            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    Node node = nodeGrid[x, y];
                    // 기즈모 색상은 실시간 IsWalkable 확인 결과 반영
                    bool currentlyWalkable = IsWalkable(node.Position); 
                    Gizmos.color = currentlyWalkable ? Color.white : Color.red;
                    Vector3 worldPos = GridToWorld(node.Position);
                    Gizmos.DrawWireCube(worldPos, cubeSize); // 와이어 큐브로 표시
                }
            }
        } 
        // 그리드가 아직 생성되지 않았을 경우 경계선 표시
        else 
        {
            Vector3 center = new Vector3(bottomLeft.x + (topRight.x - bottomLeft.x) / 2.0f + 0.5f,
                                         bottomLeft.y + (topRight.y - bottomLeft.y) / 2.0f + 0.5f, 0);
            Vector3 size = new Vector3(topRight.x - bottomLeft.x + 1, topRight.y - bottomLeft.y + 1, 0.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, size);
        }
    }
} 