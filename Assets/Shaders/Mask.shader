Shader "Zlh/PaperDemo/Mask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [IntRange]_stencilRef("_stencilRef", Range(0,255)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            stencil
            {
                ref [_stencilRef]
                comp Always
                pass replace
            }
            colormask 0
        }
    }
}
