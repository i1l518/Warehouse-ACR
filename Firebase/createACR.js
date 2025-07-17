// 1. �ʿ��� ��Ű�� ��������
const { initializeApp, cert } = require('firebase-admin/app');
const { getFirestore } = require('firebase-admin/firestore');

// 2. ���� ���� Ű ���� �ε�
const serviceAccount = require('./amr-database-c3699-firebase-adminsdk-fbsvc-ae043dca98.json');

// 3. Firebase Admin �� �ʱ�ȭ
try {
    initializeApp({
        credential: cert(serviceAccount)
    });
} catch (error) {
    // ���� �̹� �ʱ�ȭ�� ��츦 ���
}

// 4. Firestore �ν��Ͻ� ��������
const db = getFirestore();
// �÷��� �̸��� 'ACRs'�� ����
const collectionRef = db.collection('ACRs');

// 5. ���� ���� �Լ� (�񵿱�)
async function createAcrDocuments() {
    console.log('ACR ���� ������ �����մϴ�...');

    const totalDocuments = 3; // ������ ACR ����
    const totalSlots = 5; // �� ACR�� ���� ����

    for (let i = 1; i <= totalDocuments; i++) {
        const docId = `acr_${String(i).padStart(2, '0')}`;

        // 6. ������ ������ ���� (���ο� ����)

        // 5���� �� ���� �迭�� �������� ����
        const emptySlots = [];
        for (let s = 1; s <= totalSlots; s++) {
            emptySlots.push({
                slotId: s,
                status: "empty",
                item: null
            });
        }

        const acrData = {
            assignedTask: null,
            batteryLevel: 100,
            currentLocation: {
                x: 0,
                y: 0,
                z: 0
            },
            status: "idle",
            // ���ο� cargo ���� ����
            cargo: {
                slots: emptySlots,
                itemCount: 0
            }
        };

        // 7. �÷��ǿ� ID�� �����Ͽ� ���� �߰�/�����
        try {
            await collectionRef.doc(docId).set(acrData);
            console.log(`'${docId}' ���� ���� ����!`);
        } catch (error) {
            console.error(`'${docId}' ���� ���� �� ���� �߻�:`, error);
        }
    }

    console.log(`${totalDocuments}���� ACR ���� ������ �Ϸ�Ǿ����ϴ�.`);
}

// �Լ� ����
createAcrDocuments();