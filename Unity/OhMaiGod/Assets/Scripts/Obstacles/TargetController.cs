using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 타겟의 서있는 지점을 관리하는 컨트롤러
public class TargetController : MonoBehaviour
{
    [SerializeField] private bool mShowDebug = false;            // 디버그 정보 표시 여부
    [SerializeField] private bool mCanMove = false;              // 타겟이 오브젝트 위에 서있을 수 있는지 여부

    private Collider2D mTargetCollider;                         // 타겟의 콜라이더
    private List<Vector3Int> mOccupiedCells;                 // 타겟이 차지하는 셀 목록
    private List<Vector3Int> mStandingCells;                  // 서있을 수 있는 셀 목록
    private bool mIsInitialized = false;

    // 초기화
    private void Awake() // Start 대신 Awake 사용 고려 (Collider 참조 등)
    {
        mTargetCollider = GetComponent<Collider2D>();
        if (mTargetCollider == null)
        {
            Debug.LogError("TargetController에 Collider2D가 없습니다!", this);
            enabled = false;
            return;
        }
        mOccupiedCells = new List<Vector3Int>();
        mStandingCells = new List<Vector3Int>();
    }

    private void Update()
    {
        if (!TileManager.Instance.mIsInitialized) return;
        if (!mIsInitialized)
        {
            TileManager.Instance.RegisterObject(gameObject, mCanMove);
            InitializeStandingPoints();
            mIsInitialized = true;
        }
    }

    // 서있는 지점들 초기화
    private void InitializeStandingPoints()
    {
        ClearStandingCells();  // 기존 서있는 지점들 제거
        FindOccupiedCells(); // 타겟이 차지하는 셀 먼저 찾기
        FindAvailableAdjacentCells(); // 차지하는 셀 주변의 사용 가능한 셀 찾기
    }

    // 기존 서있는 지점들 제거
    private void ClearStandingCells()
    {
        TileManager.Instance.UnregisterObject(gameObject);
        mStandingCells.Clear();
    }

    // 타겟이 차지하는 셀 찾기
    private void FindOccupiedCells()
    {
        mOccupiedCells = TileManager.Instance.GetOccupiedCells(mTargetCollider.bounds);
    }

    // 차지하는 셀 주변의 사용 가능한 인접 셀 찾기
    private void FindAvailableAdjacentCells()
    {
        List<Vector3Int> neighborCells = new List<Vector3Int>();
        // 상하좌우 및 해당 셀 자체
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right, Vector3Int.zero };

        // 1. 모든 차지된 셀의 인접 셀들을 수집 (중복 제거)
        foreach (Vector3Int occupiedCell in mOccupiedCells)
        {
            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighbor = occupiedCell + dir;
                // 차지된 셀이 아닌 경우에만 후보로 추가
                if (!mOccupiedCells.Contains(neighbor))
                {
                    neighborCells.Add(neighbor);
                }
            }
        }

        if (mShowDebug) { Debug.Log($"[{gameObject.name}] 인접 후보 셀 {neighborCells.Count}개 찾음."); }

        // 2. 인접 셀들의 유효성 검사
        foreach (Vector3Int cell in neighborCells)
        {
            if (TileManager.Instance.TryGetTileInfo(cell, out TileManager.TileInfo info) && info.canMove)
            {
                mStandingCells.Add(cell);
            }
        }

        if (mShowDebug)
        {
            Debug.Log($"[{gameObject.name}] 최종 사용 가능 위치 {mStandingCells.Count}개 확정.", this);
        }
    }

    // 서있는 지점들 업데이트
    public void UpdateStandingCells()
    {
        if (TileManager.Instance == null || mTargetCollider == null) return;
        InitializeStandingPoints();
    }

    // 서있는 지점들의 위치 목록 반환
    public List<Vector3Int> GetStandingCells()
    {
        return new List<Vector3Int>(mStandingCells);
    }

    // 디버그 정보 표시
    private void OnDrawGizmos()
    {
        if (!mShowDebug || TileManager.Instance == null || !Application.isPlaying) return; // Awake/Start 이후 실행 보장

        // 차지하는 셀 표시
        if (mOccupiedCells != null)
        {
            Gizmos.color = Color.magenta;
            foreach (Vector3Int cell in mOccupiedCells)
            {
                Gizmos.DrawCube(TileManager.Instance.GetCellCenterWorld(cell), TileManager.Instance.GroundTilemap.cellSize * 0.9f);
            }
        }

        // 사용 가능한 최종 위치 표시
        if (mStandingCells != null)
        {
            Gizmos.color = Color.green;
            foreach (Vector3Int cell in mStandingCells)
            {
                Gizmos.DrawWireSphere(TileManager.Instance.GetCellCenterWorld(cell), 0.25f); // 조금 더 잘 보이게
            }
        }
    }
}
