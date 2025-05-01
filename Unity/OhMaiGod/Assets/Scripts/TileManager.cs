using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
public class TileManager : MonoBehaviour
{
    private static TileManager mInstance;

    public struct TileInfo
    {
        public TileBase tile;                                   // 타일 정보
        public Vector3 position;                                // 타일 위치
        public List<GameObject> objects;                        // 타일에 존재하는 오브젝트 리스트
        public bool canMove;                                     // 타일에 이동할 수 있는지 여부
    }
    
    [Header("Tilemaps")]
    [SerializeField] private Tilemap mGroundTilemap;    // 바닥 타일맵 (기본 맵 전체 영역)
    [SerializeField] private Tilemap mWallTilemap;      // 벽 타일맵 (벽, collider 적용, 충돌 판정)
    [SerializeField] private List<Tilemap> mSectionTilemaps;   // 구역 타일맵 (구역 나누기)

    [Header("Layer Masks")]
    [SerializeField] private LayerMask mWallLayerMask;        // 벽 레이어 마스크
    [SerializeField] private LayerMask mObjectLayerMask;      // 오브젝트 레이어 마스크
    [SerializeField] private LayerMask mNPCLayerMask;         // NPC 레이어 마스크

    [Header("Debug")]
    [SerializeField] private bool mShowDebug = false;

    private Dictionary<Vector3Int, TileInfo> mTilemapInfo = new(); // 타일맵 정보
    
    public bool mIsInitialized = false;

    // 싱글톤 인스턴스를 반환하는 프로퍼티
    public static TileManager Instance { 
        get { return mInstance; }
    }

    private void Awake()
    {
        mInstance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Ground 타일맵의 모든 셀 정보를 미리 읽어서 저장
        mTilemapInfo.Clear();  // 타일맵 정보 초기화
        foreach (Vector3Int pos in mGroundTilemap.cellBounds.allPositionsWithin)  // 모든 셀 정보 순회
        {
            TileBase tile = mGroundTilemap.GetTile(pos);  // 타일 정보 조회
            if (tile != null)  // 타일이 존재하면
            {
                TileInfo info = new()
                {
                    tile = tile,
                    position = mGroundTilemap.GetCellCenterWorld(pos),
                    objects = new List<GameObject>(),
                    canMove = true
                };
                mTilemapInfo[pos] = info;
            }
        }

        // Wall 타일맵 중 타일이 있는 위치를 Ground 타일맵의 위치로 변환하여 저장
        foreach (Vector3Int pos in mWallTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = mWallTilemap.GetTile(pos);
            if (tile != null)
            {
                Vector3 worldPos = mWallTilemap.GetCellCenterWorld(pos);
                Vector3Int groundPos = WorldToCell(worldPos);
                if (mTilemapInfo.TryGetValue(groundPos, out TileInfo info))
                {
                    info.canMove = false;
                    mTilemapInfo[groundPos] = info;
                }
            }
        }
        mIsInitialized = true;
    }

    public Tilemap GroundTilemap { get { return mGroundTilemap; } }
    public Tilemap WallTilemap { get { return mWallTilemap; } }
    public List<Tilemap> SectionTilemaps { get { return mSectionTilemaps; } }
    public LayerMask WallLayerMask { get { return mWallLayerMask; } }
    public LayerMask ObjectLayerMask { get { return mObjectLayerMask; } }
    public LayerMask NPCLayerMask { get { return mNPCLayerMask; } }
    public LayerMask ObstacleLayerMask { get { return mWallLayerMask | mObjectLayerMask | mNPCLayerMask; } }

    // 셀 좌표로 타일 정보 조회 (존재하면 true 반환)
    public bool TryGetTileInfo(Vector3Int cell, out TileInfo info)
    {
        return mTilemapInfo.TryGetValue(cell, out info);
    }

