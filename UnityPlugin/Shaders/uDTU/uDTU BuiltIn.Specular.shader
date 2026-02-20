Shader "Daz3D/uDTU BuiltIn.Specular"
{
    Properties
    {
        _Diffuse                    ("Diffuse Color", Color) = (1,1,1,1)
        _DiffuseMap                 ("Diffuse Map", 2D) = "white" {}
        _DiffuseMultiplier          ("Diffuse Multiplier", Float) = 1.0
        _SpecularColor              ("Specular Color", Color) = (1,1,1,1)
        _SpecularColorMap           ("Specular Color Map", 2D) = "white" {}
        _SpecularStrength           ("Specular Strength", Range(0,1)) = 1.0
        _GlossyLayeredWeight        ("Glossy Layered Weight", Range(0,1)) = 1.0
        _GlossyRoughness            ("Glossy Roughness", Range(0,1)) = 0.5
        _GlossyRoughnessMap         ("Glossy Roughness Map", 2D) = "white" {}
        _GlossyColor                ("Glossy Color", Color) = (1,1,1,1)
        _GlossyColorMap             ("Glossy Color Map", 2D) = "white" {}
        _NormalMap                  ("Normal Map", 2D) = "bump" {}
        _NormalStrength             ("Normal Strength", Float) = 1.0
        _Height                     ("Height", Float) = 0.0
        _HeightMap                  ("Height Map", 2D) = "white" {}
        _HeightOffset               ("Height Offset", Float) = 0.0
        _Emission                   ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap                ("Emission Map", 2D) = "black" {}
        _EmissionStrength           ("Emission Strength", Float) = 1.0
        _Alpha                      ("Alpha", Range(0,1)) = 1.0
        _AlphaMap                   ("Alpha Map", 2D) = "white" {}
        _CutoutOpacityMap           ("Cutout Opacity Map", 2D) = "white" {}
        _AlphaClipThreshold         ("Alpha Clip Threshold", Range(0,1)) = 0.5
        _TranslucencyWeight         ("Translucency Weight", Range(0,1)) = 0.0
        _TranslucencyColor          ("Translucency Color", Color) = (1,1,1,1)
        _TranslucencyColorMap       ("Translucency Color Map", 2D) = "white" {}
        _DualLobeSpecularWeight     ("Dual Lobe Specular Weight", Range(0,1)) = 0.0
        _TopCoatWeight              ("Top Coat Weight", Range(0,1)) = 0.0
        _Tiling                     ("Tiling", Vector) = (1,1,0,0)
        _Offset                     ("Offset", Vector) = (0,0,0,0)
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

        fixed4  _Diffuse;
        float   _DiffuseMultiplier;
        fixed4  _SpecularColor;
        float   _SpecularStrength;
        float   _GlossyRoughness;
        float   _NormalStrength;
        fixed4  _Emission;
        float   _EmissionStrength;
        float   _Alpha;

        struct Input
        {
            float2 uv_DiffuseMap;
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

            o.Albedo    = diffuseSample.rgb * _Diffuse.rgb * _DiffuseMultiplier;
            o.Specular  = specSample.rgb * _SpecularColor.rgb * _SpecularStrength;
            o.Smoothness = 1.0 - (roughSample.r * _GlossyRoughness);
            o.Normal    = UnpackScaleNormal(normalSample, _NormalStrength);
            o.Emission  = emitSample.rgb * _Emission.rgb * _EmissionStrength;
            o.Alpha     = min(_Alpha, min(alphaSample.r, cutoutSample.r));
        }
        ENDCG
    }

    FallBack "Standard"
}
