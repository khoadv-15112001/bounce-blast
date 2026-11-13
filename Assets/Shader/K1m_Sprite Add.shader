// Rewritten from the AssetRipper stub. Animated water used by the original "Water" sprite (material Water_GardenB):
//   _PackedTex  R = caustic layer A, G = caustic layer B, B = wave (distortion) noise, A = water body / foam gradient.
// The value  A + causticA * causticB  is mapped through the Low -> Mid -> High colour ramp, so the static frame matches
// the baked Water.png while the caustics and the foam line keep moving.
Shader "K1m/Sprite Add" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture (alpha = shape)", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		[NoScaleOffset] _PackedTex ("Packed Texture", 2D) = "black" {}
		[Header(Wave)] _WaveSpeed ("Wave Speed (X, Y)", Vector) = (0.02,-0.015,0,0)
		_WaveStrength ("Wave Distortion", Range(0, 0.1)) = 0.025
		[Header(Layer A)] _LayerAScale ("A Scale", Float) = 1
		_LayerASpeed ("A Speed (X, Y)", Vector) = (0.03,0.017,0,0)
		[Header(Layer B)] _LayerBScale ("B Scale", Float) = 0.75
		_LayerBSpeed ("B Speed (X, Y)", Vector) = (-0.021,0.026,0,0)
		[Header(Caustics)] _CausticStrength ("Caustic Strength", Range(0, 2)) = 0.6
		_TimeScale ("Time Scale", Range(0, 2)) = 0.25
		[Header(Color)] _ColorLow ("Color Low", Color) = (0,0,0,1)
		_ColorMid ("Color Mid", Color) = (0.07,0.28,0.39,1)
		_ColorHigh ("Color High", Color) = (0.6,0.85,0.95,1)
		_MidPosition ("Mid Position", Range(0.01, 0.99)) = 0.5
		[Header(Options)] [Toggle(_WORLDUV_ON)] _WorldUV ("World Space UV", Float) = 0
	}
	SubShader {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
		Cull Off
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma shader_feature_local _WORLDUV_ON
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _PackedTex;
			fixed4 _Color;
			float4 _WaveSpeed, _LayerASpeed, _LayerBSpeed;
			float _WaveStrength, _LayerAScale, _LayerBScale, _CausticStrength, _TimeScale, _MidPosition;
			fixed4 _ColorLow, _ColorMid, _ColorHigh;

			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 wuv : TEXCOORD1; fixed4 color : COLOR; };

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
			#ifdef _WORLDUV_ON
				o.wuv = mul(unity_ObjectToWorld, v.vertex).xy * 0.1;
			#else
				o.wuv = v.uv;
			#endif
				o.color = v.color * _Color;
				return o;
			}

			fixed3 Ramp (float x)
			{
				x = saturate(x);
				float m = _MidPosition;
				return x < m ? lerp(_ColorLow.rgb, _ColorMid.rgb, x / m)
				             : lerp(_ColorMid.rgb, _ColorHigh.rgb, (x - m) / (1.0 - m));
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float t = _Time.y * _TimeScale;
				float2 uv = i.wuv;

				// Low-frequency wobble from the noise channel.
				float n1 = tex2D(_PackedTex, uv + t * _WaveSpeed.xy).b;
				float n2 = tex2D(_PackedTex, uv * 0.7 - t * _WaveSpeed.yx * 0.8 + 0.37).b;
				float2 distort = float2(n1 - 0.6, n2 - 0.6) * _WaveStrength;

				// Water body / foam line (kept inside the sprite vertically so it never wraps).
				float2 baseUv = float2(i.uv.x + distort.x, saturate(i.uv.y + distort.y * 0.5));
				float body = tex2D(_PackedTex, baseUv).a;

				// Two caustic layers scrolling in different directions; their product gives the bright moving lines.
				float ca = tex2D(_PackedTex, uv * _LayerAScale + t * _LayerASpeed.xy + distort).r;
				float cb = tex2D(_PackedTex, uv * _LayerBScale + t * _LayerBSpeed.xy - distort).g;
				float v = body + ca * cb * _CausticStrength;

				fixed4 col;
				col.rgb = Ramp(v) * i.color.rgb;
				col.a = tex2D(_MainTex, i.uv).a * i.color.a;
				return col;
			}
			ENDCG
		}
	}
	Fallback "Sprites/Default"
}
