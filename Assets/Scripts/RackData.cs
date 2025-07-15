// RackData.cs (새로운 C# 스크립트 파일)
using Firebase.Firestore;
using System.Collections.Generic;

// Firestore의 데이터를 직접 매핑하기 위한 Attribute
[FirestoreData]
public class RackData
{
    // Firestore 필드 이름과 정확히 일치해야 함
    [FirestoreProperty]
    public double angle { get; set; }

    [FirestoreProperty]
    public GeoPoint position { get; set; } // Firestore의 GeoPoint는 Unity의 GeoPoint와 매핑됨

    [FirestoreProperty]
    public int status { get; set; }

    // 문서 ID는 별도로 저장
    [FirestoreDocumentId]
    public string DocumentId { get; set; }
}