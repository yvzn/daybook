// Builds design/daybook.ico from the designer's sheet (design/icon-sheet.png):
// crops the large icon at the top, removes its drop shadow, and scales it to 16, 32, 48 and 256 px.
//
// Usage: dotnet run --project tools/IconGen -- <sheet.png> <ico> [previewPng]

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    private static readonly int[] Sizes = [16, 32, 48, 256];

    [STAThread]
    private static void Main(string[] args)
    {
        var sheet = Load(args[0]);
        var icon = Square(Trim(RemoveShadow(TopIcon(sheet))));
        var frames = Sizes.ToDictionary(size => size, size => Scale(icon, size));

        WriteIco(args[1], frames);
        if (args.Length > 2) WritePreview(args[2], frames);
        Console.WriteLine($"Wrote {args[1]} ({string.Join(", ", Sizes)} px)");
    }

    private static Pixels Load(string path)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return Pixels.From(new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0));
    }

    /// <summary>The large icon sits in the top two thirds; the small previews and labels sit below.</summary>
    private static Pixels TopIcon(Pixels sheet) => Trim(sheet.Sub(0, 0, sheet.Width, sheet.Height * 2 / 3));

    /// <summary>
    /// The drop shadow is light grey. Flood-fill from the transparent surroundings across grey pixels
    /// and clear them; the white sun and cream pages are inside the blue cover, so the fill never reaches them.
    /// </summary>
    private static Pixels RemoveShadow(Pixels img)
    {
        var seen = new bool[img.Width * img.Height];
        var queue = new Queue<(int X, int Y)>();
        for (var x = 0; x < img.Width; x++) { queue.Enqueue((x, 0)); queue.Enqueue((x, img.Height - 1)); }
        for (var y = 0; y < img.Height; y++) { queue.Enqueue((0, y)); queue.Enqueue((img.Width - 1, y)); }

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || y < 0 || x >= img.Width || y >= img.Height || seen[y * img.Width + x]) continue;
            seen[y * img.Width + x] = true;
            if (img.Alpha(x, y) != 0 && !IsGrey(img, x, y)) continue;

            img.SetAlpha(x, y, 0);
            queue.Enqueue((x + 1, y)); queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1)); queue.Enqueue((x, y - 1));
        }
        return img;
    }

    private static bool IsGrey(Pixels img, int x, int y)
    {
        var (b, g, r) = img.Rgb(x, y);
        return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) <= 32;
    }

    /// <summary>Crops to the visible pixels.</summary>
    private static Pixels Trim(Pixels img)
    {
        int top = int.MaxValue, left = int.MaxValue, bottom = 0, right = 0;
        for (var y = 0; y < img.Height; y++)
            for (var x = 0; x < img.Width; x++)
                if (img.Alpha(x, y) != 0)
                {
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                    left = Math.Min(left, x); right = Math.Max(right, x);
                }

        return img.Sub(left, top, right - left + 1, bottom - top + 1);
    }

    private static BitmapSource Square(Pixels img)
    {
        var side = Math.Max(img.Width, img.Height);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawImage(img.ToBitmap(), new Rect((side - img.Width) / 2.0, (side - img.Height) / 2.0, img.Width, img.Height));
        var bitmap = new RenderTargetBitmap(side, side, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static BitmapSource Scale(BitmapSource source, int size)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.Fant);
        using (var dc = visual.RenderOpen())
            dc.DrawImage(source, new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static byte[] Png(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>ICO with PNG-compressed frames (supported since Windows Vista).</summary>
    private static void WriteIco(string path, Dictionary<int, BitmapSource> frames)
    {
        var images = frames.Select(f => (Size: f.Key, Data: Png(f.Value))).ToList();
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((short)0);             // reserved
        writer.Write((short)1);             // type: icon
        writer.Write((short)images.Count);

        var offset = 6 + 16 * images.Count;
        foreach (var (size, data) in images)
        {
            writer.Write((byte)(size >= 256 ? 0 : size)); // 0 means 256
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);          // palette colors
            writer.Write((byte)0);          // reserved
            writer.Write((short)1);         // color planes
            writer.Write((short)32);        // bits per pixel
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }
        foreach (var (_, data) in images) writer.Write(data);
    }

    /// <summary>Review sheet: each size at 1× on light and dark backgrounds.</summary>
    private static void WritePreview(string path, Dictionary<int, BitmapSource> frames)
    {
        const int cell = 270;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            string[] backgrounds = ["#FFFFFF", "#202020"];
            for (var row = 0; row < backgrounds.Length; row++)
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgrounds[row]));
                dc.DrawRectangle(brush, null, new Rect(0, row * cell, cell * frames.Count, cell));
                var col = 0;
                foreach (var (size, frame) in frames)
                    dc.DrawImage(frame, new Rect(col++ * cell + 7, row * cell + 7, size, size));
            }
        }

        var bitmap = new RenderTargetBitmap(cell * frames.Count, cell * 2, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        File.WriteAllBytes(path, Png(bitmap));
    }

    /// <summary>Editable BGRA pixel buffer.</summary>
    private sealed class Pixels(int width, int height, byte[] data)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;

        public static Pixels From(BitmapSource bitmap)
        {
            var data = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(data, bitmap.PixelWidth * 4, 0);
            return new Pixels(bitmap.PixelWidth, bitmap.PixelHeight, data);
        }

        public (int B, int G, int R) Rgb(int x, int y)
        {
            var i = (y * Width + x) * 4;
            return (data[i], data[i + 1], data[i + 2]);
        }

        public byte Alpha(int x, int y) => data[(y * Width + x) * 4 + 3];

        public void SetAlpha(int x, int y, byte alpha) => data[(y * Width + x) * 4 + 3] = alpha;

        public Pixels Sub(int left, int top, int width, int height)
        {
            var sub = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
                Array.Copy(data, ((top + y) * Width + left) * 4, sub, y * width * 4, width * 4);
            return new Pixels(width, height, sub);
        }

        public BitmapSource ToBitmap() =>
            BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, data, Width * 4);
    }
}
