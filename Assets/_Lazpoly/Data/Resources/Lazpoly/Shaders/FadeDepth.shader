// The depth of a see-through piece, drawn just before the transparent queue: it writes depth and no
// colour, so the faded colour pass that follows shows only each piece's front surface. Without it the
// arms show through the body and the hair through the hands, and the print under the piece shows on top of it.
Shader "Lazpoly/FadeDepth"
{
    SubShader
    {
        Tags { "Queue"="Transparent-1" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite On ZTest LEqual ColorMask 0 Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert (float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag () : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
