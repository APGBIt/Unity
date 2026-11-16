// Endereza la imagen de la cámara (giro y espejo) en una sola pasada de GPU.
// _Transformacion = (m00, m01, m10, m11): matriz 2×2 que aplica TransformacionCamara.cs
// sobre la coordenada centrada de salida para obtener la coordenada de entrada.
Shader "Hidden/LSRD/CamaraNormalizada"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Transformacion ("Transformacion", Vector) = (1, 0, 0, 1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Transformacion;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float2 s = float2(dot(_Transformacion.xy, c), dot(_Transformacion.zw, c)) + 0.5;
                return tex2D(_MainTex, s);
            }
            ENDCG
        }
    }
    Fallback Off
}
