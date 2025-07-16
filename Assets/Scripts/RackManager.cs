// RackManager.cs (새로운 C# 스크립트 파일)
using UnityEngine;

public class RackManager : MonoBehaviour
{
    void OnEnable()
    {
        // 이벤트 구독
        ACREvents.OnItemPickedUp += HandleItemPickedUp;
        ACREvents.OnItemPlaced += HandleItemPlaced;
    }

    void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        ACREvents.OnItemPickedUp -= HandleItemPickedUp;
        ACREvents.OnItemPlaced -= HandleItemPlaced;
    }

    // 아이템을 '집었을' 때 호출될 함수 (Outbound 시)
    private void HandleItemPickedUp(string amrId, string taskId, string rackId)
    {
        Debug.Log($"[RackManager] {amrId}가 랙 '{rackId}'에서 아이템을 집었습니다. 랙 상태를 '비어있음(1)'으로 업데이트합니다.");
        // rackId의 유효성 검사 (Gantries 컬렉션의 문서인지)
        if (rackId.StartsWith("Rack"))
        {
            // Firestore의 Gantries 컬렉션에서 해당 rackId 문서의 status를 1로 업데이트
            FirebaseManager.Instance.DB.Collection("Gantries").Document(rackId).UpdateAsync("status", 1);
        }
    }

    // 아이템을 '내려놓았을' 때 호출될 함수 (Inbound 시)
    private void HandleItemPlaced(string amrId, string taskId, string rackId)
    {
        Debug.Log($"[RackManager] {amrId}가 랙 '{rackId}'에 아이템을 놓았습니다. 랙 상태를 '점유(0)'으로 업데이트합니다.");
        if (rackId.StartsWith("Rack"))
        {
            // Firestore의 Gantries 컬렉션에서 해당 rackId 문서의 status를 0으로 업데이트
            FirebaseManager.Instance.DB.Collection("Gantries").Document(rackId).UpdateAsync("status", 0);
        }
    }
}