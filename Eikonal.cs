using System;
using Mathf = System.Math;

namespace DistanceFieldTool
{
    /// <summary>
    /// Eikonal search. Taken from https://github.com/chriscummings100/signeddistancefields/search?q=EikonalSweep&unscoped_q=EikonalSweep
    /// </summary>
    public class Eikonal
    {
        public delegate float PixelCheckDelegate(int index);

        //info about 1 pixel, used when generating textures
        public struct Pixel
        {
            public float distance;
            public bool edge; //used by eikonal sweep to denote unchaneable pixels
        }

        //internally created pixel buffer
        private readonly Pixel[] m_pixels;
        private readonly int m_x_dims;
        private readonly int m_y_dims;

        private readonly int m_validPixels;

        public int ValidPixels
        {
            get { return m_validPixels; }
        }

        //constructor creates pixel buffer ready to start generation
        public Eikonal(int width, int height, PixelCheckDelegate initializer)
        {
            m_x_dims = width;
            m_y_dims = height;
            m_pixels = new Pixel[m_x_dims * m_y_dims];
            int pixels = 0;

            for (int i = 0; i < m_pixels.Length; i++)
            {
                float value = initializer(i);

                if (value > 0)
                {
                    m_pixels[i].distance = value;
                    pixels++;
                }
                else
                    m_pixels[i].distance = 0;
            }

            m_validPixels = pixels;
        }


        //helpers to read/write pixels during generation
        Pixel GetPixel(int x, int y)
        {
            return m_pixels[y * m_x_dims + x];
        }
        void SetPixel(int x, int y, Pixel p)
        {
            m_pixels[y * m_x_dims + x] = p;
        }

        //test if we consider pixel as outside the geometry (+ve distance)
        //note: pixels outside the bounds are considered 'outer'
        bool IsOuterPixel(int pix_x, int pix_y)
        {
            if (pix_x < 0 || pix_y < 0 || pix_x >= m_x_dims || pix_y >= m_y_dims)
                return true;
            else
                return GetPixel(pix_x, pix_y).distance >= 0;
        }

        //test if pixel is an 'edge pixel', meaning at least one of its
        //neighbours is on the other side of the edge of the geometry
        //i.e. for an outer pixel, at least 1 neighbour is an inner pixel
        bool IsEdgePixel(int pix_x, int pix_y)
        {
            bool is_outer = IsOuterPixel(pix_x, pix_y);
            if (is_outer != IsOuterPixel(pix_x - 1, pix_y - 1)) return true; //[-1,-1]
            if (is_outer != IsOuterPixel(pix_x, pix_y - 1)) return true;     //[ 0,-1]
            if (is_outer != IsOuterPixel(pix_x + 1, pix_y - 1)) return true; //[+1,-1]
            if (is_outer != IsOuterPixel(pix_x - 1, pix_y)) return true;     //[-1, 0]
            if (is_outer != IsOuterPixel(pix_x + 1, pix_y)) return true;     //[+1, 0]
            if (is_outer != IsOuterPixel(pix_x - 1, pix_y + 1)) return true; //[-1,+1]
            if (is_outer != IsOuterPixel(pix_x, pix_y + 1)) return true;     //[ 0,+1]
            if (is_outer != IsOuterPixel(pix_x + 1, pix_y + 1)) return true; //[+1,+1]
            return false;
        }

        //cleans the field down so only pixels that lie on an edge 
        //contain a valid value. all others will either contain a
        //very large -ve or +ve value just to indicate inside/outside
        public void ClearAndMarkNoneEdgePixels()
        {
            for (int x = 0; x < m_x_dims; x++)
                for (int y = 0; y < m_y_dims; y++)
                {
                    Pixel pix = GetPixel(x, y);
                    pix.edge = IsEdgePixel(x, y); //for eikonal sweep, mark edge pixels
                    if (!pix.edge)
                        pix.distance = pix.distance > 0 ? 99999f : -99999f;
                    SetPixel(x, y, pix);
                }
        }

        //these 2 functions do the mathematical work of solving the eikonal
        //equations in 1D and 2D. 
        // https://en.wikipedia.org/wiki/Eikonal_equation
        float SolveEikonal1D(float horizontal, float vertical)
        {
            return Mathf.Min(horizontal, vertical) + 1f;
        }
        float SolveEikonal2D(float horizontal, float vertical)
        {
            //solve eikonal equation in 2D if or can, or revert to 1D if |h-v| >= 1
            if (Mathf.Abs(horizontal - vertical) < 1.0f)
            {
                float sum = horizontal + vertical;
                float dist = sum * sum - 2.0f * (horizontal * horizontal + vertical * vertical - 1f);
                return 0.5f * (sum + (float)Mathf.Sqrt(dist));
            }
            else
            {
                return SolveEikonal1D(horizontal, vertical);
            }
        }

