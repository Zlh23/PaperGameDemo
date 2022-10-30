// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Zlh/PaperDemo/VirtualMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            stencil
            {
                ref 1
                comp equal
                pass IncrSat
            }
            colormask 0
        }
    }

}
