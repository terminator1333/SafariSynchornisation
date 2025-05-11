using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }


    public class Lake<T> where T : class, Animal
    {
        private int slots;
        private int id;
        private List<T> AnimalList;
        private readonly object lockObj = new object();
        private SemaphoreSlim drinkingSemaphore;
        private bool hippoInLake = false;

        // Track cancellation tokens for drinking animals
        private Dictionary<int, CancellationTokenSource> cancellationTokens;

        public Lake(int id, int slots)
        {
            this.id = id;
            this.slots = slots;
            this.AnimalList = new List<T>(new T[slots]);
            this.drinkingSemaphore = new SemaphoreSlim(slots);
            this.cancellationTokens = new Dictionary<int, CancellationTokenSource>();
        }

        public async Task<bool> InsertAnimal(T animal)
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

        private async Task DrinkAsync(T animal, int index, bool isHippo, CancellationToken token)
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




}
