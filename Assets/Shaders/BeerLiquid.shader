// Bar del Piu - líquido y espuma pixel art para el vaso.
// Se aplica a un SpriteRenderer con glass_mask (textura sin atlas: UV 0..1).
// _Liquid y _Foam son fracciones de la capacidad interior del vaso.
Shader "BarDelPiu/BeerLiquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Máscara interior", 2D) = "white" {}
        _Liquid ("Líquido", Range(0, 1)) = 0.5
        _Foam ("Espuma", Range(0, 1)) = 0.15
        _LiquidLight ("Líquido claro", Color) = (0.96, 0.78, 0.30, 1)
        _LiquidDark ("Líquido oscuro", Color) = (0.85, 0.63, 0.19, 1)
        _FoamColor ("Espuma", Color) = (1, 0.97, 0.92, 1)
        _FoamShade ("Sombra de espuma", Color) = (0.91, 0.86, 0.76, 1)
        _MaskSize ("Tamaño de la máscara (px)", Vector) = (16, 24, 0, 0)
        _InnerBottom ("Fila interior inferior (px)", Float) = 3
        _InnerTop ("Fila interior superior (px)", Float) = 22
        _Wobble ("Oleaje (px)", Range(0, 2)) = 0
        _BubbleRate ("Burbujas", Range(0, 1)) = 0.5
        _Target ("Nivel objetivo", Range(0, 1)) = 0.8
        _ShowTarget ("Mostrar objetivo", Float) = 0
        _Flash ("Destello", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskSize;
                float _Liquid;
                float _Foam;
                half4 _LiquidLight;
                half4 _LiquidDark;
                half4 _FoamColor;
                half4 _FoamShade;
                float _InnerBottom;
                float _InnerTop;
                float _Wobble;
                float _BubbleRate;
                float _Target;
                float _ShowTarget;
                float _Flash;
            CBUFFER_END

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                if (mask < 0.5) return 0;

                float2 px = floor(i.uv * _MaskSize.xy);
                float innerH = _InnerTop - _InnerBottom + 1;
                float row = px.y - _InnerBottom;
                float t = _Time.y;

                float liquidPx = round(saturate(_Liquid) * innerH);
                float foamPx = round(saturate(_Foam) * innerH);
                foamPx = min(foamPx, innerH - liquidPx);

                // Oleaje por columna, cuantizado a píxeles enteros
                float wave = round(sin(px.x * 0.9 + t * 9.0) * _Wobble);
                float liqTop = liquidPx > 0 ? max(1, liquidPx + wave) : 0;
                float bump = (foamPx > 1 && Hash(px.x + floor(t * 1.5) * 7.0) > 0.65) ? 1 : 0;
                float foamTop = liqTop + foamPx + (foamPx > 0 ? bump : 0);
                foamTop = min(foamTop, innerH);

                half4 col = 0;
                if (row < liqTop)
                {
                    // Dos tonos con borde tramado y brillo en la superficie
                    float band = liqTop * 0.45;
                    bool dither = abs(row - band) < 0.5 && fmod(px.x + px.y, 2) < 1;
                    col = (row < band || dither) ? _LiquidDark : _LiquidLight;
                    if (row >= liqTop - 1 && foamPx < 1) col = lerp(col, half4(1, 1, 1, 1), 0.3);

                    // Burbujas que suben por columnas
                    float colSeed = Hash(px.x * 3.17 + 1.3);
                    if (colSeed < _BubbleRate * 0.6 && liqTop > 2)
                    {
                        float speed = 3.0 + colSeed * 6.0;
                        float y = floor(frac(colSeed * 5.0 + t * speed / max(liqTop, 1)) * liqTop);
                        if (row == y) col = lerp(col, half4(1, 1, 1, 1), 0.45);
                    }
                }
                else if (row < foamTop)
                {
                    bool bottomRow = row < liqTop + 1;
                    bool speck = Hash(px.x * 7.1 + px.y * 3.3) > 0.8;
                    col = (bottomRow || speck) ? _FoamShade : _FoamColor;
                }

                // Línea objetivo punteada (tutorial)
                if (_ShowTarget > 0.5 && row == round(_Target * innerH) - 1 && fmod(px.x, 2) < 1)
                {
                    col = col.a > 0 ? lerp(col, half4(1, 1, 1, 1), 0.6) : half4(1, 1, 1, 0.55);
                }

                col.rgb = lerp(col.rgb, 1, _Flash);
                return col * i.color;
            }
            ENDHLSL
        }
    }
}
