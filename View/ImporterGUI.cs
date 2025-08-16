using Microsoft.WindowsAPICodePack.Dialogs;
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
using System.Xml.Linq;

namespace RefImporter.View
{
    public partial class ImporterGUI : Form
    {
        public ImporterGUI()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Project Explorer .csproj
            FileDialog fileDialog = new OpenFileDialog
            {
                Title = "Select .csproj file",
                Filter = "C# Project Files (*.csproj)|*.csproj",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Multiselect = false
            };
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = fileDialog.FileName;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog folderDialog = new CommonOpenFileDialog
            {
                Title = "Select the folder containing .dll files",
                IsFolderPicker = true, // Set to select only folders
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                EnsurePathExists = true, // Ensure the path exists
                Multiselect = false
            };

            if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string selectedPath = folderDialog.FileName; // In folder mode, FileName returns the folder path

                var files = System.IO.Directory.GetFiles(selectedPath, "*.dll");
                if (files.Length == 0)
                {
                    MessageBox.Show("No .dll files were found in the selected folder.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    textBox2.Text = selectedPath;
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                var csprojPath = textBox1.Text;
                var dllDirectory = textBox2.Text;
                if (string.IsNullOrWhiteSpace(csprojPath) || string.IsNullOrWhiteSpace(dllDirectory))
                {
                    MessageBox.Show("Please select both the .csproj file and the folder containing .dll files.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!File.Exists(csprojPath))
                {
                    MessageBox.Show("The specified .csproj file does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!Directory.Exists(dllDirectory))
                {
                    MessageBox.Show("The specified folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                XDocument csprojXml = XDocument.Load(csprojPath);
                XNamespace ns = csprojXml.Root?.GetDefaultNamespace() ?? "";

                var existingRefs = csprojXml.Descendants(ns + "Reference")
                    .Select(r => r.Attribute("Include")?.Value)
                    .Where(r => r != null)
                    .ToHashSet();

                bool changesMade = false;

                foreach (var dllPath in Directory.GetFiles(dllDirectory, "*.dll"))
                {
                    string dllFileName = Path.GetFileNameWithoutExtension(dllPath);
                    if (string.IsNullOrWhiteSpace(dllFileName))
                        continue;

                    if (!string.IsNullOrEmpty(textBox3.Text))
                    {
                        if (!dllFileName.StartsWith(textBox3.Text, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;  
                        }
                    }


                    if (existingRefs.Contains(dllFileName))
                        continue;

                    if (!IsDotNetAssembly(dllPath))
                        continue;

                    //Console.WriteLine($"Adding reference to: {dllFileName}");
                    listBox1.Items.Add(dllFileName);

                    var itemGroup = csprojXml.Root.Elements(ns + "ItemGroup").FirstOrDefault() ??
                                    new XElement(ns + "ItemGroup");

                    if (!csprojXml.Root.Elements(ns + "ItemGroup").Contains(itemGroup))
                        csprojXml.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "Reference",
                        new XAttribute("Include", dllFileName),
                        new XElement(ns + "HintPath", MakeRelativePath(csprojPath, dllPath))
                    ));

                    changesMade = true;
                }

                if (changesMade)
                {
                    csprojXml.Save(csprojPath);
                    MessageBox.Show("References updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    //Console.WriteLine("References updated successfully.");
                }
                else
                {
                    // Console.WriteLine("No changes were made.");
                    MessageBox.Show("No new references were added. All DLLs are already referenced or no valid DLLs found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error processing the project file: {ex.Message}");
                MessageBox.Show($"Error processing the project file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsDotNetAssembly(string path) // Method to check if the file at 'path' is a valid .NET assembly
        {
            try
            {
                AssemblyName.GetAssemblyName(path);
                return true;
            }
            catch
            {
                return false;
            }
        }


        private static string MakeRelativePath(string fromPath, string toPath) // Method to create a relative path from 'fromPath' to 'toPath'
        {
            Uri fromUri = new Uri(Path.GetFullPath(fromPath));
            Uri toUri = new Uri(Path.GetFullPath(toPath));

            if (fromUri.Scheme != toUri.Scheme)
            {
                return toPath;
            }

            return Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
