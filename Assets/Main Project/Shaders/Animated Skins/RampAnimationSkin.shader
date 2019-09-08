Shader "Frog-go/Animated Skins/Ramp Animation" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_SecondaryTex ("Animated (RGB)", 2D) = "black" {}
		_AnimRamp ("Animation Ramp (R)", 2D) = "black" {}
		_AnimSpeed ("Animation Speed", Float) = 0.5
		_AnimStep ("Animation Step Size", Float) = 0.01
	}
	SubShader {
		Tags { "RenderType"="Opaque" }

		CGPROGRAM
		#pragma surface surf Lambert

		sampler2D _MainTex;
		sampler2D _SecondaryTex;
		sampler2D _AnimRamp;
		fixed4 _Color;
		fixed _AnimSpeed;
		fixed _AnimStep;

		struct Input {
			float2 uv_MainTex;
		};

		void surf (Input IN, inout SurfaceOutput o) {
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

			// Loop black to white.
			float animTime = fmod(_Time.y * _AnimSpeed, 1.0);
			// Round animation timer to step size.
			animTime = floor(animTime / _AnimStep) * _AnimStep;

			fixed ramp = tex2D(_AnimRamp, IN.uv_MainTex).r;
			fixed4 second = tex2D(_SecondaryTex, IN.uv_MainTex);

			if(ramp > animTime - _AnimStep && ramp < animTime + _AnimStep) {
				c.rgb = lerp(c.rgb, second.rgb, second.a);
			}

			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}

	Fallback "Legacy Shaders/Diffuse"
}