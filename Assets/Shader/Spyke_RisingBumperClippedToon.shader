Shader "Spyke/RisingBumperClippedToon" {
	Properties {
		_Color ("Color", Vector) = (1,1,1,1)
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
		_OcclusionColor ("Occlusion Color", Vector) = (0,0,0,1)
		[HDR] _EmissionColor ("Emission Color", Vector) = (0,0,0,1)
		_EmissionMap ("Emission", 2D) = "black" {}
		[Header(Alpha Fade)] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
		[Header(Forward Rendering Options)] [Toggle] _SpecularHighlights ("Specular Highlights", Float) = 1
		_GlossyReflections ("Reflections", Range(0, 1)) = 1
		[Toggle] _OrthoSpecular ("Ortho Specular (position-independent)", Float) = 0
		[HDR] _SpecularColor ("Specular Color", Vector) = (1,1,1,1)
		[Header(Custom Light 1)] [Toggle] _UseCustomLightDir ("Enable Light 1", Float) = 0
		_LightYaw ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 0
		_LightPitch ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomLightColor ("Light Color", Vector) = (1,1,1,1)
		[Header(Custom Light 2)] [Toggle] _UseCustomLight2 ("Enable Light 2", Float) = 0
		_LightYaw2 ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 180
		_LightPitch2 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomLightColor2 ("Light Color", Vector) = (1,1,1,1)
		[Header(Custom Light 3)] [Toggle] _UseCustomLight3 ("Enable Light 3", Float) = 0
		_LightYaw3 ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 270
		_LightPitch3 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomLightColor3 ("Light Color", Vector) = (1,1,1,1)
		[Header(Custom Spec 4)] [Toggle] _UseCustomSpec4 ("Enable Spec 4", Float) = 0
		_SpecYaw4 ("Yaw   (Horizontal 0-360)", Range(0, 360)) = 180
		_SpecPitch4 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
		[HDR] _CustomSpecColor4 ("Spec Color", Vector) = (1,1,1,1)
		_GroundClipAllowance ("Ground Clip Allowance", Float) = 0
		_StencilRef ("Stencil Ref", Float) = 1
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
}