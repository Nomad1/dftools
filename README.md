
## dftools
Signed Distance Field generation tools in C#

.Net 4.6 is recommended, but not required. Several classes use Vector2/Vector3i from System.Numerics or OpenTK/Unity/MonoGame, but also there is fallback struct-based implementation for these objects.  

Main tool implements four different algorithms for Signed Field generation:
* Linear Sweep - very fast custom algorithm initially designed to find large unobstructed areas
* Brute Force - simple bruteforce approach
* Dead Reckoning - port from openll project, https://github.com/cginternals/openll-asset-generator/blob/master/source/llassetgen/source/DistanceTransform.cpp
* Signed Weight Field - algorithm for weight field generation. It's generally not compatible with SDF because of low field range (2-3 pixels) and requires custom shader. However, SWF should produce much higher image quality and also two intersecting SWF fields would have perfect border.

# Other works
At this moment (May 2019) the very best algoritm for SDF generation is ImageMagik's Euclidean morphology:

```convert "${infile}" \( +clone -negate -morphology Distance Euclidean:7 -level 50%,-50% \) -morphology Distance Euclidean:7 -compose Plus -composite -level 45%,55% -filter Jinc -distort Resize 25.0% "${outfile}"```


GLSL pixel shader code for ideal font rendering:

```#define _SIMPLE_VERSION
uniform sampler2D s_texture_0;

in vec2 TexCoord;
flat in vec4 Color;

#if defined( UNIFORM_THICKNESS )
uniform float s_thickness;
#elif defined( BOLD_FONT )
const float s_thickness = 1.0f;
#else
const float s_thickness = 0.0f;
#endif


const float width = 0.25;      // ideal thickness value
const float dscale = 1.0/3.0;     // 1/3 subpixel value

void main(void)
{
    vec4 texColor = texture(s_texture_0, TexCoord);
    float dist = texColor.r;
    
    float threshold = 0.5 - clamp(s_thickness, -1.0, 1.0) * 0.1;

#if defined( __COMPAT__ )

    float alpha = smoothstep( threshold - width, threshold + width, dist);

#elif defined( SIMPLE_VERSION )

    ivec2 sz = textureSize( s_texture_0, 0 );
    float dx = dFdx( TexCoord.x ) * sz.x;
    float dy = dFdy( TexCoord.y ) * sz.y;
    float toPixels = 10.0 * inversesqrt( dx * dx + dy * dy );

    float sigDist = dist - threshold;

    float alpha = clamp( sigDist * toPixels + threshold, 0.0, 1.0 );

#else
    // texture size-based derivative calculation
    // taken from Cinder-SdfText project

 // Convert normalized texcoords to absolute texcoords.
    vec2 uv = TexCoord * vec2(textureSize( s_texture_0, 0 ));
    // Calculate derivates
    vec2 Jdx = dFdx( uv );
    vec2 Jdy = dFdy( uv );
    // calculate signed distance (in texels).
    float sigDist = dist - threshold;
    // For proper anti-aliasing, we need to calculate signed distance in pixels. We do this using derivatives.
    vec2 gradDist = normalize( vec2( dFdx( sigDist ), dFdy( sigDist ) ) );
    vec2 grad = vec2( gradDist.x * Jdx.x + gradDist.y * Jdy.x, gradDist.x * Jdx.y + gradDist.y * Jdy.y );
    // Apply anti-aliasing.
    const float kThickness = width * 0.5;
    const float kNormalization = kThickness * 0.5 * sqrt( 2.0 );
    float afwidth = min( kNormalization * length( grad ), threshold );
    float alpha = smoothstep( 0.0 - afwidth, 0.0 + afwidth, sigDist );
   
#endif

    gl_FragColor = vec4(Color.r, Color.g, Color.b, Color.a * alpha);
}
```

Above shader have 3 different modes:
* Combatibility mode (enabled with ```__COMPAT__``` define) - for old hardware and GLSL 1.5
* Simple (enabled with ```SIMPLE_VERSION```) - fast but acceptable implementation
* default - slow, but great resulting image quality for both upscaling and downscaling. Some ideas taken from Cinder-SdfText project