        //main eikonal equation solve. samples the grid to get candidate neighbours, then
        //uses one of the above 2 functions to solve
        void SolveEikonal(int x, int y)
        {
            //get pixel and leave unchanged if it is an edge pixel
            Pixel p = GetPixel(x, y);
            if (p.edge)
                return;

            //read current and sign, then correct sign to work with +ve distance
            float current = p.distance;
            float sign = current < 0 ? -1.0f : 1.0f;
            current *= sign;

            //find the smallest of the 2 horizontal neighbours (correcting for sign)
            float horizontalmin = float.MaxValue;
            if (x > 0) horizontalmin = Mathf.Min(horizontalmin, sign * GetPixel(x - 1, y).distance);
            if (x < m_x_dims - 1) horizontalmin = Mathf.Min(horizontalmin, sign * GetPixel(x + 1, y).distance);

            //find the smallest of the 2 vertical neighbours
            float verticalmin = float.MaxValue;
            if (y > 0) verticalmin = Mathf.Min(verticalmin, sign * GetPixel(x, y - 1).distance);
            if (y < m_y_dims - 1) verticalmin = Mathf.Min(verticalmin, sign * GetPixel(x, y + 1).distance);

            //solve eikonal equation in 2D
            float eikonal = SolveEikonal2D(horizontalmin, verticalmin);

            //either keep the current distance, or take the eikonal solution if it is smaller
            p.distance = sign * Mathf.Min(current, eikonal);
            SetPixel(x, y, p);
        }

        //sweep over the image using the eikonal equations to generate
        //a perfect field (gradient length == 1 everywhere).
        public void EikonalSweep()
        {
            //clean the field so any none edge pixels simply contain 99999 for outer
            //pixels, or -99999 for inner pixels. also marks pixels as edge/not edge
            ClearAndMarkNoneEdgePixels();

            //sweep using eikonal algorithm in all 4 diagonal directions
            for (int x = 0; x < m_x_dims; x++)
            {
                for (int y = 0; y < m_y_dims; y++)
                {
                    SolveEikonal(x, y);
                }
                for (int y = m_y_dims - 1; y >= 0; y--)
                {
                    SolveEikonal(x, y);
                }
            }
            for (int x = m_x_dims - 1; x >= 0; x--)
            {
                for (int y = 0; y < m_y_dims; y++)
                {
                    SolveEikonal(x, y);
                }
                for (int y = m_y_dims - 1; y >= 0; y--)
                {
                    SolveEikonal(x, y);
                }
            }

        }

        public float[] End()
        {
            //alloc array of colours
            float[] distances = new float[m_pixels.Length];

            //iterate over all pixels and calculate a colour for each one
            //note: updated in blog post 7 to include gradient
            for (int y = 0; y < m_y_dims; y++)
            {
                for (int x = 0; x < m_x_dims; x++)
                {
                    distances[y * m_x_dims + x] = GetPixel(x, y).distance;
                }
            }

            return distances;
        }

        /*
        public Texture2D End()
        {
            //allocate an 'RGBAFloat' texture of the correct dimensions
            Texture2D tex = new Texture2D(m_x_dims, m_y_dims, TextureFormat.RGBAFloat, false);

            //alloc array of colours
            Color[] cols = new Color[m_pixels.Length];

            //iterate over all pixels and calculate a colour for each one
            //note: updated in blog post 7 to include gradient
            for (int y = 0; y < m_y_dims; y++)
            {
                for (int x = 0; x < m_x_dims; x++)
                {
                    //get d, and also it's sign (i.e. inside or outside)
                    float d = GetPixel(x, y).distance;
                    float sign = d >= 0 ? 1.0f : -1.0f;
                    float maxval = float.MaxValue * sign;

                    //read neighbour distances, ignoring border pixels
                    float x0 = x > 0 ? GetPixel(x - 1, y).distance : maxval;
                    float x1 = x < (m_x_dims - 1) ? GetPixel(x + 1, y).distance : maxval;
                    float y0 = y > 0 ? GetPixel(x, y - 1).distance : maxval;
                    float y1 = y < (m_y_dims - 1) ? GetPixel(x, y + 1).distance : maxval;

                    //use the smallest neighbour in each direction to calculate the partial deriviates
                    float xgrad = sign * x0 < sign * x1 ? -(x0 - d) : (x1 - d);
                    float ygrad = sign * y0 < sign * y1 ? -(y0 - d) : (y1 - d);

                    //combine partial derivatives to get gradient
                    Vector2 grad = new Vector2(xgrad, ygrad);

                    //store distance in red channel, and gradient in green/blue channels
                    Color col = new Color();
                    col.r = d;
                    col.g = grad.x;
                    col.b = grad.y;
                    col.a = d < 999999f ? 1 : 0;
                    cols[y * m_x_dims + x] = col;
                }
            }


            //write into the texture
            tex.SetPixels(cols);
            tex.Apply();
            m_pixels = null;
            m_x_dims = m_y_dims = 0;
            return tex;
        }*/
    }
}
