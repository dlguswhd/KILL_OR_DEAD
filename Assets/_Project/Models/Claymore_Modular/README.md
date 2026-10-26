# 레이저 감지형 크레모어 - 분리형 모델

본체 / 센서 / 선을 따로 켜고 끌 수 있게 나눈 모델입니다.
부품 5개가 모두 같은 원점(크레모어 바닥 중앙)이라 위치를 맞출 필요 없이 켜고 끄기만 하면 됩니다.

## 들어있는 것

| 파일 | 내용 |
|---|---|
| `Claymore_Modular.fbx` | 부품 5개가 들어있는 FBX (텍스처 내장) |
| `Textures/` | FBX에 내장된 것과 같은 텍스처 3장 (머티리얼이 하얗게 나올 때 사용) |
| `Scripts/ClaymoreParts.cs` | 상태 전환 스크립트 |
| `Blender/Claymore_Modular.blend` | 블렌더 수정용 파일 (텍스처 포함) |
| `Preview/` | 미리보기 렌더 |

## FBX 안의 부품

| 오브젝트 | 설명 |
|---|---|
| `Body` | 크레모어 본체 (케이스, 다리, 뇌관 구멍) |
| `Cable_Intact` | 센서와 뇌관을 잇는 선 - 기본 |
| `Cable_Cut` | 선이 끊어진 해제 상태 (양쪽 끝이 처지고 구리선이 보임) |
| `Sensor_Intact` | 카메라 센서 + 거치대 - 기본 |
| `Sensor_Destroyed` | 센서 위쪽이 뜯겨 날아가고 아랫부분만 남은 상태 (그을린 내부, 기판, 끊어진 전선) |

선 끝의 플러그와 소켓은 센서 쪽에 붙어 있습니다. 그래서 선이 끊긴 상태와 센서가 부서진 상태를 같이 켜도 어긋나지 않습니다.

## 상태 조합

| 상태 | 켜는 것 |
|---|---|
| 기본 | Body + Cable_Intact + Sensor_Intact |
| 해체 | Body + **Cable_Cut** + Sensor_Intact (선만 바뀜) |
| 센서 피격 | Body + Cable_Intact + **Sensor_Destroyed** (센서만 바뀜) |
| 둘 다 | Body + Cable_Cut + Sensor_Destroyed |

## 유니티에 넣기

1. `Claymore_Modular.fbx` 를 Assets 폴더로 드래그
2. 씬에 끌어다 놓으면 자식 오브젝트 5개가 생김
3. 루트 오브젝트에 `ClaymoreParts` 스크립트 추가 -> 게임 시작 시 자동으로 기본 상태가 됨
4. 해체할 때 `Disarm()`, 센서에 총을 맞았을 때 `DestroySensor()` 호출
   - 인스펙터에서 스크립트 이름 오른쪽 위 점 3개 메뉴 -> `해체 테스트`, `센서 파괴 테스트`, `원래대로` 로 바로 확인 가능
5. 센서 피격 판정: `Sensor_Intact` 에 Box Collider 를 추가해서 카메라 부분만 감싸기
   -> 히트스캔 레이가 이 콜라이더에 맞으면 `GetComponentInParent<ClaymoreParts>()` 로 찾아서 `DestroySensor()` 호출

## 머티리얼

| 머티리얼 | 쓰는 곳 |
|---|---|
| `Claymore_Main` | 원본 텍스처 (본체, 선, 기본 센서) |
| `Claymore_Damage` | 끊긴 단면, 그을린 내부, 기판, 전선 (1024 텍스처) |
| `Claymore_SensorDestroyed` | 그을음을 입힌 원본 텍스처 (부서진 센서 전용) |

머티리얼이 하얗게 보이면: FBX 선택 -> Inspector 의 Materials 탭 -> `Extract Textures`.
그래도 안 되면 `Textures` 폴더의 이미지를 각 머티리얼의 Base Map 에 넣으면 됩니다.

## 크기와 방향

- 받은 원본 모델과 크기, 방향, 원점이 똑같습니다 (높이 약 1). 원본 자리에 그대로 바꿔 넣을 수 있습니다.
- 실제 M18A1 크기(케이스 가로 21.6cm)로 쓰려면 FBX Import Settings -> Model -> Scale Factor 를 `0.2466` 으로.

## 참고

- 원본 센서 위의 회색 클립은 한쪽 면만 있어서, 유니티에서는 앞쪽에서 보면 사라졌습니다. `Sensor_Intact` 에서는 양면으로 고쳐 두었습니다.
