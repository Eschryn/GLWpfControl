using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace OpenTK.Wpf {
    internal sealed class GLWpfControlRenderer {

        private readonly ImageSource _bitmap;
        private readonly int _colorBuffer;
        private readonly int _depthBuffer;

        private readonly bool _isHardwareRenderer;
        private readonly ISwapStrategy _swapStrategy;
        
        public int FrameBuffer { get; }

        public int Width => (int)_bitmap.Width;
        public int Height => (int)_bitmap.Height;

        public ImageSource Source => _bitmap;

        public GLWpfControlRenderer(int width, int height, bool isHardwareRenderer, int pixelBufferCount) {
            
            _isHardwareRenderer = isHardwareRenderer;

            // set up the framebuffer
            FrameBuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBuffer);

            _depthBuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, _depthBuffer);

            _colorBuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _colorBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                RenderbufferTarget.Renderbuffer, _colorBuffer);

            var error = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (error != FramebufferErrorCode.FramebufferComplete) {
                throw new GraphicsErrorException("Error creating frame buffer: " + error);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            _swapStrategy = new WritableBitmapSwapStrategy();
            _swapStrategy.Initialize(width, height, pixelBufferCount);

            _bitmap = _swapStrategy.MakeTarget(width, height);
        }

        public void DeleteBuffers() {
            GL.DeleteFramebuffer(FrameBuffer);
            GL.DeleteRenderbuffer(_depthBuffer);
            GL.DeleteRenderbuffer(_colorBuffer);

            _swapStrategy.Dispose();
        }

        public void UpdateImage()
        {
            _swapStrategy.Swap(FrameBuffer, _bitmap);
        }

        public void BeginUpdate()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBuffer);
            GL.Viewport(0, 0, Width, Height);
        }

        public void EndUpdate()
        {

        }

        private void UpdateImageHardware() {
            // There are 2 options we can use here.
            // 1. Use a D3DSurface and WGL_NV_DX_interop to perform the rendering.
            //         This is still performing RTT (render to texture) and isn't as fast as just directly drawing the stuff onto the DX buffer.
            // 2. Steal the handles using hooks into DirectX, then use that to directly render.
            //         This is the fastest possible way, but it requires a whole lot of moving parts to get anything working properly.
                
            // references for (2):
                
            // Accessing WPF's Direct3D internals.
            // note: see the WPFD3dHack.zip file on the blog post
            // http://jmorrill.hjtcentral.com/Home/tabid/428/EntryId/438/How-to-get-access-to-WPF-s-internal-Direct3D-guts.aspx
                
            // Using API hooks from C# to get d3d internals
            // this would have to be adapted to WPF, but should/maybe work.
            // http://spazzarama.com/2011/03/14/c-screen-capture-and-overlays-for-direct3d-9-10-and-11-using-api-hooks/
            // https://github.com/spazzarama/Direct3DHook
            throw new NotImplementedException();
        }
        
    }
}
