Shader "Frog-go/Animated Skins/Scrolling Emissive" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_DetailTex ("Emissive Detail (RGB)", 2D) = "black" {}
		_MaskTex ("Emissive Mask (R)", 2D) = "white" {}
		_AnimSpeed ("Animation Speed", Float) = 0.5
		_AnimStep ("Animation Step Size", Float) = 0.01
	}
	SubShader {
		Tags { "RenderType"="Opaque" }

		CGPROGRAM
		#pragma surface surf Lambert

		sampler2D _MainTex;
		sampler2D _DetailTex;
		sampler2D _MaskTex;
		fixed4 _Color;
		fixed _AnimSpeed;
		fixed _AnimStep;

		struct Input {
			float2 uv_MainTex;
			float2 uv_DetailTex;
		};

		void surf (Input IN, inout SurfaceOutput o) {
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

			float2 scrollUv = IN.uv_DetailTex;
			scrollUv.y += floor(_Time.y * _AnimSpeed / _AnimStep) * _AnimStep;
			fixed mask = tex2D(_MaskTex, IN.uv_MainTex).r;
			fixed4 emit = tex2D(_DetailTex, scrollUv) * mask;

			o.Albedo = c.rgb;
			o.Emission = emit.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}

	Fallback "Legacy Shaders/Diffuse"
}