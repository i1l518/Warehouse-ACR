// CentralControlSystem.cs (새로운 C# 스크립트 파일)
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
        // FirebaseManager로부터 DB 인스턴스를 받아옵니다.
        FirebaseManager.OnFirebaseInitialized += () => {
            db = FirebaseManager.Instance.DB;
        };
    }

    // UI 버튼이나 다른 트리거에서 이 함수를 호출한다고 가정합니다.
    public void OnInboundRequest(string itemType)
    {
        Debug.Log($"'{itemType}' 타입 물품 입고 요청 접수. 최적 랙 탐색 시작...");
        StartCoroutine(CreateInboundTaskCoroutine(itemType));
    }

    private IEnumerator CreateInboundTaskCoroutine(string itemType)
    {
        // 1. itemType에 맞는 랙 범위 정의
        int startY, endY;//수평 좌표
        switch (itemType)
        {
            case "A": startY = 0; endY = 2; break;
            case "B": startY = 3; endY = 5; break;
            case "C": startY = 6; endY = 8; break;
            default:
                Debug.LogError($"알 수 없는 아이템 타입: {itemType}");
                yield break;
        }

        // 2. Firestore에서 조건에 맞는 랙들 가져오기
        Query gantriesQuery = db.Collection("Gantries").WhereEqualTo("status", 1); // status가 1 (비어있음)
        Task<QuerySnapshot> getGantriesTask = gantriesQuery.GetSnapshotAsync();
        yield return new WaitUntil(() => getGantriesTask.IsCompleted);

        if (getGantriesTask.IsFaulted)
        {
            Debug.LogError("Gantries 데이터 조회 실패!");
            yield break;
        }

        List<RackData> availableRacks = new List<RackData>();
        foreach (var doc in getGantriesTask.Result.Documents)
        {
            RackData rack = doc.ConvertTo<RackData>();

            // Rack(x,y) 형식의 ID를 파싱하여 y값 확인
            // 예: "Rack(2,5)" -> y=5
            string[] parts = rack.DocumentId.Replace("Rack(", "").Replace(")", "").Split(',');
            int rackY = int.Parse(parts[1]);

            // itemType에 맞는 범위의 랙만 리스트에 추가
            if (rackY >= startY && rackY <= endY)
            {
                availableRacks.Add(rack);
            }
        }

        if (availableRacks.Count == 0)
        {
            Debug.LogWarning($"'{itemType}' 타입을 넣을 수 있는 빈 랙이 없습니다!");
            yield break;
        }

        // 3. 최적의 랙 선정 (높이가 낮고, y값이 낮은 순)
        RackData bestRack = availableRacks
            .OrderBy(r => r.GetPositionVector3().y) // 높이(y좌표)가 낮은 순으로 정렬
            .ThenBy(r => int.Parse(r.DocumentId.Replace("Rack(", "").Replace(")", "").Split(',')[1])) // 높이가 같다면 y 인덱스가 낮은 순
            .First();

        Debug.Log($"최적 랙 선정: {bestRack.DocumentId}");

        // 4. 해당 랙의 status를 2(작업 중)로 업데이트
        DocumentReference bestRackRef = db.Collection("Gantries").Document(bestRack.DocumentId);
        Task updateRackStatusTask = bestRackRef.UpdateAsync("status", 2);
        yield return new WaitUntil(() => updateRackStatusTask.IsCompleted);

        // --- 랙 상태 업데이트 실패 시 복구 로직 ---
        if (updateRackStatusTask.IsFaulted)
        {
            Debug.LogError($"랙 '{bestRack.DocumentId}'의 상태를 '예약됨'으로 업데이트하는 데 실패했습니다. 작업을 중단합니다.");
            yield break; // 더 이상 진행하지 않고 코루틴 종료
        }

        // 5. Task 생성
        Dictionary<string, object> newTaskData = new Dictionary<string, object>
        {
            { "type", "inbound" },
            { "itemType", itemType },
            { "sourceStationId", "inbound_station_01" }, // 예시
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

        // --- Task 생성 실패 시 복구(롤백) 로직 ---
        if (createTask.IsFaulted)
        {
            Debug.LogError("Task 생성 실패! 예약했던 랙 상태를 원래대로 복구합니다.");

            // <<<--- 복구 로직 시작 ---
            Task rollbackTask = bestRackRef.UpdateAsync("status", 1); // status를 다시 1로 설정
            yield return new WaitUntil(() => rollbackTask.IsCompleted);

            if (rollbackTask.IsFaulted)
            {
                Debug.LogError($"심각한 오류: 랙 '{bestRack.DocumentId}'의 상태 복구(롤백)마저 실패했습니다! 수동 확인이 필요합니다.");
            }
            else
            {
                Debug.Log($"랙 '{bestRack.DocumentId}'의 상태를 '비어있음(1)'으로 성공적으로 복구했습니다.");
            }

            yield break; // 작업을 중단하고 코루틴 종료
        }

        DocumentReference newTaskRef = createTask.Result;
        Debug.Log($"Task 생성 완료: {newTaskRef.Id}. 이제 유휴 ACR을 탐색합니다.");

        // =================================================================
        // 6. 유휴 ACR 탐색 및 Task 할당
        // =================================================================

        // 6-1. 'idle' 상태인 모든 ACR을 Firestore에서 조회
        Query idleAmrQuery = db.Collection("amrs").WhereEqualTo("status", "idle");
        Task<QuerySnapshot> getIdleAmrsTask = idleAmrQuery.GetSnapshotAsync();
        yield return new WaitUntil(() => getIdleAmrsTask.IsCompleted);

        if (getIdleAmrsTask.IsFaulted)
        {
            Debug.LogError("유휴 ACR 조회 실패!");
            // TODO: 생성된 Task를 '실패' 상태로 만들거나, 랙을 다시 'empty'로 복구하는 로직 필요
            yield break;
        }

        QuerySnapshot idleAmrsSnapshot = getIdleAmrsTask.Result;
        if (idleAmrsSnapshot.Count == 0)
        {
            Debug.LogWarning("현재 가용한 유휴 ACR이 없습니다. Task가 대기 상태로 유지됩니다.");
            // 이 경우, Task의 status는 'pending'으로 남아있게 되며,
            // 나중에 ACR이 'idle'이 되었을 때 이 Task를 다시 할당해주는 별도의 로직이 필요함 (고급 기능)
            yield break;
        }

        // 6-2. 최적의 ACR 선정 (여기서는 가장 첫 번째 유휴 ACR을 선택)
        // TODO: 더 복잡한 로직 추가 가능 (예: 배터리 잔량이 가장 높은 ACR, 현재 위치에서 가장 가까운 ACR 등)
        DocumentSnapshot selectedAmrDoc = idleAmrsSnapshot.Documents.First();
        Debug.Log($"최적 ACR 선정: {selectedAmrDoc.Id}");

        // 6-3. 선택된 ACR에게 Task 할당
        Task assignTask = selectedAmrDoc.Reference.UpdateAsync("assignedTask", newTaskRef.Id);
        yield return new WaitUntil(() => assignTask.IsCompleted);

        if (assignTask.IsFaulted)
        {
            Debug.LogError($"ACR '{selectedAmrDoc.Id}'에게 Task 할당 실패!");
            // TODO: 복구 로직
            yield break;
        }

        Debug.Log($"성공적으로 ACR '{selectedAmrDoc.Id}'에게 Task '{newTaskRef.Id}'를 할당했습니다.");
    }
}
