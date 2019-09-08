Shader "Frog-go/Lava"
{
	Properties
	{
		_Color ("Lava Color", Color) = (1, 1, 1, 1)
		_SecondaryColor ("Lava Secondary Color", Color) = (1, 1, 1, 1)
		_NoiseTex ("Lava Noise", 2D) = "white" {}
		_MaskTex ("Secondary Mask", 2D) = "black" {}
		_MaskThreshold ("Mask Threshold", Range(0.0, 1.0)) = 0.5
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
        Lighting Off
        
        Pass {
    		CGPROGRAM
            
    		#pragma vertex vert
            #pragma fragment frag
    		#include "UnityCG.cginc"
    			
    		sampler2D _NoiseTex;
    		sampler2D _MaskTex;
            float4 _NoiseTex_ST;
            float4 _MaskTex_ST;
    		fixed4 _Color;
    		fixed4 _SecondaryColor;
    		fixed _MaskThreshold;

    		struct appdata {
                float4 vertex : POSITION;
    			float2 uv : TEXCOORD0;
    		};
            
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            v2f vert(appdata v) {
                v2f o;
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                return o;
            }
            
    		fixed4 frag (v2f i) : SV_Target {
    			float t = _Time.y / 30.0;
    			float2 offs = _WorldSpaceCameraPos.xz / 16.0;
                
                float2 uv = floor(i.uv * 240.0) / 240.0;
    			fixed foamFac = tex2D(_NoiseTex, uv + offs + (t * float2(-0.1245, 0.3085)));
                
    			fixed4 col = _Color;

				fixed mask = tex2D(_MaskTex, ((uv + offs) * 1.6) + (t * float2(-0.1336, 0.1416)));

				if(mask < _MaskThreshold)
					col = _SecondaryColor;
    			
    			if(foamFac > 0.45)
    				col.rgb += 0.05;
    			else
    				col.rgb *= min(0.85 + foamFac, 1.0);

                col.a = 1.0;
                return col;
    		}

    		ENDCG
        }
	}
}