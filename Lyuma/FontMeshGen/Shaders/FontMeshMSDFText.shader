// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)
// Modified to support MSDF fonts - Merlin, also MIT license

Shader "FontMeshGen/MSDFText"
{
    Properties
    {
        _AltTex ("Alternate Graphics", 2D) = "white" {}
        [HDR]_Color ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0,0,0,1)

        _FadeDistance ("Fade Distance", Float) = 0
        _FadeOpacity ("Fade Opacity", Range(0, 1)) = 0

		[NoScaleOffset]_MSDFTex("MSDF Texture", 2D) = "black" {}
		[HideInInspector]_PixelRange("Pixel Range", Float) = 4.0
        _Thickness("Thickness", Range(0, 2)) = 1
        _Sharpness("Sharpness", Range(-1, 1)) = 0
        _LineCutoff("Line Cutoff", Float) = -1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
        _DisableMSDF ("Disable MSDF", Range(0,1)) = 0
        _ShowWireframe ("Show Wireframe", Float) = 0

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent-1"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "DisableBatching"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual // [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Target 3.5 for centroid support on OpenGL ES
            #pragma target 5.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float3 normal   : NORMAL;
                float4 tangent   : TANGENT;
                float4 color    : COLOR;
                float4 texcoord : TEXCOORD0;
                float4 texcoord3 : TEXCOORD2;
                float4 texcoord4 : TEXCOORD3;
                float4 shadowColor : TEXCOORD4;
                float4 barycentricColor : TEXCOORD5;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 texcoord3 : TEXCOORD1;
                float4 texcoord4 : TEXCOORD2;
                float4 shadowColor : TEXCOORD3;
                float4 barycentricColor : TEXCOORD4;
                // float3 normal : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Color;
            float4 _ShadowColor;
            float4 _ClipRect;
			sampler2D _MSDFTex; float4 _MSDFTex_TexelSize;
            sampler2D _AltTex; float4 _AltTex_TexelSize;
			float _PixelRange;
            float _Thickness;
            float _Sharpness;
            float _LineCutoff;
            float _FadeDistance;
            float _FadeOpacity;

            float _DisableMSDF, _ShowWireframe;
            #ifdef USING_STEREO_MATRICES
            static float3 centerEyePosWld = lerp(unity_StereoWorldSpaceCameraPos[0], unity_StereoWorldSpaceCameraPos[1], 0.5);
            #else
            static float3 centerEyePosWld = _WorldSpaceCameraPos;
            #endif
            static float3 centerEyePosLocal = mul(unity_WorldToObject, float4(centerEyePosWld.xyz, 1)).xyz;

            v2f vert(appdata_t v, uint vertID : SV_VertexID)
            {
                v2f OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                if (v.barycentricColor.a > 0.5) {
                    v.vertex.y += 0.1 + 0.1 * sin(_Time.y * 0.61);
                }

				OUT.color = v.color * _Color;
                OUT.texcoord = v.texcoord.xy;
                OUT.texcoord3 = v.texcoord3;
                OUT.texcoord4 = float4(v.texcoord4.xyz, v.texcoord4.w);
                OUT.shadowColor = v.shadowColor;
                OUT.barycentricColor = v.barycentricColor;
                v.normal = normalize(v.normal);
                v.tangent.xyz = normalize(v.tangent.xyz);

                float3 relVert = v.vertex.xyz - v.texcoord4.xyz;
                if (v.tangent.w < 0) {
                    float3 boxLeftDir = normalize(cross( float3(0,1,0), normalize(OUT.texcoord4.xyz - centerEyePosLocal)));
                    float3 boxFwdDir = normalize(cross(boxLeftDir,  float3(0,1,0)));
                    float2x2 boxRotationMat = float2x2(boxLeftDir.xz, boxFwdDir.xz);
                    // float2x2 localSpaceConvert = float2x2(-v.tangent.xz, -v.normal.xz);
                    // float2x2 localSpaceConvertInv = transpose(localSpaceConvert);

                    v.vertex.xz = float2(1,-1) * mul(boxRotationMat, float2(1,-1) * (v.vertex.xz - v.texcoord4.xz)) + v.texcoord4.xz;
                    // norm.xyz = mul(boxRotationMat, norm.xyz);
                    // tang.xyz = mul(boxRotationMat, tang.xyz);
                } else if (dot(v.normal, centerEyePosLocal - OUT.texcoord4.xyz) > 0 && dot(v.normal, v.tangent.xyz) < 0.9) {
                    // v.vertex.xz = v.texcoord4.xz - relVert.xz - v.normal.xz * dot(relVert.xz, v.normal.xz);
                    v.vertex.xz = -relVert.xz + v.texcoord4.xz;
                    v.vertex.xz -= 2 * v.normal.xz * dot(-relVert.xz, v.normal.xz);
                }

                OUT.vertex = UnityObjectToClipPos(v.vertex);

                return OUT;
            }

			float median(float r, float g, float b)
			{
				return max(min(r, g), min(max(r, g), b));
			}

            float computeOpacityForSigDist(float sigDist, float2 texcoord, float thickness, float sharpness) {
                float2 msdfUnit = _PixelRange / _MSDFTex_TexelSize.zw;

                float sigDistThick = sigDist - (1 - thickness) * 0.66666;
                sigDistThick *= max(dot(msdfUnit * exp(3 * sharpness), 0.5 / fwidth(texcoord)), 1); // Max to handle fading out to quads in the distance
                float opacity = clamp(sigDistThick + 0.5, 0.0, 1.0);
                return opacity;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float4 color;
                float4 shadowColor = IN.shadowColor * _ShadowColor;
                float2 texcoord = IN.texcoord;
                if (texcoord.x < 0) {
                    texcoord.x += 10;
                    float2 coord = texcoord * _AltTex_TexelSize.zw;
                    float2 fr = frac(coord + 0.5);
                    float2 size = max(abs(ddx(coord)), abs(ddy(coord)));
                    float2 delta = smoothstep((1-size)*0.5, (1+size)*0.5, fr)-fr;
                    float2 sampleuv = lerp(texcoord+delta*_AltTex_TexelSize.xy, texcoord, floor(IN.texcoord3.a) / 1024.0);
                    color = tex2D(_AltTex, sampleuv);
                    // color = float4((sampleuv.xy), 0, 1);
                } else {
                    float thicknessMod = frac(IN.texcoord3.a) == 0 ? 0.5 : frac(IN.texcoord3.a);
                    float blurrinessMod = floor(IN.texcoord3.a) / 1024.0;
                    float thickness = clamp(_Thickness + thicknessMod - 0.5, 0.5, 1.5);
                    float sharpness = clamp(_Sharpness - blurrinessMod, -1.0, 1.0);

                    float3 sampleCol = tex2D(_MSDFTex, texcoord).rgb;
                    float sigDist = median(sampleCol.r, sampleCol.g, sampleCol.b) - 0.5;
                    float opacity = computeOpacityForSigDist(sigDist, texcoord, thickness, sharpness);
                    float shadowOpacity = shadowColor.a * computeOpacityForSigDist(sigDist, texcoord, 1 * shadowColor.a, -shadowColor.a);
                    color = float4(1, 1, 1, opacity);
                    color = saturate(float4(lerp(shadowColor.rgb, color.rgb, 1 - (shadowColor.a) * (1 - opacity)), 1 - (1 - opacity) * (1 - shadowOpacity)));
                    color.a = lerp(pow(saturate(color.a - 0.05),.3), color.a, opacity * shadowOpacity);
                    color = lerp(color, float4(sampleCol, max(sampleCol.r, max(sampleCol.g, sampleCol.b))), _DisableMSDF);
                }
				color *= IN.color;
                if (_FadeDistance > 0) {
                    float dist = distance(centerEyePosLocal.xz, IN.texcoord4.xz) / IN.texcoord4.w;
                    color.a *= lerp(1, _FadeOpacity,
                        smoothstep(_FadeDistance * 0.9, _FadeDistance * 1.2, dist)) * (1 / max(1, dist / _FadeDistance - 0));
                }

                if (_ShowWireframe > 0) {
                    float3 ddxvec = ddx(IN.barycentricColor.rgb);
                    float3 ddyvec = ddy(IN.barycentricColor.rgb);
                    float3 coord = IN.barycentricColor.rgb / max(abs(ddxvec), abs(ddyvec));
                    float dist = min(coord.x, min(coord.y, coord.z));
                    float wireAmount = smoothstep(_ShowWireframe + 0.5, _ShowWireframe - 0.5, dist);
                    color.rgb *= color.a;
                    color = lerp(color, float4(0,0,0,1), wireAmount * saturate(_ShowWireframe));
                    color.rgb /= color.a;
                }

                // return float4(IN.normal, 1);
                // #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                // #endif
                return color;
            }
        ENDCG
        }
    }

    Fallback "UI/Default"
}
