// RackData.cs
using Firebase.Firestore;
using System.Collections.Generic;
using UnityEngine; // Vector3�� ����ϱ� ���� �߰�!

[FirestoreData]
public class RackData
{
    [FirestoreProperty]
    public double angle { get; set; }

    // Firestore�� map Ÿ���� C#�� Dictionary�� �޽��ϴ�.
    [FirestoreProperty]
    public Dictionary<string, double> position { get; set; }

    [FirestoreProperty]
    public int status { get; set; }

    [FirestoreDocumentId]
    public string DocumentId { get; set; }

    /// <summary>
    /// Dictionary Ÿ���� position�� Unity�� Vector3 Ÿ������ ��ȯ�մϴ�.
    /// </summary>
    public Vector3 GetPositionVector3()
    {
        if (position != null && position.ContainsKey("x") && position.ContainsKey("y") && position.ContainsKey("z"))
        {
            return new Vector3(
                (float)position["x"],
                (float)position["y"],
                (float)position["z"]
            );
        }
        // �����Ͱ� �߸��Ǿ��� ��� �⺻�� ��ȯ
        return Vector3.zero;
    }
}