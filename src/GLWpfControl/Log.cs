/*
Copyright 2019 Eschryn/zCore (https://gist.github.com/Eschryn)
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
documentation files (the "Software"), to deal in the Software without restriction, including without limitation the 
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit 
persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the 
Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE 
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR 
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR 
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using OpenTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using DbgSe = OpenTK.Graphics.OpenGL.DebugSeverity;
using DbgSrc = OpenTK.Graphics.OpenGL.DebugSource;
using DbgSrcExt = OpenTK.Graphics.OpenGL.DebugSourceExternal;
using DbgTy = OpenTK.Graphics.OpenGL.DebugType;

namespace OpenTK.Wpf
{
    public enum DebugSeverity
    {
        High = DbgSe.DebugSeverityHigh,
        Medium = DbgSe.DebugSeverityMedium,
        Low = DbgSe.DebugSeverityLow,
        Notification = DbgSe.DebugSeverityNotification,
        DontCare = DbgSe.DontCare,
    }

    public enum DebugSource
    {
        Api = DbgSrc.DebugSourceApi,
        Application = DbgSrc.DebugSourceApplication,
        Other = DbgSrc.DebugSourceOther,
        ShaderCompiler = DbgSrc.DebugSourceShaderCompiler,
        ThirdParty = DbgSrc.DebugSourceThirdParty,
        WindowSystem = DbgSrc.DebugSourceWindowSystem,
        DontCare = DbgSrc.DontCare
    }

    public enum DebugSourceExternal
    {
        Application = DbgSrcExt.DebugSourceApplication,
        ThirdParty = DbgSrcExt.DebugSourceThirdParty
    }

    public enum DebugType
    {
        DeprecatedBehavior = DbgTy.DebugTypeDeprecatedBehavior,
        Error = DbgTy.DebugTypeError,
        Marker = DbgTy.DebugTypeMarker,
        Other = DbgTy.DebugTypeOther,
        Performance = DbgTy.DebugTypePerformance,
        PopGroup = DbgTy.DebugTypePopGroup,
        Portability = DbgTy.DebugTypePortability,
        PushGroup = DbgTy.DebugTypePushGroup,
        UndefinedBehavior = DbgTy.DebugTypeUndefinedBehavior,
        DontCare = DbgTy.DontCare
    }
    public class DebugMessageEventArgs
    {
        public string Message { get; }
        public DebugSource Source { get; }
        public DebugType Type { get; }
        public int ID { get; }
        public DebugSeverity Severity { get; }
        public IntPtr UserParam { get; }

        public DebugMessageEventArgs(DebugSource source, DebugType type, int id, DebugSeverity severity, string msg, IntPtr userParam)
        {
            Source = source;
            Type = type;
            ID = id;
            Severity = severity;
            Message = msg;
            UserParam = userParam;
        }
    }

    public sealed class Context
    {
        private readonly Action dispose;

        public Context(Action dispose)
        {
            this.dispose = dispose;
        }

        public void Dispose()
        {
            dispose?.Invoke();
        }
    }

    public static class Log
    {
        const int DEBUG_BIT = (int)ContextFlagMask.ContextFlagDebugBit;
        const int GL_MAX_DEBUG_MESSAGE_LENGTH = 0x9143;

        private readonly static int MaxMessageLength;

        public delegate void DebugMessage(object sender, DebugMessageEventArgs e);
        public static event DebugMessage OnDebugMessage;

        [Conditional("SYNCHRONOUS_DEBUG")]
        static void DebugOutputSynchronous() => GL.Enable(EnableCap.DebugOutputSynchronous);

        static Log()
        {
            GL.GetInteger(GetPName.ContextFlags, out int flags);
            if ((flags & DEBUG_BIT) != 0)
                GL.Enable(EnableCap.DebugOutput);

            DebugOutputSynchronous();

            MaxMessageLength = GL.GetInteger((GetPName)GL_MAX_DEBUG_MESSAGE_LENGTH);

            GL.DebugMessageCallback(debugProcCallback, IntPtr.Zero);
        }

        public static void Activate(DebugSourceControl dsrcc, DebugTypeControl dtc, DebugSeverityControl dsc)
        {
            GL.DebugMessageControl(dsrcc, dtc, dsc, 0, new int[0], true);
        }

        public static void Activate(DebugSourceControl dsrcc, DebugTypeControl dtc, int[] ids)
        {
            GL.DebugMessageControl(dsrcc, dtc, DebugSeverityControl.DontCare, ids.Length, ids, true);
        }

        public static void Activate(DebugSourceControl dsrcc, DebugTypeControl dtc, int length, ref int ids)
        {
            GL.DebugMessageControl(dsrcc, dtc, DebugSeverityControl.DontCare, length, ref ids, true);
        }

        public static void Deactivate(DebugSourceControl dsrcc, DebugTypeControl dtc, DebugSeverityControl dsc)
        {
            GL.DebugMessageControl(dsrcc, dtc, dsc, 0, new int[0], false);
        }

        public static void Deactivate(DebugSourceControl dsrcc, DebugTypeControl dtc, int[] ids)
        {
            GL.DebugMessageControl(dsrcc, dtc, DebugSeverityControl.DontCare, ids.Length, ids, false);
        }

        public static void Deactivate(DebugSourceControl dsrcc, DebugTypeControl dtc, int length, ref int ids)
        {
            GL.DebugMessageControl(dsrcc, dtc, DebugSeverityControl.DontCare, length, ref ids, false);
        }

        public static Context PushGroup(DebugSourceExternal source, int id, string message)
        {
            GL.PushDebugGroup((DbgSrcExt)source, id, message.Length, message);
            return new Context(PopGroup);
        }

        public static void PopGroup() => GL.PopDebugGroup();

        public static void EvokeMessage(DebugSourceExternal dse, DebugType dt, int id, DebugSeverity ds, string msg)
        {
            if (msg.Length > MaxMessageLength)
                throw new ArgumentException("Message is too long");

            GL.DebugMessageInsert((DbgSrcExt)dse, (DbgTy)dt, id, (DbgSe)ds, msg.Length, msg);
        }

        public static string GetLog(int count)
        {
            GL.GetDebugMessageLog(count, MaxMessageLength, out _, out _, out _, out _, out _, out var res);
            return res;
        }

        private static DebugProc debugProcCallback = DebugCallback;
        private static void DebugCallback(DbgSrc source, DbgTy type, int id, DbgSe severity, int length, IntPtr message, IntPtr userParam)
        {
            var msg = Marshal.PtrToStringAnsi(message, length);
            OnDebugMessage?.Invoke(null, new DebugMessageEventArgs((DebugSource)source, (DebugType)type, id, (DebugSeverity)severity, msg, userParam));
        }
    }
}