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

        public Lake(int id, int slots)
        {
            this.id = id;
            this.slots = slots;
            this.AnimalList = new List<T>(new T[slots]);
            this.drinkingSemaphore = new SemaphoreSlim(slots);
        }

        public async Task<bool> InsertAnimalAsync(T animal)
        {
            string type = animal.getType();

            lock (lockObj)
            {
                while (hippoInLake)
                {
                    Monitor.Wait(lockObj); 
                }

                if (type == "h")
                {
                    // Prevent other hippos from barging in
                    hippoInLake = true;

                    // Evict all other animals
                    for (int i = 0; i < slots; i++)
                        AnimalList[i] = null;

                    AnimalList[0] = animal;
                    _ = DrinkAsync(animal, 0, isHippo: true);
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
                            _ = DrinkAsync(animal, i);
                            _ = DrinkAsync(animal, i + 1);
                            return true;
                        }
                    }
                    return false;
                }

                if (type == "f")
                {
                    // Prefer slot next to another flamingo
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
                                _ = DrinkAsync(animal, i);
                                return true;
                            }
                        }
                    }

                    // No adjacent found, place the first flamingo
                    for (int i = 0; i < slots; i++)
                    {
                        if (AnimalList[i] == null)
                        {
                            AnimalList[i] = animal;
                            _ = DrinkAsync(animal, i);
                            return true;
                        }
                    }

                    return false;
                }

                return false;
            }
        }

        private async Task DrinkAsync(T animal, int index, bool isHippo = false)
        {
            await drinkingSemaphore.WaitAsync();

            try
            {
                Console.WriteLine($"[{animal.getType()}] Animal at slot {index} is drinking...");
                await Task.Delay(animal.drinkTime);
                Console.WriteLine($"[{animal.getType()}] Animal at slot {index} finished drinking.");
            }
            finally
            {
                lock (lockObj)
                {
                    if (AnimalList[index]?.getId() == animal.getId())
                        AnimalList[index] = null;

                    if (isHippo)
                    {
                        hippoInLake = false;
                        Monitor.PulseAll(lockObj); // wake everyone up
                    }
                }
                drinkingSemaphore.Release();
            }
        }
    }



}
