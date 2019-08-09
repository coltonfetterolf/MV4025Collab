// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "MyShaders/YValue" {
	Properties
		{
			_maxHeight ("maxHeight", float) = 275.0
		}
    SubShader {
        Pass {

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                float4 pos : SV_POSITION;
                fixed3 color : COLOR0;
            };

			float _maxHeight;

            v2f vert (appdata_base v)
            {
                v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				float gray = v.vertex[1]/_maxHeight;
                o.color = float3(gray,gray,gray);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4 (i.color, 1);
            }
            ENDCG

        }
    }
}