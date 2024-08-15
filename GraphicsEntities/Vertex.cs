using System.Runtime.InteropServices;
using Silk.NET.Maths;
using YASV.RHI;

namespace YASV.GraphicsEntities;

public readonly struct Vertex(Vector3D<float> position, Vector3D<float> color, Vector2D<float> textureCoordinate)
{
    private readonly Vector3D<float> _position = position;
    private readonly Vector3D<float> _color = color;
    private readonly Vector2D<float> _textureCoordinate = textureCoordinate;

    public readonly Vector3D<float> Position { get => _position; }
    public readonly Vector3D<float> Color { get => _color; }
    public readonly Vector2D<float> TextureCoordinate { get => _textureCoordinate; }

    public static VertexInputBindingDesc[] BindingDescriptions
    {
        get
        {
            return [
                new()
                    {
                        Binding = 0,
                        Stride = Marshal.SizeOf<Vertex>(),
                        InputRate = VertexInputRate.Vertex
                    }
            ];
        }
    }

    // TODO: generate from fields
    public static VertexInputAttributeDesc[] AttributeDescriptions
    {
        get
        {
            return [
                new()
                    {
                        Binding = 0,
                        Location = 0,
                        Format = Format.R32G32B32_Float,
                        Offset = (int)Marshal.OffsetOf<Vertex>("_position")
                    },
                    new()
                    {
                        Binding = 0,
                        Location = 1,
                        Format = Format.R32G32B32_Float,
                        Offset = (int)Marshal.OffsetOf<Vertex>("_color")
                    },
                    new()
                    {
                        Binding = 0,
                        Location = 2,
                        Format = Format.R32G32_Float,
                        Offset = (int)Marshal.OffsetOf<Vertex>("_textureCoordinate")
                    }
            ];
        }
    }

    public readonly byte[] Bytes
    {
        get
        {
            // TODO: generate bytes array from fields
            var bytes = new byte[Marshal.SizeOf<Vertex>()];
            {
                var floats = new float[8];
                _position.CopyTo(floats, 0);
                _color.CopyTo(floats, 3);
                _textureCoordinate.CopyTo(floats, 6);
                System.Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            }
            return bytes;
        }
    }
}
