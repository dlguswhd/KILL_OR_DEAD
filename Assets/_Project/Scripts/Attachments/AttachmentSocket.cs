using UnityEngine;

namespace KillOrDead.Attachments
{
    [AddComponentMenu("KILL OR DEAD/Attachments/Attachment Socket")]
    public class AttachmentSocket : MonoBehaviour
    {
        public AttachmentSlotType slotType;

        [Tooltip("부착물을 좌우로 뒤집어서 단다.\n\n" +
                 "부착물 모델들은 몸통이 원점에서 +X(오른쪽)로 뻗어 있게 만들어져 있다. " +
                 "그래서 왼쪽 레일에 그대로 달면 몸통이 총 안쪽으로 파고든다. " +
                 "왼쪽 소켓에는 이 값을 켜서 바깥을 향하게 뒤집는다.")]
        public bool mirrorAttachment;

        /// <summary>
        /// 이 소켓에 다는 부착물에 얹을 추가 회전.
        /// 좌우 뒤집기는 총구 방향(로컬 +Z)을 축으로 180도 돌려서 만든다.
        /// 이 축으로 돌려야 <b>총구 방향은 그대로 두고</b> 좌우만 바뀐다.
        /// </summary>
        public Quaternion MountRotation => mirrorAttachment
            ? Quaternion.Euler(0f, 0f, 180f)
            : Quaternion.identity;
    }
}
