# Drow Translator Azure Function

An Azure Function that translates text between English (Common) and the Drow language. This project is inspired by a 2003 Perl script by Brian Sidharta, focusing on translating English to Drow and vice versa using an SQLite database for the word list.

## Features

- Translate text from English to Drow and from Drow to English.
- Handles plurals, possessives, and contractions.
- Utilizes an SQLite database for efficient word lookup.
- Web UI served directly from the function.
- REST API with plain-text and JSON endpoints.
- Returns plain text responses for easy integration with applications like Second Life scripts.

## Prerequisites

- **.NET SDK** (version 10 or later)
- **Azure Functions Core Tools** (for local development and testing)
- **Python 3** (for dictionary maintenance scripts only)
- **NuGet Packages** (restored automatically via `dotnet restore`):
  - `Microsoft.Azure.Functions.Worker` and related extensions
  - `Microsoft.Data.Sqlite` (SQLite database access)
  - `Humanizer.Core` (pluralization and singularization)
  - `Swashbuckle.AspNetCore` (Swagger / OpenAPI docs)

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/auroris/DrowTranslatascan.git
cd DrowTranslatascan
```

### 2. Install Dependencies

```bash
dotnet restore
```

### 3. Configure Local Settings

Create a `local.settings.json` in the project root (this file is not committed to source control):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  },
  "ConnectionStrings": {}
}
```

### 4. Build the Project

```bash
dotnet build
```

### 5. Run the Function Locally

```bash
func start
```

The function will be available at `http://localhost:7071`.

## Usage

### Web UI

Open `http://localhost:7071/api/Home` (or just `http://localhost:7071/` — it redirects) in a browser for the dark-fantasy themed translator UI.

### API Endpoints

#### Plain-text translation

```
GET|POST /api/Translate?text={text}&lang={Drow|Common}
```

Returns translated text as `text/plain`.

Example:

```bash
curl "http://localhost:7071/api/Translate?text=Hello,+how+are+you?&lang=Drow"
```

Response:

```
Vendui, lu'oh ph' dos?
```

#### JSON translation

```
POST /api/TranslateJson
Content-Type: application/json

{"text": "Hello, how are you?", "lang": "Drow"}
```

Response:

```json
{
  "translation": "Vendui, lu'oh ph' dos?",
  "originalText": "Hello, how are you?",
  "targetLanguage": "Drow"
}
```

#### Parameters

- `text`: The text to translate.
- `lang`: Target language — `Drow` (English → Drow) or `Common` (Drow → English).

### API Documentation

With the function running locally, visit:

- **Swagger UI**: `http://localhost:7071/swagger/index.html`
- **OpenAPI spec**: `http://localhost:7071/swagger/v1/swagger.json`

## Running Tests

```bash
cd drowtest
dotnet run
```

## Dictionary Maintenance

The word list lives in `Data/drow_dictionary.csv` and is compiled into `Data/drow_dictionary.db` (SQLite). See [`scripts/README.md`](scripts/README.md) for full instructions on finding missing words and adding new entries.

Quick reference:

```bash
# Find common English words not yet in the dictionary
python3 scripts/missing_words.py

# Add new entries and rebuild the database
python3 scripts/patch_missing_words.py
```

## Deployment

Deploy the Azure Function to your Azure account using your preferred deployment method (e.g., Visual Studio publish, Azure CLI, or GitHub Actions).

Ensure that the `Data` folder and the `drow_dictionary.db` file are included in your deployment package.

### Disabling the Azure Functions Default Homepage

By default, Azure Functions shows its own homepage at `/`. To disable it so that the `/` → `/api/Home` redirect works correctly, set the following application setting in the Azure Portal:

| Setting | Value |
|---|---|
| `AzureWebJobsDisableHomepage` | `true` |

This can be configured under **Configuration → Application settings** in your Function App's Azure Portal page.

## Repository Structure

```
DrowTranslatascan/
├── Data/                        # Dictionary (CSV source + compiled SQLite DB)
├── analysis/                    # Corpus analysis scripts and output (phonotactics)
├── drowtest/                    # Test runner project
├── lsl/                         # Second Life LSL script example
├── scripts/                     # Dictionary maintenance scripts (see scripts/README.md)
├── tel'mithrim/                 # Original unmodified Tel'Mithrim word list and license
├── AlgorithmicConverter.cs      # Fallback algorithmic English↔Drow conversion
├── HomeFunction.cs              # Web UI (dark-fantasy themed HTML)
├── Program.cs                   # Host configuration and startup
├── ProgramState.cs              # Shared program state (e.g. DB path)
├── Translate.cs                 # Translation logic and HTTP trigger functions
└── TranslateModels.cs           # Request/response model types
```

## License

### Code

This project is licensed under the [MIT License](LICENSE).

### Word List

The original Drow word list is by Tel'Mithrim/Brian Sidharta and is licensed under **Tel'Mithrim/Brian Sidharta's Software License Version 1.0**. The unmodified original is preserved in [`tel'mithrim/`](tel'mithrim/WORDLIST_LICENSE.md). The version in `Data/` is a derivative work distributed under the same license terms.

## Acknowledgments

- **Tel'Mithrim/The Grey Company and Brian Sidharta**: For developing the original Drow dictionary and Perl script. See [`tel'mithrim/WORDLIST_LICENSE.md`](tel'mithrim/WORDLIST_LICENSE.md).
- **Humanizer Library**: For providing pluralization and singularization utilities.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any improvements or additions.

## Contact Information

For questions or support, please open an issue.
