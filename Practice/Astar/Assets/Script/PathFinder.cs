using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* 알고리즘을 사용하여 경로를 찾는 싱글톤 클래스
/// </summary>
public class PathFinder : MonoBehaviour
{
    private static PathFinder mInstance;
    public static PathFinder Instance
    {
        get
        {
            if (mInstance == null)
            {
                mInstance = FindObjectOfType<PathFinder>();
                if (mInstance == null)
                {
                    GameObject go = new GameObject("PathFinder");
                    mInstance = go.AddComponent<PathFinder>();
                }
            }
            return mInstance;
        }
    }

    [SerializeField] private bool mAllowDiagonal = true; // 대각선 이동 허용 여부
    [SerializeField] private bool mDontCrossCorner = true; // 코너 통과 방지 여부
    [SerializeField] private float mStraightLineWeight = 1.2f; // 직선 경로 가중치

    private int mSizeX, mSizeY;
    private Node[,] mNodeArray;
    private List<Node> mOpenList;
    private List<Node> mClosedList;

    private void Awake()
    {
        if (mInstance != null && mInstance != this)
        {
            Destroy(gameObject);
            return;
        }
        mInstance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 시작점에서 목표점까지의 경로를 찾는 메서드
    /// </summary>
    /// <param name="_startPos">시작 위치</param>
    /// <param name="_targetPos">목표 위치</param>
    /// <param name="_bottomLeft">맵의 왼쪽 하단 좌표</param>
    /// <param name="_topRight">맵의 오른쪽 상단 좌표</param>
    /// <returns>찾은 경로의 노드 리스트</returns>
    public List<Node> FindPath(Vector2Int _startPos, Vector2Int _targetPos, Vector2Int _bottomLeft, Vector2Int _topRight)
    {
        // 시작점과 목표점이 맵 범위를 벗어나는지 확인
        if (!IsPositionInMap(_startPos, _bottomLeft, _topRight))
        {
            Debug.LogError($"시작점이 맵 범위를 벗어났습니다. 시작점: {_startPos}, 맵 범위: {_bottomLeft} ~ {_topRight}");
            return null;
        }
        if (!IsPositionInMap(_targetPos, _bottomLeft, _topRight))
        {
            Debug.LogError($"목표점이 맵 범위를 벗어났습니다. 목표점: {_targetPos}, 맵 범위: {_bottomLeft} ~ {_topRight}");
            return null;
        }

        InitializeNodeArray(_bottomLeft, _topRight);
        Node startNode = mNodeArray[_startPos.x - _bottomLeft.x, _startPos.y - _bottomLeft.y];
        Node targetNode = mNodeArray[_targetPos.x - _bottomLeft.x, _targetPos.y - _bottomLeft.y];

        // 시작점이나 목표점이 벽인지 확인
        if (startNode.isWall)
        {
            Debug.LogError($"시작점이 벽입니다. 위치: {_startPos}");
            return null;
        }
        if (targetNode.isWall)
        {
            Debug.LogError($"목표점이 벽입니다. 위치: {_targetPos}");
            return null;
        }

        mOpenList = new List<Node>() { startNode };
        mClosedList = new List<Node>();
        List<Node> finalPath = new List<Node>();

        while (mOpenList.Count > 0)
        {
            Node currentNode = GetLowestFNode();
            mOpenList.Remove(currentNode);
            mClosedList.Add(currentNode);

            if (currentNode == targetNode)
            {
                finalPath = ReconstructPath(targetNode, startNode);
                finalPath = SmoothPath(finalPath);
                Debug.Log($"경로를 찾았습니다. 경로 길이: {finalPath.Count}");
                return finalPath;
            }

            ExploreNeighbors(currentNode, targetNode, _bottomLeft, _topRight);
        }

        Debug.LogWarning($"경로를 찾을 수 없습니다. 시작점: {_startPos}, 목표점: {_targetPos}");
        return null;
    }

    private bool IsPositionInMap(Vector2Int _pos, Vector2Int _bottomLeft, Vector2Int _topRight)
    {
        return _pos.x >= _bottomLeft.x && _pos.x <= _topRight.x &&
               _pos.y >= _bottomLeft.y && _pos.y <= _topRight.y;
    }

    private void InitializeNodeArray(Vector2Int _bottomLeft, Vector2Int _topRight)
    {
        mSizeX = _topRight.x - _bottomLeft.x + 1;
        mSizeY = _topRight.y - _bottomLeft.y + 1;
        mNodeArray = new Node[mSizeX, mSizeY];

        for (int i = 0; i < mSizeX; i++)
        {
            for (int j = 0; j < mSizeY; j++)
            {
                bool isWall = CheckForWall(i + _bottomLeft.x, j + _bottomLeft.y);
                mNodeArray[i, j] = new Node(isWall, i + _bottomLeft.x, j + _bottomLeft.y);
            }
        }
    }

    private bool CheckForWall(int _x, int _y)
    {
        // 타일의 중심 좌표 계산 (0.5, 0.5 오프셋 추가)
        Vector2 checkPos = new Vector2(_x + 0.5f, _y + 0.5f);
        
        // 타일 중심에서 벽 체크
        Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPos, 0.1f);
        foreach (Collider2D col in colliders)
        {
            if (col.gameObject.layer == LayerMask.NameToLayer("Wall"))
            {
                Debug.Log($"벽 발견: 위치 ({_x}, {_y})");
                return true;
            }
        }
        return false;
    }

