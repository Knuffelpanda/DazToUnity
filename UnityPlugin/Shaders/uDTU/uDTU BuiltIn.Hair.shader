Shader "Daz3D/uDTU BuiltIn.Hair"
{
    Properties
    {
        _Diffuse                ("Diffuse Color", Color) = (1,1,1,1)
        _DiffuseMap             ("Diffuse Map", 2D) = "white" {}
        _NormalMap              ("Normal Map", 2D) = "bump" {}
        _NormalStrength         ("Normal Strength", Float) = 1.0
        _GlossyRoughness        ("Glossy Roughness", Range(0,1)) = 0.5
        _GlossyRoughnessMap     ("Glossy Roughness Map", 2D) = "white" {}
        _Alpha                  ("Alpha", Range(0,1)) = 1.0
        _AlphaMap               ("Alpha Map", 2D) = "white" {}
        _AlphaStrength          ("Alpha Strength", Float) = 1.0
        _AlphaOffset            ("Alpha Offset", Float) = 0.0
        _AlphaPower             ("Alpha Power", Float) = 1.0
        _CutoutOpacityMap       ("Cutout Opacity Map", 2D) = "white" {}
        _AlphaClipThreshold     ("Alpha Clip Threshold", Range(0,1)) = 0.1
        _Tiling                 ("Tiling", Vector) = (1,1,0,0)
        _Offset                 ("Offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 300

        CGPROGRAM
        #pragma surface surf StandardSpecular alphatest:_AlphaClipThreshold addshadow
        #pragma target 3.0

        sampler2D _DiffuseMap;
        sampler2D _NormalMap;
        sampler2D _GlossyRoughnessMap;
        sampler2D _AlphaMap;
        sampler2D _CutoutOpacityMap;

        fixed4  _Diffuse;
        float   _NormalStrength;
        float   _GlossyRoughness;
        float   _Alpha;
        float   _AlphaStrength;
        float   _AlphaOffset;
        float   _AlphaPower;

        struct Input
        {
            float2 uv_DiffuseMap;
        };

        void surf (Input IN, inout SurfaceOutputStandardSpecular o)
        {
            float2 uv = IN.uv_DiffuseMap;

            fixed4 diffuseSample  = tex2D(_DiffuseMap, uv);
            fixed4 normalSample   = tex2D(_NormalMap, uv);
            fixed4 roughSample    = tex2D(_GlossyRoughnessMap, uv);
            fixed4 alphaSample    = tex2D(_AlphaMap, uv);

            o.Albedo    = diffuseSample.rgb * _Diffuse.rgb;
            o.Specular  = fixed3(0.04, 0.04, 0.04);
            o.Smoothness = 1.0 - (roughSample.r * _GlossyRoughness);
            o.Normal    = UnpackScaleNormal(normalSample, _NormalStrength);
            o.Alpha     = pow(max(0.0, alphaSample.r * _AlphaStrength + _AlphaOffset), _AlphaPower) * _Alpha;
        }
        ENDCG
    }

    FallBack "Standard"
}
