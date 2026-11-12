Shader "OmniShade/MatCap" {
	Properties {
		_MainTex ("Main Texture", 2D) = "white" {}
		_Color ("Color", Vector) = (1,1,1,1)
		_Brightness ("Brightness", Range(0, 25)) = 1
		_Contrast ("Contrast", Range(0, 25)) = 1
		_Saturation ("Saturation", Range(0, 2)) = 1
		[Toggle] _IgnoreMainTexAlpha ("Ignore Main Texture Alpha", Float) = 0
		[HeaderGroup(MatCap)] _MatCapTex ("MatCap Texture", 2D) = "black" {}
		_MatCapColor ("MatCap Color", Vector) = (1,1,1,1)
		_MatCapBrightness ("MatCap Brightness", Range(0, 25)) = 1
		_MatCapContrast ("MatCap Contrast", Range(0, 25)) = 1
		[KeywordEnum(Multiply, Multiply Lighten)] _MatCapBlend ("MatCap Blend Mode", Float) = 0
		[Toggle(MATCAP_PERSPECTIVE)] _MatCapPerspective ("Perspective Correction", Float) = 1
		[Toggle(MATCAP_STATIC)] _MatCapStatic ("Use Static Rotation", Float) = 0
		_MatCapRot ("MatCap Static Rotation", Vector) = (0,0,0,0)
		[HeaderGroup(Normal Map)] [Normal] _NormalTex ("Normal Map", 2D) = "bump" {}
		_NormalStrength ("Normal Strength", Range(0, 5)) = 1
		[HeaderGroup(Environment And Shadows)] _AmbientBrightness ("Ambient Brightness", Range(0, 25)) = 1
		[Toggle(FOG)] _Fog ("Enable Fog", Float) = 1
		[Header(Shadows)] [Toggle(SHADOWS_ENABLED)] _ShadowsEnabled ("Enable Shadows", Float) = 1
		[HDR] _ShadowColor ("Shadow Color", Vector) = (0.3,0.3,0.3,1)
		[HeaderGroup(Culling And Blending)] [Enum(Opaque, 0, Transparent, 1, Transparent Additive, 2, Transparent Additive Alpha, 3, Opaque Cutout, 4)] _Preset ("Culling And Blend Preset", Float) = 0
		[Header(Culling)] [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
		_ZOffset ("Depth Offset", Range(-5, 5)) = 0
		[Toggle(CUTOUT)] _Cutout ("Cutout Transparency", Float) = 0
		[Hidden] _Cutoff ("Cutoff", Range(0, 1)) = 0.5
		[Header(Blending)] [Enum(UnityEngine.Rendering.BlendMode)] _SourceBlend ("Source Blend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _DestBlend ("Dest Blend", Float) = 10
		[Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Mode", Float) = 0
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;
			float4 _Color;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy) * _Color;
			}

			ENDHLSL
		}
	}
	//CustomEditor "OmniShadeGUI"
}