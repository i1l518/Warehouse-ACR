// RackData.cs (���ο� C# ��ũ��Ʈ ����)
using UnityEngine;
using Firebase.Firestore;
using System.Collections.Generic;

// RackData.cs
[FirestoreData]
public class RackData
{
    [FirestoreProperty]
    public double angle { get; set; }

    [FirestoreProperty]
    public Dictionary<string, double> position { get; set; } // { "x": 25.44, "y": 1.62, "z": 17.04 }

    [FirestoreProperty]
    public int status { get; set; }

    [FirestoreDocumentId]
    public string DocumentId { get; set; }

    // ���Ǹ� ���� ������Ƽ
    public Vector3 GetPositionVector3()
    {
        return new Vector3((float)position["x"], (float)position["y"], (float)position["z"]);
    }
}