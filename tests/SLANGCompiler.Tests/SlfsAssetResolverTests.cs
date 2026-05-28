using Xunit;
using SLANGCompiler.Build;
using SLANGCompiler.SlfsPack;

namespace SLANGCompiler.Tests;

public class SlfsAssetResolverTests
{
    [Fact]
    public void Resolve_FileSpec_LoadsContent()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"sar_test_{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(tmpFile, new byte[] { 1, 2, 3, 4 });
            var assets = SlfsAssetResolver.Resolve(new[] { $"MYFILE:{tmpFile}" });
            Assert.Single(assets);
            Assert.Equal("MYFILE", assets[0].Name);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, assets[0].Data);
            Assert.Equal(0, assets[0].Type);
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    [Fact]
    public void Resolve_FileSpec_WithType()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"sar_type_{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(tmpFile, new byte[] { 42 });
            var assets = SlfsAssetResolver.Resolve(new[] { $"X:{tmpFile}:5" });
            Assert.Single(assets);
            Assert.Equal(5, assets[0].Type);
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    [Fact]
    public void Resolve_DirSpec_WalksFiles_OrdinalSort()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"sar_dir_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            File.WriteAllBytes(Path.Combine(tmpDir, "B.BIN"), new byte[] { 2 });
            File.WriteAllBytes(Path.Combine(tmpDir, "A.BIN"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(tmpDir, "C.BIN"), new byte[] { 3 });

            var assets = SlfsAssetResolver.Resolve(new[] { tmpDir });
            Assert.Equal(3, assets.Count);
            Assert.Equal("A.BIN", assets[0].Name);
            Assert.Equal("B.BIN", assets[1].Name);
            Assert.Equal("C.BIN", assets[2].Name);
        }
        finally { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true); }
    }

    [Fact]
    public void Resolve_InvalidSpec_Throws()
    {
        Assert.Throws<ArgumentException>(() => SlfsAssetResolver.Resolve(new[] { "noColon" }));
    }

    [Fact]
    public void Resolve_MultipleSpecs_Aggregated()
    {
        var tmp1 = Path.Combine(Path.GetTempPath(), $"sar_m1_{Guid.NewGuid():N}.bin");
        var tmp2 = Path.Combine(Path.GetTempPath(), $"sar_m2_{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(tmp1, new byte[] { 1 });
            File.WriteAllBytes(tmp2, new byte[] { 2 });
            var assets = SlfsAssetResolver.Resolve(new[] { $"A:{tmp1}", $"B:{tmp2}" });
            Assert.Equal(2, assets.Count);
            Assert.Equal("A", assets[0].Name);
            Assert.Equal("B", assets[1].Name);
        }
        finally
        {
            if (File.Exists(tmp1)) File.Delete(tmp1);
            if (File.Exists(tmp2)) File.Delete(tmp2);
        }
    }
}
