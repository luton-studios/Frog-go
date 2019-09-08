Shader "Frog-go/Blob Shadow"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_ShadowColor ("Shadow Color", Color) = (0, 0, 0, 1)
		_ShadowStrength ("Shadow Strength", Range(0.0, 1.0)) = 1.0
	}
	SubShader
	{
		Tags { "Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		Lighting Off ZWrite Off
		Offset -1, 1

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			half4 _ShadowColor;
			half _ShadowStrength;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = _ShadowColor;
				col.a *= tex2D(_MainTex, i.uv).a * _ShadowStrength;
				return col;
			}
			ENDCG
		}
	}
}
