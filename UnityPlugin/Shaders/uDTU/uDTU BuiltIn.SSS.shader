Shader "Daz3D/uDTU BuiltIn.SSS"
{
    Properties
    {
        _Diffuse                        ("Diffuse Color", Color) = (1,1,1,1)
        _DiffuseMap                     ("Diffuse Map", 2D) = "white" {}
        _DiffuseMultiplier              ("Diffuse Multiplier", Float) = 1.0
        _SpecularColor                  ("Specular Color", Color) = (1,1,1,1)
        _SpecularColorMap               ("Specular Color Map", 2D) = "white" {}
        _SpecularStrength               ("Specular Strength", Range(0,1)) = 1.0
        _GlossyLayeredWeight            ("Glossy Layered Weight", Range(0,1)) = 1.0
        _GlossyRoughness                ("Glossy Roughness", Range(0,1)) = 0.5
        _GlossyRoughnessMap             ("Glossy Roughness Map", 2D) = "white" {}
        _NormalMap                      ("Normal Map", 2D) = "bump" {}
        _NormalStrength                 ("Normal Strength", Float) = 1.0
        _Emission                       ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap                    ("Emission Map", 2D) = "black" {}
        _EmissionStrength               ("Emission Strength", Float) = 1.0
        _Alpha                          ("Alpha", Range(0,1)) = 1.0
        _AlphaMap                       ("Alpha Map", 2D) = "white" {}
        _CutoutOpacityMap               ("Cutout Opacity Map", 2D) = "white" {}
        _AlphaClipThreshold             ("Alpha Clip Threshold", Range(0,1)) = 0.5
        _TranslucencyWeight             ("Translucency Weight", Range(0,1)) = 0.0
        _TranslucencyColor              ("Translucency Color", Color) = (1,1,1,1)
        _TranslucencyColorMap           ("Translucency Color Map", 2D) = "white" {}
        _DualLobeSpecularWeight         ("Dual Lobe Specular Weight", Range(0,1)) = 0.0
        _DualLobeSpecularReflectivity   ("Dual Lobe Specular Reflectivity", Range(0,1)) = 0.5
        _DualLobeSpecularRatio          ("Dual Lobe Specular Ratio", Range(0,1)) = 0.5
        _SpecularLobe1Roughness         ("Specular Lobe 1 Roughness", Range(0,1)) = 0.3
        _SpecularLobe2Roughness         ("Specular Lobe 2 Roughness", Range(0,1)) = 0.7
        _MakeupEnable                   ("Makeup Enable", Float) = 0.0
        _MakeupWeightValue              ("Makeup Weight", Range(0,1)) = 0.0
        _MakeupBaseColor                ("Makeup Base Color", Color) = (1,1,1,1)
        _MakeupRoughnessMultiplierValue ("Makeup Roughness Multiplier", Float) = 1.0
        _TopCoatWeight                  ("Top Coat Weight", Range(0,1)) = 0.0
        _TopCoatRoughness               ("Top Coat Roughness", Range(0,1)) = 0.1
        _TopCoatColor                   ("Top Coat Color", Color) = (1,1,1,1)
        _TopCoatIOR                     ("Top Coat IOR", Float) = 1.5
        _Tiling                         ("Tiling", Vector) = (1,1,0,0)
        _Offset                         ("Offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows
        #pragma target 3.0

        sampler2D _DiffuseMap;
        sampler2D _SpecularColorMap;
        sampler2D _GlossyRoughnessMap;
        sampler2D _NormalMap;
        sampler2D _EmissionMap;
        sampler2D _AlphaMap;
        sampler2D _CutoutOpacityMap;
        sampler2D _TranslucencyColorMap;

        fixed4  _Diffuse;
        float   _DiffuseMultiplier;
        fixed4  _SpecularColor;
        float   _SpecularStrength;
        float   _GlossyRoughness;
        float   _NormalStrength;
        fixed4  _Emission;
        float   _EmissionStrength;
        float   _Alpha;
        float   _TranslucencyWeight;
        fixed4  _TranslucencyColor;
        float   _SpecularLobe1Roughness;
        float   _SpecularLobe2Roughness;

        struct Input
        {
            float2 uv_DiffuseMap;
            float3 viewDir;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutputStandardSpecular o)
        {
            float2 uv = IN.uv_DiffuseMap;

            fixed4 diffuseSample  = tex2D(_DiffuseMap, uv);
            fixed4 specSample     = tex2D(_SpecularColorMap, uv);
            fixed4 roughSample    = tex2D(_GlossyRoughnessMap, uv);
            fixed4 normalSample   = tex2D(_NormalMap, uv);
            fixed4 emitSample     = tex2D(_EmissionMap, uv);
            fixed4 alphaSample    = tex2D(_AlphaMap, uv);
            fixed4 cutoutSample   = tex2D(_CutoutOpacityMap, uv);
            fixed4 translucencySample = tex2D(_TranslucencyColorMap, uv);

            o.Albedo    = diffuseSample.rgb * _Diffuse.rgb * _DiffuseMultiplier;
            o.Specular  = specSample.rgb * _SpecularColor.rgb * _SpecularStrength;

            // Average the two lobe roughness values for the combined smoothness
            float avgRoughness = (_SpecularLobe1Roughness + _SpecularLobe2Roughness) * 0.5 * _GlossyRoughness;
            o.Smoothness = 1.0 - saturate(avgRoughness);

            o.Normal    = UnpackScaleNormal(normalSample, _NormalStrength);

            // Approximate SSS via a rim-light emission term
            float3 worldN = WorldNormalVector(IN, o.Normal);
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), worldN));
            o.Emission  = emitSample.rgb * _Emission.rgb * _EmissionStrength
                        + translucencySample.rgb * _TranslucencyColor.rgb * _TranslucencyWeight * rim;

            o.Alpha     = min(_Alpha, min(alphaSample.r, cutoutSample.r));
        }
        ENDCG
    }

    FallBack "Standard"
}
