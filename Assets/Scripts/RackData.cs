// RackData.cs
using Firebase.Firestore;
using System.Collections.Generic;
using UnityEngine; // Vector3를 사용하기 위해 추가!

[FirestoreData]
public class RackData
{
    [FirestoreProperty]
    public double angle { get; set; }

    // Firestore의 map 타입을 C#의 Dictionary로 받습니다.
    [FirestoreProperty]
    public Dictionary<string, double> position { get; set; }

    [FirestoreProperty]
    public int status { get; set; }

    [FirestoreDocumentId]
    public string DocumentId { get; set; }

    /// <summary>
    /// Dictionary 타입의 position을 Unity의 Vector3 타입으로 변환합니다.
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
        // 데이터가 잘못되었을 경우 기본값 반환
        return Vector3.zero;
    }
}