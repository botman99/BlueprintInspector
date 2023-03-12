namespace BlueprintInspector
{
	partial class SelectProjectForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
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
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.label1 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.OK_Button = new System.Windows.Forms.Button();
			this.Cancel_Button = new System.Windows.Forms.Button();
			this.EngineCheckbox = new System.Windows.Forms.CheckBox();
			this.PluginsCheckbox = new System.Windows.Forms.CheckBox();
			this.DevelopersCheckbox = new System.Windows.Forms.CheckBox();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(12, 43);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(485, 20);
			this.label1.TabIndex = 0;
			this.label1.Text = "xxx";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// comboBox1
			// 
			this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(143, 74);
			this.comboBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(223, 24);
			this.comboBox1.TabIndex = 1;
			// 
			// OK_Button
			// 
			this.OK_Button.Location = new System.Drawing.Point(281, 251);
			this.OK_Button.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.OK_Button.Name = "OK_Button";
			this.OK_Button.Size = new System.Drawing.Size(85, 31);
			this.OK_Button.TabIndex = 2;
			this.OK_Button.Text = "OK";
			this.OK_Button.UseVisualStyleBackColor = true;
			this.OK_Button.Click += new System.EventHandler(this.OK_Button_Click);
			// 
			// Cancel_Button
			// 
			this.Cancel_Button.Location = new System.Drawing.Point(396, 251);
			this.Cancel_Button.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.Cancel_Button.Name = "Cancel_Button";
			this.Cancel_Button.Size = new System.Drawing.Size(85, 31);
			this.Cancel_Button.TabIndex = 3;
			this.Cancel_Button.Text = "Cancel";
			this.Cancel_Button.UseVisualStyleBackColor = true;
			this.Cancel_Button.Click += new System.EventHandler(this.Cancel_Button_Click);
			// 
			// EngineCheckbox
			// 
			this.EngineCheckbox.AutoSize = true;
			this.EngineCheckbox.Location = new System.Drawing.Point(131, 117);
			this.EngineCheckbox.Margin = new System.Windows.Forms.Padding(4);
			this.EngineCheckbox.Name = "EngineCheckbox";
			this.EngineCheckbox.Size = new System.Drawing.Size(227, 20);
			this.EngineCheckbox.TabIndex = 4;
			this.EngineCheckbox.Text = "Include Engine Content Blueprints";
			this.EngineCheckbox.UseVisualStyleBackColor = true;
			// 
			// PluginsCheckbox
			// 
			this.PluginsCheckbox.AutoSize = true;
			this.PluginsCheckbox.Location = new System.Drawing.Point(131, 158);
			this.PluginsCheckbox.Margin = new System.Windows.Forms.Padding(4);
			this.PluginsCheckbox.Name = "PluginsCheckbox";
			this.PluginsCheckbox.Size = new System.Drawing.Size(229, 20);
			this.PluginsCheckbox.TabIndex = 5;
			this.PluginsCheckbox.Text = "Include Plugins Content Blueprints";
			this.PluginsCheckbox.UseVisualStyleBackColor = true;
			// 
			// DevelopersCheckbox
			// 
			this.DevelopersCheckbox.AutoSize = true;
			this.DevelopersCheckbox.Location = new System.Drawing.Point(131, 199);
			this.DevelopersCheckbox.Margin = new System.Windows.Forms.Padding(4);
			this.DevelopersCheckbox.Name = "DevelopersCheckbox";
			this.DevelopersCheckbox.Size = new System.Drawing.Size(245, 20);
			this.DevelopersCheckbox.TabIndex = 6;
			this.DevelopersCheckbox.Text = "Include Developers folder Blueprints";
			this.DevelopersCheckbox.UseVisualStyleBackColor = true;
			// 
			// SelectProjectForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(509, 310);
			this.Controls.Add(this.DevelopersCheckbox);
			this.Controls.Add(this.PluginsCheckbox);
			this.Controls.Add(this.EngineCheckbox);
			this.Controls.Add(this.Cancel_Button);
			this.Controls.Add(this.OK_Button);
			this.Controls.Add(this.comboBox1);
			this.Controls.Add(this.label1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "SelectProjectForm";
			this.Text = "Select Project";
			this.Load += new System.EventHandler(this.OnLoad);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ComboBox comboBox1;
		private System.Windows.Forms.Button OK_Button;
		private System.Windows.Forms.Button Cancel_Button;
		private System.Windows.Forms.CheckBox EngineCheckbox;
		private System.Windows.Forms.CheckBox PluginsCheckbox;
		private System.Windows.Forms.CheckBox DevelopersCheckbox;
	}
}