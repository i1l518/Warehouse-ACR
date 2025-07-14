// StationManager.cs
using UnityEngine;
using System.Collections.Generic;

// 스테이션 정보를 담을 간단한 클래스
[System.Serializable]
public class Station
{
    public string id;
    public Transform transform;
}

public class StationManager : MonoBehaviour
{
    public static StationManager Instance { get; private set; }

    // Inspector에서 모든 스테이션을 등록
    public List<Station> stations = new List<Station>();

    // 빠른 조회를 위한 딕셔너리
    private Dictionary<string, Transform> stationDictionary = new Dictionary<string, Transform>();

    void Awake()
    {
        // 싱글턴 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 리스트에 있는 스테이션 정보들을 딕셔너리로 변환하여 빠른 검색을 준비
        foreach (var station in stations)
        {
            if (!stationDictionary.ContainsKey(station.id))
            {
                stationDictionary.Add(station.id, station.transform);
            }
        }
    }

    /// <summary>
    /// 스테이션 ID로 해당 Transform을 찾아 반환합니다.
    /// </summary>
    /// <param name="id">찾을 스테이션의 ID</param>
    /// <returns>찾은 Transform. 없으면 null.</returns>
    public Transform GetStationTransform(string id)
    {
        if (stationDictionary.TryGetValue(id, out Transform stationTransform))
        {
            return stationTransform;
        }

        Debug.LogError($"[StationManager] ID가 '{id}'인 스테이션을 찾을 수 없습니다!");
        return null;
    }
}