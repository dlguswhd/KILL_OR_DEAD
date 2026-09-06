namespace KillOrDead.Attachments
{
    /// <summary>부착물이 켜고 끌 수 있는 장치인지, 어떤 장치인지.</summary>
    public enum AttachmentFunction
    {
        /// <summary>그냥 달려 있기만 하는 부착물 (옵틱, 그립, 총구 장치 등).</summary>
        None = 0,

        /// <summary>레이저 사이트. 켜면 빛줄기가 나가고 닿는 자리에 점이 찍힌다.</summary>
        Laser = 1,

        /// <summary>전술 후레쉬. 켜면 앞을 비춘다.</summary>
        Flashlight = 2
    }
}
