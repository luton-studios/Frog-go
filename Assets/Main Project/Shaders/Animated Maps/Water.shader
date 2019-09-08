Shader "Frog-go/Water"
{
	Properties
	{
		_Color ("Water Color", Color) = (1, 1, 1, 1)
		_NoiseTex ("Water Noise", 2D) = "white" {}
		_FoamIntensity ("Foam Intensity", Float) = 1.0
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
            float4 _NoiseTex_ST;
    		fixed4 _Color;
    		fixed _FoamIntensity;

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
    			float t = _Time.y / 10.0;
    			float2 offs = _WorldSpaceCameraPos.xz / 16.0;
                
                float2 uv = floor(i.uv * 240.0) / 240.0;
    			fixed foamFac = tex2D(_NoiseTex, uv + offs + (t * float2(-0.1245, 0.3085)));
                
                t += 2.0;
    			foamFac *= tex2D(_NoiseTex, ((uv + offs) * 0.65) + (t * float2(-0.1336, 0.1416)));
                
                t += 2.0;
    			foamFac *= tex2D(_NoiseTex, ((uv + offs) * 1.41) + (t * float2(-0.1596, 0.2125)));

    			fixed4 col = _Color;
    			
    			if(foamFac > 0.18)
    				col.rgb += 0.25 * _FoamIntensity;
    			else if(foamFac > 0.1)
    				col.rgb += 0.12 * _FoamIntensity;
    			else if(foamFac > 0.05)
    				col.rgb += 0.05 * _FoamIntensity;
    			else
    				col.rgb *= min(0.95 + foamFac, 1.0);

                col.a = 1.0;
                return col;
    		}

    		ENDCG
        }
	}
}