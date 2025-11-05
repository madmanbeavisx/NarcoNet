using System.Security.Cryptography;

namespace NarcoNet.Server.Utilities;

/// <summary>
///     Utility for hashing files using a sampling approach for large files
/// </summary>
public static class FileHasher
{
    private const long SampleThreshold = 10 * 1024 * 1024; // 10MB
    private const int SampleSize = 32 * 1024; // 32KB

    /// <summary>
    ///     Hash a file using MD5 with sampling for large files
    /// </summary>
    public static async Task<string> HashFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        FileInfo fileInfo = new(filePath);
        long size = fileInfo.Length;

        byte[] dataToHash;

        await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

        // Only use sampling for files >= 10MB that are large enough to sample
        if (size is >= SampleThreshold and >= 3 * SampleSize)
        {
            // Sample from start, middle, and end
            dataToHash = new byte[SampleSize * 3];

            // Read start
            fileStream.Seek(0, SeekOrigin.Begin);
            await fileStream.ReadExactlyAsync(dataToHash.AsMemory(0, SampleSize), cancellationToken);

            // Read middle
            fileStream.Seek(size / 2, SeekOrigin.Begin);
            await fileStream.ReadExactlyAsync(dataToHash.AsMemory(SampleSize, SampleSize), cancellationToken);

            // Read end
            fileStream.Seek(size - SampleSize, SeekOrigin.Begin);
            await fileStream.ReadExactlyAsync(dataToHash.AsMemory(SampleSize * 2, SampleSize), cancellationToken);
        }
        else
        {
            // Hash the entire file
            dataToHash = new byte[size];
            await fileStream.ReadExactlyAsync(dataToHash, cancellationToken);
        }

        // Hash the data
        byte[] hash = MD5.HashData(dataToHash);

        // Append file size to hash (varint encoding)
        var result = new byte[hash.Length + 10]; // Max varint size is 10 bytes
        Array.Copy(hash, result, hash.Length);
        int varintLength = EncodeVarint(result.AsSpan(hash.Length), (ulong)size);

        return Convert.ToHexString(result.AsSpan(0, hash.Length + varintLength)).ToLowerInvariant();
    }

    /// <summary>
    ///     Encode an unsigned integer as a varint
    /// </summary>
    private static int EncodeVarint(Span<byte> buffer, ulong value)
    {
        var i = 0;
        while (value >= 0x80)
        {
            buffer[i] = (byte)(value & 0xFF | 0x80);
            value >>= 7;
            i++;
        }
        buffer[i] = (byte)(value & 0xFF);
        return i + 1;
    }
}
