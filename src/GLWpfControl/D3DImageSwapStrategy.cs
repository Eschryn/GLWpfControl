using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;
using OpenTK.Graphics.OpenGL;
using SharpDX.Direct3D9;
using OpenTK.Platform.Windows;
using All = OpenTK.Graphics.OpenGL.All;

namespace OpenTK.Wpf
{
    internal sealed class D3DImageSwapStrategy : ISwapStrategy<WriteableBitmap>
    {
        [DllImport("kernel32.dll")]
        private static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        private bool _hasRenderedAFrame = false;
        private int[] _pixelBuffers;

        private DeviceEx device;
        
        private IntPtr interopDevice;
        private IntPtr interopDepthBuffer;
        private IntPtr interopColorBuffer;
        
        private ImageSource _bitmap;
        private int _colorBuffer;
        private int _depthBuffer;
        
        public int FrameBuffer { get; private set; }

        private Surface depthSurface;

        public void Initialize(int width, int height, int pixelBufferCount)
        {
            var pp = new PresentParameters(width, height)
            {
                Windowed = false
            };
            device = new DeviceEx(new Direct3DEx(), 0, DeviceType.Hardware, IntPtr.Zero, CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded, pp);
            var mode = device.GetDisplayMode(0);
            var color = new Texture(device, width, height, 0, Usage.None, mode.Format, Pool.Default);

            interopDevice = Wgl.DXOpenDeviceNV(device.NativePointer);
            
            FrameBuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBuffer);

            _depthBuffer = GL.GenTexture();
            interopDepthBuffer = Wgl.DXRegisterObjectNV(interopDevice, depthSurface.NativePointer, 
                (uint)_depthBuffer, (int) All.TextureRectangle, WGL_NV_DX_interop.AccessReadWrite);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, 
                TextureTarget.TextureRectangle, _depthBuffer, 0);

            _colorBuffer = GL.GenTexture();
            interopColorBuffer = Wgl.DXRegisterObjectNV(interopDevice, depthSurface.NativePointer, 
                (uint)_depthBuffer, (int) All.TextureRectangle, WGL_NV_DX_interop.AccessReadWrite);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _colorBuffer, 0);

            var error = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (error != FramebufferErrorCode.FramebufferComplete) {
                //throw new GraphicsErrorException("Error creating frame buffer: " + error);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            
            // generate the pixel buffers
            _pixelBuffers = new int[pixelBufferCount];
            
            // RGBA8 buffer
            var size = sizeof(byte) * 4 * width * height;
            for (var i = 0; i < _pixelBuffers.Length; i++)
            {
                var pb = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.PixelPackBuffer, pb);
                GL.BufferData(BufferTarget.PixelPackBuffer, size, IntPtr.Zero, BufferUsageHint.StreamRead);
                _pixelBuffers[i] = pb;
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
        }

        // shifts all of the PBOs along by 1.
        private void RotatePixelBuffers()
        {
            var fst = _pixelBuffers[0];
            for (var i = 1; i < _pixelBuffers.Length; i++)
            {
                _pixelBuffers[i - 1] = _pixelBuffers[i];
            }
            _pixelBuffers[_pixelBuffers.Length - 1] = fst;
        }

        public void Swap(int frameBufferSource, WriteableBitmap target)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferSource);
            // start the (async) pixel transfer.
            GL.BindBuffer(BufferTarget.PixelPackBuffer, _pixelBuffers[0]);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.ReadPixels(0, 0, target.PixelWidth, target.PixelHeight, Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            // rotate the pixel buffers.
            if (_hasRenderedAFrame)
            {
                RotatePixelBuffers();
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, _pixelBuffers[0]);
            // copy the data over from a mapped buffer.
            target.Lock();
            var data = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly);
            CopyMemory(target.BackBuffer, data, (uint)(sizeof(byte) * 4 * target.PixelWidth * target.PixelHeight));
            target.AddDirtyRect(new Int32Rect(0, 0, target.PixelWidth, target.PixelHeight));
            target.Unlock();
            GL.UnmapBuffer(BufferTarget.PixelPackBuffer);

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);

            _hasRenderedAFrame = true;
        }

        void ISwapStrategy.Swap(int frameBufferSource, ImageSource target)
        {
            Swap(frameBufferSource, (WriteableBitmap)target);
        }

        public ImageSource MakeTarget(int width, int height)
        {
            return new D3DImage();
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    foreach (var t in _pixelBuffers)
                    {
                        GL.DeleteBuffer(t);
                    }
                }

                Wgl.DXUnregisterObjectNV(interopDevice, interopDepthBuffer);
                Wgl.DXCloseDeviceNV(interopDevice);

                _disposedValue = true;
            }
        }

        ~D3DImageSwapStrategy()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
