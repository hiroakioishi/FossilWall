Shader "Unlit/ParticleRender"
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
		float4 targetPosition;
		float4 rotation;
		float4 color;
		float  scale;
		int    id;
		int    alive;
		int    isFossil;
		float  valueToPeel;
		int    isPeeled;
		float  timerFromPeeled;
	};

	struct v2g
	{
		float4 position : POSITION_SV;
		float4 rotation : TEXCOORD1;
		float  scale    : TEXCOORD0;
	};

	struct g2f
	{
		float4 position : POSITION;
		float2 uv       : TEXCOORD1;
	};

	StructuredBuffer<ParticleData> _ParticleBuffer;

	float _VoxelSize;

	sampler2D _MainTex;
	float4 _MainTex_ST;

	// --------------------------------------------------------------------
	// Vertex Shader
	// --------------------------------------------------------------------
	v2g vert(uint id : SV_VertexID)
	{
		v2g o = (v2g)0;
		ParticleData p = _ParticleBuffer[id];
		o.position = float4(p.position.xyz, 1.0);
		o.rotation = p.rotation.xyzw;
		o.scale = p.scale * 0.5;
		return o;
	}

	// --------------------------------------------------------------------
	// Geometry Shader
	// --------------------------------------------------------------------
	static const float3 g_positions[4] =
	{
		float3(-1, 1, 0),
		float3(1, 1, 0),
		float3(-1,-1, 0),
		float3(1,-1, 0),
	};
	static const float2 g_texcoords[4] =
	{
		float2(0, 1),
		float2(1, 1),
		float2(0, 0),
		float2(1, 0),
	};

	[maxvertexcount(4)]
	void geom(point v2g In[1], inout TriangleStream<g2f> SpriteStream)
	{
		g2f o = (g2f)0;
		
		float3 vertpos = In[0].position.xyz;
		float  scale = In[0].scale * 0.25;
		[unroll]
		for (int i = 0; i < 4; i++)
		{
			float3 pos = g_positions[i] * _VoxelSize * scale;
			pos = mul(unity_CameraToWorld, pos) + vertpos;
			o.position = UnityObjectToClipPos(float4(pos, 1.0));
			o.uv = g_texcoords[i];
			// o.vpos = UnityObjectToViewPos(float4(pos, 1.0)).xyz * float3(1, 1, 1);
			// o.size = _ParticleSize;

			SpriteStream.Append(o);
		}
		SpriteStream.RestartStrip();
	}

	fixed4 frag(g2f i) : SV_Target
	{
		fixed4 col = tex2D(_MainTex, i.uv);
		col = fixed4(i.uv.xy, 0.5, 1.0);
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
			#pragma geometry geom
            #pragma fragment frag
            ENDCG
        }
    }
}
