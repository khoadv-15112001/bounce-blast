Shader "Custom/FlowingZigzagLightning_Optimized" {
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_NoiseTex ("Noise Texture", 2D) = "white" {}
		_Color ("Lightning Core Color", Vector) = (0.8,1,1,1)
		_GlowColor ("Outer Glow Color", Vector) = (0.3,0.8,1,0.6)
		_CoreBoostColor ("Core Brightness Boost Color", Vector) = (1,1,1,1)
		[Toggle] _FlowZigzag ("Flow Zigzag Pattern", Float) = 1
		_FlowSpeed ("Zigzag Flow Speed", Range(0, 10)) = 1.5
		_AnimSpeed ("Zigzag Shape Speed", Range(0, 10)) = 0.5
		_TimeScale ("Global Time Scale", Range(0, 5)) = 1
		_Thickness ("Lightning Core Thickness", Range(0.0001, 0.1)) = 0.008
		_ThicknessVariation ("Thickness Variation Amount", Range(0, 1)) = 0.2
		_ThicknessVariationSpeed ("Thickness Variation Speed", Range(0, 10)) = 1
		_ThicknessVariationScale ("Thickness Variation Scale", Range(1, 50)) = 10
		_ThicknessTaper ("Thickness Taper (Ends Thinner)", Range(0, 1)) = 0
		_ZigzagAmount ("Zigzag Amount", Range(0, 1)) = 0.1
		_ZigzagFrequency ("Zigzag Frequency", Range(1, 50)) = 12
		_ZigzagOctaves ("Zigzag Detail Layers", Range(1, 5)) = 3
		_PathSmoothness ("Path Smoothness", Range(0, 1)) = 0.5
		_GlowSize ("Glow Radius", Range(0.01, 1)) = 0.12
		_GlowSoftness ("Glow Softness", Range(0.1, 5)) = 1.5
		_GlowIntensity ("Glow Intensity", Range(0, 5)) = 0.8
		_InnerGlowWeight ("Inner Glow Weight", Range(0, 2)) = 0.8
		_MidGlowWeight ("Mid Glow Weight", Range(0, 2)) = 0.5
		_OuterGlowWeight ("Outer Glow Weight", Range(0, 2)) = 0.3
		_Intensity ("Overall Brightness", Range(0, 20)) = 4
		_CoreBoost ("Core Brightness Boost", Range(0, 10)) = 2.5
		_CoreFalloff ("Core Falloff", Range(0.1, 5)) = 3
		_BranchAmount ("Branch Amount", Range(0, 2)) = 0.3
		_BranchCount ("Branch Count", Range(0, 5)) = 3
		_BranchLength ("Branch Length", Range(0, 0.3)) = 0.08
		_BranchThickness ("Branch Thickness Multiplier", Range(1, 5)) = 2.5
		_BranchSpeed ("Branch Animation Speed", Range(0, 5)) = 0.2
		_FlickerSpeed ("Flicker Speed", Range(0, 20)) = 3
		_FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.25
		_FlickerOctaves ("Flicker Complexity", Range(1, 3)) = 2
		_PulseSpeed ("Pulse Speed", Range(0, 10)) = 0.5
		_PulseAmount ("Pulse Amount", Range(0, 1)) = 0
		_Smoothness ("Edge Smoothness", Range(0.1, 10)) = 2
		_DistanceFade ("Distance Fade", Range(0, 1)) = 0
		_NoiseScale1 ("Primary Noise Scale", Range(0.1, 5)) = 1
		_NoiseScale2 ("Secondary Noise Scale", Range(0.1, 5)) = 1.5
		_NoiseScale3 ("Detail Noise Scale", Range(0.1, 10)) = 4
		_StartPoint ("Start Point XY", Vector) = (0,0.5,0,0)
		_EndPoint ("End Point XY", Vector) = (1,0.5,0,0)
		_Reveal ("Reveal", Range(0, 1)) = 1
		_RevealSoftness ("Reveal Head Softness", Range(0.001, 0.3)) = 0.06
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
	Fallback "Transparent/Diffuse"
}