using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ABGM
{
    /// <summary>
    /// Encodes a System.Drawing.Bitmap into a BLP2 file (DXT1 or DXT5).
    /// Layout: 148-byte header + 1024-byte null palette + DXT mip data.
    /// </summary>
    public static class Blp2Encoder
    {
        private static readonly byte[] Magic = { (byte)'B', (byte)'L', (byte)'P', (byte)'2' };
        private const int  ContentDirect = 1;
        private const byte EncodingDxtc  = 2;
        private const int  HeaderSize    = 148;
        private const int  PaletteSize   = 1024; // 256 x BGRA, always zero for DXT

        public static byte[] Encode(Bitmap source, bool mipmaps = true, bool dxt5 = false)
        {
            using var ms = new MemoryStream();
            Encode(source, ms, mipmaps, dxt5);
            return ms.ToArray();
        }

        public static void Encode(Bitmap source, Stream output, bool mipmaps = true, bool dxt5 = false)
        {
            var base0 = dxt5 ? ToRgba32(source) : ToRgb24(source);
            int w = base0.Width, h = base0.Height;

            // Generate mip chain
            var mipImages = new List<Bitmap> { base0 };
            if (mipmaps)
            {
                int mw = w, mh = h;
                while ((mw > 1 || mh > 1) && mipImages.Count < 16)
                {
                    mw = Math.Max(1, mw / 2);
                    mh = Math.Max(1, mh / 2);
                    mipImages.Add(ResizeBilinear(base0, mw, mh));
                }
            }

            // Compress each mip
            var mipData = new List<byte[]>(mipImages.Count);
            foreach (var img in mipImages)
                mipData.Add(dxt5 ? CompressDxt5(img) : CompressDxt1(img));

            // Dispose intermediate mips
            for (int i = 1; i < mipImages.Count; i++)
                mipImages[i].Dispose();
            base0.Dispose();

            // BLP2 header fields that differ between DXT1 and DXT5
            byte alphaDepth    = dxt5 ? (byte)8 : (byte)0;
            byte alphaEncoding = dxt5 ? (byte)7 : (byte)0;

            // Build offsets
            int dataStart = HeaderSize + PaletteSize; // 1172
            var offsets   = new int[16];
            var sizes     = new int[16];
            int offset    = dataStart;
            for (int i = 0; i < mipData.Count; i++)
            {
                offsets[i] = offset;
                sizes[i]   = mipData[i].Length;
                offset    += mipData[i].Length;
            }

            using var bw = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);

            bw.Write(Magic);
            bw.Write(ContentDirect);
            bw.Write(EncodingDxtc);
            bw.Write(alphaDepth);
            bw.Write(alphaEncoding);
            bw.Write((byte)(mipmaps ? 1 : 0));
            bw.Write(w);
            bw.Write(h);
            foreach (var o in offsets) bw.Write(o);
            foreach (var s in sizes)   bw.Write(s);

            if (output.Position != HeaderSize)
                throw new InvalidOperationException(
                    $"BLP header size mismatch: {output.Position} vs {HeaderSize}");

            bw.Write(new byte[PaletteSize]);
            foreach (var data in mipData) bw.Write(data);
        }

        private static byte[] CompressDxt1(Bitmap img)
        {
            int pw = (img.Width  + 3) & ~3;
            int ph = (img.Height + 3) & ~3;
            var pixels = LockToRgbArray(img, pw, ph);
            var result = new byte[(pw / 4) * (ph / 4) * 8];
            int idx = 0;
            for (int by = 0; by < ph; by += 4)
            for (int bx = 0; bx < pw; bx += 4)
            {
                var block = new (byte R, byte G, byte B)[16];
                for (int dy = 0; dy < 4; dy++)
                for (int dx = 0; dx < 4; dx++)
                    block[dy * 4 + dx] = pixels[(by + dy) * pw + (bx + dx)];
                CompressBlockDxt1(block, result, idx);
                idx += 8;
            }
            return result;
        }

        private static void CompressBlockDxt1(
            (byte R, byte G, byte B)[] pixels, byte[] output, int outIdx)
        {
            byte rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
            foreach (var (r, g, b) in pixels)
            {
                if (r < rMin) rMin = r; if (r > rMax) rMax = r;
                if (g < gMin) gMin = g; if (g > gMax) gMax = g;
                if (b < bMin) bMin = b; if (b > bMax) bMax = b;
            }

            ushort c0 = PackRgb565(rMax, gMax, bMax);
            ushort c1 = PackRgb565(rMin, gMin, bMin);
            if (c0 <= c1) { var tmp = c0; c0 = c1; c1 = tmp; }

            var (r0, g0, b0) = UnpackRgb565(c0);
            var (r1, g1, b1) = UnpackRgb565(c1);
            var pal = new (int R, int G, int B)[4]
            {
                (r0, g0, b0),
                (r1, g1, b1),
                ((2*r0+r1)/3, (2*g0+g1)/3, (2*b0+b1)/3),
                ((r0+2*r1)/3, (g0+2*g1)/3, (b0+2*b1)/3)
            };

            uint indices = 0;
            for (int i = 0; i < 16; i++)
            {
                var (pr, pg, pb) = pixels[i];
                int best = 0, bestD = int.MaxValue;
                for (int j = 0; j < 4; j++)
                {
                    int dr = pr-pal[j].R, dg = pg-pal[j].G, db = pb-pal[j].B;
                    int d  = dr*dr + dg*dg + db*db;
                    if (d < bestD) { bestD = d; best = j; }
                }
                indices |= (uint)(best << (i * 2));
            }

            output[outIdx+0] = (byte)c0;
            output[outIdx+1] = (byte)(c0 >> 8);
            output[outIdx+2] = (byte)c1;
            output[outIdx+3] = (byte)(c1 >> 8);
            output[outIdx+4] = (byte)indices;
            output[outIdx+5] = (byte)(indices >> 8);
            output[outIdx+6] = (byte)(indices >> 16);
            output[outIdx+7] = (byte)(indices >> 24);
        }

        private static byte[] CompressDxt5(Bitmap img)
        {
            int pw = (img.Width  + 3) & ~3;
            int ph = (img.Height + 3) & ~3;
            var pixels = LockToRgbaArray(img, pw, ph);
            var result = new byte[(pw / 4) * (ph / 4) * 16];
            int idx = 0;
            for (int by = 0; by < ph; by += 4)
            for (int bx = 0; bx < pw; bx += 4)
            {
                var block = new (byte R, byte G, byte B, byte A)[16];
                for (int dy = 0; dy < 4; dy++)
                for (int dx = 0; dx < 4; dx++)
                    block[dy * 4 + dx] = pixels[(by + dy) * pw + (bx + dx)];
                CompressBlockDxt5(block, result, idx);
                idx += 16;
            }
            return result;
        }

        private static void CompressBlockDxt5(
            (byte R, byte G, byte B, byte A)[] pixels, byte[] output, int outIdx)
        {
            // Alpha block (8 bytes)
            byte aMin = 255, aMax = 0;
            foreach (var p in pixels)
            {
                if (p.A < aMin) aMin = p.A;
                if (p.A > aMax) aMax = p.A;
            }

            output[outIdx]     = aMax;
            output[outIdx + 1] = aMin;

            // 8-entry alpha palette
            var aPal = new byte[8];
            aPal[0] = aMax; aPal[1] = aMin;
            if (aMax > aMin)
            {
                aPal[2] = (byte)((6*aMax + 1*aMin) / 7);
                aPal[3] = (byte)((5*aMax + 2*aMin) / 7);
                aPal[4] = (byte)((4*aMax + 3*aMin) / 7);
                aPal[5] = (byte)((3*aMax + 4*aMin) / 7);
                aPal[6] = (byte)((2*aMax + 5*aMin) / 7);
                aPal[7] = (byte)((1*aMax + 6*aMin) / 7);
            }
            else
            {
                for (int i = 2; i < 8; i++) aPal[i] = aMax;
            }

            // 3-bit indices for 16 pixels packed into 6 bytes
            ulong aBits = 0;
            for (int i = 0; i < 16; i++)
            {
                int best = 0, bestD = int.MaxValue;
                for (int j = 0; j < 8; j++)
                {
                    int d = Math.Abs(pixels[i].A - aPal[j]);
                    if (d < bestD) { bestD = d; best = j; }
                }
                aBits |= (ulong)best << (i * 3);
            }
            for (int i = 0; i < 6; i++)
                output[outIdx + 2 + i] = (byte)(aBits >> (i * 8));

            // Color block (8 bytes) — same as DXT1
            var rgbBlock = new (byte R, byte G, byte B)[16];
            for (int i = 0; i < 16; i++)
                rgbBlock[i] = (pixels[i].R, pixels[i].G, pixels[i].B);
            CompressBlockDxt1(rgbBlock, output, outIdx + 8);
        }

        private static ushort PackRgb565(byte r, byte g, byte b)
            => (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));

        private static (int R, int G, int B) UnpackRgb565(ushort c)
        {
            int r5 = (c >> 11) & 0x1F;
            int g6 = (c >>  5) & 0x3F;
            int b5 =  c        & 0x1F;
            // Expand to 8-bit: replicate high bits into low bits
            return (r5 << 3 | r5 >> 2,
                    g6 << 2 | g6 >> 4,
                    b5 << 3 | b5 >> 2);
        }

        private static Bitmap ToRgb24(Bitmap src)
        {
            if (src.PixelFormat == PixelFormat.Format24bppRgb) return new Bitmap(src);
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(src, 0, 0, src.Width, src.Height);
            return bmp;
        }

        private static Bitmap ToRgba32(Bitmap src)
        {
            if (src.PixelFormat == PixelFormat.Format32bppArgb) return new Bitmap(src);
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(src, 0, 0, src.Width, src.Height);
            return bmp;
        }

        private static (byte R, byte G, byte B)[] LockToRgbArray(Bitmap img, int pw, int ph)
        {
            int w = img.Width, h = img.Height;
            var data   = img.LockBits(new Rectangle(0, 0, w, h),
                             ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var pixels = new (byte R, byte G, byte B)[pw * ph];
            int stride = data.Stride;
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte* p = ptr + y * stride + x * 3;
                    pixels[y * pw + x] = (p[2], p[1], p[0]); // BGR -> RGB
                }
            }
            img.UnlockBits(data);
            return pixels;
        }

        private static (byte R, byte G, byte B, byte A)[] LockToRgbaArray(Bitmap img, int pw, int ph)
        {
            int w = img.Width, h = img.Height;
            var data   = img.LockBits(new Rectangle(0, 0, w, h),
                             ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new (byte R, byte G, byte B, byte A)[pw * ph];
            int stride = data.Stride;
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte* p = ptr + y * stride + x * 4;
                    pixels[y * pw + x] = (p[2], p[1], p[0], p[3]); // BGRA -> RGBA
                }
            }
            img.UnlockBits(data);
            return pixels;
        }

        private static Bitmap ResizeBilinear(Bitmap src, int newW, int newH)
        {
            var fmt = src.PixelFormat == PixelFormat.Format32bppArgb
                ? PixelFormat.Format32bppArgb
                : PixelFormat.Format24bppRgb;
            var dst = new Bitmap(newW, newH, fmt);
            using var g = Graphics.FromImage(dst);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, newW, newH);
            return dst;
        }
    }
}