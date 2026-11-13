// Re-implementation of the ripped placeholder (which drew an opaque quad). Draws a progress ring procedurally:
// a band of radius _RingRadius and width _Thickness around _Center (uv), filled over _Fill of the circle starting at
// _StartAngle (counter-clockwise unless _Clockwise = 1). GardenCannon sets _Fill per renderer from the container HP.
Shader "Spyke/ContainerRingFill" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_Fill ("Fill (0-1)", Range(0, 1)) = 1
		_StartAngle ("Start Angle (deg)", Range(0, 360)) = 90
		_Clockwise ("Clockwise (0/1)", Float) = 0
		_RingRadius ("Ring centreline radius (uv)", Range(0, 0.5)) = 0.427
		_Thickness ("Stroke width (uv)", Range(0, 0.5)) = 0.145
		_Center ("Center (uv)", Vector) = (0.5,0.5,0,0)
		_Softness ("Edge softness (uv)", Range(0, 0.05)) = 0.004
	}
	SubShader {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off
		Lighting Off

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			fixed4 _Color;
			float _Fill, _StartAngle, _Clockwise, _RingRadius, _Thickness, _Softness;
			float4 _Center;

			struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
			struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.color = v.color * _Color;
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 d = i.uv - _Center.xy;
				float r = length(d);
				float soft = max(_Softness, 1e-4);
				float halfWidth = _Thickness * 0.5;
				float band = smoothstep(halfWidth + soft, halfWidth - soft, abs(r - _RingRadius));

				// Angle measured from _StartAngle, 0..1 around the circle.
				float a = degrees(atan2(d.y, d.x)) - _StartAngle;
				if (_Clockwise > 0.5) a = -a;
				float t = frac(a / 360.0 + 1.0);
				float fill = (_Fill >= 0.999) ? 1.0 : step(t, _Fill);

				fixed4 tex = tex2D(_MainTex, i.uv);
				fixed4 c = i.color;
				c.rgb *= tex.rgb;
				c.a *= band * fill;
				return c;
			}
			ENDCG
		}
	}
}
