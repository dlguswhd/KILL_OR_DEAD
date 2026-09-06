namespace KillOrDead.Attachments
{
    /// <summary>
    /// 부착물이 어느 무기 종류에 붙는지 구분한다.
    ///
    /// 부착물 에셋 팩이 무기 종류별로 나뉘어 있고 교차 장착을 금지하기 때문에 필요하다.
    ///  - Rifle  : Low Poly AR Weapon Pack 3 의 부착물
    ///  - Pistol : Low Poly Pistol Weapon Pack 3 의 부착물 (이름에 _Pistol_ 이 들어간 것들)
    ///
    /// 라이플에 권총용 부착물을 달거나 그 반대로 다는 것은 양방향 모두 막는다.
    /// 실제 크기가 달라서 어울리지 않기 때문이다.
    /// </summary>
    public enum WeaponClass
    {
        Rifle,
        Pistol
    }
}
