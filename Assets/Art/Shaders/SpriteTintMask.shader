// Recolors the orb for any ball color. The texture is a premultiplied mask made from the art team's finished ball
// (see BoardPrefabBuilder): R is the white light the pixel adds, G how much of the art's base color it adds, A how much
// of what is behind it the pixel covers. The color is rebuilt as white + G * tint, where tint is the SpriteRenderer
// color, so highlights stay white, the body takes the tint, and the halo (light where A is 0) glows over the background.
// _Intensity fades the whole ball.
//
// Portal mask: while a ball passes a warp portal (BallItem.SetPortalSplit), it is drawn only below its ring's lowest
// edge: the lower arc of the ellipse at the bottom of the ring's side wall, the ring's silhouette. The ring draws over
// the ball, so the ball goes in, and comes out, exactly along that edge, never showing on or inside the ring. The
// edge follows the ring while it opens and closes; beside the ring nothing is hidden.
// _PortalCut: x = that ellipse's center y, y = its center x, z = its half width, w = its half height (world space).
// The default edge is far above everything, so nothing is hidden.
Shader "Project/Sprite Tint Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask (R white, G tint, A cover)", 2D) = "black" {}
        _Intensity ("Intensity", Range(0, 1)) = 1
        [PerRendererData] _PortalCut ("Portal edge (x center y, y center x, z half width, w half height)", Vector) = (100000, 0, 1, 0)
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
                float2 world : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed _Intensity;
            float4 _PortalCut;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // World position, also right for batched sprites (their vertices are already in world space).
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.uv = v.uv;
                o.color = v.color;
                // The mask was made from gamma-space art, so the tint is mixed in gamma space too.
                #ifndef UNITY_COLORSPACE_GAMMA
                o.color.rgb = LinearToGammaSpace(o.color.rgb);
                #endif
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mask = tex2D(_MainTex, i.uv);
                fixed3 rgb = saturate(mask.r + mask.g * i.color.rgb);
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
                #endif

                // The ring's lowest edge at this column: the lower arc of its bottom ellipse. Beside the ring (outside its
                // width, e.g. while it is still opening) nothing is hidden.
                float u = (i.world.x - _PortalCut.y) / max(_PortalCut.z, 1e-5);
                float edge = _PortalCut.x - _PortalCut.w * sqrt(saturate(1 - u * u));
                float dy = edge - i.world.y;
                float aa = max(fwidth(dy), 1e-5);
                float au = max(fwidth(u), 1e-5);
                fixed beside = smoothstep(1 - au, 1 + au, abs(u));
                fixed visible = max(smoothstep(-aa, aa, dy), beside);

                fixed fade = i.color.a * _Intensity * visible;
                return fixed4(rgb * fade, mask.a * fade);
            }
            ENDCG
        }
    }
}
