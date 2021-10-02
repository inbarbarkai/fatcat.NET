using System.Diagnostics;
using System.Text;

namespace fatcat.Core
{
    [DebuggerDisplay("{_value}")]
    public class FatFileName
    {
        private const byte LongNameLast = 0x40;
        // Offset of letters position in a special "long file name" entry
        private static readonly byte[] LongFilePos = new byte[] { 30, 28, 24, 22, 20, 18, 16, 14, 9, 7, 5, 3, 1 };

        private readonly StringBuilder _value = new StringBuilder();

        public string Build()
        {
            var value = _value.ToString();
            _value.Length = 0;
            return value;
        }

        public void Append(byte[] buffer)
        {
            if (buffer[FatEntry.FatOffsets.Attributes] != FatEntry.FatAttributes.LongFile)
            {
                return;
            }

            if ((buffer[0] & LongNameLast) == LongNameLast && (buffer[0] & 0xff) != FatEntry.Erased)
            {
                _value.Length = 0;
            }

            for (var i = 0; i < LongFilePos.Length; i++)
            {
                var c = buffer[LongFilePos[i]];
                var d = buffer[LongFilePos[i] + 1];
                if (c != 0 && c != 0xff)
                {
                    _value.Insert(0, Encoding.Unicode.GetChars(new byte[] { c, d }));
                }
            }
        }
    }
}
