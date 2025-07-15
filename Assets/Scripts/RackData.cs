// RackData.cs (���ο� C# ��ũ��Ʈ ����)
using Firebase.Firestore;
using System.Collections.Generic;

// Firestore�� �����͸� ���� �����ϱ� ���� Attribute
[FirestoreData]
public class RackData
{
    // Firestore �ʵ� �̸��� ��Ȯ�� ��ġ�ؾ� ��
    [FirestoreProperty]
    public double angle { get; set; }

    [FirestoreProperty]
    public GeoPoint position { get; set; } // Firestore�� GeoPoint�� Unity�� GeoPoint�� ���ε�

    [FirestoreProperty]
    public int status { get; set; }

    // ���� ID�� ������ ����
    [FirestoreDocumentId]
    public string DocumentId { get; set; }
}