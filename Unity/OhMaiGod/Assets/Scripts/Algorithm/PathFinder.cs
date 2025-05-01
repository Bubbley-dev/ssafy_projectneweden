using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A* 알고리즘을 사용하여 경로를 찾는 싱글톤 클래스
/// </summary>
public class PathFinder : MonoBehaviour
{
    [SerializeField] private bool mShowDebug = true;
    private static PathFinder mInstance;
    // 싱글톤 인스턴스를 반환하는 프로퍼티
    public static PathFinder Instance
    {
        get
        {
            if (mInstance == null)
            {
                mInstance = FindAnyObjectByType<PathFinder>();
                if (mInstance == null)
                {
                    GameObject go = new GameObject("PathFinder");
                    mInstance = go.AddComponent<PathFinder>();
                }
            }
            return mInstance;
        }
    }

    private GameObject mGameObject;
    private Dictionary<Vector3Int, Node> mNodeDict; // 셀 좌표 기반 노드 딕셔너리
    private NodePriorityQueue mOpenList;                    // 열린 목록(우선순위 큐)
    private List<Node> mClosedList;                          // 닫힌 목록
    private bool mNodeDictInitialized = false; // 노드 딕셔너리 초기화 여부

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

    private void Update()
    {
        if (!TileManager.Instance.mIsInitialized) return;
        if (!mNodeDictInitialized)
        {
            InitializeNodeDict();
            mNodeDictInitialized = true;
        }
    }

    /// <summary>
    /// 시작점에서 목표점까지의 경로를 찾는 메서드 (타일맵 기준)
    /// </summary>
    /// <param name="_startCell">시작 셀</param>
    /// <param name="_targetCell">목표 셀</param>
    /// <param name="_gameObject">경로를 찾을 게임 오브젝트</param>
    /// <returns>찾은 경로의 노드 리스트</returns>
    public List<Node> FindPath(Vector3Int _startCell, Vector3Int _targetCell, GameObject _gameObject)
    {
        mGameObject = _gameObject;
        if (TileManager.Instance == null)
        {
            Debug.LogError("TileManager에 바닥 타일맵이 할당되지 않았습니다!", this);
            return null;
        }

        if (!mNodeDictInitialized)
        {
            Debug.LogError("노드 딕셔너리가 초기화되지 않았습니다!");
            return null;
        }

        if (!mNodeDict.TryGetValue(_startCell, out Node startNode))
        {
            Debug.LogError($"시작 셀에 노드가 없습니다: {_startCell}");
            return null;
        }
        if (!mNodeDict.TryGetValue(_targetCell, out Node targetNode))
        {
            Debug.LogError($"목표 셀에 노드가 없습니다: {_targetCell}");
            return null;
        }

        // 시작점이나 목표점이 이동 불가 셀인지 확인 (실시간 TileManager 참조)
        if (!TileManager.Instance.TryGetTileInfo(_startCell, out TileManager.TileInfo infoStart) || !infoStart.canMove)
        {
            Debug.LogError($"시작점이 이동 불가 셀입니다. 위치: {_startCell}");
            return null;
        }
        if (!TileManager.Instance.TryGetTileInfo(_targetCell, out TileManager.TileInfo infoTarget) || !infoTarget.canMove)
        {
            Debug.Log($"목표점이 이동 불가 셀입니다. 위치: {_targetCell}");
            return null;
        }

        mOpenList = new NodePriorityQueue();  // 오픈리스트 초기화
        mOpenList.Enqueue(startNode);
        mClosedList = new List<Node>();  // 클로즈드 리스트 초기화
        List<Node> finalPath = new List<Node>();

        while (mOpenList.Count > 0)
        {
            Node currentNode = mOpenList.Dequeue();
            mClosedList.Add(currentNode);

            if (currentNode == targetNode)
            {
                finalPath = ReconstructPath(targetNode, startNode);
                finalPath = SmoothPath(finalPath);
                return finalPath;
            }
            ExploreNeighbors(currentNode, targetNode);
        }

        Debug.LogWarning($"경로를 찾을 수 없습니다. 시작점: {_startCell}, 목표점: {_targetCell}");
        return null;
    }

    // 노드 딕셔너리를 타일매니저의 실제 타일 셀 기준으로 초기화
    private void InitializeNodeDict()
    {
        mNodeDict = new Dictionary<Vector3Int, Node>();
        foreach (var cell in TileManager.Instance.GroundTilemap.cellBounds.allPositionsWithin)
        {
            if (TileManager.Instance.TryGetTileInfo(cell, out _))
            {
                mNodeDict[cell] = new Node(cell.x, cell.y);
            }
        }
    }

    // 해당 셀에 이동할 수 있는지 확인
    private bool CheckForWall(Vector3Int cell)
    {
        return !TileManager.Instance.TryGetTileInfo(cell, out TileManager.TileInfo info) || !info.canMove;
    }

