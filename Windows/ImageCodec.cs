using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Copilume;

public static class ImageCodec
{
    public static BitmapSource Decode(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 32_000_000) throw new InvalidDataException();
        using var stream = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || frame.PixelWidth > 8192 || frame.PixelHeight > 8192 ||
            (long)frame.PixelWidth * frame.PixelHeight > 16_000_000) throw new InvalidDataException();
        // Inspect dimensions before forcing full raster decoding. Then detach from the input stream.
        stream.Position = 0;
        var detached = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        detached.Freeze();
        return detached;
    }
    public static StoredImage Normalize(byte[] bytes, bool dib)
    {
        if (dib) bytes = DibToBmp(bytes);
        var frame = Decode(bytes);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var output = new MemoryStream();
        encoder.Save(output);
        var image = new StoredImage(output.ToArray(), frame.PixelWidth, frame.PixelHeight);
        if (!image.IsValid) throw new InvalidDataException();
        return image;
    }
    public static BitmapSource Thumbnail(StoredImage image)
    {
        using var stream = new MemoryStream(image.Png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = Math.Min(256, image.Width);
        bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
    public static byte[] DibToBmp(byte[] dib)
    {
        if (dib.Length < 40 || dib.Length > 32_000_000) throw new InvalidDataException();
        int header = BitConverter.ToInt32(dib, 0), width = BitConverter.ToInt32(dib, 4), height = BitConverter.ToInt32(dib, 8);
        ushort bits = BitConverter.ToUInt16(dib, 14);
        uint compression = BitConverter.ToUInt32(dib, 16), colors = BitConverter.ToUInt32(dib, 32);
        if (header is not (40 or 108 or 124) || header > dib.Length || width <= 0 || width > 8192 || height == 0 ||
            Math.Abs((long)height) > 8192 || width * Math.Abs((long)height) > 16_000_000 ||
            BitConverter.ToUInt16(dib, 12) != 1 || bits is not (1 or 4 or 8 or 16 or 24 or 32) || compression is not (0 or 3 or 6))
            throw new InvalidDataException();
        if (colors > 256) throw new InvalidDataException();
        long palette = colors > 0 ? colors : bits <= 8 ? 1L << bits : 0;
        long offset = header + palette * 4 + (header == 40 && compression == 3 ? 12 : header == 40 && compression == 6 ? 16 : 0);
        long stride = ((long)width * bits + 31) / 32 * 4;
        if (offset + stride * Math.Abs((long)height) > dib.Length) throw new InvalidDataException();
        var bmp = new byte[dib.Length + 14];
        using var writer = new BinaryWriter(new MemoryStream(bmp));
        writer.Write((ushort)0x4D42); writer.Write(bmp.Length); writer.Write(0); writer.Write((int)offset + 14); writer.Write(dib);
        return bmp;
    }
    public static StoredImage Demo()
    {
        var pixels = new byte[320 * 180 * 4];
        for (int y = 0; y < 180; y++) for (int x = 0; x < 320; x++)
        { int p = (y * 320 + x) * 4; pixels[p] = 144; pixels[p + 1] = (byte)(80 + y / 2); pixels[p + 2] = (byte)(14 + x / 3); pixels[p + 3] = 255; }
        var bitmap = BitmapSource.Create(320, 180, 96, 96, PixelFormats.Bgra32, null, pixels, 320 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output);
        return new(output.ToArray(), 320, 180);
    }
}
