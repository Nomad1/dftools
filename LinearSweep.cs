//#define PROFILE

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
            public readonly ushort X;
            public readonly ushort Y;
            public readonly int Z;

            public Vector3i(ushort x, ushort y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
#endif

        private static void CheckObstacle(ref Vector3i obstacleCandidate, int x, int y, Vector3i value)
        {
            if (obstacleCandidate.Z <= 1 || value.Z == int.MaxValue)
                return;

            int distance =
                // Math.Abs(x - value.X) + Math.Abs(y - value.Y); // Manhattan distance
                (x - value.X) * (x - value.X) + (y - value.Y) * (y - value.Y); // squared distance from myPoint to value

            if (distance <= obstacleCandidate.Z)
                obstacleCandidate = new Vector3i(value.X, value.Y, distance);   
        }

        public delegate bool CheckFunc<T>(T param);

        public static float[] AnalyzeGrayscale(byte[] pixelData, int imageWidth, int imageHeight, bool extraPass = true, byte threshold = 0, bool normalize = true)
        {
            return AnalyzeGrayscale(pixelData, imageWidth, imageHeight, (byte pixel) => pixel > threshold, null, extraPass, normalize);
        }

        /// <summary>
        /// Performs Linear Sweep calculation for distance fields
        /// </summary>
        /// <param name="pixelData">input pixels</param>
        /// <param name="imageWidth">width</param>
        /// <param name="imageHeight">heigth</param>
        /// <param name="closestPoints">output array with Voronoi point indices</param>
        /// <param name="extraPass">perform additional sanity check pass</param>
        /// <returns>array of nearest distances</returns>
        public static float[] AnalyzeGrayscale<T>(T[] pixelData, int imageWidth, int imageHeight, CheckFunc<T> checker, int[] closestPoints = null, bool extraPass = true, bool normalize = true)
        {
#if PROFILE
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
#endif
            Vector3i[] points = new Vector3i[pixelData.Length];

            // pre processing
            int index = 0;
            for (ushort y = 0; y < imageHeight; y++)
                for (ushort x = 0; x < imageWidth; x++)
                {
                    points[index] = new Vector3i(x, y, checker(pixelData[index]) ? 0 : int.MaxValue);

                    index++;
                }

#if PROFILE
            sw.Stop();
            Console.WriteLine("Pre processing: {0}", sw.ElapsedMilliseconds);
            sw.Reset();
            sw.Start();
#endif

            // forward processing
            for (int y = 1; y < imageHeight; y++)
                for (int x = 1; x < imageWidth; x++)
                {
                    index = x + y * imageWidth;
                    if (points[index].Z <= 1)
                        continue;

                    CheckObstacle(ref points[index], x, y, points[index - 1]); // x - 1
                    CheckObstacle(ref points[index], x, y, points[index - imageWidth]); // y - 1
                    CheckObstacle(ref points[index], x, y, points[index - imageWidth - 1]); // x - 1, y - 1
                    if (x < imageWidth - 1)
                        CheckObstacle(ref points[index], x, y, points[index - imageWidth + 1]); // x + 1, y - 1
                }

#if PROFILE
            sw.Stop();
            Console.WriteLine("Forward processing: {0}", sw.ElapsedMilliseconds);
            sw.Reset();
            sw.Start();
#endif

            //backward processing
            for (int y = imageHeight - 2; y >= 0; y--)
                for (int x = imageWidth - 2; x >= 0; x--)
                {
                    index = x + y * imageWidth;

                    if (points[index].Z <= 1)
                        continue;

                    CheckObstacle(ref points[index], x, y, points[index + 1]); // x + 1
                    CheckObstacle(ref points[index], x, y, points[index + imageWidth]); // y + 1
                    CheckObstacle(ref points[index], x, y, points[index + imageWidth + 1]); // x + 1, y + 1
                    if (x > 0)
                        CheckObstacle(ref points[index], x, y, points[index + imageWidth - 1]); // x - 1, y + 1
                }


#if PROFILE
            sw.Stop();
            Console.WriteLine("Backward processing: {0}", sw.ElapsedMilliseconds);
            sw.Reset();
            sw.Start();
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
                sw.Reset();
                sw.Start();
#endif
            }
            else
            {
                // process corners

                CheckObstacle(ref points[0], 1, 0, points[imageWidth + 1]); // (0, 0)
                CheckObstacle(ref points[imageWidth - 1], imageWidth - 1, 0, points[imageWidth - 2]); // (imagewidth - 1, 0)
                CheckObstacle(ref points[imageWidth * (imageHeight - 1)], 0, imageHeight - 1, points[imageWidth * (imageHeight - 1) + 1]); // (0, imageheight - 1)
                CheckObstacle(ref points[imageWidth - 1 + imageWidth * (imageHeight - 1)], imageWidth - 1, imageHeight - 1, points[imageWidth * (imageHeight - 1) + imageWidth - 2]);  // (imagewidth - 1, imageheight - 1)
            }

            // distance calculation
            float[] values = new float[points.Length];

            if (normalize)
            {
                for (int i = 0; i < points.Length; i++)
                    values[i] = (float)Math.Sqrt(points[i].Z); // calculate correct distance
            }
            else
            {
                for (int i = 0; i < points.Length; i++)
                    values[i] = points[i].Z; // leave squared distance
            }

#if PROFILE
            sw.Stop();
            Console.WriteLine("Distance calculation: {0}", sw.ElapsedMilliseconds);
#endif

            if (closestPoints != null)
            {
                for (int i = 0; i < points.Length; i++)
                    closestPoints[i] = points[i].X + points[i].Y * imageWidth;
            }

            return values;
        }
    }
}
