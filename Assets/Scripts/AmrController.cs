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
    // --- 컴포넌트 변수 ---
    private NavMeshAgent agent;
    private NavMeshObstacle obstacle;

    // --- Inspector 설정 변수 ---
    [Header("Firebase Settings")]
    public string amrId = "amr_01";

    [Header("AMR Settings")]
    public Transform homePosition;
    public float placeItemDuration = 2.0f; // 물건을 놓는 데 걸리는 시간
    public float pickupItemDuration = 1.5f; // 물건을 줍는 데 걸리는 시간
    public float dropoutItemDuration = 1.0f; //물건을 놓는 데 걸리는 시간
    public float rotationSpeed = 120f; // 초당 회전 속도 (NavMeshAgent의 Angular Speed와 유사)

    // --- 내부 상태 변수 ---
    private DocumentReference amrDocRef;
    private ListenerRegistration listener;
    private bool isWorking = false;

    //================================================================
    // 1. Unity 생명주기 함수들
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
            Debug.LogError($"[{amrId}] Home Position이 설정되지 않았습니다! Inspector에서 할당해주세요.");
        }

        // 시작 시에는 Obstacle 역할만 하도록 설정
        SwitchToObstacle();
    }

    void OnDestroy()
    {
        FirebaseManager.OnFirebaseInitialized -= HandleFirebaseInitialized;
        listener?.Stop();
    }

    //================================================================
    // 2. Firebase 리스너 설정
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
            if (isWorking) return; // 작업 중일 때는 새로운 작업을 받지 않음

            if (snapshot.ToDictionary().TryGetValue("assignedTask", out object taskIdObj) && taskIdObj != null)
            {
                string taskId = taskIdObj.ToString();
                Debug.Log($"[{amrId}] 새로운 작업({taskId})을 할당 받았습니다.");

                isWorking = true;
                StartCoroutine(ProcessTaskCoroutine(taskId));
            }
        });
    }

    //================================================================
    // 3. 메인 작업 처리 코루틴 (교통정리 역할)
    //================================================================
    private IEnumerator ProcessTaskCoroutine(string taskId)
    {
        // --- 작업 준비 단계 ---
        DocumentReference taskDocRef = FirebaseManager.Instance.DB.Collection("tasks").Document(taskId);
        Task<DocumentSnapshot> getTask = taskDocRef.GetSnapshotAsync();
        yield return new WaitUntil(() => getTask.IsCompleted);

        if (!getTask.Result.Exists)
        {
            Debug.LogError($"[{amrId}] Task Error: Task ID '{taskId}'를 찾을 수 없습니다!");
            isWorking = false;
            yield break;
        }

        var taskData = getTask.Result.ToDictionary();
        string taskType = taskData.ContainsKey("type") ? taskData["type"].ToString() : "unknown";
        Debug.Log($"[{amrId}] 작업 타입 '{taskType}'을 시작합니다.");

        // --- Task 타입에 따른 작업 흐름 분기 ---
        if (taskType == "inbound")
        {
            yield return StartCoroutine(InboundTaskFlow(taskData));
        }
        else if (taskType == "outbound")
        {
            yield return StartCoroutine(OutboundTaskFlow(taskData)); // <<--- 출고 흐름 연결
        }
        else
        {
            Debug.LogError($"[{amrId}] 알 수 없는 Task 타입입니다: {taskType}");
        }

        // --- 작업 완료 및 복귀 단계 ---
        yield return CompleteTask(taskDocRef); // 작업 완료 보고

        Debug.Log($"[{amrId}] 시작 지점으로 복귀합니다.");

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

        Debug.Log($"[{amrId}] 시작 지점 도착. 대기 상태로 전환합니다.");
        SwitchToObstacle();
        Task idleTask = UpdateStatus("idle");
        yield return new WaitUntil(() => idleTask.IsCompleted);

        isWorking = false; // 이제 새로운 작업을 받을 수 있음
    }

    //================================================================
    // 4. 세부 작업 흐름 코루틴 (Inbound / Outbound 등)
    //================================================================

    //object으로 받는 이유: firebase와 연동하고 불러들이면서 데이터 형식이 달라질 수 있기 때문
    private IEnumerator InboundTaskFlow(Dictionary<string, object> taskData)
    {

        if (taskData.TryGetValue("sourceStationId", out object stationIdObj))
        {
            string stationId = stationIdObj.ToString();
            // StationManager에게 ID에 해당하는 Transform을 물어봄
            Transform pickupTransform = StationManager.Instance.GetStationTransform(stationId);

            if (pickupTransform != null)
            {
                Vector3 directionToPickup = (pickupTransform.position - transform.position).normalized;
                // 방향 벡터가 거의 0이면 (이미 그 위치에 매우 가깝다면) 회전하지 않음
                if (directionToPickup.sqrMagnitude > 0.001f)
                {
                    float targetYAngle = Quaternion.LookRotation(directionToPickup).eulerAngles.y;
                    yield return StartCoroutine(RotateTowards(targetYAngle));
                }

                // 1. 고정된 입고 구역으로 이동
                SwitchToAgent();
                Task moveToPickupTask = UpdateStatus("moving_to_pickup");
                yield return new WaitUntil(() => moveToPickupTask.IsCompleted);
                agent.SetDestination(pickupTransform.position);
                yield return new WaitUntil(() => HasArrived());

                //부드러운 회전
                float pickupYRotation = GetRotationFromTask(taskData, "sourceStationRotation");
                yield return StartCoroutine(RotateTowards(pickupYRotation));

                // 2. 물건 줍기 시뮬레이션
                SwitchToObstacle();
                Task pickupTask = UpdateStatus("picking_up_item");
                yield return new WaitUntil(() => pickupTask.IsCompleted);
                Debug.Log($"[{amrId}] 물건을 줍습니다... ({pickupItemDuration}초 대기)");
                yield return new WaitForSeconds(pickupItemDuration);

                // 3. 최종 목적지(랙)로 이동
                if (taskData.TryGetValue("destination", out object destObj) && destObj is Dictionary<string, object> destMap)
                {
                    if (destMap.TryGetValue("position", out object posObj) && posObj is Dictionary<string, object> posMap)
                    {
                        float x = Convert.ToSingle(posMap["x"]);
                        float y = Convert.ToSingle(posMap["y"]);
                        float z = Convert.ToSingle(posMap["z"]);
                        Vector3 targetStopPosition = new Vector3(x, 0, z);

                        // 다음 목적지를 향해 미리 부드럽게 회전
                        Vector3 directionToTarget = (targetStopPosition - transform.position).normalized;
                        float targetYAngle = Quaternion.LookRotation(directionToTarget).eulerAngles.y;
                        yield return StartCoroutine(RotateTowards(targetYAngle));

                        SwitchToAgent();
                        Task moveToDestTask = UpdateStatus("moving_to_destination");
                        yield return new WaitUntil(() => moveToDestTask.IsCompleted);

                        agent.SetDestination(targetStopPosition);
                        yield return new WaitUntil(() => HasArrived());
                        Debug.Log("목표 랙에 도착했습니다.");

                        // 1. 목표 회전값 파싱(회전)
                        float rackYRotation = GetRotationFromMap(destMap, "rotation"); ; // 기본값은 현재 방향
                        yield return StartCoroutine(RotateTowards(rackYRotation));

                        // 4. 물건 놓기 시뮬레이션
                        SwitchToObstacle();
                        Task placeTask = UpdateStatus("placing_item");
                        yield return new WaitUntil(() => placeTask.IsCompleted);
                        Debug.Log($"[{amrId}] 물건을 놓습니다... ({placeItemDuration}초 대기)");
                        yield return new WaitForSeconds(placeItemDuration);
                    }
                }
            }
        }
        else
        {
            Debug.LogError($"[{amrId}] 입고 작업에 sourceStationId 정보가 없습니다!");
        }
    }

    private IEnumerator OutboundTaskFlow(Dictionary<string, object> taskData)
    {
        // 1. 물건이 있는 랙(source)으로 이동
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

                // 부드러운 회전
                float rackYRotation = GetRotationFromMap(sourceMap, "rotation");
                yield return StartCoroutine(RotateTowards(rackYRotation));

                // 2. 물건 줍기 시뮬레이션
                SwitchToObstacle();
                Task pickupTask = UpdateStatus("picking_up_item");
                yield return new WaitUntil(() => pickupTask.IsCompleted);
                Debug.Log($"[{amrId}] 물건을 줍습니다... ({pickupItemDuration}초 대기)");
                yield return new WaitForSeconds(pickupItemDuration);

                // 3. 지정된 출고 구역(destinationStationId)으로 이동
                if (taskData.TryGetValue("destinationStationId", out object stationIdObj))
                {
                    string stationId = stationIdObj.ToString();
                    Transform dropoffTransform = StationManager.Instance.GetStationTransform(stationId);

                    if (dropoffTransform != null)
                    {
                        // 다음 목적지를 향해 미리 부드럽게 회전
                        Vector3 directionToTarget = (dropoffTransform.position - transform.position).normalized;
                        float targetYAngle = Quaternion.LookRotation(directionToTarget).eulerAngles.y;
                        yield return StartCoroutine(RotateTowards(targetYAngle));

                        SwitchToAgent();
                        Task moveToDestTask = UpdateStatus("moving_to_destination");
                        yield return new WaitUntil(() => moveToDestTask.IsCompleted);

                        agent.SetDestination(dropoffTransform.position);
                        yield return new WaitUntil(() => HasArrived());

                        // 부드럽게 회전(outbound dropoff)
                        float dropoffYRotation = GetRotationFromTask(taskData, "destinationStationRotation");
                        yield return StartCoroutine(RotateTowards(dropoffYRotation));

                        //4. 물건 놓기 시뮬레이션
                        SwitchToObstacle();
                        Task placeTask = UpdateStatus("placing_item");
                        yield return new WaitUntil(() => placeTask.IsCompleted);
                        Debug.Log($"[{amrId}] 물건을 놓습니다... ({placeItemDuration}초 대기)");
                        yield return new WaitForSeconds(placeItemDuration);
                    }
                }
                else
                {
                    Debug.LogError($"[{amrId}] 출고 작업에 destinationStationId 정보가 없습니다!");
                }
            }
            else
            {
                Debug.LogError($"[{amrId}] 출고 작업에 source 정보가 없습니다!");
            }
        }
    }


    //================================================================
    // 5. 헬퍼(Helper) 함수들
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
        Debug.Log($"[{amrId}] Task '{taskDocRef.Id}' 완료 보고.");
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
    /// Task 데이터(최상위 딕셔너리)에서 회전 정보를 안전하게 파싱합니다.
    /// </summary>
    private float GetRotationFromTask(Dictionary<string, object> taskData, string key)
    {
        if (taskData.TryGetValue(key, out object rotObj) && rotObj is Dictionary<string, object> rotMap)
        {
            return GetRotationFromMap(rotMap, "y"); // 실제 각도는 하위 맵에서 가져옴
        }
        return transform.eulerAngles.y; // 정보가 없으면 현재 방향 유지
    }

    /// <summary>
    /// 하위 맵(destination, source 등)에서 회전 정보를 안전하게 파싱합니다.
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
        else if (dataMap.TryGetValue(key, out object yObj)) // rotation: { y: 180 } 형태가 아닌 rotation: 180 형태도 지원
        {
            return Convert.ToSingle(yObj);
        }
        return transform.eulerAngles.y; // 정보가 없으면 현재 방향 유지
    }

    /// <summary>
    /// 지정된 Y축 각도로 부드럽게 회전하는 코루틴
    /// </summary>
    /// <param name="targetYAngle">목표 Y축 각도</param>
    private IEnumerator RotateTowards(float targetYAngle)
    {
        // 이동은 멈춰야 하므로 Agent를 비활성화하거나 isStopped = true로 설정
        // SwitchToObstacle()이 이 역할을 이미 하고 있을 수 있음
        if (agent.enabled)
        {
            agent.isStopped = true;
        }

        Quaternion targetRotation = Quaternion.Euler(0, targetYAngle, 0);

        // 목표 회전값에 거의 도달할 때까지 루프
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1.0f) // 1도 이내 오차까지
        {
            // 부드러운 회전을 위해 Quaternion.RotateTowards 사용
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,       // 현재 회전값
                targetRotation,           // 목표 회전값
                rotationSpeed * Time.deltaTime // 회전 속도
            );
            yield return null; // 다음 프레임까지 대기
        }

        // 오차 보정을 위해 마지막에 목표 회전값으로 정확히 설정
        transform.rotation = targetRotation;
        Debug.Log($"목표 방향({targetYAngle}도)으로 회전 완료.");

        if (agent.enabled)
        {
            agent.isStopped = false;
        }
    }
}