using System;

#if !NO_NUMERICS
using PositionType = System.Numerics.Vector2i;
#endif

namespace DistanceFieldTool
{
    // Ported from https://github.com/cginternals/openll-asset-generator/blob/master/source/llassetgen/source/DistanceTransform.cpp
    public class DeadReckoning
    {
        // just in case you don't want to referene System.Numerics, custom Vector2i implementation
#if NO_NUMERICS
        public struct PositionType
        {
            public readonly int X;
            public readonly int Y;

            public PositionType(int x, int y)
            {
                X = x;
                Y = y;
            }

            public static PositionType operator +(PositionType v1, PositionType v2)
            {
                return new PositionType(v1.X + v2.X, v1.Y + v2.Y);
            }

            public static PositionType operator -(PositionType v)
            {
                return new PositionType(-v.X, -v.Y);
            }
        }
#endif

        private readonly byte[] m_image;
        private readonly int m_imageWidth;
        private readonly int m_imageHeight;
        private readonly PositionType[] posBuffer;

        private readonly float[] m_output;

        public DeadReckoning(byte[] image, int imageWidth, int imageHeight)
        {
            m_image = image;
            m_imageWidth = imageWidth;
            m_imageHeight = imageHeight;
            m_output = new float[imageWidth * imageHeight];
            posBuffer = new PositionType[imageWidth * imageHeight];
        }

        private bool IsValid(PositionType pos)
        {
            return pos.X >= 0 && pos.Y >= 0 && pos.X < m_imageWidth && pos.Y < m_imageHeight;
        }

        byte getPixel(PositionType pos)
        {
            if (!IsValid(pos))
                return 0;
            return m_image[pos.Y * m_imageWidth + pos.X];
        }

        float getOutputPixel(PositionType pos)
        {
            if (!IsValid(pos))
                return 0;
            return m_output[pos.Y * m_imageWidth + pos.X];
        }

        void setOutputPixel(PositionType pos, float value)
        {
            m_output[pos.Y * m_imageWidth + pos.X] = value;
        }

        PositionType getPosAt(PositionType pos)
        {
            return posBuffer[pos.Y * m_imageWidth + pos.X];
        }

        PositionType posAt(PositionType pos, PositionType value)
        {
            posBuffer[pos.Y * m_imageWidth + pos.X] = value;
            return value;
        }

        void transformAt(PositionType pos, PositionType target, float distance)
        {
            target += pos;
            if (IsValid(target) && getOutputPixel(target) + distance < getOutputPixel(pos))
            {
                target = getPosAt(target);
                posAt(pos, target);
                setOutputPixel(pos, (float)Math.Sqrt((pos.X - target.X) * (pos.X - target.X) + (pos.Y - target.Y) * (pos.Y - target.Y)));
            }
        }

        public float[] Transform(float backgroundVal, int [][] closestPoints = null)
        {
            for (int y = 0; y < m_imageHeight; ++y)
                for (int x = 0; x < m_imageWidth; ++x)
                {
                    PositionType pos = new PositionType(x, y);
                    byte center = getPixel(pos);
                    posAt(pos, pos);
                    setOutputPixel(pos,
                        (center != 0 && (getPixel(new PositionType(x - 1, y)) != center || getPixel(new PositionType(x + 1, y)) != center ||
                                  getPixel(new PositionType(x, y - 1)) != center || getPixel(new PositionType(x, y + 1)) != center))
                        ? 0
                        : backgroundVal
                    );
                }

            float[] distance = { (float)Math.Sqrt(2.0F), 1.0F, (float)Math.Sqrt(2.0F), 1.0F };
            PositionType[] target = {
                new PositionType(-1, -1),
                new PositionType(0, -1),
                new PositionType(+1, -1),
                new PositionType(-1, 0),
            };

            for (int y = 0; y < m_imageHeight; ++y)
                for (int x = 0; x < m_imageWidth; ++x)
                    for (int i = 0; i < 4; ++i)
                        transformAt(new PositionType(x, y), target[i], distance[i]);

            for (int y = 0; y < m_imageHeight; ++y)
                for (int x = 0; x < m_imageWidth; ++x)
                    for (int i = 0; i < 4; ++i)
                        transformAt(new PositionType(m_imageWidth - x - 1, m_imageHeight - y - 1), -(target[3 - i]), distance[3 - i]);

            for (int y = 0; y < m_imageHeight; ++y)
                for (int x = 0; x < m_imageWidth; ++x)
                {
                    PositionType pos = new PositionType(x, y);
                    if (getPixel(pos) != 0)
                        setOutputPixel(pos, -getOutputPixel(pos));

                    if (closestPoints != null)
                    {
                        var npos = getPosAt(pos);
                        closestPoints[pos.Y * m_imageWidth + pos.X] = new[] { npos.X, npos.Y };
                    }
                }

            return m_output;
        }

        public static float[] AnalyzeGrayscale(byte[] pixelData, int imageWidth, int imageHeight, int[][] closestPoints = null, bool extraPass = true, byte threshold = 0)
        {
            byte[] data = new byte[imageWidth * imageHeight];

            for (int i = 0; i < data.Length; i++)
            {
                int pixel = pixelData[i];

                if (pixel > threshold)
                {
                    data[i] = 255;
                }
                else
                    data[i] = 0;
            }

            DeadReckoning dr = new DeadReckoning(data, imageWidth, imageHeight);
            return dr.Transform(float.MaxValue, closestPoints);
        }
    }
}
