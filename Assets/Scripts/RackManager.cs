// RackManager.cs (���ο� C# ��ũ��Ʈ ����)
using UnityEngine;

public class RackManager : MonoBehaviour
{
    void OnEnable()
    {
        // �̺�Ʈ ����
        ACREvents.OnItemPickedUp += HandleItemPickedUp;
        ACREvents.OnItemPlaced += HandleItemPlaced;
    }

    void OnDisable()
    {
        // �̺�Ʈ ���� ���� (�޸� ���� ����)
        ACREvents.OnItemPickedUp -= HandleItemPickedUp;
        ACREvents.OnItemPlaced -= HandleItemPlaced;
    }

    // �������� '������' �� ȣ��� �Լ� (Outbound ��)
    private void HandleItemPickedUp(string amrId, string taskId, string rackId)
    {
        Debug.Log($"[RackManager] {amrId}�� �� '{rackId}'���� �������� �������ϴ�. �� ���¸� '�������(1)'���� ������Ʈ�մϴ�.");
        // rackId�� ��ȿ�� �˻� (Gantries �÷����� ��������)
        if (rackId.StartsWith("Rack"))
        {
            // Firestore�� Gantries �÷��ǿ��� �ش� rackId ������ status�� 1�� ������Ʈ
            FirebaseManager.Instance.DB.Collection("Gantries").Document(rackId).UpdateAsync("status", 1);
        }
    }

    // �������� '����������' �� ȣ��� �Լ� (Inbound ��)
    private void HandleItemPlaced(string amrId, string taskId, string rackId)
    {
        Debug.Log($"[RackManager] {amrId}�� �� '{rackId}'�� �������� ���ҽ��ϴ�. �� ���¸� '����(0)'���� ������Ʈ�մϴ�.");
        if (rackId.StartsWith("Rack"))
        {
            // Firestore�� Gantries �÷��ǿ��� �ش� rackId ������ status�� 0���� ������Ʈ
            FirebaseManager.Instance.DB.Collection("Gantries").Document(rackId).UpdateAsync("status", 0);
        }
    }
}