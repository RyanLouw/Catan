Shader "Catan/Board"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 vertex : SV_POSITION; float shade : TEXCOORD0; };
            fixed4 _Color;
            v2f vert(appdata input) { v2f output; output.vertex = UnityObjectToClipPos(input.vertex); output.shade = .65 + .35 * saturate(dot(UnityObjectToWorldNormal(input.normal),normalize(float3(-.4,1,-.6)))); return output; }
            fixed4 frag(v2f input) : SV_Target { return fixed4(_Color.rgb * input.shade, _Color.a); }
            ENDCG
        }
    }
}
