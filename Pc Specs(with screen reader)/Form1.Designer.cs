namespace Pc_Specs_with_screen_reader_
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label infoLabel;
        private System.Windows.Forms.Button loadButton;
        private System.Windows.Forms.Button screenReaderButton;
        private System.Windows.Forms.Button tempUnitButton;
        private System.Windows.Forms.Button updateButton;
        // removed: tempLogButton
        // cpu/gpu/temp labels removed (temps shown in infoLabel)

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            infoLabel = new Label();
            loadButton = new Button();
            screenReaderButton = new Button();
            tempUnitButton = new Button();
            updateButton = new Button();
            SuspendLayout();
            // 
            // infoLabel
            // 
            infoLabel.BackColor = Color.Black;
            infoLabel.ForeColor = Color.Lime;
            infoLabel.Location = new Point(12, 12);
            infoLabel.Name = "infoLabel";
            infoLabel.Size = new Size(654, 300);
            infoLabel.TabIndex = 0;
            // 
            // loadButton
            // 
            loadButton.BackColor = Color.White;
            loadButton.ForeColor = Color.Lime;
            loadButton.Location = new Point(12, 330);
            loadButton.Name = "loadButton";
            loadButton.Size = new Size(120, 30);
            loadButton.TabIndex = 1;
            loadButton.Text = "Load Specs";
            loadButton.UseVisualStyleBackColor = false;
            loadButton.Click += loadButton_Click;
            // 
            // screenReaderButton
            // 
            screenReaderButton.BackColor = Color.White;
            screenReaderButton.ForeColor = Color.Lime;
            screenReaderButton.Location = new Point(150, 330);
            screenReaderButton.Name = "screenReaderButton";
            screenReaderButton.Size = new Size(160, 30);
            screenReaderButton.TabIndex = 2;
            screenReaderButton.Text = "Screen Reader: Off";
            screenReaderButton.UseVisualStyleBackColor = false;
            screenReaderButton.Click += screenReaderButton_Click;
            // 
            // tempUnitButton
            // 
            tempUnitButton.BackColor = Color.White;
            tempUnitButton.ForeColor = Color.Lime;
            tempUnitButton.Location = new Point(330, 330);
            tempUnitButton.Name = "tempUnitButton";
            tempUnitButton.Size = new Size(100, 30);
            tempUnitButton.TabIndex = 3;
            tempUnitButton.Text = "Show °F";
            tempUnitButton.UseVisualStyleBackColor = false;
            tempUnitButton.Click += tempUnitButton_Click;
            // 
            // updateButton
            // 
            updateButton.BackColor = Color.White;
            updateButton.ForeColor = Color.Lime;
            updateButton.Location = new Point(450, 330);
            updateButton.Name = "updateButton";
            updateButton.Size = new Size(150, 30);
            updateButton.TabIndex = 4;
            updateButton.Text = "Latest Source Code";
            updateButton.UseVisualStyleBackColor = false;
            updateButton.Click += updateButton_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            ClientSize = new Size(602, 363);
            Controls.Add(infoLabel);
            Controls.Add(loadButton);
            Controls.Add(screenReaderButton);
            Controls.Add(tempUnitButton);
            Controls.Add(updateButton);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = true;
            Name = "Form1";
            Text = "Pc Specs";
            ResumeLayout(false);
        }

        #endregion
    }
}
