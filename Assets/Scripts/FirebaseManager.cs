using UnityEngine;
using Firebase;
using Firebase.Firestore;
using System.Threading.Tasks;
using System;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; } // �̱��� ����

    public FirebaseFirestore DB { get; private set; }

    // "�ʱ�ȭ�� �Ϸ�Ǿ����ϴ�" ��� ����� ������ �̺�Ʈ
    public static event Action OnFirebaseInitialized;


    void Awake()
    {
        // �̱��� �ν��Ͻ� ����
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ���� �ٲ� �ı����� ����
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
            // Firebase ���� ���������� �ʱ�ȭ��
            FirebaseApp app = FirebaseApp.DefaultInstance;
            DB = FirebaseFirestore.DefaultInstance;
            Debug.Log("Firebase �ʱ�ȭ ����!");

            // �ʱ�ȭ�� ���������� �������� ��ο��� �˸�!
            OnFirebaseInitialized?.Invoke();

            // TODO: �͸� ���� �Ǵ� �α��� ���� �߰�
        }
        else
        {
            Debug.LogError($"Firebase ���Ӽ� Ȯ�� ����: {dependencyStatus}");
        }
    }
}