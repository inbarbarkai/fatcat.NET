using System;
using fatcat.Core;
using Xunit.Abstractions;

namespace fatcat.Tests
{
    public class TestEntryInfo : IXunitSerializable, IEquatable<FatEntry>
    {
        public string Name { get; set; }

        public bool IsDirectory { get; set; }

        public bool IsDeleted { get; set; }

        public TestEntryInfo()
        {

        }

        public TestEntryInfo(string name, bool isDirectory, bool isDeleted = false)
        {
            this.Name = name;
            this.IsDirectory = isDirectory;
            this.IsDeleted = isDeleted;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            this.Name = info.GetValue<string>(nameof(Name));
            this.IsDirectory = info.GetValue<bool>(nameof(IsDirectory));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Name), this.Name);
            info.AddValue(nameof(IsDirectory), this.IsDirectory);
        }

        public bool Equals(FatEntry other)
        {
            if (other == null)
            {
                return false;
            }
            return this.IsDeleted == other.IsErased && this.Name == other.LongName && this.IsDirectory == other.IsDirectory;
        }
    }
}
