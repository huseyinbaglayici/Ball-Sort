// One layer of the orb art (Art/Sprites/Orb): a plain sprite, drawn premultiplied. The blend comes from the material:
// OrbLayer (One, OneMinusSrcAlpha) for the normal layers, OrbLayerAdditive (One, One) for the glass reflection.
//
// Portal mask: while a ball passes a warp portal (BallItem.SetPortalSplit), it is drawn only below its ring's lowest
// edge: the lower arc of the ellipse at the bottom of the ring's side wall, the ring's silhouette. The ring draws over
// the ball, so the ball goes in, and comes out, exactly along that edge, never showing on or inside the ring. The
// edge follows the ring while it opens and closes; beside the ring nothing is hidden.
// _PortalCut: x = that ellipse's center y, y = its center x, z = its half width, w = its half height (world space).
// The default edge is far above everything, so nothing is hidden.
//
// Event horizon: the parts of a ball going into the source portal and coming out of the target one (_PortalHeat 1)
// glow at the ring's edge instead of just slipping behind it. Over a band just under the edge its light is taken over by the hole's own
// ramp: white-gold, then magenta, then the ring wall's dark violet right at the edge, so the ball reads as pulled into
// the hole, and comes out of it the same way. Nothing of it ever shows in the void. _PortalHeat scales the band's
// height (1 going in, 2 coming out, so it reads at game size); 0 turns it off.
Shader "Project/Orb Layer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
        [PerRendererData] _PortalCut ("Portal edge (x center y, y center x, z half width, w half height)", Vector) = (100000, 0, 1, 0)
        [PerRendererData] _PortalHeat ("Event horizon glow at the portal edge (0 off, else its height)", Float) = 0
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
        Blend One [_DstBlend]

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
                float2 world : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _PortalCut;
            float _PortalHeat;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // World position, also right for batched sprites (their vertices are already in world space).
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;
                fixed3 rgb = tex.rgb;

                // The ring's lowest edge at this column: the lower arc of its bottom ellipse. Beside the ring (outside its
                // width, e.g. while it is still opening) nothing is hidden.
                float u = (i.world.x - _PortalCut.y) / max(_PortalCut.z, 1e-5);
                float edge = _PortalCut.x - _PortalCut.w * sqrt(saturate(1 - u * u));
                float dy = edge - i.world.y;
                float aa = max(fwidth(dy), 1e-5);
                float au = max(fwidth(u), 1e-5);
                fixed beside = smoothstep(1 - au, 1 + au, abs(u));
                fixed visible = max(smoothstep(-aa, aa, dy), beside);

                // Event horizon: 0 a band's height under the edge, 1 on it; the band scales with the ring. The light
                // there is taken over smoothly (no stripes): first a hot white-gold glow added on, then it reddens to
                // magenta and sinks into the wall's dark violet right at the edge.
                fixed burn = step(1e-3, _PortalHeat) * (1 - beside)
                    * (1 - saturate(dy / max(0.3 * _PortalCut.z * _PortalHeat, 1e-5)));
                fixed3 heat = lerp(lerp(fixed3(1.0, 0.85, 0.55), fixed3(0.85, 0.25, 0.75), smoothstep(0.2, 0.75, burn)),
                    fixed3(0.08, 0.03, 0.2), smoothstep(0.65, 1, burn));
                #ifndef UNITY_COLORSPACE_GAMMA
                heat = GammaToLinearSpace(heat);
                #endif
                rgb = saturate(rgb + fixed3(1.0, 0.8, 0.5) * 0.35 * smoothstep(0, 0.5, burn) * (1 - burn));
                rgb = lerp(rgb, heat, burn * burn);

                fixed alpha = tex.a * visible;
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
