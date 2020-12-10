using System;
using System.Drawing;
using System.Windows.Forms;

namespace exampleForm
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (Form form = new Form())
            {
                form.Text = "Event handle examples";

                Button button = new Button();
                button.Location = new Point(90, 80);
                button.Size = new Size(100, 100);
                button.Text = "Create an event";
                button.Click += new EventHandler(Button_Click);
                form.Controls.Add(button);

                form.ShowDialog();
            }       

        }

        private static void Button_Click(object sender, EventArgs e)
        {
            if (sender is Button)
            {
                MessageBox.Show($"Button {(sender as Button).Text} was clicked.");
            }
        }
    }
}
