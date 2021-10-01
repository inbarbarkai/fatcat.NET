﻿using System.Collections.Generic;
using System.Threading.Tasks;
using fatcat.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace fatcat.Tests
{
    public class FatSystemTests
    {
        [InlineData("hello-world.img")]
        [InlineData("empty.img")]
        [Theory]
        public async Task InfoTest(string imagePath)
        {
            using var serviceProvider = TestHelper.CreateDefault()
                .BuildServiceProvider();
            using var stream = TestHelper.GetTestFileStream(imagePath);

            var system = serviceProvider.GetRequiredService<FatSystem>();
            await system.Initialize(stream).ConfigureAwait(false);

            system.FsType.Should().Be("FAT32");
            system.OemName.Should().Be("mkdosfs");
            system.BytesPerCluster.Should().Be(512UL);
            system.FatSize.Should().Be(403456UL);
            system.DataSize.Should().Be(51642368UL);
            system.TotalSize.Should().Be(52428800UL);
        }

        [MemberData(nameof(GetEntriesTestData))]
        [Theory]
        public async Task GetEntriesTest(string imagePath, string path, TestEntryInfo[] expectedEntries)
        {
            using var serviceProvider = TestHelper.CreateDefault()
                .BuildServiceProvider();
            using var stream = TestHelper.GetTestFileStream(imagePath);

            var system = serviceProvider.GetRequiredService<FatSystem>();
            await system.Initialize(stream).ConfigureAwait(false);

            var entries = await system.GetEntries(path);

            entries.Should().NotBeNull();
            entries.Entries.Count.Should().Be(expectedEntries.Length);
            foreach (var entry in expectedEntries)
            {
                entries.Entries.Should().Contain(e => e.LongName == entry.Name && e.IsDirectory == entry.IsDirectory);
            }
        }

        public static IEnumerable<object[]> GetEntriesTestData()
        {
            const string imagePath = "hello-world.img";
            yield return new object[] { imagePath, "/", new[] { new TestEntryInfo("files", true), new TestEntryInfo("hello.txt", false) } };
            yield return new object[] { imagePath, "/files/", new[] { new TestEntryInfo("other_file.txt", false), new TestEntryInfo("", true) } };
        }

        [Fact]
        public async Task GetEntriesFailure()
        {
            using var serviceProvider = TestHelper.CreateDefault()
                .BuildServiceProvider();
            using var stream = TestHelper.GetTestFileStream("hello-world.img");

            var system = serviceProvider.GetRequiredService<FatSystem>();
            await system.Initialize(stream).ConfigureAwait(false);

            var entries = await system.GetEntries("/xyz");
            entries.Should().BeNull();
        }
    }
}
