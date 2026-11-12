Shader "Spyke/Rainbow" {
	Properties {
		[HideInInspector] [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Center Color", Vector) = (1,1,1,1)
		_C1 ("C1", Vector) = (1,1,1,1)
		_C2 ("C2", Vector) = (1,1,1,1)
		_C3 ("C3", Vector) = (1,1,1,1)
		_C4 ("C4", Vector) = (1,1,1,1)
		_C5 ("C5", Vector) = (1,1,1,1)
		_C6 ("C6", Vector) = (1,1,1,1)
		_CenterLine ("Center Line", Range(0, 0.025)) = 0.015
		_Roundness ("Roundness", Range(0, 0.25)) = 0.1
		_Padding ("Padding", Range(0.05, 0.35)) = 0.1
		_GlowPower ("Glow Power", Range(3, 1)) = 1
		_GlowRange ("Glow Range", Range(0, 0.3)) = 0.12
		[HideInInspector] [PerRendererData] _Size ("Size", Vector) = (1,1,1,1)
		[MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
		[HideInInspector] _RendererColor ("RendererColor", Vector) = (1,1,1,1)
		[HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
		[HideInInspector] [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
		[HideInInspector] [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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