using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Node 클래스 정의는 Node.cs 파일로 분리됨
// A* 알고리즘 로직은 AStarPathfinderUtil.cs 파일로 분리됨

public class MyGameManager : MonoBehaviour
{
    // 그리드 관련 변수 제거됨
    // A* 관련 변수 제거됨

    // MyGameManager는 이제 다른 게임 관리 목적을 수행하거나,
    // 다른 책임이 없다면 제거될 수 있습니다.

    // 예시: 다른 매니저 참조 또는 게임 상태 관리
    // public UIManager uiManager;
    // public ScoreManager scoreManager;

    void Start()
    {
        // 다른 게임 시스템 초기화 코드가 여기에 들어갈 수 있습니다.
        Debug.Log("MyGameManager 시작됨 (경로 탐색 로직 제거됨)");
    }

    // PathFinding 메서드 제거됨
    // ProcessNeighbours 메서드 제거됨
    // RetracePath 메서드 제거됨
    // CalculateHeuristic 메서드 제거됨
    // OnDrawGizmos 메서드 제거됨
}