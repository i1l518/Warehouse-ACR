using UnityEngine;

public class AmrGripper : MonoBehaviour
{
    // 각 슬라이딩 관절 GameObject들을 여기에 할당합니다.
    public Transform[] slidingJoints;

    // 각 관절이 열린 상태일 때의 로컬 위치 (인스펙터에서 설정)
    public Vector3[] openLocalPositions;
    // 각 관절이 닫힌 상태일 때의 로컬 위치 (인스펙터에서 설정)
    public Vector3[] closeLocalPositions;

    public float moveSpeed = 5f; // 움직임 속도 (조절 가능)

    // 현재 그리퍼가 열린 상태인지 닫힌 상태인지
    private bool isGripperOpen = true;

    void Start()
    {
        // 에러 방지: 배열 길이 일치 여부 확인
        if (slidingJoints.Length != openLocalPositions.Length ||
            slidingJoints.Length != closeLocalPositions.Length)
        {
            Debug.LogError("슬라이딩 관절 배열 길이가 일치하지 않습니다. 인스펙터에서 확인해주세요.");
            this.enabled = false; // 스크립트 비활성화
            return;
        }

        // 시작 시 그리퍼를 초기 상태로 설정 (예: 열린 상태)
        SetGripperState(true, true); // 두 번째 true는 즉시 설정 (부드러운 이동 없음)
    }

    void Update()
    {
        // 모든 슬라이딩 관절을 목표 위치로 부드럽게 이동시킵니다.
        for (int i = 0; i < slidingJoints.Length; i++)
        {
            Vector3 targetPosition = isGripperOpen ? openLocalPositions[i] : closeLocalPositions[i];

            // Vector3.Lerp를 사용하여 현재 위치에서 목표 위치까지 부드럽게 이동
            slidingJoints[i].localPosition = Vector3.Lerp(
                slidingJoints[i].localPosition,
                targetPosition,
                Time.deltaTime * moveSpeed
            );
        }

        // (선택 사항) 테스트를 위한 키 입력
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleGripper();
        }
    }

    /// <summary>
    /// 그리퍼의 상태를 전환합니다 (열림 <-> 닫힘).
    /// </summary>
    public void ToggleGripper()
    {
        isGripperOpen = !isGripperOpen;
        Debug.Log("그리퍼 상태 전환: " + (isGripperOpen ? "열림" : "닫힘"));
    }

    /// <summary>
    /// 그리퍼를 특정 상태로 설정합니다.
    /// </summary>
    /// <param name="open">true면 열림, false면 닫힘</param>
    /// <param name="instant">true면 즉시 목표 위치로 이동, false면 부드럽게 이동</param>
    public void SetGripperState(bool open, bool instant = false)
    {
        isGripperOpen = open;
        Debug.Log("그리퍼 상태 설정: " + (isGripperOpen ? "열림" : "닫힘"));

        if (instant)
        {
            for (int i = 0; i < slidingJoints.Length; i++)
            {
                slidingJoints[i].localPosition = isGripperOpen ? openLocalPositions[i] : closeLocalPositions[i];
            }
        }
    }
}