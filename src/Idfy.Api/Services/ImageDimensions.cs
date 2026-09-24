using System.Buffers.Binary;

namespace Idfy.Api.Services;

/// <summary>Reads pixel dimensions from JPEG and PNG headers without decoding the image.</summary>
public static class ImageDimensions
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <returns>False when the format is not JPEG/PNG or the header is malformed.</returns>
    public static bool TryGetSize(ReadOnlySpan<byte> image, out int width, out int height)
    {
        width = height = 0;

        // PNG: signature, then the IHDR chunk with big-endian width/height at offsets 16/20.
        if (image.Length >= 24 && image.StartsWith(PngSignature) && image[12..16].SequenceEqual("IHDR"u8))
        {
            width = BinaryPrimitives.ReadInt32BigEndian(image[16..]);
            height = BinaryPrimitives.ReadInt32BigEndian(image[20..]);
            return width > 0 && height > 0;
        }

        // JPEG: walk marker segments until a start-of-frame (SOFn) segment.
        if (image.Length >= 4 && image[0] == 0xFF && image[1] == 0xD8)
        {
            var i = 2;
            while (i + 3 < image.Length)
            {
                if (image[i] != 0xFF) return false;

                var marker = image[i + 1];
                if (marker == 0xFF) { i++; continue; } // fill byte
                if (marker is 0x01 or (>= 0xD0 and <= 0xD9)) { i += 2; continue; } // no length field

                var length = BinaryPrimitives.ReadUInt16BigEndian(image[(i + 2)..]);
                // SOF0-SOF15, excluding DHT (C4), JPG (C8) and DAC (CC).
                if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                {
                    if (i + 9 > image.Length) return false;
                    height = BinaryPrimitives.ReadUInt16BigEndian(image[(i + 5)..]);
                    width = BinaryPrimitives.ReadUInt16BigEndian(image[(i + 7)..]);
                    return width > 0 && height > 0;
                }

                i += 2 + length;
            }
        }

        return false;
    }
}
