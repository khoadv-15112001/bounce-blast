Shader "TextMeshPro/Spyke/MultiLayerText" {
	Properties {
		_0_LayerColor ("", Vector) = (1,1,1,1)
		_0_LayerSecColor ("", Vector) = (1,1,1,1)
		_0_LayerOutlineColor ("", Vector) = (1,1,1,1)
		_0_LayerOutlineWidth ("", Range(0, 1)) = 0
		_0_LayerOffsetX ("", Range(-1, 1)) = 0
		_0_LayerOffsetY ("", Range(-1, 2)) = 0
		_0_LayerDilate ("", Range(-1, 1)) = 0
		_0_LayerScale ("", Range(0, 0.25)) = 0
		_0_LayerGradDown ("", Range(0, 1)) = 0
		_0_LayerGradUp ("", Range(0, 1)) = 1
		_0_LayerSoftness ("", Range(0, 1)) = 0
		_0_LayerCenterFill ("", Range(0, 1)) = 0
		_1_LayerColor ("", Vector) = (1,1,1,1)
		_1_LayerSecColor ("", Vector) = (1,1,1,1)
		_1_LayerOutlineColor ("", Vector) = (1,1,1,1)
		_1_LayerOutlineWidth ("", Range(0, 1)) = 0
		_1_LayerOffsetX ("", Range(-1, 1)) = 0
		_1_LayerOffsetY ("", Range(-1, 2)) = 0
		_1_LayerDilate ("", Range(-1, 1)) = 0
		_1_LayerScale ("", Range(0, 0.25)) = 0
		_1_LayerGradDown ("", Range(0, 1)) = 0
		_1_LayerGradUp ("", Range(0, 1)) = 1
		_1_LayerSoftness ("", Range(0, 1)) = 0
		_1_LayerCenterFill ("", Range(0, 1)) = 0
		_2_LayerColor ("", Vector) = (1,1,1,1)
		_2_LayerSecColor ("", Vector) = (1,1,1,1)
		_2_LayerOutlineColor ("", Vector) = (1,1,1,1)
		_2_LayerOutlineWidth ("", Range(0, 1)) = 0
		_2_LayerOffsetX ("", Range(-1, 1)) = 0
		_2_LayerOffsetY ("", Range(-1, 2)) = 0
		_2_LayerDilate ("", Range(-1, 1)) = 0
		_2_LayerScale ("", Range(0, 0.25)) = 0
		_2_LayerGradDown ("", Range(0, 1)) = 0
		_2_LayerGradUp ("", Range(0, 1)) = 1
		_2_LayerSoftness ("", Range(0, 1)) = 0
		_2_LayerCenterFill ("", Range(0, 1)) = 0
		_3_LayerColor ("", Vector) = (1,1,1,1)
		_3_LayerSecColor ("", Vector) = (1,1,1,1)
		_3_LayerOutlineColor ("", Vector) = (1,1,1,1)
		_3_LayerOutlineWidth ("", Range(0, 1)) = 0
		_3_LayerOffsetX ("", Range(-1, 1)) = 0
		_3_LayerOffsetY ("", Range(-1, 2)) = 0
		_3_LayerDilate ("", Range(-1, 1)) = 0
		_3_LayerScale ("", Range(0, 0.25)) = 0
		_3_LayerGradDown ("", Range(0, 1)) = 0
		_3_LayerGradUp ("", Range(0, 1)) = 1
		_3_LayerSoftness ("", Range(0, 1)) = 0
		_3_LayerCenterFill ("", Range(-0, 1)) = 0
		_4_LayerColor ("", Vector) = (1,1,1,1)
		_4_LayerSecColor ("", Vector) = (1,1,1,1)
		_4_LayerOutlineColor ("", Vector) = (1,1,1,1)
		_4_LayerOutlineWidth ("", Range(0, 1)) = 0
		_4_LayerOffsetX ("", Range(-1, 1)) = 0
		_4_LayerOffsetY ("", Range(-1, 2)) = 0
		_4_LayerDilate ("", Range(-1, 1)) = 0
		_4_LayerScale ("", Range(0, 0.25)) = 0
		_4_LayerGradDown ("", Range(0, 1)) = 0
		_4_LayerGradUp ("", Range(0, 1)) = 1
		_4_LayerSoftness ("", Range(0, 1)) = 0
		_4_LayerCenterFill ("", Range(0, 1)) = 0
		_WeightNormal ("Weight Normal", Float) = 0
		_WeightBold ("Weight Bold", Float) = 0.5
		_ShaderFlags ("Flags", Float) = 0
		_ScaleRatioA ("Scale RatioA", Float) = 1
		_ScaleRatioB ("Scale RatioB", Float) = 1
		_ScaleRatioC ("Scale RatioC", Float) = 1
		_MainTex ("Font Atlas", 2D) = "white" {}
		_TextureWidth ("Texture Width", Float) = 512
		_TextureHeight ("Texture Height", Float) = 512
		_GradientScale ("Gradient Scale", Float) = 5
		_ScaleX ("Scale X", Float) = 1
		_ScaleY ("Scale Y", Float) = 1
		_PerspectiveFilter ("Perspective Correction", Range(0, 1)) = 0.875
		_Sharpness ("Sharpness", Range(-1, 1)) = 0
		_VertexOffsetX ("Vertex OffsetX", Float) = 0
		_VertexOffsetY ("Vertex OffsetY", Float) = 0
		_ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
		_MaskSoftnessX ("Mask SoftnessX", Float) = 0
		_MaskSoftnessY ("Mask SoftnessY", Float) = 0
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_CullMode ("Cull Mode", Float) = 0
		_ColorMask ("Color Mask", Float) = 15
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

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	//CustomEditor "Spyke.SpykeFeatureLib.Common.Editor.MultiLayerTextShaderGUI"
}