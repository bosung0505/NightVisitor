using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, GameObject> prefabDictionary; // 프리팹을 동적으로 등록하기 위한 보관소

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 씬이 넘어가도 유지되도록 처리 (단일 씬 구조면 필요 없을 수 있지만 안전장치)
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        prefabDictionary = new Dictionary<string, GameObject>();
    }

    /// <summary>
    /// 로딩 화면 뒤에서 미리 오브젝트들을 생성해 풀(Pool)에 채워둡니다.
    /// </summary>
    public void PreWarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        
        string tag = prefab.name; // 프리팹 이름을 태그로 사용

        if (!poolDictionary.ContainsKey(tag))
        {
            poolDictionary[tag] = new Queue<GameObject>();
            prefabDictionary[tag] = prefab;
        }

        int currentCount = poolDictionary[tag].Count;
        int needed = count - currentCount;

        for (int i = 0; i < needed; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            poolDictionary[tag].Enqueue(obj);
        }
    }

    /// <summary>
    /// 풀에서 비활성화된 오브젝트를 꺼내어 원하는 위치와 회전값으로 배치하고 활성화합니다.
    /// 풀이 비어있다면 즉시 새 오브젝트를 생성하여 반환합니다.
    /// </summary>
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!poolDictionary.ContainsKey(tag) || poolDictionary[tag].Count == 0)
        {
            // 풀이 없거나 비어있으면 새로 생성 (보험용)
            if (prefabDictionary.ContainsKey(tag))
            {
                GameObject newObj = Instantiate(prefabDictionary[tag], position, rotation, parent);
                newObj.SetActive(true);
                return newObj;
            }
            else
            {
                Debug.LogWarning($"[ObjectPoolManager] '{tag}' 프리팹이 PreWarm 되지 않았습니다! 풀을 찾을 수 없습니다.");
                return null;
            }
        }

        GameObject objToSpawn = poolDictionary[tag].Dequeue();
        
        // 간혹 Destroy 되어버린 쓰레기값이 있을 경우를 대비
        while (objToSpawn == null && poolDictionary[tag].Count > 0)
        {
            objToSpawn = poolDictionary[tag].Dequeue();
        }

        if (objToSpawn == null)
        {
            // 큐에 있던 게 다 null이었다면 새로 만듦
            GameObject newObj = Instantiate(prefabDictionary[tag], position, rotation, parent);
            newObj.SetActive(true);
            return newObj;
        }

        // 정상적으로 꺼냈다면 위치 셋업 후 활성화
        objToSpawn.transform.SetParent(parent);
        objToSpawn.transform.position = position;
        objToSpawn.transform.rotation = rotation;
        objToSpawn.SetActive(true);

        return objToSpawn;
    }

    /// <summary>
    /// 사용이 끝난 오브젝트(죽은 시체 등)를 다시 비활성화하여 풀 큐(Queue)로 반납합니다.
    /// 반납할 때 부모를 다시 PoolManager 쪽으로 가져옵니다.
    /// </summary>
    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;
        
        // 이름에 (Clone)이 붙어있다면 제거해서 태그를 찾음
        string tag = obj.name.Replace("(Clone)", "").Trim();

        obj.SetActive(false);
        obj.transform.SetParent(transform);

        if (!poolDictionary.ContainsKey(tag))
        {
            poolDictionary[tag] = new Queue<GameObject>();
            prefabDictionary[tag] = obj; // 임시 프리팹 등록 (안전장치)
        }

        // 이미 큐에 들어있는 중복 삽입 방지 (성능 위해 리스트보단 포함여부만 체크)
        if (!poolDictionary[tag].Contains(obj))
        {
            poolDictionary[tag].Enqueue(obj);
        }
    }

    /// <summary>
    /// 스테이지가 끝난 후 풀을 완전히 비워서 메모리를 비우고 싶을 때 호출합니다.
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var queue in poolDictionary.Values)
        {
            foreach (GameObject obj in queue)
            {
                if (obj != null) Destroy(obj);
            }
            queue.Clear();
        }
        poolDictionary.Clear();
        prefabDictionary.Clear();
        Debug.Log("[ObjectPoolManager] 모든 풀의 오브젝트가 파괴되고 메모리가 정리되었습니다.");
    }
}
