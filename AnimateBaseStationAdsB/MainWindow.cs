﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace AnimateBaseStationAdsB
{
    internal class MainWindow : GameWindow
    {
        public PlaneTrack[] Planes { get; set; }
        public int Index { get; set; }
        public string Text { get; set; } = "Init...";

        public BitmapFont.BitmapFont Font { get; set; }

        private static int _texMap;

        private static float _mapWidth;
        private static float _mapHeight;

        public Vector3 WindowSize { get; set; }

        public double MapMinX;
        public double MapMaxX;
        public double MapMinY;
        public double MapMaxY;
        public double MapMinZ;
        public double MapMaxZ;
        public Vector3 MinVector { get; set; }
        public Vector3 MaxVector { get; set; }

        public bool NewData { get; set; }
        public int Frame { get; set; }
        public double R { get; set; }

        public DateTime StartTime;
        public DateTime EndTime;
        public DateTime CurrentTime;

        public MainWindow() : base(960, 540)
        {
            Resize += MainWindow_Resize;
            Load += MainWindow_Load;
            RenderFrame += MainWindow_RenderFrame;
            UpdateFrame += MainWindow_UpdateFrame;
        }

        private void MainWindow_UpdateFrame(object sender, FrameEventArgs e)
        {
            CurrentTime = CurrentTime.AddSeconds(30);
            if (CurrentTime > EndTime)
                Environment.Exit(0);
            //else
                //SaveScreen($"frames/{Frame++}.png");

            Title = $"{Frame} frames";
            Text = "Planes over Georgia\n" +
                    "@parzivail/cnewmanJax2012\n" +
                    $"Time: {CurrentTime}";
            R += 0.1f;
        }

        private void MainWindow_Resize(object sender, EventArgs e)
        {
            GL.Viewport(ClientRectangle);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, Width, Height, 0, -1000, 1000);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
        }

        private void MainWindow_RenderFrame(object sender, FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit |
                     ClearBufferMask.DepthBufferBit |
                     ClearBufferMask.StencilBufferBit);


            GL.PushMatrix();

            GL.PushMatrix();
            GL.Color4(0f, 0f, 0f, 1f);
            GL.Translate(10, 10, -10);
            GL.Enable(EnableCap.Texture2D);
            Font.RenderString(Text);
            GL.Disable(EnableCap.Texture2D);
            GL.PopMatrix();

            GL.Translate(Width / 2f, Height / 2f, 0);
            GL.Rotate(60, 1, 0, 0);
            GL.Rotate(R, 0, 0, 1);
            GL.Translate(-Width / 2f, -Height / 2f, 0);

            GL.PushMatrix();
            //GL.Translate(0, 0, -100);
            GL.Enable(EnableCap.Texture2D);
            GL.Color3(Color.White);
            GL.BindTexture(TextureTarget.Texture2D, _texMap);
            GL.Begin(PrimitiveType.Quads);
            GL.Color4(1, 1, 1, 0.5f);
            GL.TexCoord2(0, 0);
            GL.Vertex2(0, 0);
            GL.TexCoord2(1, 0);
            GL.Vertex2(Width, 0);
            GL.TexCoord2(1, 1);
            GL.Vertex2(Width, Height);
            GL.TexCoord2(0, 1);
            GL.Vertex2(0, Height);
            GL.End();
            GL.Disable(EnableCap.Texture2D);
            GL.PopMatrix();

            GL.PushMatrix();
            foreach (var plane in Planes)
            {
                if (plane.Start > CurrentTime || plane.End < CurrentTime)
                    continue;

                var curTimePercent = (CurrentTime - plane.Start).TotalMilliseconds / (plane.End - plane.Start).TotalMilliseconds;

                GL.Begin(PrimitiveType.LineStrip);
                var d = 0.3f;

                for (var i = curTimePercent - d; i < curTimePercent; i += d / 100)
                {
                    var point = plane.Spline.GetPoint(i);

                    var distance = 1 - (curTimePercent - i) / d;
                    var altColor = point.Z.Remap(0, WindowSize.Z, 0, 1).Clamp(0, 1);
                    GL.Color4(0, altColor, 1 - altColor, distance);

                    GL.Vertex3(point);
                }
                GL.Vertex3(plane.Spline.GetPoint(curTimePercent));
                GL.End();

                GL.Begin(PrimitiveType.Points);
                GL.Vertex3(plane.Spline.GetPoint(curTimePercent));
                GL.End();
            }
            GL.PopMatrix();

            GL.PopMatrix();
            SwapBuffers();
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            GL.ClearColor(Color.White);
            GL.Enable(EnableCap.DepthTest);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.PointSize(4);
            GL.LineWidth(2);
            Fx.D3.Init();

            var pair = new Bitmap("map.png").LoadGlTexture();
            _texMap = pair.Key;
            _mapWidth = pair.Value.Width;
            _mapHeight = pair.Value.Height;
            
            Planes = JsonConvert.DeserializeObject<List<PlaneTrack>>(File.ReadAllText("keyframes.json"))
                .Where(track => track.Keyframes.Count > 1 &&
                track.Start != DateTime.MinValue)
                .ToArray();
            var keyframes = Planes.SelectMany(kf => kf.Keyframes).ToArray();

            var avgX = keyframes.Select(ll => ll.Lon).Average();
            var avgY = keyframes.Select(ll => ll.Lat).Average();
            var avgZ = keyframes.Select(ll => ll.Alt).Average();

            keyframes = keyframes.Where(ll => Distance(ll.Lon, ll.Lat, avgX, avgY) < 10).ToArray();

            MapMinX = keyframes.Min(ll => ll.Lon);
            MapMaxX = keyframes.Max(ll => ll.Lon);
            MapMaxY = keyframes.Min(ll => ll.Lat);
            MapMinY = keyframes.Max(ll => ll.Lat);
            MapMaxZ = keyframes.Max(ll => ll.Alt);
            MapMinZ = keyframes.Min(ll => ll.Alt);

            MinVector = new Vector3((float)MapMinX, (float)MapMinY, (float)MapMinZ);
            MaxVector = new Vector3((float)MapMaxX, (float)MapMaxY, (float)MapMaxZ);
            MinVector = new Vector3((float)MapMinX, (float)MapMinY, (float)MapMinZ);
            MaxVector = new Vector3((float)MapMaxX, (float)MapMaxY, (float)MapMaxZ);

            StartTime = CurrentTime = Planes.Select(track => track.Start).Min();
            EndTime = Planes.Select(track => track.End).Max();

            Width = (int)_mapWidth;
            Height = (int)_mapHeight;

            WindowSize = new Vector3(Width, Height, 100);

            Console.WriteLine($"Window: {Width}x{Height}");
            Console.WriteLine($"Bounds: lon({MapMaxX},{MapMinX}) lat({MapMinY},{MapMaxY}) alt({MapMinZ},{MapMaxZ})");

            foreach (var planeTrack in Planes)
                planeTrack.Spline =
                    new Spline3D(
                        planeTrack.Keyframes.Select(
                            ll =>
                                new Vector3((float)ll.Lon, (float)ll.Lat, (float)ll.Alt).Remap(MinVector, MaxVector,
                                    Vector3.Zero, WindowSize)).ToList());

            Font = BitmapFont.BitmapFont.LoadBinaryFont("dina", Assets.FntDina, Assets.PageDina);
        }

        public double Distance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }

        public void SaveScreen(string filename)
        {
            using (var bmp = new Bitmap(ClientRectangle.Width, ClientRectangle.Height))
            {
                var data = bmp.LockBits(ClientRectangle, ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                GL.ReadPixels(0, 0, ClientRectangle.Width, ClientRectangle.Height, PixelFormat.Bgr,
                    PixelType.UnsignedByte, data.Scan0);
                bmp.UnlockBits(data);
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

                bmp.Save(filename, ImageFormat.Png);
            }
        }
    }
}