using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Safarihelper.helper;
namespace Safarihelper
{
    class helper
    {
        public static TimeSpan GetRandomNormalTime(double meanTimeSeconds)
        {

            var random = new Random();
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);


            // Use 20% standard deviation
            double stdDev = meanTimeSeconds * 0.2;
            double randNormal = meanTimeSeconds + stdDev * randStdNormal;

            // Ensure time is positive
            return TimeSpan.FromSeconds(Math.Max(0.1, randNormal));
        }

    }
}

public class Animal
{
    public static int nextID = 0;
    public string type { get; protected set; }
    public int ID { get; }
    public double drinkMean { get; protected set; }

    protected Lake lake;

    public TimeSpan drinkTime => Safarihelper.helper.GetRandomNormalTime(drinkMean);

    // Constructor
    public Animal(Lake lake)
    {
        this.lake = lake;
        this.ID = Interlocked.Increment(ref nextID);   // Atomic operation
    }
    public void RunAsyncWrapper()
    {
        // This will run the async method and wait for it to finish
        RunAsync().GetAwaiter().GetResult();
    }

    public async Task RunAsync()
    {
        // Asynchronously insert animal and retry if it fails
        while (!await lake.InsertAnimal(this))
        {
            await Task.Delay(100); // retry after a short delay
        }
    }

    public string getType() => type;
    public int getId() => ID;
}

class Flamingo : Animal
{
    public Flamingo(Lake lake) : base(lake)
    {
        this.type = "f";
        this.drinkMean = 3.5;
    }


}

class Zebra : Animal
{
    public Zebra(Lake lake) : base(lake)
    {
        this.type = "z";
        this.drinkMean = 5;
    }
}

class Hippopotamus : Animal
{
    public Hippopotamus(Lake lake) : base(lake)
    {
        this.type = "h";
        this.drinkMean = 5;
    }
}



public class Lake
{
    private int slots;
    private int id;
    private List<Animal> AnimalList;
    private readonly object lockObj = new object();
    private SemaphoreSlim drinkingSemaphore;
    private bool hippoInLake = false;

    // Track cancellation tokens for drinking animals
    private Dictionary<int, CancellationTokenSource> cancellationTokens;

    public Lake(int id, int slots)
    {
        this.id = id;
        this.slots = slots;
        this.AnimalList = new List<Animal>(slots);  // Create an empty list with the specified capacity
        for (int i = 0; i < slots; i++)
        {
            AnimalList.Add(null); // Add nulls initially
        }
        this.drinkingSemaphore = new SemaphoreSlim(slots);
        this.cancellationTokens = new Dictionary<int, CancellationTokenSource>();
    }

    public async Task<bool> InsertAnimal(Animal animal)
    {
        string type = animal.getType();

        lock (lockObj)
        {
            while (hippoInLake)
                Monitor.Wait(lockObj);

            if (type == "h")
            {
                // Mark hippo in lake
                hippoInLake = true;

                // Cancel all active drinking tasks
                foreach (var kvp in cancellationTokens)
                    kvp.Value.Cancel();

                cancellationTokens.Clear();

                // Evict all animals
                for (int i = 0; i < slots; i++)
                    AnimalList[i] = null;

                AnimalList[0] = animal;

                var cts = new CancellationTokenSource();
                cancellationTokens[0] = cts;
                _ = DrinkAsync(animal, 0, true, cts.Token);

                return true;
            }

            if (type == "z")
            {
                for (int i = 0; i < slots - 1; i++)
                {
                    if (AnimalList[i] == null && AnimalList[i + 1] == null)
                    {
                        AnimalList[i] = animal;
                        AnimalList[i + 1] = animal;

                        var cts1 = new CancellationTokenSource();
                        var cts2 = new CancellationTokenSource();
                        cancellationTokens[i] = cts1;
                        cancellationTokens[i + 1] = cts2;

                        _ = DrinkAsync(animal, i, false, cts1.Token);
                        _ = DrinkAsync(animal, i + 1, false, cts2.Token);
                        return true;
                    }
                }
                return false;
            }

            if (type == "f")
            {
                for (int i = 0; i < slots; i++)
                {
                    if (AnimalList[i] == null)
                    {
                        bool hasNeighborFlamingo =
                            (i > 0 && AnimalList[i - 1]?.getType() == "f") ||
                            (i < slots - 1 && AnimalList[i + 1]?.getType() == "f");

                        if (hasNeighborFlamingo)
                        {
                            AnimalList[i] = animal;
                            var cts = new CancellationTokenSource();
                            cancellationTokens[i] = cts;
                            _ = DrinkAsync(animal, i, false, cts.Token);
                            return true;
                        }
                    }
                }

                for (int i = 0; i < slots; i++)
                {
                    if (AnimalList[i] == null)
                    {
                        AnimalList[i] = animal;
                        var cts = new CancellationTokenSource();
                        cancellationTokens[i] = cts;
                        _ = DrinkAsync(animal, i, false, cts.Token);
                        return true;
                    }
                }

                return false;
            }

            return false;
        }
    }

