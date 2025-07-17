// 1. 필요한 패키지 가져오기
const { initializeApp, cert } = require('firebase-admin/app');
const { getFirestore } = require('firebase-admin/firestore');

// 2. 서비스 계정 키 파일 로드
const serviceAccount = require('./amr-database-c3699-firebase-adminsdk-fbsvc-ae043dca98.json');

// 3. Firebase Admin 앱 초기화
try {
    initializeApp({
        credential: cert(serviceAccount)
    });
} catch (error) {
    // 앱이 이미 초기화된 경우를 대비
}

// 4. Firestore 인스턴스 가져오기
const db = getFirestore();
// 컬렉션 이름을 'ACRs'로 변경
const collectionRef = db.collection('ACRs');

// 5. 문서 생성 함수 (비동기)
async function createAcrDocuments() {
    console.log('ACR 문서 생성을 시작합니다...');

    const totalDocuments = 3; // 생성할 ACR 개수
    const totalSlots = 5; // 각 ACR의 슬롯 개수

    for (let i = 1; i <= totalDocuments; i++) {
        const docId = `acr_${String(i).padStart(2, '0')}`;

        // 6. 생성할 데이터 정의 (새로운 구조)

        // 5개의 빈 슬롯 배열을 동적으로 생성
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
            // 새로운 cargo 구조 적용
            cargo: {
                slots: emptySlots,
                itemCount: 0
            }
        };

        // 7. 컬렉션에 ID를 지정하여 문서 추가/덮어쓰기
        try {
            await collectionRef.doc(docId).set(acrData);
            console.log(`'${docId}' 문서 생성 성공!`);
        } catch (error) {
            console.error(`'${docId}' 문서 생성 중 오류 발생:`, error);
        }
    }

    console.log(`${totalDocuments}개의 ACR 문서 생성이 완료되었습니다.`);
}

// 함수 실행
createAcrDocuments();