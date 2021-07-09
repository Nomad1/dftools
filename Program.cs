using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

#if !NO_NUMERICS
using PositionType = System.Numerics.Vector2i;
#endif


namespace DistanceFieldTool
{
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

    public class DistanceFieldTool
    {
        // just in case you don't want to reference System.Numerics here goes custom Vector3i implementation

        private static string s_syntax = "Syntax: DistanceFieldTool.exe input.png output.png [width] [algorithm]\n" +
                "Algorythm could be one of:\n" +
                "\tsweep - Linear Sweep (custom algorithm, default)\n" +
                "\tbrute - Brute Force approach\n" +
                "\tdr - Dead Reckoning (port from openll)\n" +
                "\tswf - Signed Weighed Field (experimental)\n" +
                "\teikonal - Eikonal Sweep (port from shaderfun)\n";


        /// <summary>
        /// Simple wrapper for Death Reckoning alhorithm
        /// </summary>
        /// <param name="inPixels">In pixels.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="resultPixels">Result pixels.</param>
        /// <param name="vectorWidth">Vector width.</param>
        private static void ProcessDR(int[] inPixels, int width, int height, int[] resultPixels, int vectorWidth)
        {
            byte[] data = new byte[width * height];

            for (int i = 0; i < data.Length; i++)
            {
                int pixel = inPixels[i];

                if (((pixel >> 16) & 0xff) != 0)
                {
                    data[i] = 0;
                }
                else
                    data[i] = 255;
            }

            DeadReckoning dr = new DeadReckoning(data, width, height);
            float[] values = dr.Transform(255);

            for (int i = 0; i < values.Length; i++)
            {
                float nvalue = values[i] / vectorWidth;
                if (nvalue < -1.0f)
                    continue;

                if (nvalue > 1.0f)
                    nvalue = 1.0f;

                nvalue = nvalue * 0.5f + 0.5f;
                int ivalue = (int)(nvalue * 255.0f) & 0xff;

                resultPixels[i] = (int)((ivalue << 16) | (ivalue << 8) | (ivalue) | (ivalue << 24));
            }
        }

        /// <summary>
        /// Linear Sweep algorithm. We need to call it for normal and inverted image
        /// </summary>
        /// <param name="inPixels">In pixels.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="resultPixels">Result pixels.</param>
        /// <param name="vectorWidth">Vector width.</param>
        private static void ProcessSweep(int[] inPixels, int width, int height, int[] resultPixels, int vectorWidth)
        {
            byte[] idata = new byte[width * height];
            byte[] data = new byte[width * height];

            for (int i = 0; i < data.Length; i++)
            {
                int pixel = inPixels[i];

                if (((pixel >> 16) & 0xff) != 0)
                {
                    data[i] = 0;
                    idata[i] = 255;
                }
                else
                    data[i] = 255;
            }

            float[] values = LinearSweep.AnalyzeGrayscale(data, width, height, false);
            float[] ivalues = LinearSweep.AnalyzeGrayscale(idata, width, height, false);

            for (int i = 0; i < values.Length; i++)
            {
                float nvalue = (data[i] == 0 ? values[i] : 1.0f - ivalues[i]) / vectorWidth;
                if (nvalue < -1.0f)
                    continue;

                if (nvalue > 1.0f)
                    nvalue = 1.0f;

                nvalue = nvalue * 0.5f + 0.5f;
                int ivalue = (int)(nvalue * 255.0f) & 0xff;

                resultPixels[i] = (int)((ivalue << 16) | (ivalue << 8) | (ivalue) | (ivalue << 24));
            }
        }

