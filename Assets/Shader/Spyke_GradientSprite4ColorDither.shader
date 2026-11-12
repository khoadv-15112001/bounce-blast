Shader "Spyke/GradientSprite4ColorDither" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_StartColor ("Start Color", Vector) = (1,1,1,1)
		_SecondColor ("Second Color", Vector) = (1,1,1,1)
		_ThirdColor ("Third Color", Vector) = (1,1,1,1)
		_EndColor ("End Color", Vector) = (1,1,1,1)
		[Range(0,1)] _Point1 ("Point 1 (0=near start, 1=near end)", Float) = 0.33
		[Range(0,1)] _Point2 ("Point 2 (>=Point1)", Float) = 0.66
		[KeywordEnum(Vertical, Horizontal)] _ORIENT ("Orientation", Float) = 0
		[Range(0,1)] _DitherStrength ("Dither Strength", Float) = 0.35
		[MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
		_Color ("Tint", Vector) = (1,1,1,1)
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