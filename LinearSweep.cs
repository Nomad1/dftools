#define PROFILE

using System;
#if !NO_NUMERICS
using System.Numerics;
#endif

namespace DistanceFieldTool
{
    /// <summary>
    /// This approach scans image to find largest unobstructed area.
    /// Two scans are needed: in forward and backwards direction
    /// </summary>
    public static class LinearSweep
    {
        // just in case you don't want to referene System.Numerics, custom Vector3i implementation
#if NO_NUMERICS
        public struct Vector3i
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;

            public Vector3i(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
#endif

        private static void CheckObstacle(ref Vector3i obstacleCandidate, int x, int y, Vector3i value)
        {
            if (obstacleCandidate.Z <= 1)
                return;

            int distance = (x - value.X) * (x - value.X) + (y - value.Y) * (y - value.Y); // squared distance from myPoint to value

            if (distance < obstacleCandidate.Z)
                obstacleCandidate = new Vector3i(value.X, value.Y, distance);
        }

        private static Vector3i PreProcessObstacle(bool isObstacle, int x, int y, int imageWidth, int imageHeight)
        {
            Vector3i myPoint = new Vector3i(x, y, 0);

            if (isObstacle)
                return myPoint;

            if (x == 0)
                return new Vector3i(-1, y, 1);

            if (y == 0)
                return new Vector3i(x, -1, 1);

            if (x == imageWidth - 1)
                return new Vector3i(imageWidth, y, 1);

            if (y == imageHeight - 1)
                return new Vector3i(x, imageHeight, 1);

            return new Vector3i(-1, -1, int.MaxValue);
        }

        public static float[] AnalyzeGrayscale(byte[] pixelData, int imageWidth, int imageHeight, bool extraPass = true)
        {
#if PROFILE
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
#endif
            Vector3i[] points = new Vector3i[pixelData.Length];

            // pre processing
            int index = 0;
            for (int y = 0; y < imageHeight; y++)
                for (int x = 0; x < imageWidth; x++)
                {
                    points[index] = PreProcessObstacle(pixelData[index] != 0, x, y, imageWidth, imageHeight);
                    index++;
                }

#if PROFILE
            sw.Stop();
            Console.WriteLine("Pre processing: {0}", sw.ElapsedMilliseconds);
            sw.Restart();
#endif

            // forward processing
            for (int y = 1; y < imageHeight - 1; y++)
                for (int x = 1; x < imageWidth - 1; x++)
                {
                    index = x + y * imageWidth;
                    if (points[index].Z <= 1)
                        continue;

                    CheckObstacle(ref points[index], x, y, points[index - 1]); // x - 1
                    CheckObstacle(ref points[index], x, y, points[index - imageWidth]); // y - 1
                    CheckObstacle(ref points[index], x, y, points[index - imageWidth - 1]); // x - 1, y - 1
                    CheckObstacle(ref points[index], x, y, points[index - imageWidth + 1]); // x + 1, y - 1
                }

#if PROFILE
            sw.Stop();
            Console.WriteLine("Forward processing: {0}", sw.ElapsedMilliseconds);
            sw.Restart();
#endif

            // backward processing
            for (int y = imageHeight - 2; y > 0; y--)
                for (int x = imageWidth - 2; x > 0; x--)
                {
                    index = x + y * imageWidth;

                    if (points[index].Z <= 1)
                        continue;

                    CheckObstacle(ref points[index], x, y, points[index + 1]); // x + 1
                    CheckObstacle(ref points[index], x, y, points[index + imageWidth]); // y + 1
                    CheckObstacle(ref points[index], x, y, points[index + imageWidth + 1]); // x + 1, y + 1
                    CheckObstacle(ref points[index], x, y, points[index + imageWidth - 1]); // x - 1, y + 1
                }

#if PROFILE
            sw.Stop();
            Console.WriteLine("Backward processing: {0}", sw.ElapsedMilliseconds);
            sw.Restart();
#endif

            if (extraPass)
            {
                // final pass. sometimes needed
                for (int y = 1; y < imageHeight - 1; y++)
                    for (int x = 1; x < imageWidth - 1; x++)
                    {
                        index = x + y * imageWidth;
                        if (points[index].Z <= 1)
                            continue;

                        CheckObstacle(ref points[index], x, y, points[index - 1]); // x - 1
                        CheckObstacle(ref points[index], x, y, points[index - imageWidth]); // y - 1
                        CheckObstacle(ref points[index], x, y, points[index - imageWidth - 1]); // x - 1, y - 1
                        CheckObstacle(ref points[index], x, y, points[index - imageWidth + 1]); // x + 1, y - 1
                        CheckObstacle(ref points[index], x, y, points[index + 1]); // x + 1
                        CheckObstacle(ref points[index], x, y, points[index + imageWidth]); // y + 1
                        CheckObstacle(ref points[index], x, y, points[index + imageWidth + 1]); // x + 1, y + 1
                        CheckObstacle(ref points[index], x, y, points[index + imageWidth - 1]); // x - 1, y + 1
                    }


#if PROFILE
                sw.Stop();
                Console.WriteLine("Final pass processing: {0}", sw.ElapsedMilliseconds);
                sw.Restart();
#endif
            }


            // distance calculation
            float[] values = new float[points.Length];

            for (int i = 0; i < points.Length; i++)
                values[i] = (float)Math.Sqrt(points[i].Z); // calculate correct distance

#if PROFILE
            sw.Stop();
            Console.WriteLine("Distance calculation: {0}", sw.ElapsedMilliseconds);
#endif

            return values;
        }
    }
}
