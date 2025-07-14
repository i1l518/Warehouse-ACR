// 1. 필요한 패키지 가져오기
const { initializeApp, cert } = require('firebase-admin/app');
const { getFirestore, Timestamp } = require('firebase-admin/firestore'); // Timestamp를 사용하기 위해 추가

// 2. 서비스 계정 키 파일 로드
const serviceAccount = require('./amr-database-c3699-firebase-adminsdk-fbsvc-ae043dca98.json');

// 3. Firebase Admin 앱 초기화
// 앱이 이미 초기화되지 않았을 경우에만 초기화하도록 방어 코드를 추가할 수 있습니다.
try {
  initializeApp({
    credential: cert(serviceAccount)
  });
} catch (error) {
  // 이미 초기화된 경우 에러가 발생할 수 있으므로 무시하거나 로그를 남길 수 있습니다.
  // console.log("Firebase Admin SDK가 이미 초기화되었습니다.");
}


// 4. Firestore 인스턴스 가져오기
const db = getFirestore();

// --- Task 문서 생성 함수 (새로 추가) ---
async function createTaskDocuments() {
  console.log('Task 문서 생성을 시작합니다...');
  const tasksCollectionRef = db.collection('tasks');

  // 생성할 테스트용 Task 데이터 배열
  const testTasks = [
{
    // === 입고 작업 1 ===
    type: "inbound",
    itemType: "A",
    sourceStationId: "inbound_station_01", // "어느 입고 스테이션에서 물건을 가져올 것인가"
    sourceStationRotation: { y: 180 },
    destination: {
      rackId: "rack_005",
      position: { x: 10, y: 0, z: 15 },
      rotation: { y: 90 }
    }
  },
  {
    // === 입고 작업 2 ===
    type: "inbound",
    itemType: "B",
    sourceStationId: "inbound_station_02", // 다른 입고 스테이션을 지정
    destination: {
      rackId: "rack_012",
      position: { x: -5, y: 0, z: 20 },
      rotation: { y: 90 }
    }
  },
  {
    // === 출고 작업 1 ===
    type: "outbound",
    itemType: "C",
    source: { // "어느 랙에서 물건을 꺼낼 것인가"
      rackId: "rack_028",
      position: { x: 25, y: 0, z: -10 }
    },
    destinationStationId: "outbound_station_01" // "어느 출고 스테이션으로 물건을 가져다 놓을 것인가"
  },
  {
    // === 출고 작업 2 ===
    type: "outbound",
    itemType: "A", // A 타입 물품을 다시 출고
    source: {
        rackId: "rack_005", // 위 입고 작업 1에서 넣었던 랙
        position: { x: 10, y: 0, z: 15 },
        rotation: { y: 90 }
    },
    destinationStationId: "outbound_station_02", // 다른 출고 스테이션을 지정
    destinationStationRotation: { y: 0 }
  }
  ];

  for (const task of testTasks) {
    // 6. 각 Task에 공통 필드 추가
    const taskData = {
      ...task, // testTasks 배열의 객체를 그대로 복사
      status: "pending", // 초기 상태는 'pending'(대기)
      assignedAmrId: null,
      createdAt: Timestamp.now(), // 현재 서버 시간으로 생성 시간 기록
      completedAt: null
    };

    // 7. 컬렉션에 문서 추가 (Task는 보통 자동 ID를 사용)
    try {
      // .add()를 사용하면 Firebase가 고유한 ID를 자동으로 생성해줍니다.
      const docRef = await tasksCollectionRef.add(taskData);
      console.log(`'${docRef.id}' Task 문서 생성 성공!`);
    } catch (error) {
      console.error(`Task 문서 생성 중 오류 발생:`, error);
    }
  }

  console.log(`${testTasks.length}개의 Task 문서 생성이 완료되었습니다.`);
}

// --- 실행할 함수 선택 ---
// 둘 중 하나를 선택하거나, 둘 다 순차적으로 실행할 수 있습니다.

async function main() {
  // await createAmrDocuments(); // AMR 문서를 생성하고 싶을 때 주석 해제
  await createTaskDocuments(); // Task 문서를 생성하고 싶을 때 주석 해제
}

main();