    private async Task DrinkAsync(Animal animal, int index, bool isHippo, CancellationToken token)
    {
        await drinkingSemaphore.WaitAsync(token);
        try
        {
            Console.WriteLine($"[{animal.getType()}] Animal at slot {index} is drinking...");
            await Task.Delay(animal.drinkTime, token);
            Console.WriteLine($"[{animal.getType()}] Animal at slot {index} finished drinking.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[{animal.getType()}] Animal at slot {index} was interrupted by a hippo.");
        }
        finally
        {
            lock (lockObj)
            {
                if (AnimalList[index]?.getId() == animal.getId())
                    AnimalList[index] = null;

                cancellationTokens.Remove(index);

                if (isHippo)
                {
                    hippoInLake = false;
                    Monitor.PulseAll(lockObj); // Wake up all waiting animals
                }
            }

            drinkingSemaphore.Release();
        }
    }
}



class program
{
    private static readonly Random _rnd = new Random();
    private static double arrivalMean;

    static void Main(string[] args)
    {
        List<Lake> lakes = new List<Lake>
        {
            new Lake(1, 5),
            new Lake(2, 7),
            new Lake(3, 10)
        };

        new Thread(() => SpawnAnimals<Flamingo>(lakes, arrivalMean = 2.0))
        {
            IsBackground = true,
            Name = "FlamingoSpawner"
        }.Start();

        new Thread(() => SpawnAnimals<Zebra>(lakes, arrivalMean = 3.0))
        {
            IsBackground = true,
            Name = "ZebraSpawner"
        }.Start();

        new Thread(() => SpawnAnimals<Hippopotamus>(lakes, arrivalMean = 10.0))
        {
            IsBackground = true,
            Name = "HippopotamusSpawner"
        }.Start();
    }

    private static void SpawnAnimals<T>(List<Lake> lakes, double arrivalMean) where T : Animal
    {
        while (true)
        {
            TimeSpan delay = GetRandomNormalTime(arrivalMean);
            Thread.Sleep(delay);
            var lake = lakes[_rnd.Next(lakes.Count)];
            Animal a;

            if (typeof(T) == typeof(Flamingo))
            {
                a = new Flamingo(lake);
            }
            else if (typeof(T) == typeof(Zebra))
            {
                a = new Zebra(lake);
            }
            else if (typeof(T) == typeof(Hippopotamus))
            {
                a = new Hippopotamus(lake);
            }
            else
            {
                throw new InvalidOperationException("Unknown animal type");
            }
            Thread thread = new Thread(a.RunAsyncWrapper)
            {
                IsBackground = true, // Makes the thread a background thread
                Name = $"{a.GetType().Name}-{a.ID}" // Naming the thread for identification
            };

            thread.Start();
        }
    }
}
