namespace WiiUSharp.Nus;

internal static class BigEndian
{
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
