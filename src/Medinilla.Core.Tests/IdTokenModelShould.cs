using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.Core.Tests;

public class IdTokenModelShould
{
    [Theory]
    [InlineData(IdTokenType.Central, "Central")]
    [InlineData(IdTokenType.EMAID, "EMAID")]
    [InlineData(IdTokenType.ISO14443, "ISO14443")]
    [InlineData(IdTokenType.ISO15693, "ISO15693")]
    [InlineData(IdTokenType.KeyCode, "KeyCode")]
    [InlineData(IdTokenType.Local, "Local")]
    [InlineData(IdTokenType.MacAddress, "MacAddress")]
    [InlineData(IdTokenType.NoAuthorization, "NoAuthorization")]
    public void ConvertTokenTypesToAndFromStrings(IdTokenType type, string storedValue)
    {
        // Inspect the EF model only; no database connection is opened.
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new MedinillaOcppDbContext(options);
        var property = context.Model.FindEntityType(typeof(IdToken))!
            .FindProperty(nameof(IdToken.IdType))!;
        var converter = property.GetTypeMapping().Converter;

        Assert.NotNull(converter);
        Assert.Equal(typeof(IdTokenType), property.ClrType);
        Assert.Equal("text", property.GetColumnType());
        Assert.Equal(storedValue, converter.ConvertToProvider(type));
        Assert.Equal(type, converter.ConvertFromProvider(storedValue));
    }
}
