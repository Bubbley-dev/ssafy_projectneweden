using UnityEngine;

/// A* 알고리즘에서 사용되는 노드 클래스
[System.Serializable]
public class Node
{
    public Node ParentNode;       // 이전 노드

    // G : 시작점으로부터의 실제 이동 거리
    // H : 목표점까지의 예상 거리 (맨해튼 거리)
    // F : G + H (총 예상 비용)
    public int x, y, G, H;        // x, y: 노드의 좌표, G: 시작점까지 비용, H: 목표점까지 예상 비용
    public int F { get { return G + H; } }
    
    // 이동 방향 정보
    public Vector2Int Direction { get; set; }    // 현재 노드까지의 이동 방향

    // 노드 생성자 (좌표만)
    public Node(int _x, int _y)
    {
        x = _x;
        y = _y;
        G = 0;
        H = 0;
        Direction = Vector2Int.zero;  // 초기 방향은 없음
    }
} 