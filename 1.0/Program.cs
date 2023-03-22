using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class Program
{
    private readonly object ComponentsLock = new object();
    
    bool StopThreads = false;

    static async Task Main(string[] args)
    {
        var program = new Program();
        // Read YAML input data from a file
        var input = File.ReadAllText("input.yaml");

        // Create a deserializer with camel case naming convention
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        // Deserialize input data into a custom class
        var data = deserializer.Deserialize<Data>(input);

        // Initialize output data with initial values
        var productionLog = new List<ProductionEntry>();

        // Start a separate thread for each building
        var tasks = data.Buildings.Select(building => Task.Run(() => 
        FactoryRun(building, data, program, productionLog))).ToList();

        // Initialize a variable for remaining checks before exiting the loop
        int remainingChecks = 4;

        // Loop until end condition is true (i.e., no more available components)
        while (remainingChecks != 0)
        {
            // Check end condition by looping through all recipes and components
            foreach (var recipe in data.Recipes)
            {
                lock (program.ComponentsLock)
                {
                    // Check if there are enough components for the recipe
                    if (!recipe.Components.Any(component => data.Products.Any(product => product.Id == component.Id && product.Num >= component.Num)))
                    {
                        remainingChecks--;
                        break;
                    }
                }

            }
            await Task.Delay(200); // Delay for 200ms before checking again
        }

        program.StopThreads = true; // Signal all factory threads to stop
        await Task.WhenAll(tasks); // Wait for all factory threads to finish

        //output

        data.Products.RemoveAll(product => product.Num == 0);

        Console.WriteLine("Production Log:");
        foreach (var entry in productionLog.OrderBy(x => x.StartTime))
        {
            Console.WriteLine($"time: {entry.StartTime,2}, {entry.Recipe} at {entry.Building}");
        }

        Console.WriteLine("Final Products:");
        foreach (var product in data.Products)
        {
            Console.WriteLine($"{product.Id}: {product.Num}");
        }

        int endTime = data.Buildings.Max(building => building.StartTime);
        Console.WriteLine($"totalTime: {endTime}");
    }

    /// <summary>
    /// // Runs the factory process for the given building
    /// </summary>
    /// <param name="building"></param>
    /// <param name="data"></param>
    /// <param name="program"></param>
    /// <param name="productionLog"></param>
    public static void FactoryRun(Building building, Data data, Program program, List<ProductionEntry> productionLog)
    {
        var factoryRecipes = new List<Recipe>();
        int startTime = 0;

        building.StartTime = startTime;

        // Loop through projects in data that match the building's project
        foreach (var project in data.Projects.Where(p => p.Name == building.Project))
        {
            foreach (var ability in project.Abilities)
            {
                // Get recipe from data that matches the ability name
                var recipe = data.Recipes.FirstOrDefault(r => r.Name == ability.Name);

                // If recipe exists, add a new recipe with adjusted time to factoryRecipes list
                if (recipe != null)
                {
                    factoryRecipes.Add(new Recipe
                    {
                        Name = recipe.Name,
                        Components = recipe.Components,
                        Product = recipe.Product,
                        TimeToProduce = ability.Duration
                    });
                }
            }
        }

        // Continue running factory process until program stop flag is set
        while (!program.StopThreads)
        {
            foreach (var recipe in factoryRecipes)
            {
                // Check if recipe can be produced
                if (CheckRecipe(recipe, data, program))
                {
                    startTime = data.Buildings.Max(b => b.StartTime);

                    // Wait for recipe production time
                    Thread.Sleep(recipe.TimeToProduce);

                    // Acquire lock on program components and update product count
                    lock (program.ComponentsLock)
                    {
                        var product = data.Products.FirstOrDefault(p => p.Id == recipe.Product.Id);

                        if (product != null)
                        {
                            product.Num += recipe.Product.Num;
                        }
                        else
                        {
                            data.Products.Add(new Product
                            {
                                Id = recipe.Product.Id,
                                Num = recipe.Product.Num
                            });
                        }

                        // Log production entry and update start/end times for building and factory process
                        productionLog.Add(new ProductionEntry
                        {
                            StartTime = startTime,
                            Building = building.Name,
                            Recipe = recipe.Name
                        });

                        startTime += recipe.TimeToProduce;
                        building.StartTime = startTime;
                        building.EndTime += recipe.TimeToProduce;
                    }

                    // Break out of recipe loop once a recipe has been produced
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Checks if it is possible to create the recipe specified as a parameter and, 
    /// if possible, takes the appropriate components from the data
    /// </summary>
    /// <param name="recipe"></param>
    /// <param name="data"></param>
    /// <param name="program"></param>
    /// <returns></returns>
    public static bool CheckRecipe(Recipe recipe, Data data, Program program)
    {
        // Ensure exclusive access to shared data
        lock (program.ComponentsLock)
        {
            // Check if recipe is valid and all required components are available in sufficient quantity
            if (recipe != null && recipe.Components.All(component =>
                data.Products.FirstOrDefault(product => product.Id == component.Id)?.Num >= component.Num))
            {
                // Consume required components
                foreach (var component in recipe.Components)
                {
                    data.Products.First(product => product.Id == component.Id).Num -= component.Num;
                }

                return true;
            }
            else
            {
                return false;
            }
        }
    }

}

// Define a custom class to hold the input data structure
public class Data
{
    public List<Product> Products { get; set; }
    public List<Recipe> Recipes { get; set; }
    public List<Project> Projects { get; set; }
    public List<Building> Buildings { get; set; }
}

// Define other classes for each sub-object in the input data
public class Product
{
    public string Id { get; set; }
    public int Num { get; set; }
}

public class Recipe
{
    public string Name { get; set; }
    public List<Product> Components { get; set; }
    public Product Product { get; set; }
    public int TimeToProduce { get; set; }
}

public class Project
{
    public string Name { get; set; }
    public List<Ability> Abilities { get; set; }
}

public class Ability
{
    public string Name { get; set; }
    public int Duration { get; set; }
}

public class Building
{
    public string Name { get; set; }
    public string Project { get; set; }
    public int StartTime { get; set; }
    public int EndTime { get; set; }
}

// Define helper classes for tracking production log
public class ProductionEntry
{
    public string Building { get; set; }
    public string Recipe { get; set; }
    public int StartTime { get; set; }
}