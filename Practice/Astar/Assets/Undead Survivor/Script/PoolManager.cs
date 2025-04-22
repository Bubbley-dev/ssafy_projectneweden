using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    // 프리펩 보관 변수
    public GameObject[] prefabs;

    // 풀링 담당 리스트
    List<GameObject>[] pools;

    private void Awake()
    {
        pools = new List<GameObject>[prefabs.Length];
        // 각 프리펩에 대한 풀링 리스트 초기화
        for (int i = 0; i < pools.Length; i++)
        {
            pools[i] = new List<GameObject>();
        }
    }

    public GameObject Get(int index)
    {
        GameObject select = null;

        // 선택한 풀의 비활성화된 게임오브젝트 접근
       foreach (GameObject obj in pools[index])
        {
            // 발견하면 select 변수에 할당
            if (!obj.activeSelf)
            {
                select = obj;
                break;
            }
        }

        // 못찾았으면
        if (!select)
        {
            // 새롭게 생성하고 select 변수에 할당
            select = Instantiate(prefabs[index], transform);
            pools[index].Add(select);
        }
        select.SetActive(true);
        return select;
    }
}