        /// <summary>
        /// Brute force search algorithm. Searches for nearest zero pixel
        /// </summary>
        /// <param name="inPixels">In pixels.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="resultPixels">Result pixels.</param>
        /// <param name="vectorWidth">Vector width.</param>
        private static void ProcessBruteForce(int[] inPixels, int width, int height, int[] resultPixels, int vectorWidth)
        {
            Vector3i[] nibble;

            {
                nibble = new Vector3i[(vectorWidth * 2 + 1) * (vectorWidth * 2 + 1)];

                int maxRadiusSqrd = vectorWidth * vectorWidth;

                for (int ny = -vectorWidth; ny <= vectorWidth; ny++)
                    for (int nx = -vectorWidth; nx <= vectorWidth; nx++)
                    {
                        int distanceSqrd = nx * nx + ny * ny;

                        if (distanceSqrd > maxRadiusSqrd)
                            distanceSqrd = maxRadiusSqrd;

                        int nindex = nx + vectorWidth + (ny + vectorWidth) * (vectorWidth * 2 + 1);

                        nibble[nindex] = new Vector3i(nx, ny, distanceSqrd);
                    }

                Array.Sort(nibble,(x, y) => x.Z.CompareTo(y.Z));
            }

            float[] vectorPixels = new float[width * height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = x + y * width;

                    int selfPixel = ((inPixels[index] >> 16) & 0xff) == 0 ? 1 : 0;
                    float nearest = 2.0f;

                    for (int i = 0; i < nibble.Length; i++)
                    {
                        int nx = nibble[i].X + x;
                        int ny = nibble[i].Y + y;

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            int pixelIndex = nx + ny * width;

                            int pixel = (((inPixels[pixelIndex] >> 16) & 0xff) == 0 ? 1 : 0);

                            if (pixel != selfPixel)
                            {
                                nearest = (float)Math.Sqrt(nibble[i].Z) / vectorWidth;
                                break;
                            }
                        }
                    }

                    vectorPixels[index] = selfPixel == 0 ? nearest : -nearest;
                }

            for (int i = 0; i < vectorPixels.Length; i++)
            {
                if (vectorPixels[i] < -1.0f)
                    continue;

                if (vectorPixels[i] > 1.0f)
                    vectorPixels[i] = 1.0f;

                float nvalue = vectorPixels[i] * 0.5f + 0.5f;

                int ivalue = (int)(nvalue * 255.0f) & 0xff;

                resultPixels[i] = (int)((ivalue << 16) | (ivalue << 8) | (ivalue) | (ivalue << 24));
            }
        }

        /// <summary>
        /// Signed Weight Field algorithm
        /// </summary>
        /// <param name="inPixels">In pixels.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="resultPixels">Result pixels.</param>
        /// <param name="vectorWidth">Vector width.</param>
        private static void ProcessSWF(int[] inPixels, int width, int height, int[] resultPixels, int vectorWidth)
        {
            float[] nibble;

            {
                float sum = 0;
                nibble = new float[(vectorWidth * 2 + 1) * (vectorWidth * 2 + 1)];

                int maxRadiusSqrd = vectorWidth * vectorWidth;

                for (int ny = -vectorWidth; ny <= vectorWidth; ny++)
                    for (int nx = -vectorWidth; nx <= vectorWidth; nx++)
                    {
                        int distanceSqrd = nx * nx + ny * ny;

                        if (distanceSqrd > maxRadiusSqrd)
                            continue;

                        int nindex = nx + vectorWidth + (ny + vectorWidth) * (vectorWidth * 2 + 1);

                        float value;
                        if (nx == 0 && ny == 0)
                            value = 1.0f;
                        else
                            value = (float)Math.Pow(distanceSqrd, -1.0); // 1/distance^5

                        nibble[nindex] = value;
                        sum += value;
                    }

                for (int i = 0; i < nibble.Length; i++)
                    nibble[i] /= sum;
            }

            float[] vectorPixels = new float[width * height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = x + y * width;

                    for (int ny = -vectorWidth; ny <= vectorWidth; ny++)
                        for (int nx = -vectorWidth; nx <= vectorWidth; nx++)
                            if (nx + x >= 0 && nx + x < width && ny + y >= 0 && ny + y < height)
                            {
                                int nindex = nx + vectorWidth + (ny + vectorWidth) * (vectorWidth * 2 + 1);

                                if (nibble[nindex] < 0.01f)
                                    continue;

                                int pixelIndex = (nx + x) + (ny + y) * width;

                                int pixel = inPixels[pixelIndex];

                                if (((pixel >> 16) & 0xff) == 0)
                                    continue;

                                vectorPixels[index] += nibble[nindex];
                            }
                }


            for (int i = 0; i < vectorPixels.Length; i++)
            {
                float nvalue = vectorPixels[i];

                if (nvalue <= 0.0f)
                    continue;

                if (nvalue > 1.0f)
                    nvalue = 1.0f;

                int ivalue = (int)(nvalue * 255.0f) & 0xff;

                resultPixels[i] = (int)((ivalue << 16) | (ivalue << 8) | (ivalue) | (ivalue << 24));
            }
        }

