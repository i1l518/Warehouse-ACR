// 1. 필요한 패키지 가져오기
const { initializeApp, cert } = require('firebase-admin/app');
const { getFirestore } = require('firebase-admin/firestore');

// 2. 서비스 계정 키 파일 로드
// 파일 경로는 이 스크립트 파일이 있는 위치를 기준으로 정확하게 작성해야 합니다.
const serviceAccount = require('./amr-database-c3699-firebase-adminsdk-fbsvc-ae043dca98.json');

// 3. Firebase Admin 앱 초기화
initializeApp({
  credential: cert(serviceAccount)
});

// 4. Firestore 인스턴스 가져오기
const db = getFirestore();
const collectionRef = db.collection('amrs');

// 5. 문서 생성 함수 (비동기)
async function createAmrDocuments() {
  console.log('AMR 문서 생성을 시작합니다...');

  const totalDocuments = 3; // 생성할 AMR 개수

  for (let i = 1; i <= totalDocuments; i++) {
    // 문서 ID를 'amr_01', 'amr_02' 형식으로 생성
    const docId = `amr_${String(i).padStart(2, '0')}`; // padStart로 amr_01, amr_02... 포맷 유지

    // 6. 생성할 데이터 정의 (올바른 구조로)
    const amrData = {
      assignedTask: null,
      batteryLevel: 100,
      currentLocation: { // Map 타입은 JavaScript 객체로 표현
        x: 0,
        y: 0,
        z: 0
      },
      status: "idle" // 문자열은 따옴표로 감싸기
    };

    // 7. 컬렉션에 ID를 지정하여 문서 추가/덮어쓰기
    try {
      // .doc(ID).set(DATA)를 사용하여 ID를 직접 지정합니다.
      await collectionRef.doc(docId).set(amrData);
      console.log(`'${docId}' 문서 생성 성공!`);
    } catch (error) {
      console.error(`'${docId}' 문서 생성 중 오류 발생:`, error);
    }
  }

  console.log(`${totalDocuments}개의 AMR 문서 생성이 완료되었습니다.`);
}

// 함수 실행
createAmrDocuments();