// The colored sphere of an orb: richer color, sphere shading, a soft highlight and a dark edge line in its own tone.
Shader "Project/Orb Core"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Radius ("Sphere radius in sprite uv", Float) = 0.293
        _Saturation ("Saturation", Float) = 1.45
        _Contrast ("Contrast", Float) = 1.08
        _ShadeFloor ("Shade floor", Range(0, 1)) = 0.9
        _ShadeGain ("Shade gain", Range(0, 1)) = 0.12
        _LightDir ("Light direction", Vector) = (-0.55, 0.65, 0.52, 0)
        _RimLight ("Rim light", Range(0, 1)) = 0.3
        _EdgeGlow ("Edge glow", Range(0, 1)) = 0.25
        _BottomShade ("Bottom shade", Range(0, 1)) = 0.22
        [PerRendererData] _Shade ("Brightness scale", Float) = 1
        [PerRendererData] _Sat ("Saturation scale", Float) = 1
        _Specular ("Highlight", Range(0, 1)) = 0.22
        _SpecularPos ("Highlight position", Vector) = (-0.38, 0.42, 0.045, 0)
        _OutlineStart ("Edge line start", Range(0.5, 1)) = 0.9
        _OutlineWidth ("Edge line width", Range(0.01, 0.3)) = 0.08
        _OutlineTone ("Edge line tone", Range(0, 1)) = 0.42
        [PerRendererData] _PortalCut ("Portal edge", Vector) = (100000, 0, 1, 0)
        [PerRendererData] _PortalHeat ("Portal heat", Float) = 0
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
            #pragma vertex OrbVert
            #pragma fragment frag
            #include "OrbCommon.cginc"

            float _Radius, _Saturation, _Contrast, _ShadeFloor, _ShadeGain, _RimLight, _EdgeGlow, _BottomShade, _Specular;
            float _OutlineStart, _OutlineWidth, _OutlineTone, _Shade, _Sat;
            float4 _LightDir, _SpecularPos;

            fixed4 frag(OrbV2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;

                // Sphere normal from the sprite: the core is a centered circle.
                float2 n = (i.uv - 0.5) / _Radius;
                float r = length(n);
                float nz = sqrt(saturate(1 - saturate(r) * saturate(r)));

                fixed lum = dot(tex.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 rgb = saturate(lum + (tex.rgb - lum) * _Saturation * _Sat);
                rgb = saturate((rgb - 0.5) * _Contrast + 0.5) * _Shade;
                fixed3 vivid = rgb;

                float3 light = normalize(_LightDir.xyz);
                float ndl = saturate(dot(float3(n, nz), light));
                rgb *= _ShadeFloor + _ShadeGain * ndl;

                float rim = pow(saturate((r - 0.62) / 0.38), 2) * saturate(-n.x * 0.6 + n.y * 0.8);
                rgb = saturate(rgb + rim * fixed3(0.9, 0.95, 1.0) * _RimLight);
                float bottom = pow(saturate((r - 0.55) / 0.45), 1.5) * saturate(-n.y);
                rgb *= 1 - _BottomShade * bottom;

                // A soft lit band just inside the edge line, in the color's own lighter tone.
                float band = smoothstep(0.62, 0.84, r) * (1 - smoothstep(0.86, 0.92, r));
                rgb = saturate(rgb + band * _EdgeGlow * (vivid * 0.6 + 0.4));

                float2 d = n - _SpecularPos.xy;
                float spec = exp(-dot(d, d) / _SpecularPos.z);
                rgb = saturate(rgb + spec * _Specular);

                float edgeLine = saturate((r - _OutlineStart) / _OutlineWidth);
                rgb = lerp(rgb, vivid * _OutlineTone, edgeLine);

                return OrbPortal(rgb, tex.a, i.world);
            }
            ENDCG
        }
    }
}