        private static void ProcessEikonal(int[] inPixels, int width, int height, int[] resultPixels, int vectorWidth)
        {
            Eikonal eikonal = new Eikonal(width, height, (int arg) => inPixels[arg] != 0 ? 0.5f : -0.5f);

            eikonal.EikonalSweep();

            float[] values = eikonal.End();

            for (int i = 0; i < values.Length; i++)
            {
                float nvalue = values[i] / vectorWidth;
                if (nvalue <= 0.0f)
                    continue;

                if (nvalue > 1.0f)
                    nvalue = 1.0f;

                int ivalue = (int)(nvalue * 255.0f) & 0xff;

                resultPixels[i] = (int)((ivalue << 16) | (ivalue << 8) | (ivalue) | (ivalue << 24));
            }
        }

        private static void ProcessDistanceField(string inFile, string outfile, int vectorWidth, string algo)
        {
            int width;
            int height;
            int[] inPixels = LoadBitmap(inFile, out width, out height);

            int[] resultPixels = new int[width * height];

            //
            switch(algo)
            {
                case "dr":
                    Console.WriteLine("Working with Dead Reckoning algorithm");
                    ProcessDR(inPixels, width, height, resultPixels, vectorWidth);
                    break;
                case "sweep":
                    Console.WriteLine("Working with Linear Sweep algorithm");
                    ProcessSweep(inPixels, width, height, resultPixels, vectorWidth);
                    break;
                case "brute":
                    Console.WriteLine("Working with Brute Force algorithm");
                    ProcessBruteForce(inPixels, width, height, resultPixels, vectorWidth);
                    break;
                case "swf":
                    Console.WriteLine("Working with Signed Weight Field algorithm");
                    ProcessSWF(inPixels, width, height, resultPixels, vectorWidth);
                    break;
                case "eikonal":
                    Console.WriteLine("Working with Eikonal algorithm");
                    ProcessEikonal(inPixels, width, height, resultPixels, vectorWidth);
                    break;
                default:
                    Console.WriteLine("Algorithm {0} not found", algo);
                    goto case "sweep";
            }

            //
            SaveBitmap(resultPixels, width, height, outfile);
        }

        #region Helpers

        /// <summary>
        /// Very simple method to load int32 array from PNG file with System.Drawing
        /// </summary>
        /// <returns>The bitmap.</returns>
        /// <param name="file">File.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public static int[] LoadBitmap(string file, out int width, out int height)
        {
            int[] buffer;
            using (Bitmap image = new Bitmap(file))
            {
                width = image.Width;
                height = image.Height;

                BitmapData bitmapdata = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                buffer = new int[bitmapdata.Width * bitmapdata.Height];

                Marshal.Copy(bitmapdata.Scan0, buffer, 0, buffer.Length);

                image.UnlockBits(bitmapdata);

                image.Dispose();
            }

            return buffer;
        }

        /// <summary>
        /// Very simple method to save int32 array to PNG file with System.Drawing
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="imageWidth">Image width.</param>
        /// <param name="imageHeight">Image height.</param>
        /// <param name="outFile">Out file.</param>
        public static void SaveBitmap(int[] data, int imageWidth, int imageHeight, string outFile)
        {
            GCHandle gch = default(GCHandle);

            try
            {
                gch = GCHandle.Alloc(data, GCHandleType.Pinned);
                using (var bitmap = new Bitmap(imageWidth, imageHeight, imageWidth * 4, PixelFormat.Format32bppArgb, gch.AddrOfPinnedObject()))
                {
                    string name = Path.GetFileNameWithoutExtension(outFile) + ".png";

                    Console.WriteLine("Saving bitmap {0}", name);

                    bitmap.Save(name);
                }
            }
            finally
            {
                if (gch != null && gch.IsAllocated)
                    gch.Free();
            }
        }

        #endregion

        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine(s_syntax);
                return;
            }

            string fileName = args[0];
            string outFileName = args[1];
            int width = args.Length > 2 ? int.Parse(args[2]) : 4;
            string algo = args.Length > 3 ? args[3] : "sweep";

            if (!File.Exists(fileName))
            {
                Console.Error.WriteLine("File {0} not found", fileName);
                return;
            }

            ProcessDistanceField(fileName, outFileName, width, algo);
        }
    }
}
