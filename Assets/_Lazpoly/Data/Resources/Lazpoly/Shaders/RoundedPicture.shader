// A picture on a card (a property's picture from BuildingPreview): the texture drawn inside a rounded rectangle
// with smooth corners, a thin edge line over it, optionally greyed, and optionally only a part of it (a close-up).
// The picture's own transparency is not used: the shape alone decides what shows. Fades with its canvas group.
Shader "Lazpoly/RoundedPicture"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "white" {}
        _Size ("Size on the screen (width, height)", Vector) = (100,100,0,0)
        _Radius ("Corner radius, in the size's units", Float) = 10
        _View ("Part shown (x, y, width, height)", Vector) = (0,0,1,1)
        _Grey ("Greyed", Range(0,1)) = 0
        _Edge ("Edge line", Color) = (0,0,0,0)
        _EdgeWidth ("Edge line width, in the size's units", Float) = 1.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            uniform float4 _Size;
            uniform float _Radius;
            uniform float4 _View;
            uniform float _Grey;
            uniform float4 _Edge;
            uniform float _EdgeWidth;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 colour = tex2D(_MainTex, _View.xy + i.texcoord * _View.zw);

                // How far outside the rounded rectangle this point lies (negative inside), in the size's units.
                float2 corner = abs(i.texcoord - float2(0.5, 0.5)) * _Size.xy - (_Size.xy * 0.5 - float2(_Radius, _Radius));
                float outside = length(max(corner, float2(0.0, 0.0))) + min(max(corner.x, corner.y), 0.0) - _Radius;
                float soft = max(fwidth(outside), 0.0001);

                float shade = dot(colour.rgb, float3(0.299, 0.587, 0.114));
                colour.rgb = lerp(colour.rgb, float3(shade, shade, shade), _Grey);
                colour.rgb = lerp(colour.rgb, _Edge.rgb, _Edge.a * saturate((outside + _EdgeWidth) / soft + 0.5));
                colour.rgb *= i.color.rgb;
                colour.a = saturate(0.5 - outside / soft) * i.color.a;
                return colour;
            }
            ENDCG
        }
    }
}
