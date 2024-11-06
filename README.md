# ArbitrageGainer Project

This project is a web application using F# and Giraffe for managing trading strategies and integrating real-time market data from Polygon's WebSocket API.

## Prerequisites

- .NET SDK (version 6.0 or above)

## Installation Instructions

Follow these steps to set up the project:

1. **Clone the Repository**
   ```sh
   git clone <repository_url>
   cd ArbitrageGainer
   ```

2. **Install Required Packages**

   Install the necessary NuGet packages using the following commands:

   ```sh
   dotnet add package Giraffe --version 5.0.0
   dotnet add package DotNetEnv --version 2.4.0
   dotnet add package System.Text.Json --version 8.0.5
   dotnet add package FSharp.SystemTextJson --version 1.3.13
   dotnet add package Newtonsoft.Json --version 13.0.3
   ```

3. **Environment Variables**

   Create an `.env` file in the project directory with the following content:

   ```
   API_KEY=<your_polygon_api_key>
   ```
   This file is used to store your API key for the Polygon WebSocket.

4. **Run the Application**

   To run the application, use the following command:

   ```sh
   dotnet run
   ```

## Project Libraries

Here is a list of libraries we have installed and used in this project:

1. **Giraffe** - Web framework for F# that integrates seamlessly with ASP.NET Core.
   - Command: `dotnet add package Giraffe --version 5.0.0`

2. **DotNetEnv** - Library to load environment variables from an `.env` file.
   - Command: `dotnet add package DotNetEnv --version 2.4.0`

3. **System.Text.Json** - For JSON serialization and deserialization.
   - Command: `dotnet add package System.Text.Json --version 8.0.5`

4. **FSharp.SystemTextJson** - For JSON serialization in F# with System.Text.Json.
   - Command: `dotnet add package FSharp.SystemTextJson --version 1.3.13`

5. **Newtonsoft.Json** - Used for JSON manipulation and conversion.
   - Command: `dotnet add package Newtonsoft.Json --version 13.0.3`

## Usage

This application listens on port 8080 and provides the following functionalities:
- Real-time WebSocket client integration with Polygon for market data.
- Trading strategy management through a REST API.

You can access the web application via [http://localhost:8080](http://localhost:8080).

## Troubleshooting

### Port Already in Use
If you receive an error like `Failed to bind to address http://0.0.0.0:8080: address already in use`, it means another process is already using the port. You can either:
- Terminate the process using port 8080 by running:
  ```sh
  lsof -i :8080
  kill -9 <PID>
  ```
- Change the port in the `Program.fs` file to an available port, for example:
  ```fsharp
  .UseUrls("http://0.0.0.0:8081")
  ```

## License

This project is licensed under the MIT License.