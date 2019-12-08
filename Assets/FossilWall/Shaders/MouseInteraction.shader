Shader "Hidden/MouseInteraction"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
	}
	CGINCLUDE

	#include "UnityCG.cginc"

	sampler2D _MainTex;

	float4 _InputParams;
	float  _AspectRatio;

	half frag(v2f_img i) : SV_Target
	{
		float2 pos = i.uv.xy - _InputParams.xy;
		pos.x *= _AspectRatio;
		float  mag = pos.x * pos.x + pos.y * pos.y;
		float  rad2 = _InputParams.z *_InputParams.z;

		float amount = exp(-mag / rad2) * _InputParams.w;
		return saturate(tex2D(_MainTex, i.uv.xy).r + amount);
	}

	ENDCG

    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

		Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
			ENDCG
        }
    }
}
