// StationManager.cs
using UnityEngine;
using System.Collections.Generic;

// �����̼� ������ ���� ������ Ŭ����
[System.Serializable]
public class Station
{
    public string id;
    public Transform transform;
}

public class StationManager : MonoBehaviour
{
    public static StationManager Instance { get; private set; }

    // Inspector���� ��� �����̼��� ���
    public List<Station> stations = new List<Station>();

    // ���� ��ȸ�� ���� ��ųʸ�
    private Dictionary<string, Transform> stationDictionary = new Dictionary<string, Transform>();

    void Awake()
    {
        // �̱��� ����
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // ����Ʈ�� �ִ� �����̼� �������� ��ųʸ��� ��ȯ�Ͽ� ���� �˻��� �غ�
        foreach (var station in stations)
        {
            if (!stationDictionary.ContainsKey(station.id))
            {
                stationDictionary.Add(station.id, station.transform);
            }
        }
    }

    /// <summary>
    /// �����̼� ID�� �ش� Transform�� ã�� ��ȯ�մϴ�.
    /// </summary>
    /// <param name="id">ã�� �����̼��� ID</param>
    /// <returns>ã�� Transform. ������ null.</returns>
    public Transform GetStationTransform(string id)
    {
        if (stationDictionary.TryGetValue(id, out Transform stationTransform))
        {
            return stationTransform;
        }

        Debug.LogError($"[StationManager] ID�� '{id}'�� �����̼��� ã�� �� �����ϴ�!");
        return null;
    }
}