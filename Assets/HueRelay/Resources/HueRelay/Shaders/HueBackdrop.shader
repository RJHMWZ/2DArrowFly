Shader "HueRelay/Backdrop"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        GrabPass { "_HueBackdrop" }
        Pass
        {
            ZWrite Off Cull Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _HueBackdrop;float4 _HueBackdrop_TexelSize;float _HueBlurPixels;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float4 grab:TEXCOORD0;float4 color:COLOR;};
            v2f vert(appdata v) {v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.grab=ComputeGrabScreenPos(o.vertex);o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.grab.xy/i.grab.w;float2 d=_HueBackdrop_TexelSize.xy*_HueBlurPixels;float3 c=0;float sum=0;
                [unroll]for(int x=-2;x<=2;x++)[unroll]for(int y=-2;y<=2;y++){float w=exp(-(x*x+y*y)*.5);c+=tex2D(_HueBackdrop,uv+d*float2(x,y)).rgb*w;sum+=w;}
                c=c/sum;c=lerp(c,i.color.rgb,i.color.a);return fixed4(c,1);
            }
            ENDCG
        }
    }
}
