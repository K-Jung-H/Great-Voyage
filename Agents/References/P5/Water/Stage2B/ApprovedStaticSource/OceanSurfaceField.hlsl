#ifndef GREAT_VOYAGE_OCEAN_SURFACE_FIELD
#define GREAT_VOYAGE_OCEAN_SURFACE_FIELD

float OceanHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float OceanNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f*f*f*(f*(f*6-15)+10);
    return lerp(lerp(OceanHash(cell), OceanHash(cell+float2(1,0)), f.x),
                lerp(OceanHash(cell+float2(0,1)), OceanHash(cell+1), f.x), f.y);
}

float OceanDetail(float2 p)
{
    return .65 * OceanNoise(p) + .25 * OceanNoise(p * 2.03 + 7.1) + .1 * OceanNoise(p * 4.11 - 13.7);
}

struct OceanSurfaceShape
{
    float broadField;
    float ripple;
    float crestDistance;
    float crestGate;
    float crestBreakup;
    float crestEdge;
    float minorCrest;
    float crestFringe;
    float surfaceVariation;
};

OceanSurfaceShape EvaluateOceanShape(float2 position, float frequency, float curvature, float rippleScale)
{
    OceanSurfaceShape shape;
    float2 direction = float2(.36, .9329523);
    float2 lateral = float2(direction.y, -direction.x);
    float2 p = float2(dot(position, lateral), dot(position, direction)) * frequency;
    float2 warp = float2(OceanDetail(p*.34+4.7), OceanDetail(p*.34-21.3)) - .5;
    float2 q = p + warp * .85;
    float bend = sin(q.x*.54 + OceanNoise(q*.24)*1.4) + .27*sin(q.x*1.13-q.y*.13);
    float phase = q.y + bend * curvature;
    float companion = q.y*1.31 + q.x*.22 + .56*sin(q.x*.43+1.6) + 1.9;
    shape.broadField = .5 + .5*(.78*sin(phase) + .22*sin(companion));
    float ripplePhase = phase*rippleScale + .65*sin(q.x*1.86+OceanNoise(q*.8)*1.6);
    shape.ripple = sin(ripplePhase) * smoothstep(.24,.72,OceanNoise(q*.7+8.4));
    shape.crestDistance = abs(sin(phase-1.5707963 + (OceanNoise(p*4.2)-.5)*.19 + (OceanNoise(p*11.7)-.5)*.055));
    shape.crestGate = smoothstep(.65,.9,sin(phase));
    shape.crestBreakup = OceanDetail(float2(q.x*.82,q.y*.6)+14.1);
    shape.crestEdge = OceanNoise(p*3.9+2.8);
    shape.crestFringe = smoothstep(.67,.83,OceanDetail(p*5.1-12.4));
    shape.surfaceVariation = OceanNoise(p*float2(.85,2.65))-.5;
    shape.minorCrest = (1-smoothstep(.025,.09,abs(sin(ripplePhase-1.5707963))))
        * smoothstep(.7,.86,OceanNoise(q*1.8+37.1)) * smoothstep(.35,.65,sin(ripplePhase));
    return shape;
}

#endif
