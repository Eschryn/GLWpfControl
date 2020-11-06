using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics.OpenGL;

namespace OpenTK.Wpf
{
    internal sealed class InteropBitmapSwapStrategy : ISwapStrategy<WriteableBitmap>
    {
        [DllImport("kernel32.dll")]
        private static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        private bool _hasRenderedAFrame = false;
        private int[] _pixelBuffers;

        public void Initialize(int width, int height, int pixelBufferCount)
        {
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
            return new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);
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

                _disposedValue = true;
            }
        }

        ~InteropBitmapSwapStrategy()
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
