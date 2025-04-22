using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC의 이동을 제어하는 클래스
/// </summary>
public class NPCController : MonoBehaviour
{
    [SerializeField] private float mMoveSpeed = 2f; // 이동 속도
    [SerializeField] private float mReachedDistance = 0.01f; // 목표 지점 도달 판정 거리
    [SerializeField] private Vector2Int mMapBottomLeft; // 맵의 왼쪽 하단 좌표
    [SerializeField] private Vector2Int mMapTopRight; // 맵의 오른쪽 상단 좌표
    [SerializeField] private TargetType mTargetType; // 찾아갈 목표물의 타입
    [SerializeField] private float mTargetSearchInterval = 2f; // 목표물 탐색 간격
    [SerializeField] private float mPathUpdateInterval = 2f; // 경로 업데이트 간격

    private List<Node> mCurrentPath; // 현재 경로
    private int mCurrentPathIndex; // 현재 경로 인덱스
    private Vector2 mTargetPosition; // 현재 목표 위치
    private bool mIsMoving; // 이동 중 여부
    private Transform mCurrentTarget; // 현재 목표물
    private float mLastTargetSearchTime; // 마지막 목표물 탐색 시간
    private float mLastPathUpdateTime; // 마지막 경로 업데이트 시간

    private void Start()
    {
        mLastTargetSearchTime = Time.time;
        mLastPathUpdateTime = Time.time;
        FindAndMoveToTarget();
    }

    private void Update()
    {
        // 일정 간격으로 목표물 탐색
        if (Time.time - mLastTargetSearchTime >= mTargetSearchInterval)
        {
            FindAndMoveToTarget();
            mLastTargetSearchTime = Time.time;
        }

        // 현재 목표물이 있고, 일정 간격으로 경로 업데이트
        if (mCurrentTarget != null && Time.time - mLastPathUpdateTime >= mPathUpdateInterval)
        {
            UpdatePath();
            mLastPathUpdateTime = Time.time;
        }

        if (!mIsMoving) return;

        // 현재 목표 지점으로 이동
        Vector2 direction = (mTargetPosition - (Vector2)transform.position).normalized;
        
        // 한 번에 한 축으로만 이동
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            // x축 이동이 더 크면 x축으로만 이동
            direction = new Vector2(Mathf.Sign(direction.x), 0);
        }
        else
        {
            // y축 이동이 더 크면 y축으로만 이동
            direction = new Vector2(0, Mathf.Sign(direction.y));
        }

        transform.position += (Vector3)(direction * mMoveSpeed * Time.deltaTime);

