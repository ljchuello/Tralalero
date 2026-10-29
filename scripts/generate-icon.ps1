# Generates Tralalero/Assets/tralalero.ico with a small microphone glyph.
# Run with Windows PowerShell 5.1: powershell.exe -ExecutionPolicy Bypass -File scripts\generate-icon.ps1
$ErrorActionPreference = 'Stop'

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class TralaleroIconGenerator
{
    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float d = radius * 2f;
        path.AddArc(r.X, r.Y, d, d, 180f, 90f);
        path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
        path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    private static Bitmap Render(int s)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float pad = s * 0.06f;
            float radius = s * 0.24f;
            using (GraphicsPath path = RoundedRect(new RectangleF(pad, pad, s - 2f * pad, s - 2f * pad), radius))
            using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(79, 70, 229), Color.FromArgb(14, 165, 233), 45f))
            {
                g.FillPath(brush, path);
            }

            float cx = s / 2f;
            float micW = s * 0.20f;
            float micH = s * 0.36f;
            float micTop = s * 0.20f;
            using (GraphicsPath mic = RoundedRect(new RectangleF(cx - micW / 2f, micTop, micW, micH), micW / 2f))
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillPath(white, mic);
            }

            using (Pen pen = new Pen(Color.White, Math.Max(1.2f, s * 0.045f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, cx - s * 0.155f, micTop + s * 0.13f, s * 0.31f, s * 0.30f, 0f, 180f);
                g.DrawLine(pen, cx, s * 0.58f, cx, s * 0.70f);
                g.DrawLine(pen, cx - s * 0.10f, s * 0.70f, cx + s * 0.10f, s * 0.70f);
            }

            using (Pen wave = new Pen(Color.FromArgb(190, 242, 255), Math.Max(1f, s * 0.018f)))
            {
                wave.StartCap = LineCap.Round;
                wave.EndCap = LineCap.Round;
                float y = s * 0.84f;
                float[] xs = new float[] { 0.18f, 0.27f, 0.36f, 0.45f, 0.54f, 0.63f, 0.72f, 0.82f };
                float[] hs = new float[] { 0.02f, 0.05f, 0.09f, 0.14f, 0.14f, 0.09f, 0.05f, 0.02f };
                List<PointF> pts = new List<PointF>();
                for (int i = 0; i < xs.Length; i++)
                {
                    pts.Add(new PointF(s * xs[i], y - s * hs[i]));
                }
                g.DrawCurve(wave, pts.ToArray());
            }
        }
        return bmp;
    }

    public static void Save(string path)
    {
        int[] sizes = new int[] { 16, 32, 48, 64, 128, 256 };
        List<byte[]> pngs = new List<byte[]>();
        foreach (int size in sizes)
        {
            using (Bitmap bmp = Render(size))
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }
        }

        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write((uint)pngs[i].Length);
                bw.Write((uint)offset);
                offset += pngs[i].Length;
            }
            foreach (byte[] png in pngs)
            {
                bw.Write(png);
            }
        }
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing

$assets = Join-Path $PSScriptRoot '..\Tralalero\Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$target = Join-Path $assets 'tralalero.ico'
[TralaleroIconGenerator]::Save($target)
Write-Host "Generated $target"
