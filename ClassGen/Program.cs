using Scriban;

public class Component
{
    public string Name { get; set; }

    public string Description { get; set; }

    public List<ComponentVariable> Variables { get; set; }
}

public class ComponentVariable
{
    public string ComponentName { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Type { get; set; }

    public bool Required { get; set; }
}

public class Program
{
    static List<Component> GetComponents()
    {
        // read the compononents file in ord[er to figure out how to group things
        using var reader = new StreamReader(File.Open("ocpp/components.csv", FileMode.Open));
        var componentsText = reader.ReadToEnd().Split('\n').Skip(1);

        var components = new List<Component>();

        foreach (var component in componentsText)
        {
            if (string.IsNullOrEmpty(component))
            {
                continue;
            }

            var fieldsTemp = component.Split(',');
            var cmp = new Component()
            {
                Name = fieldsTemp[0],
                Description = fieldsTemp[1],
                Variables = new List<ComponentVariable>()
            };

            components.Add(cmp);

            Console.WriteLine("Name: {0} Description: {1}", cmp.Name, cmp.Description);
        }

        return components;
    }

    static void UpdateComponentVariables(List<Component> components)
    {
        using var reader = new StreamReader(File.OpenRead("ocpp/dm_components_vars.csv"));
        var variables = reader.ReadToEnd().Split('\n')
            .Skip(1)
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x =>
            {
                var tmp = x.Split(';');
                return new ComponentVariable()
                {
                    ComponentName = tmp[0],
                    Name = tmp[1],
                    Required = tmp[3].ToLower() == "no" ? false : true,
                    Type = tmp[4],
                    // Unit = tmp[5]
                    Description = tmp[6].Trim('\n').Trim('\r')
                };
            })
            .GroupBy(x => x.ComponentName);

        foreach (var component in components)
        {
            var variablesTmp = variables.FirstOrDefault(x => x.Key == component.Name);
            if (variablesTmp != null)
            {
                component.Variables.AddRange(variablesTmp);
                Console.WriteLine("Updated variables for component {0}", component.Name);
            }
        }
    }

    public static void Main(string[] args)
    {
        var components = GetComponents();
        UpdateComponentVariables(components);

        var template = Template.Parse(File.ReadAllText("class_template.liquid"));
        foreach (var component in components)
        {
            if (component.Variables.Count > 0)
            {
                if (!Directory.Exists("output"))
                {
                    Directory.CreateDirectory("output");
                }

                var result = template.Render(new { model = new { component } }, m => m.Name);
                var file = $"output\\{component.Name}.cs";
                File.WriteAllText(file, result);

                Console.WriteLine("Generated source {0}", file);
            }
            Console.WriteLine("No variables for component {0}", component.Name);
        }
    }
}