    // 현재 노드의 이웃 노드들을 탐색하는 메서드
    private void ExploreNeighbors(Node _currentNode, Node _targetNode)
    {
        // 상하좌우 4방향
        Vector3Int[] directions = new Vector3Int[]
        {
            Vector3Int.up,    // 위쪽 셀
            Vector3Int.down,  // 아래쪽 셀
            Vector3Int.left,  // 왼쪽 셀
            Vector3Int.right  // 오른쪽 셀
        };

        foreach (Vector3Int dir in directions)
        {
            Vector3Int neighborCell = new Vector3Int(_currentNode.x, _currentNode.y, 0) + dir;
            if (!mNodeDict.ContainsKey(neighborCell)) continue; // 실제 노드가 있는 셀만 탐색
            // 실시간으로 이동 가능 여부 체크
            if (!TileManager.Instance.TryGetTileInfo(neighborCell, out TileManager.TileInfo info) || !info.canMove) continue;
            Node neighborNode = mNodeDict[neighborCell];
            if (mClosedList.Contains(neighborNode)) continue;

            int moveCost = _currentNode.G + 10;
            if (_currentNode.ParentNode != null)
            {
                Vector2Int currentDirection = new Vector2Int(dir.x, dir.y);
                Vector2Int previousDirection = _currentNode.Direction;
                if (currentDirection != previousDirection)
                {
                    if (currentDirection == -previousDirection)
                        moveCost += 30;
                    else
                        moveCost += 15;
                }
            }
            int heuristic = Mathf.Abs(neighborNode.x - _targetNode.x) + Mathf.Abs(neighborNode.y - _targetNode.y);
            if (!mOpenList.Contains(neighborNode) || moveCost < neighborNode.G)
            {
                neighborNode.G = moveCost;
                neighborNode.H = heuristic * 10;
                neighborNode.ParentNode = _currentNode;
                neighborNode.Direction = new Vector2Int(dir.x, dir.y);
                if (!mOpenList.Contains(neighborNode))
                {
                    mOpenList.Enqueue(neighborNode);
                }
                else
                {
                    mOpenList.UpdatePriority(neighborNode);
                }
            }
        }
    }

    // 목표 노드에서 시작 노드까지의 경로를 재구성하는 메서드
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

    // 경로를 부드럽게 만드는 메서드
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

    // 두 노드 사이에 직선 경로가 있는지 확인하는 메서드
    private bool HasDirectPath(Node _start, Node _end)
    {
        int x0 = _start.x;
        int y0 = _start.y;
        int x1 = _end.x;
        int y1 = _end.y;

        // Bresenham's Line Algorithm (정수 좌표 직선 순회)
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            Vector3Int cell = new Vector3Int(x0, y0, 0);
            if (!TileManager.Instance.TryGetTileInfo(cell, out TileManager.TileInfo info) || !info.canMove)
                return false;

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        return true;
    }

    // 두 지점 사이의 경로 비용을 계산하는 메서드
    public float CalculatePathCost(Vector3Int _startCell, Vector3Int _targetCell, GameObject _gameObject)
    {
        // FindPath로 경로를 구함
        List<Node> path = FindPath(_startCell, _targetCell, _gameObject);
        if (path == null || path.Count == 0)
            return float.MaxValue; // 경로가 없으면 최대값 반환

        // 마지막 노드의 G값(누적 실제 비용)을 반환
        return path[path.Count - 1].G;
    }

    // 디버그용 기즈모를 그리는 메서드
    private void OnDrawGizmos()
    {
        if (mNodeDict == null) return;
        if (!mShowDebug) return;

        // 모든 노드 표시 (타일이 있는 셀만)
        foreach (var pair in mNodeDict)
        {
            Node node = pair.Value;
            Vector2 pos = new Vector2(node.x + 0.5f, node.y + 0.5f);

            // 벽은 빨간색으로 표시
            if (CheckForWall(new Vector3Int(node.x, node.y, 0)))
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

// 최소 힙 기반 우선순위 큐 (F값 기준)
public class NodePriorityQueue
{
    private List<Node> heap = new List<Node>();
    public int Count => heap.Count;
    public void Enqueue(Node node)
    {
        heap.Add(node);
        int i = heap.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (Compare(heap[i], heap[parent]) >= 0) break;
            Swap(i, parent);
            i = parent;
        }
    }
    public Node Dequeue()
    {
        if (heap.Count == 0) return null;
        Node root = heap[0];
        heap[0] = heap[heap.Count - 1];
        heap.RemoveAt(heap.Count - 1);
        Heapify(0);
        return root;
    }
    public bool Contains(Node node) => heap.Contains(node);
    public void UpdatePriority(Node node)
    {
        int i = heap.IndexOf(node);
        if (i >= 0)
        {
            // 위로 올리기
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (Compare(heap[i], heap[parent]) >= 0) break;
                Swap(i, parent);
                i = parent;
            }
            // 아래로 내리기
            Heapify(i);
        }
    }
    private void Heapify(int i)
    {
        int left = 2 * i + 1, right = 2 * i + 2, smallest = i;
        if (left < heap.Count && Compare(heap[left], heap[smallest]) < 0) smallest = left;
        if (right < heap.Count && Compare(heap[right], heap[smallest]) < 0) smallest = right;
        if (smallest != i)
        {
            Swap(i, smallest);
            Heapify(smallest);
        }
    }
    private int Compare(Node a, Node b)
    {
        // F값이 낮은 게 우선, F가 같으면 H가 낮은 게 우선
        if (a.F != b.F) return a.F.CompareTo(b.F);
        return a.H.CompareTo(b.H);
    }
    private void Swap(int i, int j)
    {
        Node tmp = heap[i];
        heap[i] = heap[j];
        heap[j] = tmp;
    }
}