using System;
using System.Text;

namespace fatcat
{
    internal static class Utilities
    {
        internal static ushort ReadShort(byte[] buffer, int offset)
        {
            ushort result = (ushort)((buffer[offset] & 0xff) | ((buffer[offset + 1] & 0xff) << 8));
            return result;
        }

        internal static ulong ReadLong(byte[] buffer, int offset)
        {
            ulong result = buffer[offset] & 0xffUL
                | ((buffer[offset + 1] & 0xffUL) << 8)
                | ((buffer[offset + 2] & 0xffUL) << 16)
                | ((buffer[offset + 3] & 0xffUL) << 24);
            return result;
        }

        internal static DateTime ReadDateTime(byte[] buffer, int offset)
        {
            int H = ReadShort(buffer, offset);
            int D = ReadShort(buffer, offset + 2);

            var seconds = 2 * (H & 0x1f);
            var minutes = (H >> 5) & 0x3f;
            var hours = (H >> 11) & 0x1f;

            var day = D & 0x1f;
            var month = (D >> 5) & 0xf;
            var year = 1980 + ((D >> 9) & 0x7f);

            return new DateTime(year, month, day, hours, minutes, seconds);
        }

        internal static void WriteShort(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xff);
            buffer[offset + 1] = (byte)((value >> 8) & 0xff);
        }
        internal static void WriteLong(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)((value) & 0xffUL);
            buffer[offset + 1] = (byte)(((value) >> 8) & 0xffUL);
            buffer[offset + 2] = (byte)(((value) >> 16) & 0xffUL);
            buffer[offset + 3] = (byte)(((value) >> 24) & 0xffUL);
        }

        static readonly char[] Units = new char[] { 'B', 'K', 'M', 'G', 'T', 'P' };

        internal static string PrettySize(ulong bytes)
        {
            double size = bytes;
            int n = 0;

            while (size >= 1024)
            {
                size /= 1024;
                n++;
            }

            return $"{size}{Units[n]}";
        }

        internal static string Decode(this byte[] bytes)
            => Encoding.UTF8.GetString(bytes);

        internal static string Decode(this byte[] bytes, int count)
            => Encoding.UTF8.GetString(bytes, 0, count);

        internal static string Decode(this byte[] bytes, int index, int count)
            => Encoding.UTF8.GetString(bytes, index, count);
    }
}