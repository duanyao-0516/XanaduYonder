#ifndef XANADU_BAKERY_LIGHT_INCLUDED
#define XANADU_BAKERY_LIGHT_INCLUDED

sampler2D _BakeryShadowMap;
float4 _BakeryShadowMap_TexelSize;
float4x4 _BakeryLightProjection;
half3 _BakeryKey;
half _BakeryShadowsOn;

float3 BakeryShadowPosition(float3 world)
{
    // The shadow camera renders to a texture. Its CPU projection gives lower-left viewport UVs.
    return mul(_BakeryLightProjection, float4(world, 1)).xyz * .5 + .5;
}

half BakeryShadowTap(float2 uv, float depth)
{
    return step(depth, DecodeFloatRG(tex2D(_BakeryShadowMap, uv).rg));
}

half BakeryVisibility(float3 position, half facingLight)
{
    if (_BakeryShadowsOn < .5 || any(position < .001) || any(position > .999)) return 1;
    float depth = position.z - (.0013 + .0028 * (1 - facingLight));
    float2 spread = _BakeryShadowMap_TexelSize.xy * 1.55;
    float2 uv = position.xy;
    half lit = BakeryShadowTap(uv, depth) * .28;
    lit += BakeryShadowTap(uv + spread, depth) * .18;
    lit += BakeryShadowTap(uv - spread, depth) * .18;
    lit += BakeryShadowTap(uv + float2(-spread.x, spread.y), depth) * .18;
    lit += BakeryShadowTap(uv + float2(spread.x, -spread.y), depth) * .18;
    return lerp(.22, 1, lit);
}

#endif
