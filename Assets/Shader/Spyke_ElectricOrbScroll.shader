// Re-implementation of the ripped placeholder (which drew opaque quads). Additive particle shader whose texture
// scrolls with time; each particle gets its own offset from its random seed (vertex colour alpha is kept for fading).
Shader "Spyke/ElectricOrbScroll" {
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_ScrollSpeed ("Scroll Speed (X, Y)", Vector) = (0.1,0,0,0)
		_RandomOffset ("Per Particle Random Offset (X, Y)", Vector) = (1,1,0,0)
		[Toggle(_DEBUGRND_ON)] _DebugRnd ("DEBUG: show random as color (R=x, G=y)", Float) = 0
	}
	SubShader {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
		Blend One One
		ZWrite Off
		Cull Off
		Lighting Off

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex; float4 _MainTex_ST;
			fixed4 _Color;
			float4 _ScrollSpeed;
			float4 _RandomOffset;

			struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
			struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				// Per-particle phase from the particle's world position (stable while it lives, differs between particles).
				float3 wp = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz + v.vertex.xyz * 0.001;
				float rnd = frac(sin(dot(floor(wp.xy * 7.0), float2(12.9898, 78.233))) * 43758.5453);
				float2 offset = _ScrollSpeed.xy * _Time.y + _RandomOffset.xy * rnd;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex) + offset;
				o.color = v.color * _Color;
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 t = tex2D(_MainTex, i.uv);
				fixed3 c = t.rgb * i.color.rgb * (t.a * i.color.a);
				return fixed4(c, 1);
			}
			ENDCG
		}
	}
}
