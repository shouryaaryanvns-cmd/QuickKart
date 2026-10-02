using System;
using System.Drawing;
using System.Windows.Forms;
using QuickKart.Models;

namespace QuickKart.Forms
{
    public sealed class AddressForm : Form
    {
        private readonly TextBox houseTextBox;
        private readonly TextBox streetTextBox;
        private readonly TextBox cityTextBox;
        private readonly TextBox stateTextBox;
        private readonly TextBox pincodeTextBox;
        private readonly TextBox landmarkTextBox;

        public AddressRequest Address { get; private set; }

        public AddressForm()
        {
            Text = "QuickKart - Add delivery address";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 510);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            var title = AppTheme.Heading("Delivery address", 19f);
            title.Location = new Point(36, 26);
            Controls.Add(title);

            var subtitle = AppTheme.Caption("This address will be saved to your QuickKart profile.");
            subtitle.Location = new Point(39, 66);
            Controls.Add(subtitle);

            houseTextBox = AddField("House / flat number", 105);
            streetTextBox = AddField("Street / locality", 165);
            cityTextBox = AddField("City *", 225);
            stateTextBox = AddField("State *", 285);
            pincodeTextBox = AddField("Pincode *", 345);
            landmarkTextBox = AddField("Landmark", 405);
            pincodeTextBox.MaxLength = 10;

            var cancelButton = AppTheme.LinkButton("Cancel");
            cancelButton.Width = 120;
            cancelButton.Location = new Point(226, 458);
            cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);

            var saveButton = AppTheme.PrimaryButton("Save address");
            saveButton.Width = 130;
            saveButton.Location = new Point(354, 452);
            saveButton.Click += SaveButton_Click;
            Controls.Add(saveButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private TextBox AddField(string caption, int top)
        {
            Label label = AppTheme.Caption(caption);
            label.Location = new Point(39, top - 22);
            Controls.Add(label);

            TextBox input = AppTheme.Input();
            input.Width = 442;
            input.Location = new Point(39, top);
            Controls.Add(input);
            return input;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(cityTextBox.Text) ||
                string.IsNullOrWhiteSpace(stateTextBox.Text) ||
                string.IsNullOrWhiteSpace(pincodeTextBox.Text))
            {
                MessageBox.Show(
                    this,
                    "City, state and pincode are required.",
                    "Check address",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Address = new AddressRequest
            {
                HouseNo = houseTextBox.Text,
                Street = streetTextBox.Text,
                City = cityTextBox.Text,
                State = stateTextBox.Text,
                Pincode = pincodeTextBox.Text,
                Landmark = landmarkTextBox.Text
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
