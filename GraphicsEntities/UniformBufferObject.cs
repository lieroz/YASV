using System.Runtime.InteropServices;
using Silk.NET.Maths;

namespace YASV.GraphicsEntities;

[StructLayout(LayoutKind.Explicit)]
// TODO: generate UBO types?
public struct UniformBufferObject
{
    [FieldOffset(0)] private Matrix4X4<float> _model;
    [FieldOffset(64)] private Matrix4X4<float> _view;
    [FieldOffset(128)] private Matrix4X4<float> _projection;

    public Matrix4X4<float> Model { readonly get => _model; set => _model = value; }
    public Matrix4X4<float> View { readonly get => _view; set => _view = value; }
    public Matrix4X4<float> Projection { readonly get => _projection; set => _projection = value; }

    public readonly byte[] Bytes
    {
        get
        {
            // TODO: How to optimize this?
            var bytes = new byte[sizeof(float) * 4 * 4 * 3];
            {
                var rows = new float[4 * 4 * 3];
                _model.Row1.CopyTo(rows, 0);
                _model.Row2.CopyTo(rows, 4);
                _model.Row3.CopyTo(rows, 8);
                _model.Row4.CopyTo(rows, 12);
                _view.Row1.CopyTo(rows, 16);
                _view.Row2.CopyTo(rows, 20);
                _view.Row3.CopyTo(rows, 24);
                _view.Row4.CopyTo(rows, 28);
                _projection.Row1.CopyTo(rows, 32);
                _projection.Row2.CopyTo(rows, 36);
                _projection.Row3.CopyTo(rows, 40);
                _projection.Row4.CopyTo(rows, 44);
                Buffer.BlockCopy(rows, 0, bytes, 0, bytes.Length);
            }
            return bytes;
        }
    }
}
