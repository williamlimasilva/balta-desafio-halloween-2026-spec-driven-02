using LinkShortener.Api.Services;
using Xunit;

namespace LinkShortener.Tests.Unit;

public class CodeGeneratorTests
{
    private const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

    [Fact]
    public void GenerateCode_ReturnsSevenCharactersFromAllowedAlphabet()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = CodeGenerator.GenerateCode();

            Assert.Equal(7, code.Length);
            Assert.All(code, character => Assert.Contains(character, AllowedCharacters));
        }
    }
}