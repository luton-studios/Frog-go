Shader "Frog-go/Particles/Solid Color Additive" {
	Properties {
		_TintColor ("Tint Color", Color) = (1, 1, 1, 1)
	}

	Category {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
		Blend SrcAlpha One
		ColorMask RGB
		Cull Off Lighting Off ZWrite Off

		SubShader {
			Pass {
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_particles

				#include "UnityCG.cginc"

				fixed4 _TintColor;

				struct appdata_t {
					float4 vertex : POSITION;
					fixed4 color : COLOR;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f {
					float4 vertex : SV_POSITION;
					fixed4 color : COLOR;
				};

				v2f vert (appdata_t v)
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.color = v.color;
					return o;
				}

				fixed4 frag (v2f i) : SV_Target
				{
					return i.color * _TintColor;
				}
				ENDCG
			}
		}
	}
}