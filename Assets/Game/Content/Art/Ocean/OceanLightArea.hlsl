#ifndef OCEAN_LIGHT_AREA_INCLUDED
#define OCEAN_LIGHT_AREA_INCLUDED

// Fixed-pattern hash. No time-evolving graph or wake exclusion.


float2 LightHash(float2 p)
{
    p = frac(p * float2(.1031, .11369));
    p += dot(p, p.yx + 19.19);
    return frac(float2(p.x * p.y, (p.x + p.y) * p.x));
}
#endif
