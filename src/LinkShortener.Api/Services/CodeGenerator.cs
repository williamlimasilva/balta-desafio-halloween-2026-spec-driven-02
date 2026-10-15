using System.Security.Cryptography;

namespace LinkShortener.Api.Services;

public static class CodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
    private const int CodeLength = 7;

    public static string GenerateCode()
    {
        var code = new char[CodeLength];

        for (var index = 0; index < code.Length; index++)
        {
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}