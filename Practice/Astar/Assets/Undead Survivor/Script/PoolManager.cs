using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    // ������ ���� ����
    public GameObject[] prefabs;

    // Ǯ�� ��� ����Ʈ
    List<GameObject>[] pools;

    private void Awake()
    {
        pools = new List<GameObject>[prefabs.Length];
        // �� �����鿡 ���� Ǯ�� ����Ʈ �ʱ�ȭ
        for (int i = 0; i < pools.Length; i++)
        {
            pools[i] = new List<GameObject>();
        }
    }

    public GameObject Get(int index)
    {
        GameObject select = null;

        // ������ Ǯ�� ��Ȱ��ȭ�� ���ӿ�����Ʈ ����
       foreach (GameObject obj in pools[index])
        {
            // �߰��ϸ� select ������ �Ҵ�
            if (!obj.activeSelf)
            {
                select = obj;
                break;
            }
        }

        // ��ã������
        if (!select)
        {
            // ���Ӱ� �����ϰ� select ������ �Ҵ�
            select = Instantiate(prefabs[index], transform);
            pools[index].Add(select);
        }
        select.SetActive(true);
        return select;
    }
}
