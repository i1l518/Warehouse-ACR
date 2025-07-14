using Firebase.Firestore;
using System;
using UnityEngine;
using UnityEngine.AI;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class AmrController : MonoBehaviour
{
    // --- ������Ʈ ���� ---
    private NavMeshAgent agent;
    private NavMeshObstacle obstacle;

    // --- Inspector ���� ���� ---
    [Header("Firebase Settings")]
    public string amrId = "amr_01";

    [Header("AMR Settings")]
    public Transform homePosition;
    public float placeItemDuration = 2.0f; // ������ ���� �� �ɸ��� �ð�
    public float pickupItemDuration = 1.5f; // ������ �ݴ� �� �ɸ��� �ð�
    public float dropoutItemDuration = 1.0f; //������ ���� �� �ɸ��� �ð�
    public float rotationSpeed = 120f; // �ʴ� ȸ�� �ӵ� (NavMeshAgent�� Angular Speed�� ����)

    // --- ���� ���� ���� ---
    private DocumentReference amrDocRef;
    private ListenerRegistration listener;
    private bool isWorking = false;

    //================================================================
    // 1. Unity �����ֱ� �Լ���
    //================================================================
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        obstacle = GetComponent<NavMeshObstacle>();
        FirebaseManager.OnFirebaseInitialized += HandleFirebaseInitialized;
    }

    void Start()
    {
        if (homePosition == null)
        {
            Debug.LogError($"[{amrId}] Home Position�� �������� �ʾҽ��ϴ�! Inspector���� �Ҵ����ּ���.");
        }

        // ���� �ÿ��� Obstacle ���Ҹ� �ϵ��� ����
        SwitchToObstacle();
    }

    void OnDestroy()
    {
        FirebaseManager.OnFirebaseInitialized -= HandleFirebaseInitialized;
        listener?.Stop();
    }

    //================================================================
    // 2. Firebase ������ ����
    //================================================================
    private void HandleFirebaseInitialized()
    {
        SetupFirestoreListener();
    }

    private void SetupFirestoreListener()
    {
        amrDocRef = FirebaseManager.Instance.DB.Collection("amrs").Document(amrId);
        listener = amrDocRef.Listen(snapshot =>
        {
            if (isWorking) return; // �۾� ���� ���� ���ο� �۾��� ���� ����

            if (snapshot.ToDictionary().TryGetValue("assignedTask", out object taskIdObj) && taskIdObj != null)
            {
                string taskId = taskIdObj.ToString();
                Debug.Log($"[{amrId}] ���ο� �۾�({taskId})�� �Ҵ� �޾ҽ��ϴ�.");

                isWorking = true;
                StartCoroutine(ProcessTaskCoroutine(taskId));
            }
        });
    }

    //================================================================
    // 3. ���� �۾� ó�� �ڷ�ƾ (�������� ����)
    //================================================================
    private IEnumerator ProcessTaskCoroutine(string taskId)
    {
        // --- �۾� �غ� �ܰ� ---
        DocumentReference taskDocRef = FirebaseManager.Instance.DB.Collection("tasks").Document(taskId);
        Task<DocumentSnapshot> getTask = taskDocRef.GetSnapshotAsync();
        yield return new WaitUntil(() => getTask.IsCompleted);

        if (!getTask.Result.Exists)
        {
            Debug.LogError($"[{amrId}] Task Error: Task ID '{taskId}'�� ã�� �� �����ϴ�!");
            isWorking = false;
            yield break;
        }

        var taskData = getTask.Result.ToDictionary();
        string taskType = taskData.ContainsKey("type") ? taskData["type"].ToString() : "unknown";
        Debug.Log($"[{amrId}] �۾� Ÿ�� '{taskType}'�� �����մϴ�.");

        // --- Task Ÿ�Կ� ���� �۾� �帧 �б� ---
        if (taskType == "inbound")
        {
            yield return StartCoroutine(InboundTaskFlow(taskData));
        }
        else if (taskType == "outbound")
        {
            yield return StartCoroutine(OutboundTaskFlow(taskData)); // <<--- ��� �帧 ����
        }
        else
        {
            Debug.LogError($"[{amrId}] �� �� ���� Task Ÿ���Դϴ�: {taskType}");
        }

        // --- �۾� �Ϸ� �� ���� �ܰ� ---
        yield return CompleteTask(taskDocRef); // �۾� �Ϸ� ����

        Debug.Log($"[{amrId}] ���� �������� �����մϴ�.");

        Vector3 directionToHome = (homePosition.position - transform.position).normalized;
        if (directionToHome.sqrMagnitude > 0.001f)
        {
            float targetYAngle = Quaternion.LookRotation(directionToHome).eulerAngles.y;
            yield return StartCoroutine(RotateTowards(targetYAngle));
        }

        SwitchToAgent();
        Task returnHomeTask = UpdateStatus("returning_to_home");
        yield return new WaitUntil(() => returnHomeTask.IsCompleted);
        agent.SetDestination(homePosition.position);
        yield return new WaitUntil(() => HasArrived());

        Debug.Log($"[{amrId}] ���� ���� ����. ��� ���·� ��ȯ�մϴ�.");
        SwitchToObstacle();
        Task idleTask = UpdateStatus("idle");
        yield return new WaitUntil(() => idleTask.IsCompleted);

        isWorking = false; // ���� ���ο� �۾��� ���� �� ����
    }

    //================================================================
    // 4. ���� �۾� �帧 �ڷ�ƾ (Inbound / Outbound ��)
    //================================================================

    //object���� �޴� ����: firebase�� �����ϰ� �ҷ����̸鼭 ������ ������ �޶��� �� �ֱ� ����
    private IEnumerator InboundTaskFlow(Dictionary<string, object> taskData)
    {

        if (taskData.TryGetValue("sourceStationId", out object stationIdObj))
        {
            string stationId = stationIdObj.ToString();
            // StationManager���� ID�� �ش��ϴ� Transform�� ���
            Transform pickupTransform = StationManager.Instance.GetStationTransform(stationId);

            if (pickupTransform != null)
            {
                Vector3 directionToPickup = (pickupTransform.position - transform.position).normalized;
                // ���� ���Ͱ� ���� 0�̸� (�̹� �� ��ġ�� �ſ� �����ٸ�) ȸ������ ����
                if (directionToPickup.sqrMagnitude > 0.001f)
                {
                    float targetYAngle = Quaternion.LookRotation(directionToPickup).eulerAngles.y;
                    yield return StartCoroutine(RotateTowards(targetYAngle));
                }

                // 1. ������ �԰� �������� �̵�
                SwitchToAgent();
                Task moveToPickupTask = UpdateStatus("moving_to_pickup");
                yield return new WaitUntil(() => moveToPickupTask.IsCompleted);
                agent.SetDestination(pickupTransform.position);
                yield return new WaitUntil(() => HasArrived());

                //�ε巯�� ȸ��
                float pickupYRotation = GetRotationFromTask(taskData, "sourceStationRotation");
                yield return StartCoroutine(RotateTowards(pickupYRotation));

                // 2. ���� �ݱ� �ùķ��̼�
                SwitchToObstacle();
                Task pickupTask = UpdateStatus("picking_up_item");
                yield return new WaitUntil(() => pickupTask.IsCompleted);
                Debug.Log($"[{amrId}] ������ �ݽ��ϴ�... ({pickupItemDuration}�� ���)");
                yield return new WaitForSeconds(pickupItemDuration);

                // 3. ���� ������(��)�� �̵�
                if (taskData.TryGetValue("destination", out object destObj) && destObj is Dictionary<string, object> destMap)
                {
                    if (destMap.TryGetValue("position", out object posObj) && posObj is Dictionary<string, object> posMap)
                    {
                        float x = Convert.ToSingle(posMap["x"]);
                        float y = Convert.ToSingle(posMap["y"]);
                        float z = Convert.ToSingle(posMap["z"]);
                        Vector3 targetStopPosition = new Vector3(x, 0, z);

                        // ���� �������� ���� �̸� �ε巴�� ȸ��
                        Vector3 directionToTarget = (targetStopPosition - transform.position).normalized;
                        float targetYAngle = Quaternion.LookRotation(directionToTarget).eulerAngles.y;
                        yield return StartCoroutine(RotateTowards(targetYAngle));

                        SwitchToAgent();
                        Task moveToDestTask = UpdateStatus("moving_to_destination");
                        yield return new WaitUntil(() => moveToDestTask.IsCompleted);

                        agent.SetDestination(targetStopPosition);
                        yield return new WaitUntil(() => HasArrived());
                        Debug.Log("��ǥ ���� �����߽��ϴ�.");

                        // 1. ��ǥ ȸ���� �Ľ�(ȸ��)
                        float rackYRotation = GetRotationFromMap(destMap, "rotation"); ; // �⺻���� ���� ����
                        yield return StartCoroutine(RotateTowards(rackYRotation));

                        // 4. ���� ���� �ùķ��̼�
                        SwitchToObstacle();
                        Task placeTask = UpdateStatus("placing_item");
                        yield return new WaitUntil(() => placeTask.IsCompleted);
                        Debug.Log($"[{amrId}] ������ �����ϴ�... ({placeItemDuration}�� ���)");
                        yield return new WaitForSeconds(placeItemDuration);
                    }
                }
            }
        }
        else
        {
            Debug.LogError($"[{amrId}] �԰� �۾��� sourceStationId ������ �����ϴ�!");
        }
    }

    private IEnumerator OutboundTaskFlow(Dictionary<string, object> taskData)
    {
        // 1. ������ �ִ� ��(source)���� �̵�
        if (taskData.TryGetValue("source", out object sourceObj) && sourceObj is Dictionary<string, object> sourceMap)
        {
            if (sourceMap.TryGetValue("position", out object posObj) && posObj is Dictionary<string, object> posMap)
            {
                float x = Convert.ToSingle(posMap["x"]);
                float y = Convert.ToSingle(posMap["y"]);
                float z = Convert.ToSingle(posMap["z"]);
                Vector3 pickupPosition = new Vector3(x, 0, z);

                Vector3 directionToSource = (pickupPosition - transform.position).normalized;
                if (directionToSource.sqrMagnitude > 0.001f)
                {
                    float targetYAngle = Quaternion.LookRotation(directionToSource).eulerAngles.y;
                    yield return StartCoroutine(RotateTowards(targetYAngle));
                }

                SwitchToAgent();
                Task moveToSourceTask = UpdateStatus("moving_to_source");
                yield return new WaitUntil(() => moveToSourceTask.IsCompleted);

                agent.SetDestination(pickupPosition);
                yield return new WaitUntil(() => HasArrived());

                // �ε巯�� ȸ��
                float rackYRotation = GetRotationFromMap(sourceMap, "rotation");
                yield return StartCoroutine(RotateTowards(rackYRotation));

                // 2. ���� �ݱ� �ùķ��̼�
                SwitchToObstacle();
                Task pickupTask = UpdateStatus("picking_up_item");
                yield return new WaitUntil(() => pickupTask.IsCompleted);
                Debug.Log($"[{amrId}] ������ �ݽ��ϴ�... ({pickupItemDuration}�� ���)");
                yield return new WaitForSeconds(pickupItemDuration);

                // 3. ������ ��� ����(destinationStationId)���� �̵�
                if (taskData.TryGetValue("destinationStationId", out object stationIdObj))
                {
                    string stationId = stationIdObj.ToString();
                    Transform dropoffTransform = StationManager.Instance.GetStationTransform(stationId);

                    if (dropoffTransform != null)
                    {
                        // ���� �������� ���� �̸� �ε巴�� ȸ��
                        Vector3 directionToTarget = (dropoffTransform.position - transform.position).normalized;
                        float targetYAngle = Quaternion.LookRotation(directionToTarget).eulerAngles.y;
                        yield return StartCoroutine(RotateTowards(targetYAngle));

                        SwitchToAgent();
                        Task moveToDestTask = UpdateStatus("moving_to_destination");
                        yield return new WaitUntil(() => moveToDestTask.IsCompleted);

                        agent.SetDestination(dropoffTransform.position);
                        yield return new WaitUntil(() => HasArrived());

                        // �ε巴�� ȸ��(outbound dropoff)
                        float dropoffYRotation = GetRotationFromTask(taskData, "destinationStationRotation");
                        yield return StartCoroutine(RotateTowards(dropoffYRotation));

                        //4. ���� ���� �ùķ��̼�
                        SwitchToObstacle();
                        Task placeTask = UpdateStatus("placing_item");
                        yield return new WaitUntil(() => placeTask.IsCompleted);
                        Debug.Log($"[{amrId}] ������ �����ϴ�... ({placeItemDuration}�� ���)");
                        yield return new WaitForSeconds(placeItemDuration);
                    }
                }
                else
                {
                    Debug.LogError($"[{amrId}] ��� �۾��� destinationStationId ������ �����ϴ�!");
                }
            }
            else
            {
                Debug.LogError($"[{amrId}] ��� �۾��� source ������ �����ϴ�!");
            }
        }
    }


    //================================================================
    // 5. ����(Helper) �Լ���
    //================================================================
    private async Task CompleteTask(DocumentReference taskDocRef)
    {
        await amrDocRef.UpdateAsync("assignedTask", null);

        Dictionary<string, object> taskUpdate = new Dictionary<string, object>
        {
            { "status", "completed" },
            { "completedAt", Timestamp.GetCurrentTimestamp() }
        };
        await taskDocRef.UpdateAsync(taskUpdate);
        Debug.Log($"[{amrId}] Task '{taskDocRef.Id}' �Ϸ� ����.");
    }

    private Task UpdateStatus(string newStatus)
    {
        if (amrDocRef == null) return Task.CompletedTask;
        return amrDocRef.UpdateAsync("status", newStatus);
    }

    private void SwitchToAgent()
    {
        if (obstacle != null && obstacle.enabled)
        {
            obstacle.enabled = false;
        }
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
        }
    }

    private void SwitchToObstacle()
    {
        if (agent != null && agent.enabled)
        {
            if (agent.hasPath) agent.ResetPath();
            agent.enabled = false;
        }

        if (obstacle != null && !obstacle.enabled)
        {
            obstacle.enabled = true;
        }
    }

    private bool HasArrived()
    {
        if (!agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                return !agent.hasPath || agent.velocity.sqrMagnitude == 0f;
            }
        }
        return false;
    }

    /// <summary>
    /// Task ������(�ֻ��� ��ųʸ�)���� ȸ�� ������ �����ϰ� �Ľ��մϴ�.
    /// </summary>
    private float GetRotationFromTask(Dictionary<string, object> taskData, string key)
    {
        if (taskData.TryGetValue(key, out object rotObj) && rotObj is Dictionary<string, object> rotMap)
        {
            return GetRotationFromMap(rotMap, "y"); // ���� ������ ���� �ʿ��� ������
        }
        return transform.eulerAngles.y; // ������ ������ ���� ���� ����
    }

    /// <summary>
    /// ���� ��(destination, source ��)���� ȸ�� ������ �����ϰ� �Ľ��մϴ�.
    /// </summary>
    private float GetRotationFromMap(Dictionary<string, object> dataMap, string key)
    {
        if (dataMap.TryGetValue(key, out object rotObj) && rotObj is Dictionary<string, object> rotMap)
        {
            if (rotMap.TryGetValue("y", out object yObj))
            {
                return Convert.ToSingle(yObj);
            }
        }
        else if (dataMap.TryGetValue(key, out object yObj)) // rotation: { y: 180 } ���°� �ƴ� rotation: 180 ���µ� ����
        {
            return Convert.ToSingle(yObj);
        }
        return transform.eulerAngles.y; // ������ ������ ���� ���� ����
    }

    /// <summary>
    /// ������ Y�� ������ �ε巴�� ȸ���ϴ� �ڷ�ƾ
    /// </summary>
    /// <param name="targetYAngle">��ǥ Y�� ����</param>
    private IEnumerator RotateTowards(float targetYAngle)
    {
        // �̵��� ����� �ϹǷ� Agent�� ��Ȱ��ȭ�ϰų� isStopped = true�� ����
        // SwitchToObstacle()�� �� ������ �̹� �ϰ� ���� �� ����
        if (agent.enabled)
        {
            agent.isStopped = true;
        }

        Quaternion targetRotation = Quaternion.Euler(0, targetYAngle, 0);

        // ��ǥ ȸ������ ���� ������ ������ ����
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1.0f) // 1�� �̳� ��������
        {
            // �ε巯�� ȸ���� ���� Quaternion.RotateTowards ���
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,       // ���� ȸ����
                targetRotation,           // ��ǥ ȸ����
                rotationSpeed * Time.deltaTime // ȸ�� �ӵ�
            );
            yield return null; // ���� �����ӱ��� ���
        }

        // ���� ������ ���� �������� ��ǥ ȸ�������� ��Ȯ�� ����
        transform.rotation = targetRotation;
        Debug.Log($"��ǥ ����({targetYAngle}��)���� ȸ�� �Ϸ�.");

        if (agent.enabled)
        {
            agent.isStopped = false;
        }
    }
}