        // 목표 지점에 도달했는지 확인
        if (Vector2.Distance(transform.position, mTargetPosition) <= mReachedDistance)
        {
            mCurrentPathIndex++;
            
            // 다음 목표 지점이 있으면 이동
            if (mCurrentPathIndex < mCurrentPath.Count)
            {
                // 다음 경로 노드의 타일 중심으로 이동
                mTargetPosition = new Vector2(
                    mCurrentPath[mCurrentPathIndex].x + 0.5f,
                    mCurrentPath[mCurrentPathIndex].y + 0.5f
                );
            }
            else
            {
                // 경로의 끝에 도달
                mIsMoving = false;
                transform.position = mTargetPosition;
            }
        }
    }

    /// <summary>
    /// 목표물을 찾고 이동을 시작하는 메서드
    /// </summary>
    private void FindAndMoveToTarget()
    {
        if (mTargetType == TargetType.None) return;

        // "Target" 태그를 가진 모든 오브젝트 찾기
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");
        Transform closestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (GameObject target in targets)
        {
            // 목표물의 이름이 TargetType과 일치하는지 확인
            if (target.name == mTargetType.ToString())
            {
                float distance = Vector2.Distance(transform.position, target.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target.transform;
                }
            }
        }

        // 가장 가까운 목표물이 있으면 목표물의 가장 가까운 StandingPoint 위치로 이동
        if (closestTarget != null)
        {
            TargetController targetController = closestTarget.GetComponent<TargetController>();
            if (targetController != null)
            {
                List<Vector2> standingPoints = targetController.GetStandingPositions();
                if (standingPoints.Count > 0)
                {
                    // 가장 가까운 StandingPoint 찾기
                    Vector2 closestStandingPoint = standingPoints[0];
                    float minDistance = float.MaxValue;
                    foreach (Vector2 point in standingPoints)
                    {
                        float distance = Vector2.Distance(transform.position, point);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestStandingPoint = point;
                        }
                    }

                    mCurrentTarget = closestTarget;
                    MoveTo(closestStandingPoint);
                }
            }
        }
        else
        {
            Debug.LogWarning($"타입이 {mTargetType}인 목표물을 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 현재 경로를 업데이트하는 메서드
    /// </summary>
    private void UpdatePath()
    {
        if (mCurrentTarget == null) return;

        TargetController targetController = mCurrentTarget.GetComponent<TargetController>();
        if (targetController != null)
        {
            List<Vector2> standingPoints = targetController.GetStandingPositions();
            if (standingPoints.Count > 0)
            {
                // 가장 가까운 StandingPoint 찾기
                Vector2 closestStandingPoint = standingPoints[0];
                float minDistance = float.MaxValue;
                foreach (Vector2 point in standingPoints)
                {
                    // 거리 계산 (standing point와 실제 target의 방향이 대각선이라면 그만큼 멀리 있는 것으로 판단)
                    Vector2 direction = (point - (Vector2)mCurrentTarget.position).normalized;
                    float distance = Vector2.Distance(transform.position, point);
                    if (direction.x != 0 && direction.y != 0)
                    {
                        distance += 2f;
                    }

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestStandingPoint = point;
                    }
                }

                MoveTo(closestStandingPoint);
            }
        }
    }

    /// <summary>
    /// 목표 위치로 이동을 시작하는 메서드
    /// </summary>
    /// <param name="_targetPosition">목표 위치</param>
    public void MoveTo(Vector2 _targetPosition)
    {
        // NPC의 현재 위치를 타일 중심으로 조정
        Vector2Int startPos = new Vector2Int(
            Mathf.FloorToInt(transform.position.x),
            Mathf.FloorToInt(transform.position.y)
        );

        // 목표 위치를 타일 중심으로 조정
        Vector2Int targetPos = new Vector2Int(
            Mathf.FloorToInt(_targetPosition.x),
            Mathf.FloorToInt(_targetPosition.y)
        );

        Debug.Log($"시작 위치: {startPos}, 목표 위치: {targetPos}");

        mCurrentPath = PathFinder.Instance.FindPath(startPos, targetPos, mMapBottomLeft, mMapTopRight);
        
        if (mCurrentPath != null && mCurrentPath.Count > 0)
        {
            mCurrentPathIndex = 0;
            // 첫 번째 경로 노드의 타일 중심으로 이동
            mTargetPosition = new Vector2(
                mCurrentPath[0].x + 0.5f,
                mCurrentPath[0].y + 0.5f
            );
            mIsMoving = true;
        }
        else
        {
            Debug.LogWarning("경로를 찾을 수 없습니다.");
            mIsMoving = false;
        }
    }

    private void OnDrawGizmos()
    {
        // 현재 경로 표시
        if (mCurrentPath != null && mCurrentPath.Count > 0)
        {
            // 경로 선 표시
            Gizmos.color = Color.yellow;
            for (int i = 0; i < mCurrentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(
                    new Vector2(mCurrentPath[i].x, mCurrentPath[i].y),
                    new Vector2(mCurrentPath[i + 1].x, mCurrentPath[i + 1].y)
                );
            }

            // 경로 노드 표시
            Gizmos.color = Color.blue;
            foreach (Node node in mCurrentPath)
            {
                Gizmos.DrawWireSphere(new Vector2(node.x, node.y), 0.2f);
            }

            // 현재 목표 노드 강조 표시
            if (mCurrentPathIndex < mCurrentPath.Count)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(new Vector2(mCurrentPath[mCurrentPathIndex].x, mCurrentPath[mCurrentPathIndex].y), 0.3f);
            }
        }

        // 현재 목표물 표시
        if (mCurrentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(mCurrentTarget.position, 0.5f);
            
            // 목표물 방향 표시
            Gizmos.DrawLine(transform.position, mCurrentTarget.position);
        }

        // NPC의 현재 위치와 이동 방향 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        if (mIsMoving)
        {
            Vector2 direction = (mTargetPosition - (Vector2)transform.position).normalized;
            Gizmos.DrawRay(transform.position, direction);
        }

        // 맵 경계 표시
        Gizmos.color = Color.white;
        Vector2 bottomLeft = new Vector2(mMapBottomLeft.x, mMapBottomLeft.y);
        Vector2 topRight = new Vector2(mMapTopRight.x, mMapTopRight.y);
        Vector2 topLeft = new Vector2(mMapBottomLeft.x, mMapTopRight.y);
        Vector2 bottomRight = new Vector2(mMapTopRight.x, mMapBottomLeft.y);
        
        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);

        // NPC의 목표 타입 표시
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, 
            $"Target: {mTargetType}\nMoving: {mIsMoving}");

        // 현재 목표 위치 표시
        if (mIsMoving)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(mTargetPosition, 0.3f);
        }
    }
}
