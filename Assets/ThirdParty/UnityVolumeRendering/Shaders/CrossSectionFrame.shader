Shader "VolumeRendering/CrossSectionFrame"
{
    // Thin rounded-rectangle OUTLINE drawn on a unit quad, in world space. Interior is transparent (the
    // anatomy shows through). _BaseColor is tinted white<->blue at runtime (CropPlaneFrame). The cut itself
    // is defined by the transform (CrossSectionPlane.GetMatrix), so this visual never affects the clip.
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Thickness ("Border Thickness", Range(0.002, 0.2)) = 0.035
        _Radius ("Corner Radius", Range(0.0, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _BaseColor;
            float _Thickness;
            float _Radius;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Signed distance to a rounded box centred at the origin, half-extents b, corner radius r.
            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UV 0..1 -> centred -0.5..0.5
                float2 p = i.uv - 0.5;
                float halfT = 0.5 * _Thickness; // 'half' is a reserved type keyword — don't use it
                // Inset the rounded-rect boundary so the whole stroke stays inside the quad.
                float2 b = float2(0.5, 0.5) - halfT;
                float r = min(_Radius, 0.5 - halfT);
                float d = abs(sdRoundBox(p, b, r));   // distance to the frame centre-line
                float aa = max(fwidth(d), 1e-5);
                float alpha = 1.0 - smoothstep(halfT - aa, halfT + aa, d);
                fixed4 col = _BaseColor;
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}
