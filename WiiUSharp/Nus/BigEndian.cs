namespace WiiUSharp.Nus;

internal static class BigEndian
{
    public static int Read24(byte[] bytes, int offset) =>
        bytes[offset] << 16 | bytes[offset + 1] << 8 | bytes[offset + 2];

    public static ushort ReadUInt16(byte[] bytes, int offset) =>
        (ushort)(bytes[offset] << 8 | bytes[offset + 1]);

    public static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);

    public static ulong ReadUInt64(byte[] bytes, int offset) =>
        (ulong)ReadUInt32(bytes, offset) << 32 | ReadUInt32(bytes, offset + 4);

    public static void Write(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    public static void Write(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    public static void Write(byte[] bytes, int offset, ulong value)
    {
        Write(bytes, offset, (uint)(value >> 32));
        Write(bytes, offset + 4, (uint)value);
    }

    public static void Write24(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 16);
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)value;
    }
}
