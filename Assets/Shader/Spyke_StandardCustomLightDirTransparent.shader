// Transparent variant (mesh particles: lid / egg shell / box shards), alpha blended and tinted by the vertex
// colour so particles fade. Generated from Spyke_StandardCustomLightDir.shader.
// Re-implementation of the original "Spyke/StandardCustomLightDir" (the ripped file was a placeholder that only
// sampled the texture, which made every item look flat and dark). Property names are unchanged, so the original
// materials (CubePlasticAtlas, stone, egg_box, twirler, ball_mat, ...) keep their tuned values:
//   - up to 3 custom directional lights + 1 custom specular light, given as yaw / pitch (world space),
//   - smoothness-driven specular (optionally view-independent "ortho" specular), simple fresnel rim,
//   - occlusion map tinted by _OcclusionColor, emission, optional normal map,
//   - atlas albedo: the tile is selected through _MainTex_ST (set per renderer by GardenCannon),
//   - world-Y clipping and the blend / ZWrite / ZTest options.
// Direction convention: a light with (yaw, pitch) comes FROM (sin(yaw)cos(pitch), -sin(pitch), cos(yaw)cos(pitch)).
Shader "Spyke/StandardCustomLightDirTransparent" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		[Toggle(_ATLASALBEDO_ON)] _AtlasAlbedo ("Atlas Albedo", Float) = 0
		_AtlasGrid ("Atlas Grid (cols, rows)", Vector) = (8,4,0,0)
		_ClipBelowY ("Clip Below World Y (0 = off)", Float) = -99999
		_ClipAboveY ("Clip Above World Y (0 = off)", Float) = 0
		_Metallic ("Metallic", Range(0, 1)) = 0
		_MetallicMap ("Metallic (RGB)", 2D) = "white" {}
		_SmoothnessMap ("Smoothness (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0, 1)) = 0.5
		_BumpScale ("Normal Scale", Float) = 1
		[Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
		_OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
		_OcclusionMap ("Occlusion", 2D) = "white" {}
		_OcclusionColor ("Occlusion Color", Color) = (0,0,0,1)
		[HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
		_EmissionMap ("Emission", 2D) = "black" {}
		[Header(Alpha Fade)] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
		[Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
		[Header(Forward Rendering Options)] [Toggle] _SpecularHighlights ("Specular Highlights", Float) = 1
		_GlossyReflections ("Reflections", Range(0, 1)) = 1
		[Toggle] _OrthoSpecular ("Ortho Specular (position-independent)", Float) = 0
		[HDR] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
		[Header(Custom Light 1)] [Toggle] _UseCustomLightDir ("Enable Light 1", Float) = 0
		_LightYaw ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 0
		_LightPitch ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomLightColor ("Light Color", Color) = (1,1,1,1)
		[Header(Custom Light 2)] [Toggle] _UseCustomLight2 ("Enable Light 2", Float) = 0
		_LightYaw2 ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 180
		_LightPitch2 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomLightColor2 ("Light Color", Color) = (1,1,1,1)
		[Header(Custom Light 3)] [Toggle] _UseCustomLight3 ("Enable Light 3", Float) = 0
		_LightYaw3 ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 270
		_LightPitch3 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomLightColor3 ("Light Color", Color) = (1,1,1,1)
		[Header(Custom Spec 4)] [Toggle] _UseCustomSpec4 ("Enable Spec 4", Float) = 0
		_SpecYaw4 ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 180
		_SpecPitch4 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomSpecColor4 ("Spec Color", Color) = (1,1,1,1)
		_StencilRef ("Stencil Ref", Float) = 1
		[Header(Garden Cannon)] _GCAmbient ("Ambient (added, not in the original)", Color) = (0.4,0.4,0.44,1)
		_GCRim ("Fresnel Rim", Range(0, 1)) = 0.15
		_GCSaturation ("Saturation", Range(0, 2)) = 1.25
		_GCExposure ("Exposure", Range(0.5, 1.5)) = 1.08
	}

	SubShader {
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
		LOD 200

		Pass {
			Tags { "LightMode"="Always" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite [_ZWrite]
			ZTest [_ZTest]
			Cull Back

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"

			sampler2D _MainTex; float4 _MainTex_ST;
			sampler2D _OcclusionMap;
			sampler2D _EmissionMap;
			sampler2D _BumpMap;
			sampler2D _SmoothnessMap;
			fixed4 _Color;
			float _ClipBelowY, _ClipAboveY;
			float _Glossiness, _BumpScale, _OcclusionStrength;
			fixed4 _OcclusionColor;
			half4 _EmissionColor;
			float _SpecularHighlights, _GlossyReflections, _OrthoSpecular;
			half4 _SpecularColor;
			float _UseCustomLightDir, _LightYaw, _LightPitch; half4 _CustomLightColor;
			float _UseCustomLight2, _LightYaw2, _LightPitch2; half4 _CustomLightColor2;
			float _UseCustomLight3, _LightYaw3, _LightPitch3; half4 _CustomLightColor3;
			float _UseCustomSpec4, _SpecYaw4, _SpecPitch4; half4 _CustomSpecColor4;
			half4 _GCAmbient;
			float _GCRim;
			float _GCSaturation;
			float _GCExposure;

			struct appdata {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uvAlbedo : TEXCOORD0;
				float2 uvRaw : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float3 worldNormal : TEXCOORD3;
				float3 worldTangent : TEXCOORD4;
				float3 worldBinormal : TEXCOORD5;
				fixed4 color : COLOR;
			};

			float3 DirFromYawPitch(float yaw, float pitch)
			{
				float y = radians(yaw), p = radians(pitch);
				return normalize(float3(sin(y) * cos(p), -sin(p), cos(y) * cos(p)));
			}

			v2f vert(appdata v)
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uvAlbedo = TRANSFORM_TEX(v.uv, _MainTex);
				o.uvRaw = v.uv;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
				o.worldBinormal = cross(o.worldNormal, o.worldTangent) * v.tangent.w * unity_WorldTransformParams.w;
				o.color = v.color;
				return o;
			}

			half3 Specular(float3 n, float3 v, float3 l, half3 color, float exponent)
			{
				float3 h = normalize(l + v);
				float s = pow(saturate(dot(n, h)), exponent) * saturate(dot(n, l) * 4.0);
				return color * s;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				if (_ClipBelowY > -9999.0 && i.worldPos.y < _ClipBelowY) discard;
				if (_ClipAboveY != 0.0 && i.worldPos.y > _ClipAboveY) discard;

				fixed4 tex = tex2D(_MainTex, i.uvAlbedo);
				fixed3 albedo = tex.rgb * _Color.rgb * i.color.rgb;

				// Normal (optional normal map; "bump" default = flat).
				float3 tn = UnpackNormal(tex2D(_BumpMap, i.uvRaw));
				tn.xy *= _BumpScale;
				tn = normalize(tn);
				float3 n = normalize(tn.x * i.worldTangent + tn.y * i.worldBinormal + tn.z * i.worldNormal);
				if (dot(n, n) < 1e-6) n = normalize(i.worldNormal);

				// View direction: orthographic camera -> constant (towards the camera).
				float3 camFwd = -UNITY_MATRIX_V[2].xyz;
				float useOrtho = saturate(step(0.5, _OrthoSpecular) + step(0.5, unity_OrthoParams.w));
				float3 v = normalize(lerp(normalize(_WorldSpaceCameraPos - i.worldPos), -camFwd, useOrtho));

				float3 l1 = DirFromYawPitch(_LightYaw, _LightPitch);
				float3 l2 = DirFromYawPitch(_LightYaw2, _LightPitch2);
				float3 l3 = DirFromYawPitch(_LightYaw3, _LightPitch3);
				float3 l4 = DirFromYawPitch(_SpecYaw4, _SpecPitch4);

				half3 diffuse = _GCAmbient.rgb;
				diffuse += _CustomLightColor.rgb * saturate(dot(n, l1)) * step(0.5, _UseCustomLightDir);
				diffuse += _CustomLightColor2.rgb * saturate(dot(n, l2)) * step(0.5, _UseCustomLight2);
				diffuse += _CustomLightColor3.rgb * saturate(dot(n, l3)) * step(0.5, _UseCustomLight3);

				half3 col = albedo * diffuse;

				// Specular
				float smooth = _Glossiness * tex2D(_SmoothnessMap, i.uvRaw).r;
				float exponent = exp2(smooth * 9.0 + 1.0);
				half3 spec = Specular(n, v, l1, _CustomLightColor.rgb, exponent) * (0.35 * step(0.5, _UseCustomLightDir));
				spec += Specular(n, v, l4, _CustomSpecColor4.rgb, exponent) * step(0.5, _UseCustomSpec4);
				col += spec * _SpecularColor.rgb * (smooth * step(0.5, _SpecularHighlights));

				// Fresnel rim (stands in for the glossy reflections of the original).
				float fres = pow(1.0 - saturate(dot(n, v)), 4.0);
				col += _SpecularColor.rgb * fres * _GCRim * _GlossyReflections * smooth;

				// Occlusion tinted towards _OcclusionColor.
				half ao = lerp(1.0, tex2D(_OcclusionMap, i.uvRaw).g, _OcclusionStrength);
				col *= lerp(_OcclusionColor.rgb, 1.0, ao);

				col += tex2D(_EmissionMap, i.uvRaw).rgb * _EmissionColor.rgb;

				// Richer, punchier colours (the original reads deeper than plain lambert) and a soft shoulder
				// instead of hard clipping, so bright yellows keep their shading instead of turning flat white-yellow.
				col *= _GCExposure;
				half luma = dot(col, half3(0.299, 0.587, 0.114));
				col = max(0, lerp(luma.xxx, col, _GCSaturation));
				col = col / (1.0 + max(0, col - 0.85) * 0.9);
				return fixed4(col, tex.a * _Color.a * i.color.a);
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
}
