// ACREvents.cs (���ο� C# ��ũ��Ʈ ����)
using System;

public static class ACREvents
{
    // �������� ������ �� �߻��ϴ� �̺�Ʈ
    // �Ķ����: � ACR��, � Task ������, ��� ��ġ(�� ID ��)���� �������� �����°�
    public static event Action<string, string, string> OnItemPickedUp;
    public static void RaiseOnItemPickedUp(string amrId, string taskId, string locationId)
    {
        OnItemPickedUp?.Invoke(amrId, taskId, locationId);
    }

    // �������� ���������� �� �߻��ϴ� �̺�Ʈ
    // �Ķ����: � ACR��, � Task ������, ��� ��ġ�� �������� ���Ҵ°�
    public static event Action<string, string, string> OnItemPlaced;
    public static void RaiseOnItemPlaced(string amrId, string taskId, string locationId)
    {
        OnItemPlaced?.Invoke(amrId, taskId, locationId);
    }
}