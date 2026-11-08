// The board's print: text lying on the tiles and the lawn, drawn like Unity's GUI/Text shader but
// tested against depth, so a pawn or a building standing on a name hides it the way a real piece
// hides the print under it. (The built-in text shader draws through everything.)
// The font's atlas is drawn at 128 px and has no mipmaps, so a name a few dozen pixels tall on screen would pick
// single texels: jagged edges that crawl, worst on the far rows seen at a slant. Each pixel therefore averages a
// 4x4 grid of samples spread over its own footprint on the atlas (a box filter along both screen directions),
// which gives clean, steady edges at any size and angle.
// The text's vertex colour is sRGB and is turned linear in linear colour space, like the painted figures' colours.
Shader "Lazpoly/Print"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Lighting Off Cull Off ZTest LEqual ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            uniform float4 _MainTex_ST;
            uniform fixed4 _Color;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // A TextMesh's colour reaches the shader as stored (sRGB); in linear colour space it must be made linear
                // here, or the dark ink comes out a washed grey. _Color is a material colour, which Unity converts itself.
                o.color = v.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                o.color.rgb = GammaToLinearSpace(o.color.rgb);
                #endif
                o.color *= _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 across = ddx(i.texcoord);
                float2 up = ddy(i.texcoord);
                float coverage = 0;
                [unroll] for (int y = 0; y < 4; y++)
                {
                    [unroll] for (int x = 0; x < 4; x++)
                    {
                        float2 offset = (float2(x, y) + 0.5) / 4.0 - 0.5;
                        float2 uv = i.texcoord + across * offset.x + up * offset.y;
                        coverage += tex2Dlod(_MainTex, float4(uv, 0, 0)).a;
                    }
                }

                fixed4 col = i.color;
                col.a *= coverage / 16.0;
                return col;
            }
            ENDCG
        }
    }
}
