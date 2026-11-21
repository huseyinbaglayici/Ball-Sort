// Front glass of a tube: a dark cylinder with a thin edge line, inner glow, rim light and a faint reflection streak.
Shader "Project/Tube Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        // Atlas uv rect of the sprite (min u, min v, 1 / width, 1 / height), set per renderer.
        [PerRendererData] _UvRect ("Sprite uv rect", Vector) = (0, 0, 1, 1)
        _HalfWidth ("Glass half width in sprite u", Float) = 0.427
        _ArtGain ("Art highlight gain", Range(0, 2)) = 0.5
        _EdgeColor ("Edge line", Color) = (0.62, 0.95, 1, 1)
        _EdgeWidth ("Edge line width", Range(0, 0.2)) = 0.035
        _EdgeGain ("Edge line strength", Range(0, 2)) = 1.7
        _GlowColor ("Inner glow", Color) = (0.55, 0.5, 1, 1)
        _GlowFalloff ("Inner glow falloff", Range(1, 24)) = 10
        _GlowGain ("Inner glow strength", Range(0, 2)) = 0.5
        _FresnelPower ("Cylinder fresnel power", Range(0.5, 8)) = 2.5
        _FresnelDark ("Edge darkening", Range(0, 1)) = 0.55
        _RimColor ("Rim light", Color) = (0.6, 0.8, 1, 1)
        _RimGain ("Rim light strength", Range(0, 2)) = 0.3
        _StreakColor ("Reflection streak", Color) = (0.8, 0.9, 1, 1)
        _StreakPos ("Streak position (-1..1)", Range(-1, 1)) = -0.55
        _StreakWidth ("Streak width", Range(0.01, 0.5)) = 0.08
        _StreakGain ("Streak strength", Range(0, 1)) = 0.3
        _StreakDrift ("Streak drift speed", Range(0, 1)) = 0
        _InteriorDark ("Interior darkening", Range(0, 1)) = 0.3
        _EndFade ("Mouth and base fade in sprite v", Range(0, 0.3)) = 0.14
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"
            "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                // Sprite-local x in -1..1 across the glass, and local v in 0..1.
                float2 local : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _UvRect;
            float _HalfWidth, _ArtGain, _EdgeWidth, _EdgeGain, _GlowFalloff, _GlowGain;
            float _FresnelPower, _FresnelDark, _RimGain, _StreakPos, _StreakWidth, _StreakGain, _StreakDrift;
            float _InteriorDark, _EndFade;
            fixed4 _EdgeColor, _GlowColor, _RimColor, _StreakColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                float2 local = (v.uv - _UvRect.xy) * _UvRect.zw;
                o.local = float2((local.x - 0.5) / _HalfWidth, local.y);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float x = i.local.x;
                float ax = abs(x);
                float aa = max(fwidth(ax), 1e-4);
                // Rows near the mouth and base belong to the curved ends; the straight-wall light fades there.
                float wall = smoothstep(0, _EndFade, i.local.y) * smoothstep(1, 1 - _EndFade, i.local.y);

                // Cylinder: the normal turns away from the viewer toward the edges.
                float nz = sqrt(saturate(1 - x * x));
                float fres = pow(1 - nz, _FresnelPower);

                float edge = smoothstep(1 - _EdgeWidth - aa, 1 - _EdgeWidth + aa, ax);
                float glow = pow(saturate(ax), _GlowFalloff) * (1 - edge);
                float rim = smoothstep(1 - _EdgeWidth * 3, 1 - _EdgeWidth, ax) * (1 - edge);
                float pos = _StreakPos + sin(_Time.y * _StreakDrift) * 0.15;
                float d = (x - pos) / _StreakWidth;
                float streak = exp(-d * d) * wall;

                fixed3 light = tex.rgb * _ArtGain
                    + _EdgeColor.rgb * (edge * _EdgeGain)
                    + _GlowColor.rgb * (glow * _GlowGain)
                    + _RimColor.rgb * (rim * _RimGain)
                    + _StreakColor.rgb * (streak * _StreakGain);
                // Renderer color: rgb is the glass tint, alpha the share this copy draws.
                light *= i.color.rgb;
                float dark = saturate(_InteriorDark + fres * _FresnelDark) * (1 - edge);

                float alpha = tex.a * i.color.a;
                return fixed4(light * alpha, dark * alpha);
            }
            ENDCG
        }
    }
}
