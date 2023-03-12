using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlueprintInspector
{
	public partial class SelectProjectForm : Form
	{
		public List<string> ProjectFiles;
		public int SelectedIndex = -1;
		public bool bIncludeEngine = false;
		public bool bIncludePlugins = false;
		public bool bIncludeDevelopers = false;

		public SelectProjectForm()
		{
			InitializeComponent();

			StartPosition = FormStartPosition.CenterParent;

			EngineCheckbox.Checked = Properties.Settings.Default.IncludeEngine;
			PluginsCheckbox.Checked = Properties.Settings.Default.IncludePlugins;
			DevelopersCheckbox.Checked = Properties.Settings.Default.IncludeDevelopers;
		}

		private void OnLoad(object sender, EventArgs e)
		{
			if (ProjectFiles.Count == 1)
			{
				this.Text = "Project";

				label1.Text = String.Format("Generating the JSON file for project {0}", Path.GetFileNameWithoutExtension(ProjectFiles[0]));

				comboBox1.Hide();
			}
			else
			{
				label1.Text = "Select the project you wish to generate the JSON file for:";

			}

			for (int index = 0; index < ProjectFiles.Count; ++index)  // even though this is hidden for just a single project, add the only project to the (hidden) list
			{
				string projectName = Path.GetFileNameWithoutExtension(ProjectFiles[index]);
				comboBox1.Items.Add(projectName);
			}

			comboBox1.SelectedIndex = 0;  // default to first project (this will be used to set 'SelectedIndex' for just a single project)
		}

		private void OK_Button_Click(object sender, EventArgs e)
		{
			SelectedIndex = comboBox1.SelectedIndex;

			Properties.Settings.Default.IncludeEngine = EngineCheckbox.Checked;
			Properties.Settings.Default.IncludePlugins = PluginsCheckbox.Checked;
			Properties.Settings.Default.IncludeDevelopers = DevelopersCheckbox.Checked;

			Properties.Settings.Default.Save();  // this will go into C:\Users\<user>\AppData\Local\...\user.config

			bIncludeEngine = EngineCheckbox.Checked;
			bIncludePlugins = PluginsCheckbox.Checked;
			bIncludeDevelopers = DevelopersCheckbox.Checked;

			this.DialogResult = DialogResult.OK;

			Close();
		}

		private void Cancel_Button_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;

			Close();
		}
	}
}
