Shader "Unlit/testShader"
{
    Properties
    {
        _SpherePosition ("Sphere Position", Vector)= (1,1,1,1)
        _Color1 ("Primary Color", Color) = (0,0,0,1)
        _Color2 ("Secondary Color", Color) = (1,1,1,1)
        _MaxDistance ("Top Distance", float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f {
                float3 worldPos : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            fixed4 _SpherePosition;

            v2f vert (
                float4 vertex : POSITION, 
                float3 worldPos : TEXCOORD0
                )
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.worldPos = mul(unity_ObjectToWorld, vertex);
                return o;
            }

            fixed4 _Color1;
            fixed4 _Color2;
            float _MaxDistance;

            fixed4 frag (v2f i) : SV_Target
            {
                // fixed4 c = 0;
                float dist = distance(_SpherePosition, i.worldPos) / _MaxDistance;
                // c.rgb = lerp();
                return lerp(_Color1, _Color2, dist);
            }
            ENDCG
        }
    }
}
