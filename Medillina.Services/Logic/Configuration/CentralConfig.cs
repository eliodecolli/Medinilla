using System.Reflection;
using System.Text.Json;

namespace Medinilla.Core.Logic.Configuration;

public class MedinillaConfiguration
{
    public bool UseDefaultUser { get; set; }
    public DefaultUnitConfiguration DefaultUnit { get; set; }
    public DefaultUserConfiguration DefaultUser { get; set; }
    public string DefaultAuthDetails { get; set; }
}

public class DefaultUnitConfiguration
{
    public string Name { get; set; }
    public double Price { get; set; }
}

public class DefaultUserConfiguration
{
    public string DisplayName { get; set; }
    public double ActiveCredit { get; set; }
    public string Token { get; set; }
}

public class CentralConfig
{
    public static MedinillaConfiguration GetMedinillaConfiguration()
    {
        var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var filePath = Path.Combine(exeDirectory, "config.json");

        var jsonContent = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var config = JsonSerializer.Deserialize<MedinillaConfiguration>(jsonContent, options);
        return config;
    }
}
