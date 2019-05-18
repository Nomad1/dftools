## Distance Field tools
This project contains several utilities written by @Nomad1 or ported from other projects for (Signed Distance Fields)[https://steamcdn-a.akamaihd.net/apps/valve/2007/SIGGRAPH2007_AlphaTestedMagnification.pdf] images generation. 

Code is written in C#. You need Mono or .Net 2.0 to run it (.Net 4.6.1 with System.Numerics.Vectors is recommended). Several classes use Vector2/Vector3i with System.Numerics.Vectors/OpenTK/Unity/MonoGame compatible syntax. Also there are fallback struct-based implementations activated by default with ```NO_NUMERICS``` define.

The tool implements different algorithms for Signed Field generation:
* Linear Sweep - very fast custom algorithm initially designed to find large unobstructed areas
* Brute Force - simple bruteforce approach
* Dead Reckoning - port from openll project generator: <https://github.com/cginternals/openll-asset-generator/>
* Eikonal Sweep - new algorithm described on <https://shaderfun.com>. Have some flaws, possibly due to Unity backporting: <https://github.com/chriscummings100/signeddistancefields/>  
* Signed Weight Field - experimental algorithm for weight-based field generation. It's generally not compatible with SDF because of low field range (2-3 pixels) and requires custom shader. However, SWF should produce much higher image quality and also two intersecting SWF fields would have perfect border. I'm working on this one and hope to upload other SWF tools in nearest future (added on 18 May 2019. We'll see how long it takes).

## Misc stuff 
This stuff is for reference only and not included in the code.

### ImageMagik commands to generate SDFs
At this moment (May 2019) the very best algoritm for reater-based SDF generation is ImageMagik's Euclidean morphology:

```sh
convert "${infile}" \( +clone -negate -morphology Distance Euclidean:7 -level 50%,-50% \) -morphology Distance Euclidean:7 -compose Plus -composite -level 45%,55% -filter Jinc -distort Resize 25.0% "${outfile}"
```

### Shader
GLSL pixel shader code for SDF font rendering:

```glsl
#define _SIMPLE_VERSION
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

### Vector-based distance fields
Also best quality SDFs are generated from vector graphics. Very best generator among with 4-colored SDF is (msdf)[https://github.com/Chlumsky/msdfgen] project and if you want to draw fonts in runtime it would be wise to use pseude SDF-based bezier curve rendering by Adam Simmons: <https://www.shadertoy.com/view/ltXSDB>.