    // 월드 좌표를 타일맵 셀 좌표로 변환
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return mGroundTilemap.WorldToCell(worldPos);
    }

    // 타일맵 셀 좌표의 중심 월드 좌표 반환
    public Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        return mGroundTilemap.GetCellCenterWorld(cell);
    }

    // 오브젝트의 Bounds를 얻는 함수 (Collider2D > Renderer > transform.position)
    private Bounds GetObjectBounds(GameObject obj)
    {
        Collider2D col = obj.GetComponent<Collider2D>();
        if (col != null)
        {
            return col.bounds;
        }
        SpriteRenderer spr = obj.GetComponent<SpriteRenderer>();
        if (spr != null)
        {
            return spr.bounds;
        }
        // 컴포넌트가 없으면 1x1 크기로 처리
        return new Bounds(obj.transform.position, Vector3.one);
    }

    // 오브젝트를 차지하는 셀에 자동 등록
    public void RegisterObject(GameObject obj, bool canMove = true)
    {
        Bounds bounds = GetObjectBounds(obj);
        var cells = GetOccupiedCells(bounds);
        foreach (var cell in cells)
        {
            if (mTilemapInfo.TryGetValue(cell, out TileInfo info))
            {
                if (mShowDebug) Debug.Log($"셀 {cell}에 오브젝트 {obj.name} 등록");
                if (info.objects == null)
                    info.objects = new List<GameObject>();
                if (!info.objects.Contains(obj))
                    info.objects.Add(obj);
                info.canMove = canMove;
                mTilemapInfo[cell] = info;
            }
        }
        if (mShowDebug) Debug.Log($"오브젝트 등록: {obj.name}");
    }

    // 오브젝트를 차지하는 셀에서 자동 해제
    public void UnregisterObject(GameObject obj)
    {
        Bounds bounds = GetObjectBounds(obj);
        var cells = GetOccupiedCells(bounds);
        foreach (var cell in cells)
        {
            if (mTilemapInfo.TryGetValue(cell, out TileInfo info) && info.objects != null)
            {
                info.objects.Remove(obj);
                mTilemapInfo[cell] = info;
            }
        }
        if (mShowDebug) Debug.Log($"오브젝트 삭제: {obj.name}");
    }

    // 오브젝트의 월드 바운드로부터 차지하는 셀 목록 반환
    public List<Vector3Int> GetOccupiedCells(Bounds bounds)
    {
        List<Vector3Int> cells = new();
        Vector3Int minCell = mGroundTilemap.WorldToCell(bounds.min);
        Vector3Int maxCell = mGroundTilemap.WorldToCell(bounds.max);

        if (mShowDebug) Debug.Log($"[GetOccupiedCells] bounds: {bounds}, minCell: {minCell}, maxCell: {maxCell}");

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cell = new(x, y, 0);
                if (mTilemapInfo.ContainsKey(cell))
                {
                    cells.Add(cell);
                    if (mShowDebug) Debug.Log($"[GetOccupiedCells] 포함: {cell}");
                }
                else
                {
                    if (mShowDebug) Debug.LogWarning($"[GetOccupiedCells] 셀 {cell}은(는) mTilemapInfo에 없음! (GroundTilemap에 타일이 없음)");
                }
            }
        }
        if (cells.Count == 0 && mShowDebug) Debug.LogWarning($"[GetOccupiedCells] 차지하는 셀이 하나도 없습니다!");
        return cells;
    }

    public Vector3Int GetObjectCell(GameObject obj)
    {
        Vector3Int cell = WorldToCell(obj.transform.position);
        if (ExistObjectInCell(cell))
            return cell;
        return Vector3Int.zero;
    }

    public bool ExistObjectInCell(Vector3Int cell)
    {
        if (mTilemapInfo.TryGetValue(cell, out TileInfo info))
            return info.objects != null && info.objects.Count > 0;
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!mShowDebug) return;
        Gizmos.color = Color.green;
        foreach (var cell in mTilemapInfo)
        {
            Gizmos.DrawWireCube(cell.Value.position, Vector3.one);
        }
        Gizmos.color = Color.red;
        foreach (var cell in mTilemapInfo)
        {
            if (!cell.Value.canMove)
                Gizmos.DrawCube(cell.Value.position, Vector3.one);
        }
        Gizmos.color = Color.blue;
        foreach (var cell in mTilemapInfo)
        {
            // 오브젝트가 있는 셀만 표시
            if (cell.Value.objects != null && cell.Value.objects.Count > 0)
                Gizmos.DrawSphere(cell.Value.position, 0.1f);
        }
    }
}