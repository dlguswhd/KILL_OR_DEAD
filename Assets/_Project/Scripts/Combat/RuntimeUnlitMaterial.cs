using UnityEngine;

namespace KillOrDead.Combat
{
    /// <summary>
    /// 런타임에 URP 언릿 머티리얼을 만드는 헬퍼.
    ///
    /// <b>중요</b>: 그냥 <c>new Material(Shader.Find("Universal Render Pipeline/Unlit"))</c>로 만들면
    /// 블렌드 상태가 Zero/Zero로 남아서 <b>에러 없이 화면에 아무것도 안 그려진다.</b>
    /// 반드시 여기를 거쳐서 만들 것.
    /// </summary>
    public static class RuntimeUnlitMaterial
    {
        public static Material CreateOpaque(Color color)
        {
            var material = Create(color);

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.Zero);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Geometry;

            return material;
        }

        /// <summary>레이저 빔처럼 "빛나는" 표현용. 뒤 배경에 색을 더하는 가산 합성.</summary>
        public static Material CreateAdditive(Color color)
        {
            var material = Create(color);

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;

            return material;
        }

        private static Material Create(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float) UnityEngine.Rendering.CullMode.Off);

            return material;
        }
    }
}
