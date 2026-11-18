Shader "HueRelay/Canvas"
{
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 local:TEXCOORD0;float2 halfSize:TEXCOORD1;float3 shape:NORMAL;};
            struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 local:TEXCOORD0;float4 shape:TEXCOORD1;float kind:TEXCOORD2;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.local=v.local;o.shape=float4(v.halfSize,v.shape.xy);o.kind=v.shape.z;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float4 c=i.color;
                if(i.kind>.5)
                {
                    float inner=max(0,i.shape.x-i.shape.y*.3);
                    c.a*=saturate((i.shape.x+i.shape.y-length(i.local))/(i.shape.x+i.shape.y-inner));
                }
                else if(i.shape.w>0)
                {
                    float2 q=abs(i.local)-i.shape.xy+i.shape.z;
                    float distance=length(max(q,0))+min(max(q.x,q.y),0)-i.shape.z;
                    float x=distance/(i.shape.w*1.41421356237),a=abs(x),t=1/(1+.3275911*a);
                    float erf=sign(x)*(1-(((((1.061405429*t-1.453152027)*t)+1.421413741)*t-.284496736)*t+.254829592)*t*exp(-a*a));
                    c.a*=saturate(.5*(1-erf));
                }
                return c;
            }
            ENDCG
        }
    }
}
