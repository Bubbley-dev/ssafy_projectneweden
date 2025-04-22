using UnityEngine;

[System.Serializable]
public class Node
{
    public Vector2Int Position { get; private set; } // 노드의 그리드 좌표
    public bool IsWall { get; set; }                // 이 노드가 벽인지 여부
    public Node ParentNode { get; set; }             // A* 알고리즘에서 이 노드로 오기 직전의 노드

    public float GCost { get; set; } // 시작 노드로부터의 비용
    public float HCost { get; set; } // 목표 노드까지의 예상 비용 (휴리스틱)
    public float FCost { get { return GCost + HCost; } } // 총 비용 (G + H)

    public Node(Vector2Int position, bool isWall)
    {
        Position = position;
        IsWall = isWall;
        ParentNode = null;
        GCost = float.MaxValue; // 비용 초기화 (매우 큰 값)
        HCost = 0;
    }

    // HashSet/Dictionary 등에서 효율적인 비교를 위한 Equals 및 GetHashCode 재정의
    public override bool Equals(object obj)
    {
        return obj is Node other && Position.Equals(other.Position);
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode();
    }
} 