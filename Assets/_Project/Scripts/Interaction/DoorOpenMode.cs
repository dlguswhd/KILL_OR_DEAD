namespace KillOrDead.Interaction
{
    /// <summary>문을 어떤 방식으로 열지. 플레이어가 마우스 휠로 고른다.</summary>
    public enum DoorOpenMode
    {
        /// <summary>손으로 조용히 연다. 다시 누르면 닫힌다.</summary>
        Normal = 0,

        /// <summary>왼발로 차서 벌컥 연다. 시끄럽지만 빠르다.</summary>
        Kick = 1,
    }
}
