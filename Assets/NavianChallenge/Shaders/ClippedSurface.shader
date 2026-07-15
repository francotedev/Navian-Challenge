// Simple two-sided, alpha-blended surface shader for the atlas meshes that can be cut by the
// same plane as the MRI volume. The cut is driven by global shader properties set from
// CrossSectionController, and matches UVR's plane test exactly:
//   discard when dot(worldPos - _SectionPoint, _SectionNormal) > 0
// so the meshes slice away together with the volume. Lit with a fixed key direction (the
// anatomy doesn't need accurate scene lighting), Cull Off so the cut reveals interior walls.
Shader "Navian/ClippedSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _SectionEnabled;
            float4 _SectionPoint;   // world-space point on the cut plane
            float4 _SectionNormal;  // world-space cut normal (voxels on the +side are removed)

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos        : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float3 worldNormal: TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                if (_SectionEnabled > 0.5 && dot(i.worldPos - _SectionPoint.xyz, _SectionNormal.xyz) > 0.0)
                    discard;

                float3 n = normalize(i.worldNormal) * (facing > 0 ? 1.0 : -1.0);
                float ndotl = saturate(dot(n, normalize(float3(0.3, 0.85, 0.45))));
                float3 lit = _Color.rgb * (0.45 + 0.55 * ndotl);
                return fixed4(lit, _Color.a);
            }
            ENDCG
        }
    }
}
