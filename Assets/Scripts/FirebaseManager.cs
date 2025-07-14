using UnityEngine;
using Firebase;
using Firebase.Firestore;
using System.Threading.Tasks;
using System;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; } // 싱글턴 패턴

    public FirebaseFirestore DB { get; private set; }

    // "초기화가 완료되었습니다" 라는 방송을 내보낼 이벤트
    public static event Action OnFirebaseInitialized;


    void Awake()
    {
        // 싱글턴 인스턴스 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않음
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    async void Start()
    {
        await InitializeFirebase();
    }

    private async Task InitializeFirebase()
    {
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus == DependencyStatus.Available)
        {
            // Firebase 앱이 성공적으로 초기화됨
            FirebaseApp app = FirebaseApp.DefaultInstance;
            DB = FirebaseFirestore.DefaultInstance;
            Debug.Log("Firebase 초기화 성공!");

            // 초기화가 성공적으로 끝났음을 모두에게 알림!
            OnFirebaseInitialized?.Invoke();

            // TODO: 익명 인증 또는 로그인 로직 추가
        }
        else
        {
            Debug.LogError($"Firebase 종속성 확인 실패: {dependencyStatus}");
        }
    }
}