Shader "Daz3D/uDTU BuiltIn.Metallic"
{
    Properties
    {
        _Diffuse                ("Diffuse Color", Color) = (1,1,1,1)
        _DiffuseMap             ("Diffuse Map", 2D) = "white" {}
        _DiffuseMultiplier      ("Diffuse Multiplier", Float) = 1.0
        _Metallic               ("Metallic", Range(0,1)) = 0.0
        _MetallicMap            ("Metallic Map", 2D) = "white" {}
        _Roughness              ("Roughness", Range(0,1)) = 0.5
        _RoughnessMap           ("Roughness Map", 2D) = "white" {}
        _NormalMap              ("Normal Map", 2D) = "bump" {}
        _NormalStrength         ("Normal Strength", Float) = 1.0
        _Height                 ("Height", Float) = 0.0
        _HeightMap              ("Height Map", 2D) = "white" {}
        _HeightOffset           ("Height Offset", Float) = 0.0
        _Emission               ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap            ("Emission Map", 2D) = "black" {}
        _EmissionStrength       ("Emission Strength", Float) = 1.0
        _Alpha                  ("Alpha", Range(0,1)) = 1.0
        _AlphaMap               ("Alpha Map", 2D) = "white" {}
        _CutoutOpacityMap       ("Cutout Opacity Map", 2D) = "white" {}
        _AlphaClipThreshold     ("Alpha Clip Threshold", Range(0,1)) = 0.5
        _TranslucencyWeight     ("Translucency Weight", Range(0,1)) = 0.0
        _TranslucencyColor      ("Translucency Color", Color) = (1,1,1,1)
        _TranslucencyColorMap   ("Translucency Color Map", 2D) = "white" {}
        _DualLobeSpecularWeight ("Dual Lobe Specular Weight", Range(0,1)) = 0.0
        _TopCoatWeight          ("Top Coat Weight", Range(0,1)) = 0.0
        _Tiling                 ("Tiling", Vector) = (1,1,0,0)
        _Offset                 ("Offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _DiffuseMap;
        sampler2D _MetallicMap;
        sampler2D _RoughnessMap;
        sampler2D _NormalMap;
        sampler2D _EmissionMap;
        sampler2D _AlphaMap;
        sampler2D _CutoutOpacityMap;
        sampler2D _TranslucencyColorMap;

        fixed4  _Diffuse;
        float   _DiffuseMultiplier;
        float   _Metallic;
        float   _Roughness;
        float   _NormalStrength;
        fixed4  _Emission;
        float   _EmissionStrength;
        float   _Alpha;
        float   _AlphaClipThreshold;

        struct Input
        {
            float2 uv_DiffuseMap;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_DiffuseMap;

            fixed4 diffuseSample  = tex2D(_DiffuseMap, uv);
            fixed4 metallicSample = tex2D(_MetallicMap, uv);
            fixed4 roughSample    = tex2D(_RoughnessMap, uv);
            fixed4 normalSample   = tex2D(_NormalMap, uv);
            fixed4 emitSample     = tex2D(_EmissionMap, uv);
            fixed4 alphaSample    = tex2D(_AlphaMap, uv);
            fixed4 cutoutSample   = tex2D(_CutoutOpacityMap, uv);

            o.Albedo    = diffuseSample.rgb * _Diffuse.rgb * _DiffuseMultiplier;
            o.Metallic  = metallicSample.r * _Metallic;
            o.Smoothness = 1.0 - (roughSample.r * _Roughness);
            o.Normal    = UnpackScaleNormal(normalSample, _NormalStrength);
            o.Emission  = emitSample.rgb * _Emission.rgb * _EmissionStrength;
            o.Alpha     = min(_Alpha, min(alphaSample.r, cutoutSample.r));
        }
        ENDCG
    }

    FallBack "Standard"
}
