using System.Numerics;

namespace BF1942.Core.MasterServer;

/// <summary>
/// Decrypts what a master server sends in answer to a list request: the GameSpy "enctype 2" stream cipher, ported from Luigi Auriemma's enctype2 code via the
/// BF1942-Master project.
/// </summary>
/// <remarks>
/// The message is <c>headerSize ^ 0xEC</c>, a header of 6 to 14 random bytes, the encrypted data, and the encrypted footer <c>\final\</c>. The header, with the
/// game's key mixed in, seeds the key stream.
/// </remarks>
public static class Enctype2Decoder
{
    private const byte HeaderSizeMask = 0xEC;

    /// <summary>
    /// The cipher's state is one array of words: a table of 256 substitution words, a ring the key stream steps through from 256, its position and three
    /// registers, and from 309 the 16 words of the current block of key stream.
    /// </summary>
    private const int StateLength = 326;
    private const int PositionIndex = 304;
    private const int RegistersIndex = 305;
    private const int BlockIndex = 309;
    private const int BlockWords = 16;

    /// <summary>
    /// The key stream is made in blocks of 64 bytes, of which the first 63 are used.
    /// </summary>
    private const int BytesPerBlock = 63;

    private static ReadOnlySpan<byte> Footer => "\\final\\"u8;

    /// <summary>
    /// The data of a message, without its header and footer.
    /// </summary>
    /// <exception cref="InvalidDataException">The message is too short, or is not what <paramref name="key"/> decrypts.</exception>
    public static byte[] Decode(string key, ReadOnlySpan<byte> message)
    {
        if (message.Length < 1)
            throw new InvalidDataException("The message from the master server is empty");

        var data = message.ToArray();
        data[0] ^= HeaderSizeMask;
        int headerSize = data[0];

        if (headerSize < key.Length || data.Length < 1 + headerSize + Footer.Length)
            throw new InvalidDataException("The message from the master server is too short to be a server list");

        for (int i = 0; i < key.Length; i++)
            data[1 + i] ^= (byte)key[i];

        var state = new uint[StateLength];
        InitializeKey(data.AsSpan(1, headerSize), state);

        int dataStart = 1 + headerSize;
        Decrypt(state, data.AsSpan(dataStart));

        int dataLength = data.Length - dataStart - Footer.Length;
        if (!data.AsSpan(dataStart + dataLength).SequenceEqual(Footer))
            throw new InvalidDataException("The message from the master server does not end the way a server list does; it is cut off or was not made with this key");

        return data[dataStart..(dataStart + dataLength)];
    }

    private static void Decrypt(uint[] state, Span<byte> data)
    {
        AdvanceKeyStream(state);

        int position = 0;
        foreach (ref byte value in data)
        {
            if (position == BytesPerBlock)
            {
                position = 0;
                AdvanceKeyStream(state);
            }

            value ^= (byte)(state[BlockIndex + position / 4] >> (position % 4 * 8));
            position++;
        }
    }

    /// <summary>
    /// Makes the next block of key stream from the registers of the state.
    /// </summary>
    private static void AdvanceKeyStream(uint[] state)
    {
        uint t2 = state[PositionIndex];
        uint t1 = state[RegistersIndex];
        uint t3 = state[RegistersIndex + 1];
        uint t5 = state[RegistersIndex + 2];

        for (int word = 0; word < BlockWords; word++)
        {
            while (t5 < 65536)
            {
                t1 += t5;
                t3 += t1;
                t1 += t3;
                state[t2 + 256] = t1;
                state[t2 + 272] = t3;
                state[t2 + 288] = t5;
                t3 = MixRight(state, t3);
                t1 = MixLeft(state, t1);
                t5 <<= 1;
                t2++;
            }

            state[BlockIndex + word] = t3 ^ t1;

            t2--;
            t1 = MixRight(state, ~state[t2 + 256]);
            t3 = MixLeft(state, state[t2 + 272]);
            t5 = (state[t2 + 288] << 1) + 1;
        }

        state[PositionIndex] = t2;
        state[RegistersIndex] = t1;
        state[RegistersIndex + 1] = t3;
        state[RegistersIndex + 2] = t5;
    }

    /// <summary>
    /// Scrambles the substitution table with the header, much as RC4 sets up its table: four permutations packed into the bytes of each word, each swapped
    /// around twice by a position that moves with the key. Then the key stream is set up to start from its beginning.
    /// </summary>
    private static void InitializeKey(ReadOnlySpan<byte> header, uint[] state)
    {
        for (uint round = 0; round < 4; round++)
        {
            for (int i = 0; i < 256; i++)
                state[i] = (state[i] << 8) + (uint)i;

            uint position = round;
            for (int step = 0; step < 2 * 256; step++)
            {
                int slot = step % 256;
                uint word = state[slot];
                position = (position + word + header[slot % header.Length]) & 0xFF;
                state[slot] = state[position];
                state[position] = word;
            }
        }

        for (int i = 0; i < 256; i++)
            state[i] ^= (uint)i;

        uint t1 = 0;
        uint t2 = 0;
        uint t4 = 1;
        for (int i = 0; i < 16; i++)
        {
            t2 += t4;
            t1 += t2;
            t2 += t1;
            state[256 + i] = t2;
            state[272 + i] = t1;
            state[288 + i] = t4;
            t1 = MixRight(state, t1);
            t2 = MixLeft(state, t2);
            t4 <<= 1;
        }

        state[PositionIndex] = 16;
        state[RegistersIndex] = t2;
        state[RegistersIndex + 1] = t1;
        state[RegistersIndex + 2] = t4;
    }

    /// <summary>
    /// Two rounds of rotating by a byte to the right and looking the result up in the substitution table.
    /// </summary>
    private static uint MixRight(uint[] state, uint value) => Substitute(state, BitOperations.RotateRight(Substitute(state, BitOperations.RotateRight(value, 8)), 8));

    /// <summary>
    /// The same, rotating to the left.
    /// </summary>
    private static uint MixLeft(uint[] state, uint value) => BitOperations.RotateLeft(Substitute(state, BitOperations.RotateLeft(Substitute(state, value), 8)), 8);

    private static uint Substitute(uint[] state, uint value) => value ^ state[value & 0xFF];
}
