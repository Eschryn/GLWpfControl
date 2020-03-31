using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OpenTK.Wpf
{
    internal interface ISwapStrategy : IDisposable
    {
        void Initialize(int width, int height, int pixelBufferCount);

        void Swap(int frameBufferSource, ImageSource target);

        ImageSource MakeTarget(int width, int height);
    }


    internal interface ISwapStrategy<T> : ISwapStrategy where T : ImageSource
    {
        void Swap(int frameBufferSource, T target);


    }
}
