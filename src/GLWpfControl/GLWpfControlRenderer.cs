#define SYNCHRONOUS_DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Platform.Windows;
using SharpDX.Direct3D9;
using All = OpenTK.Graphics.OpenGL.All;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace OpenTK.Wpf
{
    internal sealed class GLWpfControlRenderer
    {

        private readonly ImageSource _bitmap;
        private readonly int _colorBuffer;
        private readonly int _depthBuffer;
        private readonly ISwapStrategy _swapStrategy;

        private DeviceEx device;
        private Surface colorSurface;

        private IntPtr interopDevice;
        private IntPtr interopColorBuffer;

        public int FrameBuffer { get; }

        public int Width => (int)_bitmap.Width;
        public int Height => (int)_bitmap.Height;

        public ImageSource Source => _bitmap;

        public GLWpfControlRenderer(int width, int height, bool isHardwareRenderer, int pixelBufferCount)
        {
            Log.OnDebugMessage += (sender, e) =>
            {
                throw new Exception();
            };

            const CreateFlags DEVICE_FLAGS = CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.PureDevice;
            var @params = new PresentParameters(width, height)
            {
                Windowed = true,
                DeviceWindowHandle = IntPtr.Zero,
                PresentationInterval = PresentInterval.Default,
                SwapEffect = SwapEffect.Discard,
                BackBufferFormat = Format.Unknown
            };
            device = new DeviceEx(
                direct3D: new Direct3DEx(),
                adapter: 0,
                deviceType: DeviceType.Hardware,
                controlHandle: IntPtr.Zero,
                createFlags: DEVICE_FLAGS,
                presentParameters: @params);

            var colorSurfaceShare = IntPtr.Zero;
            colorSurface = Surface.CreateRenderTarget(
                device,
                width,
                height,
                format: Format.X8R8G8B8,
                multisampleType: MultisampleType.None,
                multisampleQuality: 0,
                lockable: false,
                sharedHandle: ref colorSurfaceShare);


            FrameBuffer = GL.GenFramebuffer();
            _colorBuffer = GL.GenTexture();

            interopDevice = Wgl.DXOpenDeviceNV(device.NativePointer);
            Wgl.DXSetResourceShareHandleNV(
                dxResource: colorSurface.NativePointer,
                shareHandle: colorSurfaceShare);

            interopColorBuffer = Wgl.DXRegisterObjectNV(
                hDevice: interopDevice,
                dxObject: colorSurface.NativePointer,
                name: (uint)_colorBuffer,
                type: (uint)All.Texture2D,
                access: WGL_NV_DX_interop.AccessReadWrite);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBuffer);
            GL.FramebufferTexture2D(
                target: FramebufferTarget.Framebuffer,
                attachment: FramebufferAttachment.ColorAttachment0,
                textarget: TextureTarget.Texture2D,
                texture: _colorBuffer,
                level: 0);

            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            _depthBuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, _depthBuffer);

            var error = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (error != FramebufferErrorCode.FramebufferComplete)
            {
                throw new GraphicsErrorException("Error creating frame buffer: " + error);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            _swapStrategy = new WritableBitmapSwapStrategy();
            _swapStrategy.Initialize(width, height, pixelBufferCount);

            _bitmap = _swapStrategy.MakeTarget(width, height);
        }

        public void DeleteBuffers()
        {
            GL.DeleteFramebuffer(FrameBuffer);
            GL.DeleteRenderbuffer(_depthBuffer);
            GL.DeleteRenderbuffer(_colorBuffer);

            Wgl.DXUnregisterObjectNV(interopDevice, interopColorBuffer);
            Wgl.DXCloseDeviceNV(interopDevice);
        }

        public void UpdateImage()
        {
            _swapStrategy.Swap(FrameBuffer, _bitmap);
        }

        public void BeginUpdate()
        {
            Wgl.DXLockObjectsNV(interopDevice, 1, interopColorBuffer);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBuffer);
            GL.Viewport(0, 0, Width, Height);
        }

        public void EndUpdate()
        {
            Wgl.DXUnlockObjectsNV(interopDevice, 1, interopColorBuffer);
        }

        private void UpdateImageHardware()
        {
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