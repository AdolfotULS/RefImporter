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
using System.Threading;
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

        private async void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;
            button1.Enabled = false;
            button2.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                var csprojPath = textBox1.Text;
                var dllDirectory = textBox2.Text;
                var prefixFilter = textBox3.Text;

                if (string.IsNullOrWhiteSpace(csprojPath) || string.IsNullOrWhiteSpace(dllDirectory))
                {
                    MessageBox.Show("Select the .csproj file and the DLLs folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!File.Exists(csprojPath))
                {
                    MessageBox.Show("The .csproj file does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!Directory.Exists(dllDirectory))
                {
                    MessageBox.Show("The DLLs folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                progressBar1.Style = ProgressBarStyle.Continuous;
                progressBar1.Value = 0;
                progressBar1.Visible = true;

                var progress = new Progress<int>(v => progressBar1.Value = Math.Max(0, Math.Min(100, v)));

                var result = await Task.Run(() =>
                    ProcessReferencesSequential(csprojPath, dllDirectory, prefixFilter, progress),
                    CancellationToken.None
                );

                // Update UI in bulk (smoother)
                listBox1.BeginUpdate();
                listBox1.Items.Clear();
                if (result.Added.Count > 0)
                    listBox1.Items.AddRange(result.Added.ToArray());
                listBox1.EndUpdate();

                if (result.Errors.Count > 0)
                {
                    var msg = "Process finished with warnings:\n- " + string.Join("\n- ", result.Errors.Take(10));
                    if (result.Errors.Count > 10) msg += $"\n(+{result.Errors.Count - 10} more)";
                    MessageBox.Show(msg, "Warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                MessageBox.Show(
                    result.ChangesMade
                        ? "References updated successfully."
                        : "No new references were added (they already existed or there were no valid DLLs).",
                    "Result",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar1.Visible = false;
                Cursor = Cursors.Default;
                button3.Enabled = true;
                button1.Enabled = true;
                button2.Enabled = true;
            }
        }

        // Sequential processing logic outside the UI thread:
        private static (bool ChangesMade, List<string> Added, List<string> Errors) ProcessReferencesSequential(
            string csprojPath,
            string dllDirectory,
            string prefixFilter,
            IProgress<int> progress)
        {
            var added = new List<string>();
            var errors = new List<string>();
            bool changesMade = false;

            // Load XML
            var csprojXml = XDocument.Load(csprojPath);
            XNamespace ns = csprojXml.Root?.GetDefaultNamespace() ?? "";

            // Existing references (by name and by HintPath)
            var existingByInclude = csprojXml.Descendants(ns + "Reference")
                .Select(r => r.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingByHintPath = csprojXml.Descendants(ns + "Reference")
                .Select(r => r.Element(ns + "HintPath")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Reference ItemGroup
            var itemGroup = csprojXml.Root.Elements(ns + "ItemGroup").FirstOrDefault()
                           ?? new XElement(ns + "ItemGroup");
            if (!csprojXml.Root.Elements(ns + "ItemGroup").Contains(itemGroup))
                csprojXml.Root.Add(itemGroup);

            var dlls = Directory.GetFiles(dllDirectory, "*.dll");
            int total = Math.Max(1, dlls.Length);
            int count = 0;

            foreach (var dllPath in dlls)
            {
                count++;
                progress?.Report((int)(count * 100.0 / total));

                string dllName = Path.GetFileNameWithoutExtension(dllPath);
                if (string.IsNullOrWhiteSpace(dllName)) continue;

                if (!string.IsNullOrEmpty(prefixFilter) &&
                    !dllName.StartsWith(prefixFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Avoid duplicates by Include or HintPath
                if (existingByInclude.Contains(dllName) || existingByHintPath.Contains(dllName))
                    continue;

                // .NET assembly validation: can throw, so try/catch per DLL
                try
                {
                    AssemblyName.GetAssemblyName(dllPath);
                }
                catch (Exception ex)
                {
                    errors.Add($"{dllName}: not a valid .NET assembly ({ex.GetType().Name}).");
                    continue;
                }

                // Add reference
                var relPath = MakeRelativePath(csprojPath, dllPath);
                var reference = new XElement(ns + "Reference",
                    new XAttribute("Include", dllName),
                    new XElement(ns + "HintPath", relPath)
                );

                itemGroup.Add(reference);
                existingByInclude.Add(dllName);
                existingByHintPath.Add(dllName);
                added.Add(dllName);
                changesMade = true;
            }

            // Backup and save
            if (changesMade)
            {
                var backup = csprojPath + ".bak";
                try { File.Copy(csprojPath, backup, overwrite: true); } catch { /* best effort */ }
                csprojXml.Save(csprojPath);
            }

            progress?.Report(100);
            return (changesMade, added, errors);
        }

        // (reuse your MakeRelativePath as is)
        private static string MakeRelativePath(string fromPath, string toPath)
        {
            Uri fromUri = new Uri(Path.GetFullPath(fromPath));
            Uri toUri = new Uri(Path.GetFullPath(toPath));
            if (fromUri.Scheme != toUri.Scheme)
                return toPath;
            return Uri.UnescapeDataString(
                fromUri.MakeRelativeUri(toUri).ToString().Replace('/', Path.DirectorySeparatorChar));
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
    }
}
