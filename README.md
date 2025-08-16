# RefImporter

RefImporter is a Windows Forms application developed in C# (.NET Framework). It provides a graphical user interface for importing DLL references into a C# project file (.csproj), streamlining the process for developers working with multiple dependencies.

## Features

- Windows Forms-based GUI (`ImporterGUI`)
- Select a `.csproj` file and a folder containing `.dll` files
- Automatically adds missing DLL references to the project file
- Ensures only valid .NET assemblies are referenced
- Displays messages for success, warnings, or errors

## Getting Started

### Prerequisites

- .NET Framework (suitable for Windows Forms applications)
- Visual Studio or a compatible IDE

### Installation

1. Clone the repository:
    ```bash
    git clone https://github.com/AdolfotULS/RefImporter.git
    ```
2. Open the solution file (`RefImporter.sln`) in Visual Studio.
3. Build the solution to restore dependencies.
4. Run the application.

### Usage

1. Launch the application; the main GUI window will appear.
2. Click the button to select your `.csproj` file.
3. Click the button to select the folder containing your `.dll` files.
4. The application checks which DLLs are already referenced and adds missing ones as needed.
5. Success and error messages inform you of the results.

## Project Structure

- `Program.cs`: Main entry point. Initializes and runs the graphical interface.
- `View/ImporterGUI.cs`: Implements the main Windows Form, handles user interaction, file dialogs, reference importing, and error handling.
- `Properties/AssemblyInfo.cs`: Assembly metadata and versioning information.

## Contributing

Contributions, issues, and feature requests are welcome!

## License

See the repository for license information.

---

*This README was generated based on the code structure and files. For more detailed usage and features, please expand this document as needed.*
