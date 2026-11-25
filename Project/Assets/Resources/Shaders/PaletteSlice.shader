Shader "UI/PaletteSlice"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ColorTop ("Top Color", Color) = (1,1,1,1)
        _ColorBottom ("Bottom Color", Color) = (0.7,0.7,0.7,1)
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _SelectWeight ("Select Weight", Range(0, 1)) = 1
        _SliceBounds ("Slice Bounds", Vector) = (-100, -100, 0.005, 0.005)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 localUv       : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_TexelSize;

            fixed4 _ColorTop;
            fixed4 _ColorBottom;
            fixed4 _RimColor;
            float _SelectWeight;
            float4 _SliceBounds;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.localUv = (v.vertex.xy - _SliceBounds.xy) * _SliceBounds.zw;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sampleTex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half alpha = sampleTex.a * IN.color.a;

                float2 off = _MainTex_TexelSize.xy * 6.0;
                half n1 = tex2D(_MainTex, IN.texcoord + float2(off.x, 0.0)).a;
                half n2 = tex2D(_MainTex, IN.texcoord - float2(off.x, 0.0)).a;
                half n3 = tex2D(_MainTex, IN.texcoord + float2(0.0, off.y)).a;
                half n4 = tex2D(_MainTex, IN.texcoord - float2(0.0, off.y)).a;
                half neighborMin = min(min(n1, n2), min(n3, n4));
                half edgeFactor = saturate((sampleTex.a - neighborMin) * 1.45);

                half radial = saturate(length(IN.localUv - 0.5) * 1.45);
                half grad = saturate(IN.localUv.y * 0.65 + (1.0 - radial) * 0.35);
                fixed3 baseRgb = lerp(_ColorBottom.rgb, _ColorTop.rgb, grad);

                half lum = dot(baseRgb, half3(0.299, 0.587, 0.114));
                fixed3 mutedRgb = lerp(fixed3(lum, lum, lum), baseRgb, 0.45) * 0.58;

                fixed3 rgb = lerp(mutedRgb, baseRgb, _SelectWeight);
                rgb *= lerp(1.0, 0.74, edgeFactor * (1.0 - _SelectWeight));
                rgb = lerp(rgb, _RimColor.rgb, edgeFactor * 0.45 * _SelectWeight);
                rgb *= IN.color.rgb;

                fixed4 color = fixed4(rgb, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
