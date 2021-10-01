using Xunit.Abstractions;

namespace fatcat.Tests
{
    public class TestEntryInfo : IXunitSerializable
    {
        public string Name { get; set; }

        public bool IsDirectory { get; set; }

        public TestEntryInfo()
        {

        }

        public TestEntryInfo(string name, bool isDirectory)
        {
            this.Name = name;
            this.IsDirectory = isDirectory;
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
    }
}
