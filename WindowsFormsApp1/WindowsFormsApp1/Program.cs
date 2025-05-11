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


    public class Lake<T> where T : class
    {
        // Fields (private variables)
        private int slots;
        private int id;
        private List<T> AnimalList;
        private List<T> FlamingoWait;
        static Mutex mutexHippo;
        static Mutex mutexFlamingoZebra;
        private SemaphoreSlim drinkingSemaphore;


        public Lake(int id, int slots)
        {
            this.id = id;
            this.slots = slots;
            this.AnimalList = new List<T>(slots);
            this.FlamingoWait = new List<T>();
            mutexHippo = new Mutex();
            mutexFlamingoZebra = new Mutex();
            this.drinkingSemaphore = new SemaphoreSlim(slots); // or a smaller number if only some can drink at a time




        }

        public int Id
        {
            get { return id; }
            set { id = value; }
        }
        public int Slots
        {
            get { return slots; }
            set { slots = value; }
        }

        public async Task StartDrinking()
        {
            List<Task> drinkingTasks = new List<Task>();

            for (int i = 0; i < AnimalList.Count; i++)
            {
                var animal = AnimalList[i];
                if (animal != null)
                {
                    drinkingTasks.Add(DrinkAsync(animal, i));
                }
            }

            await Task.WhenAll(drinkingTasks); // Wait for all to finish
        }
        private async Task DrinkAsync(T animal, int index)
        {
            await drinkingSemaphore.WaitAsync();

            try
            {
                Console.WriteLine($"Animal at slot {index} is drinking...");
                int drinkTime = ((Animal)(object)animal).drinkTime;
                await Task.Delay(drinkTime); // simulate drinking
                Console.WriteLine($"Animal at slot {index} finished drinking.");
            }
            finally
            {
                drinkingSemaphore.Release();
            }
        }




        private void EnsureListSize()
        {
            while (AnimalList.Count < slots)
                AnimalList.Add(null);
        }
        public async Task<bool> insertAnimal(Animal animal)
        {
            string type = animal.getType();

            if (type.Equals("h"))
            {
                mutexHippo.WaitOne();  // Enter critical section
                this.AnimalList.Clear();
                EnsureListSize();
                this.AnimalList[0] = animal;

                _ = DrinkAsync((T)(object)animal, 0); // fire-and-forget, safe in async context

                mutexHippo.ReleaseMutex();
                return true;
            }
            else
            {
                mutexFlamingoZebra.WaitOne();
                switch (type)
                {
                    case "z":
                        int? startIndex = null;
                        EnsureListSize();

                        for (int i = 0; i < this.AnimalList.Count - 1; i++)
                        {
                            if (this.AnimalList[i] == null && this.AnimalList[i + 1] == null)
                            {
                                startIndex = i;
                                break;
                            }
                        }

                        if (startIndex.HasValue)
                        {
                            int first = startIndex.Value;
                            int second = (first == slots - 1) ? 0 : first + 1;

                            this.AnimalList[first] = animal;
                            this.AnimalList[second] = animal;

                            _ = DrinkAsync((T)(object)animal, first);
                            _ = DrinkAsync((T)(object)animal, second);

                            mutexFlamingoZebra.ReleaseMutex();
                            return true;
                        }
                        break;

                    case "f":
                        startIndex = null;
                        EnsureListSize();

                        for (int i = 0; i < this.AnimalList.Count; i++)
                        {
                            var current = this.AnimalList[i];
                            if (current == null) continue;




                            if (current.GetType().Name.ToLower().StartsWith("f"))
                            {
                                if ((i > 0 && this.AnimalList[i - 1] == null) ||
                                    (i < this.AnimalList.Count - 1 && this.AnimalList[i + 1] == null))
                                {
                                    startIndex = i;
                                    break;
                                }
                            }
                        }

                        // You may want to do something with `startIndex` here.
                        break;
                }

                mutexFlamingoZebra.ReleaseMutex();
            }

            return false;
        }

    }
}
