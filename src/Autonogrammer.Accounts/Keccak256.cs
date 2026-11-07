using System.Buffers.Binary;
using System.Numerics;

namespace Autonogrammer.Accounts;

/// <summary>
/// Keccak-256 as Ethereum uses it: the original Keccak padding (domain byte 0x01), not the NIST SHA3-256 padding (0x06). It exists only to
/// compute the EIP-55 checksum of an address.
/// </summary>
internal static class Keccak256
{
    private const int RateBytes = 136;
    private const int RateLanes = RateBytes / 8;
    private const int DigestBytes = 32;
    private const int Rounds = 24;

    private static readonly ulong[] RoundConstants =
    [
        0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808AUL, 0x8000000080008000UL,
        0x000000000000808BUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
        0x000000000000008AUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000AUL,
        0x000000008000808BUL, 0x800000000000008BUL, 0x8000000000008089UL, 0x8000000000008003UL,
        0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800AUL, 0x800000008000000AUL,
        0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL,
    ];

    // The rotation offset of each lane, indexed by x + 5y.
    private static readonly int[] RotationOffsets =
    [
         0,  1, 62, 28, 27,
        36, 44,  6, 55, 20,
         3, 10, 43, 25, 39,
        41, 45, 15, 21,  8,
        18,  2, 61, 56, 14,
    ];

    /// <summary>Hashes the bytes.</summary>
    /// <param name="message">The bytes to hash.</param>
    /// <returns>The 32-byte Keccak-256 digest.</returns>
    internal static byte[] Hash(ReadOnlySpan<byte> message)
    {
        var state = new ulong[25];
        int offset = 0;
        while (message.Length - offset >= RateBytes)
        {
            Absorb(state, message.Slice(offset, RateBytes));
            Permute(state);
            offset += RateBytes;
        }

        var block = new byte[RateBytes];
        int remaining = message.Length - offset;
        message.Slice(offset, remaining).CopyTo(block);
        block[remaining] ^= 0x01;
        block[RateBytes - 1] ^= 0x80;
        Absorb(state, block);
        Permute(state);

        var digest = new byte[DigestBytes];
        for (int lane = 0; lane < DigestBytes / 8; lane++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(digest.AsSpan(lane * 8, 8), state[lane]);
        }

        return digest;
    }

    private static void Absorb(ulong[] state, ReadOnlySpan<byte> block)
    {
        for (int lane = 0; lane < RateLanes; lane++)
        {
            state[lane] ^= BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(lane * 8, 8));
        }
    }

    private static void Permute(ulong[] state)
    {
        Span<ulong> c = stackalloc ulong[5];
        Span<ulong> b = stackalloc ulong[25];
        for (int round = 0; round < Rounds; round++)
        {
            for (int x = 0; x < 5; x++)
            {
                c[x] = state[x] ^ state[x + 5] ^ state[x + 10] ^ state[x + 15] ^ state[x + 20];
            }

            for (int x = 0; x < 5; x++)
            {
                ulong d = c[(x + 4) % 5] ^ BitOperations.RotateLeft(c[(x + 1) % 5], 1);
                for (int y = 0; y < 25; y += 5)
                {
                    state[x + y] ^= d;
                }
            }

            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    int lane = x + 5 * y;
                    b[y + 5 * ((2 * x + 3 * y) % 5)] = BitOperations.RotateLeft(state[lane], RotationOffsets[lane]);
                }
            }

            for (int y = 0; y < 25; y += 5)
            {
                for (int x = 0; x < 5; x++)
                {
                    state[x + y] = b[x + y] ^ (~b[(x + 1) % 5 + y] & b[(x + 2) % 5 + y]);
                }
            }

            state[0] ^= RoundConstants[round];
        }
    }
}
