# Drow Translator Azure Function

An Azure Function that translates text between English (Common) and the Drow language. This project is a C# adaptation of a 2001 Perl script by Brian Sidharta, focusing on translating English to Drow and vice versa using an SQLite database for the word list.

## Features

- Translate text from English to Drow and from Drow to English.
- Handles plurals, possessives, and contractions.
- Utilizes an SQLite database for efficient word lookup.
- Returns plain text responses for easy integration with applications like Second Life scripts.

## Prerequisites

- **.NET Core SDK** (version 3.1 or later)
- **Azure Functions Core Tools** (for local development and testing)
- **SQLite** (to manage the database file)
- **NuGet Packages**:
  - `Humanizer.Core` (for pluralization and singularization)
  - `System.Data.SQLite` (for SQLite database access)

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/drow-translator-azure-function.git
cd drow-translator-azure-function
```

### 2. Install Dependencies

Restore NuGet packages:

```bash
dotnet restore
```

### 3. Build the Project

```bash
dotnet build
```

### 4. Run the Function Locally

```bash
func start
```

## Usage

### Sending a Request

You can send HTTP POST requests to the function endpoint to translate text.

#### Example Request

```bash
curl -X POST "http://localhost:7071/api/Translate" \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "text=Hello, how are you?&lang=Drow"
```

#### Parameters

- `text`: The text you want to translate.
- `lang`: The target language (`Drow` or `Common`).

### Response

The function returns the translated text as plain text.

#### Example Response

```
Vendui, lu'oh ph' dos?
```

## Deployment

Deploy the Azure Function to your Azure account using your preferred deployment method (e.g., Visual Studio publish, Azure CLI, or GitHub Actions).

Ensure that the `Data` folder and the `drow_dictionary.db` file are included in your deployment package.

## License

### Code

This project is licensed under the [MIT License](LICENSE).

### Word List

The Drow word list included in this project is licensed under **Tel'Mithrim/Brian Sidharta's Software License Version 1.0**. See [WORDLIST_LICENSE](WORDLIST_LICENSE) for details.

> "This product includes software developed by Tel'Mithrim (http://www.grey-company.org/)."

## Acknowledgments

- **Tel'Mithrim/The Grey Company and Brian Sidharta**: For developing the original Drow dictionary and Perl script.
- **Humanizer Library**: For providing pluralization and singularization utilities.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any improvements or additions.

## Contact Information

For questions or support, please open an issue.