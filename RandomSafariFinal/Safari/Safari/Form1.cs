using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Safarihelper;
using System.Linq;

namespace Safari
{
    public partial class Form1 : Form
    {
        private List<Lake> lakes;
        private Dictionary<Lake, PictureBox[]> lakeSlotsVisual = new Dictionary<Lake, PictureBox[]>(); // maps each lake to its visual slots
        private Random rnd = new Random();
        private int lakeSlotSize = 60; // legacy value, not used now with circular layout

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            this.BackgroundImage = Image.FromFile("nature.png");
            this.BackgroundImageLayout = ImageLayout.Stretch;

            SetupLakesAndVisuals(); // draw lakes and prepare visual slots
            StartSpawning();        // start generating animals
        }

        private void SetupLakesAndVisuals()
        {
            int numLakes = rnd.Next(3, 7); // Random number of lakes: 3 to 6
            lakes = new List<Lake>();
            lakeSlotsVisual.Clear();

            for (int i = 0; i < numLakes; i++)
            {
                int slots = rnd.Next(5, 11); // Each lake gets 5 to 10 slots
                lakes.Add(new Lake(i + 1, slots));
            }

            // Track occupied zones to avoid overlap
            List<Rectangle> occupiedAreas = new List<Rectangle>();

            int lakeSize = 150;
            int radius = 100;
            int animalSize = 50;
            int padding = 100;

            for (int i = 0; i < lakes.Count; i++)
            {
                Lake lake = lakes[i];
                int slotCount = lake.SlotCount();

                PictureBox[] slotBoxes = new PictureBox[slotCount];
                lakeSlotsVisual[lake] = slotBoxes;

                lake.OnAnimalStatusChange = (slot, animal, status) =>
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        UpdateSlotVisual(lake, slot, animal, status);
                    }));
                };

                // Try to place lake in a non-overlapping position
                Point center;
                int attempts = 0;
                do
                {
                    int x = rnd.Next(radius + padding, this.ClientSize.Width - radius - padding);
                    int y = rnd.Next(radius + padding, this.ClientSize.Height - radius - padding);
                    center = new Point(x, y);

                    var bounds = new Rectangle(
                        x - radius - padding,
                        y - radius - padding,
                        (radius + animalSize + padding) * 2,
                        (radius + animalSize + padding) * 2
                    );

                    if (!occupiedAreas.Any(area => area.IntersectsWith(bounds)))
                    {
                        occupiedAreas.Add(bounds);
                        break;
                    }

                    attempts++;
                } while (attempts < 100);

                // Add lake image
                PictureBox lakeImage = new PictureBox
                {
                    Image = Image.FromFile("lake.png"),
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Width = lakeSize,
                    Height = lakeSize,
                    Left = center.X - lakeSize / 2,
                    Top = center.Y - lakeSize / 2,
                    BackColor = Color.Transparent
                };
                this.Controls.Add(lakeImage);
                lakeImage.SendToBack();

                // Place animals in circular layout
                double angleStep = 2 * Math.PI / slotCount;
                for (int j = 0; j < slotCount; j++)
                {
                    double angle = j * angleStep;
                    int x = center.X + (int)(radius * Math.Cos(angle)) - animalSize / 2;
                    int y = center.Y + (int)(radius * Math.Sin(angle)) - animalSize / 2;

                    PictureBox pb = new PictureBox
                    {
                        Width = animalSize,
                        Height = animalSize,
                        Left = x,
                        Top = y,
                        BackColor = Color.Transparent,
                        BorderStyle = BorderStyle.FixedSingle,
                        SizeMode = PictureBoxSizeMode.StretchImage
                    };
                    this.Controls.Add(pb);
                    pb.BringToFront();
                    slotBoxes[j] = pb;
                }
            }
        }



        // Updates animal images in the UI (entering or leaving a slot)
        private void UpdateSlotVisual(Lake lake, int slot, Animal animal, string status)
        {
            if (!lakeSlotsVisual.ContainsKey(lake)) return;
            PictureBox pb = lakeSlotsVisual[lake][slot];

            if (status == "enter")
            {
                string file = null;
                string type = animal.getType();

                if (type == "f")
                    file = "flamingo.png";
                else if (type == "z")
                    file = "zebra.png";
                else if (type == "h")
                    file = "hippo.png";

                if (file != null && File.Exists(file))
                {
                    pb.Image = Image.FromFile(file);
                }
            }
            else if (status == "exit")
            {
                pb.Image = null;
            }
        }

        // Starts parallel spawners for each animal type
        private void StartSpawning()
        {
            StartAnimalSpawner<Flamingo>(2.0);      // spawn flamingos on average every 2 seconds
            StartAnimalSpawner<Zebra>(3.0);         // spawn zebras every 3 seconds
            StartAnimalSpawner<Hippopotamus>(10.0); // hippos less frequently
        }

        // Animal spawner thread for the given species
        private void StartAnimalSpawner<T>(double mean) where T : Animal
        {
            new Thread(() =>
            {
                while (true)
                {
                    // Wait for a randomized delay before spawning
                    TimeSpan delay = Safarihelper.helper.GetRandomNormalTime(mean);
                    Thread.Sleep(delay);

                    Lake lake = lakes[rnd.Next(lakes.Count)]; // pick a random lake

                    Animal a;
                    if (typeof(T) == typeof(Flamingo))
                        a = new Flamingo(lake);
                    else if (typeof(T) == typeof(Zebra))
                        a = new Zebra(lake);
                    else if (typeof(T) == typeof(Hippopotamus))
                        a = new Hippopotamus(lake);
                    else
                        throw new InvalidOperationException("Unknown animal type");

                    // Start the animal's behavior in its own thread
                    new Thread(a.RunAsyncWrapper) { IsBackground = true }.Start();
                }
            })
            { IsBackground = true }.Start();
        }
    }
}
