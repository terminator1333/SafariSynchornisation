using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Safarihelper.helper;

namespace Safarihelper
{
    // Helper class for generating normally distributed random times
    class helper
    {
        public static TimeSpan GetRandomNormalTime(double meanTimeSeconds)
        {
            var random = new Random();

            // Box-Muller transform to create a normal (Gaussian) distribution
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            double stdDev = meanTimeSeconds * 0.2; // 20% of mean as standard deviation
            double randNormal = meanTimeSeconds + stdDev * randStdNormal;

            return TimeSpan.FromSeconds(Math.Max(0.1, randNormal)); // enforce minimum drink time
        }
    }

    // Base class for all animals
    public class Animal
    {
        public static int nextID = 0;
        public string type { get; protected set; }
        public int ID { get; }
        public double drinkMean { get; protected set; }

        protected Lake lake;

        // Drink time is based on normally distributed randomness
        public TimeSpan drinkTime => Safarihelper.helper.GetRandomNormalTime(drinkMean);

        public Animal(Lake lake)
        {
            this.lake = lake;
            this.ID = Interlocked.Increment(ref nextID); // Assign a unique ID atomically
        }

        // Wrapper to call async logic synchronously from threads
        public void RunAsyncWrapper()
        {
            RunAsync().GetAwaiter().GetResult();
        }

        // Try to enter a lake until successful
        public async Task RunAsync()
        {
            while (!await lake.InsertAnimal(this))
            {
                await Task.Delay(100); // Retry delay
            }
        }

        public string getType() => type;
        public int getId() => ID;
    }

    // Derived animal types with specific drinking means
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

    // The lake manages entry and drinking logic for animals
    public class Lake
    {
        private int slots;
        private int id;
        private List<Animal> AnimalList;
        private readonly object lockObj = new object();
        private SemaphoreSlim drinkingSemaphore;
        private bool hippoInLake = false;

        // UI callback for visual updates
        public Action<int, Animal, string> OnAnimalStatusChange;

        // Track cancellation tokens for animals currently drinking
        private Dictionary<int, CancellationTokenSource> cancellationTokens;

        public int SlotCount() => slots;

        public Lake(int id, int slots)
        {
            this.id = id;
            this.slots = slots;

            // Initialize lake slot array with nulls
            this.AnimalList = new List<Animal>(slots);
            for (int i = 0; i < slots; i++) AnimalList.Add(null);

            this.drinkingSemaphore = new SemaphoreSlim(slots);
            this.cancellationTokens = new Dictionary<int, CancellationTokenSource>();
        }

        // Try to insert animal to lake under species-specific rules
        public async Task<bool> InsertAnimal(Animal animal)
        {
            string type = animal.getType();

            lock (lockObj)
            {
                // Hippo exclusivity rule: wait until no hippo is present
                while (hippoInLake)
                    Monitor.Wait(lockObj);

                // Hippo logic: evict everyone, cancel drinks, occupy first slot
                if (type == "h")
                {
                    hippoInLake = true;

                    // Notify UI to clear visuals
                    for (int i = 0; i < slots; i++)
                    {
                        if (AnimalList[i] != null)
                        {
                            OnAnimalStatusChange?.Invoke(i, AnimalList[i], "exit");
                        }
                    }

                    // Cancel ongoing drinks
                    var tokensToCancel = new List<CancellationTokenSource>(cancellationTokens.Values);
                    cancellationTokens.Clear();
                    foreach (var tokenSource in tokensToCancel)
                        tokenSource.Cancel();

                    // Clear lake state
                    for (int i = 0; i < slots; i++)
                        AnimalList[i] = null;

                    // Let hippo drink in slot 0
                    AnimalList[0] = animal;
                    var cts = new CancellationTokenSource();
                    cancellationTokens[0] = cts;
                    _ = DrinkAsync(animal, 0, true, cts.Token);
                    return true;
                }

                // Zebra logic: requires two adjacent empty slots (wraps at end)
                if (type == "z")
                {
                    for (int i = 0; i <= slots - 1; i++)
                    {
                        if ((i != slots - 1 && AnimalList[i] == null && AnimalList[i + 1] == null) ||
                            (i == slots - 1 && AnimalList[0] != null)) // wrap-around case
                        {
                            if (i == slots - 1)
                            {
                                AnimalList[i] = animal;
                                AnimalList[0] = animal;
                            }
                            else
                            {
                                AnimalList[i] = animal;
                                AnimalList[i + 1] = animal;
                            }

                            var cts1 = new CancellationTokenSource();
                            cancellationTokens[i] = cts1;
                            _ = DrinkAsync(animal, i, false, cts1.Token);
                            return true;
                        }
                    }
                    return false;
                }

                // Flamingo logic: prefers being next to another flamingo
                if (type == "f")
                {
                    bool flamingoInLake = false;

                    // First pass: try to place next to another flamingo
                    for (int i = 0; i < slots; i++)
                    {
                        if (AnimalList[i]?.getType() == "f")
                        {
                            flamingoInLake = true;
                        }

                        if (AnimalList[i] == null)
                        {
                            bool hasNeighborFlamingo =
                                (i > 0 && AnimalList[i - 1]?.getType() == "f") ||
                                (i < slots - 1 && AnimalList[i + 1]?.getType() == "f") ||
                                (i == slots - 1 && AnimalList[0]?.getType() == "f") ||
                                (i == 0 && AnimalList[slots - 1]?.getType() != "z" && AnimalList[slots - 1]?.getType() == "f");

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

                    // Second pass: if no flamingo exists, place anywhere
                    if (!flamingoInLake)
                    {
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
                    }

                    return false;
                }

                return false; // fallback if unknown animal
            }
        }

        // Asynchronous drink logic (called after animal is inserted)
        private async Task DrinkAsync(Animal animal, int index, bool isHippo, CancellationToken token)
        {
            await drinkingSemaphore.WaitAsync(token);
            try
            {
                OnAnimalStatusChange?.Invoke(index, animal, "enter");
                await Task.Delay(animal.drinkTime, token);
                OnAnimalStatusChange?.Invoke(index, animal, "exit");
            }
            catch (OperationCanceledException)
            {
                // Interrupted due to hippo entry — expected
            }
            finally
            {
                lock (lockObj)
                {
                    // Clean up only if this is the same animal that was there
                    if (AnimalList[index]?.getId() == animal.getId())
                    {
                        // Clean up paired zebra slots
                        if (index == slots - 1 && AnimalList[index]?.getType() == "z")
                            AnimalList[0] = null;
                        else if (AnimalList[index]?.getType() == "z")
                            AnimalList[index + 1] = null;

                        AnimalList[index] = null;
                    }

                    cancellationTokens.Remove(index);

                    if (isHippo)
                    {
                        hippoInLake = false;
                        Monitor.PulseAll(lockObj); // notify all waiting animals
                    }
                }

                drinkingSemaphore.Release();
            }
        }
    }
}
