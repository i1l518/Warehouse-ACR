// CentralControlSystem.cs (���ο� C# ��ũ��Ʈ ����)
using Firebase.Firestore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CentralControlSystem : MonoBehaviour
{
    private FirebaseFirestore db;

    void Start()
    {
        // FirebaseManager�κ��� DB �ν��Ͻ��� �޾ƿɴϴ�.
        FirebaseManager.OnFirebaseInitialized += () => {
            db = FirebaseManager.Instance.DB;
        };
    }

    // UI ��ư�̳� �ٸ� Ʈ���ſ��� �� �Լ��� ȣ���Ѵٰ� �����մϴ�.
    public void OnInboundRequest(string itemType)
    {
        Debug.Log($"'{itemType}' Ÿ�� ��ǰ �԰� ��û ����. ���� �� Ž�� ����...");
        StartCoroutine(CreateInboundTaskCoroutine(itemType));
    }

    private IEnumerator CreateInboundTaskCoroutine(string itemType)
    {
        // 1. itemType�� �´� �� ���� ����
        int startY, endY;//���� ��ǥ
        switch (itemType)
        {
            case "A": startY = 0; endY = 2; break;
            case "B": startY = 3; endY = 5; break;
            case "C": startY = 6; endY = 8; break;
            default:
                Debug.LogError($"�� �� ���� ������ Ÿ��: {itemType}");
                yield break;
        }

        // 2. Firestore���� ���ǿ� �´� ���� ��������
        Query gantriesQuery = db.Collection("Gantries").WhereEqualTo("status", 1); // status�� 1 (�������)
        Task<QuerySnapshot> getGantriesTask = gantriesQuery.GetSnapshotAsync();
        yield return new WaitUntil(() => getGantriesTask.IsCompleted);

        if (getGantriesTask.IsFaulted)
        {
            Debug.LogError("Gantries ������ ��ȸ ����!");
            yield break;
        }

        List<RackData> availableRacks = new List<RackData>();
        foreach (var doc in getGantriesTask.Result.Documents)
        {
            RackData rack = doc.ConvertTo<RackData>();

            // Rack(x,y) ������ ID�� �Ľ��Ͽ� y�� Ȯ��
            // ��: "Rack(2,5)" -> y=5
            string[] parts = rack.DocumentId.Replace("Rack(", "").Replace(")", "").Split(',');
            int rackY = int.Parse(parts[1]);

            // itemType�� �´� ������ ���� ����Ʈ�� �߰�
            if (rackY >= startY && rackY <= endY)
            {
                availableRacks.Add(rack);
            }
        }

        if (availableRacks.Count == 0)
        {
            Debug.LogWarning($"'{itemType}' Ÿ���� ���� �� �ִ� �� ���� �����ϴ�!");
            yield break;
        }

        // 3. ������ �� ���� (���̰� ����, y���� ���� ��)
        RackData bestRack = availableRacks
            .OrderBy(r => r.GetPositionVector3().y) // ����(y��ǥ)�� ���� ������ ����
            .ThenBy(r => int.Parse(r.DocumentId.Replace("Rack(", "").Replace(")", "").Split(',')[1])) // ���̰� ���ٸ� y �ε����� ���� ��
            .First();

        Debug.Log($"���� �� ����: {bestRack.DocumentId}");

        // 4. �ش� ���� status�� 2(�۾� ��)�� ������Ʈ
        DocumentReference bestRackRef = db.Collection("Gantries").Document(bestRack.DocumentId);
        Task updateRackStatusTask = bestRackRef.UpdateAsync("status", 2);
        yield return new WaitUntil(() => updateRackStatusTask.IsCompleted);

        // --- �� ���� ������Ʈ ���� �� ���� ���� ---
        if (updateRackStatusTask.IsFaulted)
        {
            Debug.LogError($"�� '{bestRack.DocumentId}'�� ���¸� '�����'���� ������Ʈ�ϴ� �� �����߽��ϴ�. �۾��� �ߴ��մϴ�.");
            yield break; // �� �̻� �������� �ʰ� �ڷ�ƾ ����
        }

        // 5. Task ����
        Dictionary<string, object> newTaskData = new Dictionary<string, object>
        {
            { "type", "inbound" },
            { "itemType", itemType },
            { "sourceStationId", "inbound_station_01" }, // ����
            { "sourceStationRotation", new Dictionary<string, object> { { "y", 180 } } },
            { "destination", new Dictionary<string, object> {
                { "rackId", bestRack.DocumentId },
                { "position", bestRack.position },
                { "rotation", new Dictionary<string, object> { { "y", bestRack.angle } } }
            }},
            { "status", "pending" },
            { "assignedAmrId", null },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "completedAt", null }
        };

        Task<DocumentReference> createTask = db.Collection("tasks").AddAsync(newTaskData);
        yield return new WaitUntil(() => createTask.IsCompleted);

        // --- Task ���� ���� �� ����(�ѹ�) ���� ---
        if (createTask.IsFaulted)
        {
            Debug.LogError("Task ���� ����! �����ߴ� �� ���¸� ������� �����մϴ�.");

            // <<<--- ���� ���� ���� ---
            Task rollbackTask = bestRackRef.UpdateAsync("status", 1); // status�� �ٽ� 1�� ����
            yield return new WaitUntil(() => rollbackTask.IsCompleted);

            if (rollbackTask.IsFaulted)
            {
                Debug.LogError($"�ɰ��� ����: �� '{bestRack.DocumentId}'�� ���� ����(�ѹ�)���� �����߽��ϴ�! ���� Ȯ���� �ʿ��մϴ�.");
            }
            else
            {
                Debug.Log($"�� '{bestRack.DocumentId}'�� ���¸� '�������(1)'���� ���������� �����߽��ϴ�.");
            }

            yield break; // �۾��� �ߴ��ϰ� �ڷ�ƾ ����
        }

        DocumentReference newTaskRef = createTask.Result;
        Debug.Log($"Task ���� �Ϸ�: {newTaskRef.Id}. ���� ���� ACR�� Ž���մϴ�.");

        // TODO: ���� ACR�� ã�� �� Task ID�� assignedTask�� ������Ʈ�ϴ� ���� �߰�
    }
}