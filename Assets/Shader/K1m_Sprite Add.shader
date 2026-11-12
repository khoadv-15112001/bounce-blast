Shader "K1m/Sprite Add" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture (unused)", 2D) = "white" {}
		_Color ("Tint", Vector) = (1,1,1,1)
		[NoScaleOffset] _PackedTex ("Packed Texture", 2D) = "black" {}
		[Header(Wave)] _WaveSpeed ("Wave Speed (X, Y)", Vector) = (0.02,-0.015,0,0)
		[Header(Layer A)] _LayerAScale ("A Scale", Float) = 1
		_LayerASpeed ("A Speed (X, Y)", Vector) = (0.03,0.017,0,0)
		[Header(Layer B)] _LayerBScale ("B Scale", Float) = 0.75
		_LayerBSpeed ("B Speed (X, Y)", Vector) = (-0.021,0.026,0,0)
		[Header(Color)] _ColorLow ("Color Low", Vector) = (0,0,0,1)
		_ColorMid ("Color Mid", Vector) = (0.07,0.28,0.39,1)
		_ColorHigh ("Color High", Vector) = (0.6,0.85,0.95,1)
		_MidPosition ("Mid Position", Range(0.01, 0.99)) = 0.5
		[Header(Options)] [Toggle(_WORLDUV_ON)] _WorldUV ("World Space UV", Float) = 0
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
	Fallback "Sprites/Default"
}