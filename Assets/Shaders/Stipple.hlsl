float stipple(float2 uv, float2 texelSize, float stippleStrength) {
    float2 pixel = uv / texelSize.xy;
    float transparency = 1 - 2 * stippleStrength;
    return transparency + sin(int(pixel.x) * int(pixel.y));
}