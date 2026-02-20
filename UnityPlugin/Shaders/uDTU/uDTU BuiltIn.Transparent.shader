Shader "Daz3D/uDTU BuiltIn.Transparent"
{
    Properties
    {
        _Diffuse                    ("Diffuse Color", Color) = (1,1,1,1)
        _DiffuseMap                 ("Diffuse Map", 2D) = "white" {}
        _DiffuseMultiplier          ("Diffuse Multiplier", Float) = 1.0
        _Metallic                   ("Metallic", Range(0,1)) = 0.0
        _MetallicMap                ("Metallic Map", 2D) = "white" {}
        _Smoothness                 ("Smoothness", Range(0,1)) = 0.5
        _NormalMap                  ("Normal Map", 2D) = "bump" {}
        _NormalStrength             ("Normal Strength", Float) = 1.0
        _Emission                   ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap                ("Emission Map", 2D) = "black" {}
        _EmissionStrength           ("Emission Strength", Float) = 1.0
        _Alpha                      ("Alpha", Range(0,1)) = 1.0
        _AlphaMap                   ("Alpha Map", 2D) = "white" {}
        _IndexOfRefraction          ("Index of Refraction", Float) = 1.5
        _IndexOfRefractionWeight    ("IOR Weight", Range(0,1)) = 0.0
        _Coat                       ("Coat", Range(0,1)) = 0.0
        _Tiling                     ("Tiling", Vector) = (1,1,0,0)
        _Offset                     ("Offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 300

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        sampler2D _DiffuseMap;
        sampler2D _MetallicMap;
        sampler2D _NormalMap;
        sampler2D _EmissionMap;
        sampler2D _AlphaMap;

        fixed4  _Diffuse;
        float   _DiffuseMultiplier;
        float   _Metallic;
        float   _Smoothness;
        float   _NormalStrength;
        fixed4  _Emission;
        float   _EmissionStrength;
        float   _Alpha;

        struct Input
        {
            float2 uv_DiffuseMap;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_DiffuseMap;

            fixed4 diffuseSample  = tex2D(_DiffuseMap, uv);
            fixed4 metallicSample = tex2D(_MetallicMap, uv);
            fixed4 normalSample   = tex2D(_NormalMap, uv);
            fixed4 emitSample     = tex2D(_EmissionMap, uv);
            fixed4 alphaSample    = tex2D(_AlphaMap, uv);

            o.Albedo    = diffuseSample.rgb * _Diffuse.rgb * _DiffuseMultiplier;
            o.Metallic  = metallicSample.r * _Metallic;
            o.Smoothness = _Smoothness;
            o.Normal    = UnpackScaleNormal(normalSample, _NormalStrength);
            o.Emission  = emitSample.rgb * _Emission.rgb * _EmissionStrength;
            o.Alpha     = _Alpha * alphaSample.r;
        }
        ENDCG
    }

    FallBack "Transparent/Diffuse"
}
