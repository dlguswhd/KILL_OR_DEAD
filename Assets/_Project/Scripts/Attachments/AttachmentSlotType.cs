namespace KillOrDead.Attachments
{
    public enum AttachmentSlotType
    {
        Muzzle,
        Optic,
        UnderBarrel,

        /// <summary>오른쪽 측면 레일.</summary>
        SideRailRight,

        /// <summary>
        /// 왼쪽 측면 레일. 레이저와 후레쉬를 양쪽에 하나씩 달려고 추가한 자리다.
        /// <see cref="SideRailRight"/>용 부착물은 여기에도 그대로 달 수 있다(같은 규격이다).
        /// </summary>
        SideRailLeft
    }
}
