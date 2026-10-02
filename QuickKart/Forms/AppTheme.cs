using System.Drawing;
using System.Windows.Forms;

namespace QuickKart.Forms
{
    internal static class AppTheme
    {
        public static readonly Color Primary = Color.FromArgb(31, 122, 74);
        public static readonly Color PrimaryDark = Color.FromArgb(20, 92, 55);
        public static readonly Color Accent = Color.FromArgb(241, 167, 51);
        public static readonly Color Background = Color.FromArgb(244, 247, 245);
        public static readonly Color Text = Color.FromArgb(38, 48, 43);
        public static readonly Color Muted = Color.FromArgb(103, 116, 109);
        public static readonly Color Danger = Color.FromArgb(190, 53, 53);

        public static Label Heading(string text, float size)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                ForeColor = Text,
                Font = new Font("Segoe UI Semibold", size, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 9)
            };
        }

        public static Label Caption(string text)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 0, 0, 4)
            };
        }

        public static TextBox Input(bool password = false)
        {
            return new TextBox
            {
                Width = 330,
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = password,
                Margin = new Padding(0, 0, 0, 12)
            };
        }

        public static Button PrimaryButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Width = 330,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = Primary,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 6, 0, 8)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        public static Button LinkButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Width = 330,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Primary,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            button.FlatAppearance.BorderColor = Primary;
            return button;
        }
    }
}
