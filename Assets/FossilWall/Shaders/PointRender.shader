Shader "Unlit/PointRender"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
	CGINCLUDE

	#include "UnityCG.cginc"

	struct ParticleData
	{
		float4 velocity;
		float4 position;
		float4 rotation;
		float  scale;
		int    id;
		int    alive;
		float  timerFromPeeled;
	};

	struct v2f
	{
		float4 position : SV_POSITION;
	//	float2 uv       : TEXCOORD1;
	};

	StructuredBuffer<ParticleData> _ParticleBuffer;

	
	sampler2D _MainTex;
	float4 _MainTex_ST;

	v2f vert(uint id : SV_VertexID)
	{
		v2f o = (v2f)0;
		ParticleData p = _ParticleBuffer[id];
		o.position = UnityObjectToClipPos(float4(p.position.xyz, 1.0));
		return o;
	}
	
	fixed4 frag(v2f i) : SV_Target
	{
		//fixed4 col = tex2D(_MainTex, i.uv);
		//col = fixed4(i.uv.xy, 0.5, 1.0);
		fixed4 col = fixed4(1, 1, 0, 1);
		return col;
	}
	ENDCG

    SubShader
    {
		Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		LOD 100

		ZWrite Off
		Blend One One

        Pass
        {
            CGPROGRAM
			#pragma target   5.0
			#pragma vertex   vert
			#pragma fragment frag
            ENDCG
        }
    }
}
