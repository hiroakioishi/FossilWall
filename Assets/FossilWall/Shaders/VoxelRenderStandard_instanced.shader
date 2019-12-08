Shader "Hidden/VoxelFossilWall/VoxelRenderStandard_instanced"
{
    Properties
    {
        _Color      ("Color", Color) = (1,1,1,1)
        _MainTex    ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic   ("Metallic", Range(0,1)) = 0.0

		_ColorA ("Color A", Color) = (0,0,0,0)
		_ColorB ("Color B", Color) = (1,1,1,1)

		_ColorAIntensity ("Color A Intensity", Float) = 1.0
		_ColorBIntensity ("Color B Intensity", Float) = 1.0

		_ColorTex ("Color Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
		#pragma instancing_options procedural:setup
        // #pragma target 3.0

        sampler2D _MainTex;

		sampler2D _ColorTex;

        struct Input
        {
            float2 uv_MainTex;
			float4 color : Color;
        };

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

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		StructuredBuffer<ParticleData> _ParticleBuffer;
		#endif

		float _VoxelSize;

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

		fixed4 _ColorA;
		fixed4 _ColorB;

		float _ColorAIntensity;
		float _ColorBIntensity;

		// Quaterion to Rotation Matrix4x4
		float4x4 quaternion_to_rotation_matrix(float4 q)
		{
			float n = 1.0 / sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
			q.x *= n;
			q.y *= n;
			q.z *= n;
			q.w *= n;

			return float4x4(
				1.0 - 2.0 * q.y * q.y - 2.0 * q.z * q.z, 2.0 * q.x * q.y - 2.0 * q.z * q.w, 2.0 * q.x * q.z + 2.0 * q.y * q.w, 0.0,
				2.0 * q.x * q.y + 2.0 * q.z * q.w, 1.0 - 2.0 * q.x * q.x - 2.0 * q.z * q.z, 2.0 * q.y * q.z - 2.0 * q.x * q.w, 0.0,
				2.0 * q.x * q.z - 2.0 * q.y * q.w, 2.0 * q.y * q.z + 2.0 * q.x * q.w, 1.0 - 2.0 * q.x * q.x - 2.0 * q.y * q.y, 0.0,
				0.0 , 0.0, 0.0, 1.0
			);
		}

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

		void vert(inout appdata_full v)
		{
			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

			ParticleData pd = _ParticleBuffer[unity_InstanceID];

			float3 pos = pd.position.xyz;
			float3 scl = _VoxelSize * pd.scale;
			float4 rot = pd.rotation;
			float4 col = pd.color.r > 0.5 ? _ColorA * _ColorAIntensity : _ColorB * _ColorBIntensity;
			
			float4x4 object2world = (float4x4)0;
			object2world._11_22_33_44 = float4(scl.xyz, 1.0);
			float4x4 rotMatrix = quaternion_to_rotation_matrix(rot);
			object2world = mul(rotMatrix, object2world);
			object2world._14_24_34 += pos.xyz;

			v.vertex = mul(object2world, v.vertex);
			v.normal = normalize(mul(object2world, v.normal));
			v.color = col;
#endif
		}

		void setup(){}

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = IN.color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
