using System.Buffers.Binary;
using System.Text;

namespace AlphaTrimmer.Core;

internal static class ImageContainer
{
    internal static bool HasAnimation(Stream stream, bool png)
    {
        stream.Position = png ? 8 : 12;
        Span<byte> header = stackalloc byte[8];
        while (stream.Position < stream.Length)
        {
            stream.ReadExactly(header);
            uint length = png ? BinaryPrimitives.ReadUInt32BigEndian(header) : BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            string type = Encoding.ASCII.GetString(png ? header[4..] : header[..4]);
            if (png ? type == "acTL" : type is "ANIM" or "ANMF")
                return true;
            long next = checked(stream.Position + length + (png ? 4 : length % 2));
            if (next > stream.Length)
                throw new InvalidDataException(FailureText.TruncatedChunk);
            stream.Position = next;
            if (png && type == "IEND")
                break;
        }
        stream.Position = 0;
        return false;
    }

    internal static bool IsPng(ReadOnlySpan<byte> header) => header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    internal static bool IsWebP(ReadOnlySpan<byte> header) => header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8);
}
