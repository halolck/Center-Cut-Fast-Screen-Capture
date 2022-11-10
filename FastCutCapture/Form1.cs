using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FastCutCapture
{
    public partial class Form1 : Form
    {
        int h = Screen.PrimaryScreen.Bounds.Height;
        int w = Screen.PrimaryScreen.Bounds.Width;
        public Rectangle rect;
        ScreenStateLogger screenStateLogger = new ScreenStateLogger();
        int i = 0;
        bool captureflag = false;

        public Form1()
        {
            InitializeComponent();
            numericUpDown1.Maximum = w;
            numericUpDown2.Maximum = h;
            screenStateLogger.ChangeSize(new Rectangle(w / 2 - (int)numericUpDown1.Value / 2, h / 2 - (int)numericUpDown2.Value / 2, (int)numericUpDown1.Value, (int)numericUpDown2.Value));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (captureflag)
            {
                screenStateLogger.Stop();
                pictureBox1.Image = null;
            }
            else
            {
                capturetest();
            }
            captureflag = !captureflag;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            screenStateLogger.ChangeSize(new Rectangle(w / 2 - (int)numericUpDown1.Value / 2, h / 2 - (int)numericUpDown2.Value / 2, (int)numericUpDown1.Value, (int)numericUpDown2.Value));

        }

        private void capturetest()
        {
            screenStateLogger.ScreenRefreshed += (sender, data) =>
            {
                //New frame in dataMemoryStream ms = new MemoryStream(img);
                MemoryStream ms = new MemoryStream(data);

                Bitmap bibit = new Bitmap(ms);
                pictureBox1.Image = bibit;
                ms.Close();
                i++;
            };
            screenStateLogger.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.Text = "Capture FPS: " + i.ToString();
            i = 0;
        }
    }
    public class ScreenStateLogger
    {
        bool CaptureSizeChangeFlug = false;

        private bool _run, _init;
        private Rectangle rects = new Rectangle(0, 0, 100, 100);

        public ScreenStateLogger()
        {

        }

        public void ChangeSize(Rectangle rec)
        {
            rects = rec;
            CaptureSizeChangeFlug = false;
        }

        public void Start()
        {
            _run = true;
            var factory = new Factory1();
            //Get first adapter
            var adapter = factory.GetAdapter1(0);
            //Get device from adapter
            var device = new SharpDX.Direct3D11.Device(adapter);
            //Get front buffer of the adapter
            var output = adapter.GetOutput(0);
            var output1 = output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            int width = output.Description.DesktopBounds.Right;
            int height = output.Description.DesktopBounds.Bottom;

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription 
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            var screenTexture = new Texture2D(device, textureDesc);

            Task.Factory.StartNew(() =>
            {
                // Duplicate the output
                using (var duplicatedOutput = output1.DuplicateOutput(device))
                {
                    while (_run)
                    {
                        try
                        {
                            
                            Rectangle rect = rects;
                            if (CaptureSizeChangeFlug)
                            {
                                textureDesc.Height = rects.Height;
                                textureDesc.Width = rects.Width;
                                CaptureSizeChangeFlug = false;
                            }
                            
                            SharpDX.DXGI.Resource screenResource;
                            OutputDuplicateFrameInformation duplicateFrameInformation;

                            // Try to get duplicated frame within given time is ms
                            duplicatedOutput.AcquireNextFrame(5, out duplicateFrameInformation, out screenResource);

                            // copy resource into memory that can be accessed by the CPU
                            using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                                device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

                            // Get the desktop capture texture
                            var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                            // Create Drawing.Bitmap
                            using (var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
                            {
                                // Copy pixels from screen capture Texture to GDI bitmap
                                var mapDest = bitmap.LockBits(new Rectangle(0, 0, rect.Width, rect.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
                                if (1 + 1 == 2)
                                {
                                    var sourcePtr = mapSource.DataPointer + (rect.Left - 1) * 4 + width * (rect.Top - 1) * 4; //width /半分問題
                                    var destPtr = mapDest.Scan0;
                                    for (int y = 0; y < rect.Height; y++)
                                    {
                                        // Copy a single line 
                                        Utilities.CopyMemory(destPtr, sourcePtr, rect.Width * 4);

                                        // Advance pointers
                                        sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                                        destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                                    }

                                    // Release source and dest locks
                                    bitmap.UnlockBits(mapDest);
                                    device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                                    using (var ms = new MemoryStream())
                                    {
                                        bitmap.Save(ms, ImageFormat.Bmp);
                                        ScreenRefreshed?.Invoke(this, ms.ToArray());
                                        _init = true;
                                    }
                                }

                            }
                            screenResource.Dispose();
                            duplicatedOutput.ReleaseFrame();
                        }
                        catch (SharpDXException e)
                        {
                            if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                            {
                                Trace.TraceError(e.Message);
                                Trace.TraceError(e.StackTrace);
                            }
                        }
                    }
                }
            });
            while (!_init) ;
        }

        public void Stop()
        {
            _run = false;
        }

        public EventHandler<byte[]> ScreenRefreshed;
    }
}
