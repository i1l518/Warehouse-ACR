// ACREvents.cs (새로운 C# 스크립트 파일)
using System;

public static class ACREvents
{
    // 아이템을 집었을 때 발생하는 이벤트
    // 파라미터: 어떤 ACR이, 어떤 Task 정보로, 어느 위치(랙 ID 등)에서 아이템을 집었는가
    public static event Action<string, string, string> OnItemPickedUp;
    public static void RaiseOnItemPickedUp(string amrId, string taskId, string locationId)
    {
        OnItemPickedUp?.Invoke(amrId, taskId, locationId);
    }

    // 아이템을 내려놓았을 때 발생하는 이벤트
    // 파라미터: 어떤 ACR이, 어떤 Task 정보로, 어느 위치에 아이템을 놓았는가
    public static event Action<string, string, string> OnItemPlaced;
    public static void RaiseOnItemPlaced(string amrId, string taskId, string locationId)
    {
        OnItemPlaced?.Invoke(amrId, taskId, locationId);
    }
}