    private Node GetLowestFNode()
    {
        Node lowestNode = mOpenList[0];
        for (int i = 1; i < mOpenList.Count; i++)
        {
            if (mOpenList[i].F <= lowestNode.F && mOpenList[i].H < lowestNode.H)
                lowestNode = mOpenList[i];
        }
        return lowestNode;
    }

    private void ExploreNeighbors(Node _currentNode, Node _targetNode, Vector2Int _bottomLeft, Vector2Int _topRight)
    {
        // 상하좌우 4방향만 확인
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),  // 위
            new Vector2Int(1, 0),  // 오른쪽
            new Vector2Int(0, -1), // 아래
            new Vector2Int(-1, 0)  // 왼쪽
        };

        // 현재 이동 방향 확인
        Vector2Int currentDirection = Vector2Int.zero;
        if (_currentNode.ParentNode != null)
        {
            currentDirection = new Vector2Int(
                _currentNode.x - _currentNode.ParentNode.x,
                _currentNode.y - _currentNode.ParentNode.y
            );
        }

        foreach (Vector2Int dir in directions)
        {
            int checkX = _currentNode.x + dir.x;
            int checkY = _currentNode.y + dir.y;

            // 맵 범위 내에 있는지 확인
            if (checkX >= _bottomLeft.x && checkX <= _topRight.x &&
                checkY >= _bottomLeft.y && checkY <= _topRight.y)
            {
                Node neighborNode = mNodeArray[checkX - _bottomLeft.x, checkY - _bottomLeft.y];
                
                if (!neighborNode.isWall && !mClosedList.Contains(neighborNode))
                {
                    // 기본 이동 비용
                    int moveCost = _currentNode.G + 10;

                    // 방향 전환 시 페널티 적용
                    if (currentDirection != Vector2Int.zero && dir != currentDirection)
                    {
                        moveCost += 2; // 방향 전환 시 2의 페널티
                    }

                    int heuristic = Mathf.Abs(neighborNode.x - _targetNode.x) + 
                                  Mathf.Abs(neighborNode.y - _targetNode.y);

                    if (moveCost < neighborNode.G || !mOpenList.Contains(neighborNode))
                    {
                        neighborNode.G = moveCost;
                        neighborNode.H = heuristic * 10;
                        neighborNode.ParentNode = _currentNode;

                        if (!mOpenList.Contains(neighborNode))
                            mOpenList.Add(neighborNode);
                    }
                }
            }
        }
    }

    private List<Node> ReconstructPath(Node _targetNode, Node _startNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = _targetNode;

        while (currentNode != _startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.ParentNode;
        }
        path.Add(_startNode);
        path.Reverse();

        return path;
    }

    /// <summary>
    /// 경로를 부드럽게 만드는 메서드
    /// </summary>
    private List<Node> SmoothPath(List<Node> _path)
    {
        if (_path == null || _path.Count <= 2) return _path;

        List<Node> smoothedPath = new List<Node>();
        smoothedPath.Add(_path[0]);

        int currentIndex = 0;
        while (currentIndex < _path.Count - 1)
        {
            int nextIndex = currentIndex + 1;
            while (nextIndex < _path.Count - 1)
            {
                // 두 노드가 같은 행이나 열에 있는 경우에만 직선 경로로 간주
                if ((_path[currentIndex].x == _path[nextIndex + 1].x || 
                     _path[currentIndex].y == _path[nextIndex + 1].y) &&
                    HasDirectPath(_path[currentIndex], _path[nextIndex + 1]))
                {
                    nextIndex++;
                }
                else
                {
                    break;
                }
            }
            smoothedPath.Add(_path[nextIndex]);
            currentIndex = nextIndex;
        }

        return smoothedPath;
    }

    /// <summary>
    /// 두 노드 사이에 직선 경로가 있는지 확인하는 메서드
    /// </summary>
    private bool HasDirectPath(Node _start, Node _end)
    {
        Vector2 startPos = new Vector2(_start.x + 0.5f, _start.y + 0.5f);
        Vector2 endPos = new Vector2(_end.x + 0.5f, _end.y + 0.5f);
        Vector2 direction = (endPos - startPos).normalized;
        float distance = Vector2.Distance(startPos, endPos);

        RaycastHit2D hit = Physics2D.Raycast(startPos, direction, distance, LayerMask.GetMask("Wall"));
        return hit.collider == null;
    }

    private void OnDrawGizmos()
    {
        if (mNodeArray == null) return;

        // 모든 노드 표시
        for (int x = 0; x < mSizeX; x++)
        {
            for (int y = 0; y < mSizeY; y++)
            {
                Node node = mNodeArray[x, y];
                // 타일의 중심 좌표로 표시
                Vector2 pos = new Vector2(node.x + 0.5f, node.y + 0.5f);

                // 벽은 빨간색으로 표시
                if (node.isWall)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(pos, Vector3.one * 0.8f);
                }
                // 열린 리스트의 노드는 초록색으로 표시
                else if (mOpenList != null && mOpenList.Contains(node))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                }
                // 닫힌 리스트의 노드는 파란색으로 표시
                else if (mClosedList != null && mClosedList.Contains(node))
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                }
                // 그 외의 노드는 회색으로 표시
                else
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                }

                // 타일 중심점 표시
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(pos, 0.05f);
            }
        }
    }
} 