using UnityEngine;

public class ColliderTester : MonoBehaviour
{
    void Start()
    {
        // Wall 레이어 정보 출력
        int wallLayer = LayerMask.NameToLayer("Wall");
        int wallLayerMask = LayerMask.GetMask("Wall");
        
        Debug.Log($"Wall Layer Info - Layer: {wallLayer}, Mask: {wallLayerMask}");
        
        // 현재 위치에서 콜라이더 체크
        Vector2 testPos = transform.position;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(testPos, 1f);
        
        Debug.Log($"Testing position: {testPos}");
        Debug.Log($"Found {colliders.Length} colliders:");
        foreach (var col in colliders)
        {
            Debug.Log($"- {col.gameObject.name} (Layer: {LayerMask.LayerToName(col.gameObject.layer)})");
        }
    }

    void OnDrawGizmos()
    {
        // 테스트 범위 시각화
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
} 