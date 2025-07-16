using UnityEngine;

public class AmrGripper : MonoBehaviour
{
    // �� �����̵� ���� GameObject���� ���⿡ �Ҵ��մϴ�.
    public Transform[] slidingJoints;

    // �� ������ ���� ������ ���� ���� ��ġ (�ν����Ϳ��� ����)
    public Vector3[] openLocalPositions;
    // �� ������ ���� ������ ���� ���� ��ġ (�ν����Ϳ��� ����)
    public Vector3[] closeLocalPositions;

    public float moveSpeed = 5f; // ������ �ӵ� (���� ����)

    // ���� �׸��۰� ���� �������� ���� ��������
    private bool isGripperOpen = true;

    void Start()
    {
        // ���� ����: �迭 ���� ��ġ ���� Ȯ��
        if (slidingJoints.Length != openLocalPositions.Length ||
            slidingJoints.Length != closeLocalPositions.Length)
        {
            Debug.LogError("�����̵� ���� �迭 ���̰� ��ġ���� �ʽ��ϴ�. �ν����Ϳ��� Ȯ�����ּ���.");
            this.enabled = false; // ��ũ��Ʈ ��Ȱ��ȭ
            return;
        }

        // ���� �� �׸��۸� �ʱ� ���·� ���� (��: ���� ����)
        SetGripperState(true, true); // �� ��° true�� ��� ���� (�ε巯�� �̵� ����)
    }

    void Update()
    {
        // ��� �����̵� ������ ��ǥ ��ġ�� �ε巴�� �̵���ŵ�ϴ�.
        for (int i = 0; i < slidingJoints.Length; i++)
        {
            Vector3 targetPosition = isGripperOpen ? openLocalPositions[i] : closeLocalPositions[i];

            // Vector3.Lerp�� ����Ͽ� ���� ��ġ���� ��ǥ ��ġ���� �ε巴�� �̵�
            slidingJoints[i].localPosition = Vector3.Lerp(
                slidingJoints[i].localPosition,
                targetPosition,
                Time.deltaTime * moveSpeed
            );
        }

        // (���� ����) �׽�Ʈ�� ���� Ű �Է�
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleGripper();
        }
    }

    /// <summary>
    /// �׸����� ���¸� ��ȯ�մϴ� (���� <-> ����).
    /// </summary>
    public void ToggleGripper()
    {
        isGripperOpen = !isGripperOpen;
        Debug.Log("�׸��� ���� ��ȯ: " + (isGripperOpen ? "����" : "����"));
    }

    /// <summary>
    /// �׸��۸� Ư�� ���·� �����մϴ�.
    /// </summary>
    /// <param name="open">true�� ����, false�� ����</param>
    /// <param name="instant">true�� ��� ��ǥ ��ġ�� �̵�, false�� �ε巴�� �̵�</param>
    public void SetGripperState(bool open, bool instant = false)
    {
        isGripperOpen = open;
        Debug.Log("�׸��� ���� ����: " + (isGripperOpen ? "����" : "����"));

        if (instant)
        {
            for (int i = 0; i < slidingJoints.Length; i++)
            {
                slidingJoints[i].localPosition = isGripperOpen ? openLocalPositions[i] : closeLocalPositions[i];
            }
        }
    }
}