// HairCutLit - Custom hair shader function for cutting support
// Used as a Custom Function node in ShaderGraph or injected into
// com.unity.demoteam.hair's HairVertex subgraph.
//
// This function modifies the vertex position to hide particles
// beyond the cut point for each strand.

#ifndef HAIR_CUT_LIT_INCLUDED
#define HAIR_CUT_LIT_INCLUDED

// Bound from HairCutSystem.cs
StructuredBuffer<int> _CutIndices;
int _StrandParticleCount;
int _StrandCount;

// Apply hair cut to vertex position
// vertexID: the global vertex/particle index
// position: current world position of the particle
// prevPosition: position of the previous particle in the strand (for clamping)
void ApplyHairCut_float(
    in float vertexID,
    in float3 position,
    in float3 prevPosition,
    out float3 outPosition,
    out float outAlpha)
{
    int vid = (int)vertexID;
    int strandIdx = vid / _StrandParticleCount;
    int particleIdx = vid % _StrandParticleCount;

    outPosition = position;
    outAlpha = 1.0;

    if (strandIdx >= 0 && strandIdx < _StrandCount)
    {
        int cutIdx = _CutIndices[strandIdx];

        if (particleIdx >= cutIdx)
        {
            // Beyond cut point: collapse to previous particle position
            // This makes the strand appear shorter without breaking strip rendering
            outPosition = prevPosition;
            outAlpha = 0.0;
        }
        else if (particleIdx == cutIdx - 1)
        {
            // Last visible particle: full opacity, acts as the new tip
            outAlpha = 1.0;
        }
    }
}

// Simplified version for non-ShaderGraph usage
void ApplyHairCut_half(
    in half vertexID,
    in half3 position,
    in half3 prevPosition,
    out half3 outPosition,
    out half outAlpha)
{
    float3 outPosF;
    float outAlphaF;
    ApplyHairCut_float(
        (float)vertexID,
        (float3)position,
        (float3)prevPosition,
        outPosF,
        outAlphaF);
    outPosition = (half3)outPosF;
    outAlpha = (half)outAlphaF;
}

#endif // HAIR_CUT_LIT_INCLUDED
