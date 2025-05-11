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


    public class Lake<T>
    {
        // Fields (private variables)
        private int slots;
        private int id;
        private List<T> AnimalList;
        static Mutex mutexHippo;

        public Lake(int id, int slots)
        {
            this.id = id;
            this.slots = slots;
            this.AnimalList = new List<T>(slots);
            mutexHippo= new Mutex();

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


        public Boolean insertAnimal(Animal animal)
        {




            String type = animal.getType();

            switch (type)

            {

                case "h":

                    mutexHippo.WaitOne();  // Enter critical section
                    this.AnimalList.Clear();

                    this.AnimalList[0] = animal;
                    break;



                case "z":

                    int? startIndex = null;

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
                        Console.WriteLine("First pair of adjacent nulls starts at index: " + startIndex.Value);
                    }
                    else
                    {
                        Console.WriteLine("No adjacent nulls found.");
                    }


                    break;




                case "f":
                    Console.WriteLine("Option 3 selected.");
                    break;
                default:
                    Console.WriteLine("Invalid option.");
                    break;

            }







            return false;
        }





    }
}
