
# ArbitrageGainer Project

ArbitrageGainer is a web application developed using F# and Giraffe, designed to manage trading strategies and integrate real-time market data from Polygon's WebSocket API. The application adheres to functional programming principles and the Onion Architecture, ensuring a clean, maintainable, and scalable codebase without relying on Object-Oriented Programming (OOP) constructs.

## Table of Contents
- Features
  - Trading Strategy Management
- Prerequisites
- Installation Instructions
- Project Libraries
- Usage
  - Trading Strategy Management
- Source Code Structure
- Troubleshooting
- License
- Future Improvements

## Features
### Trading Strategy Management
The system handles user inputs to provide and update trading strategy parameters. These actions are exposed as REST API endpoints, allowing users to manage trading strategies without a graphical user interface. The endpoints can be invoked and tested using tools like curl.

#### Implemented Functionalities:

- **Create/Update Trading Strategy**: Allows users to submit trading strategy parameters to create a new strategy or update an existing one.
- **Retrieve Current Trading Strategy**: Enables users to fetch the currently active trading strategy.
- **Validation of Input Parameters**: Ensures that all trading strategy parameters meet the required validation rules before being processed.

### Source Code Files

- **Domain Layer**:
  - `Core\Domain.fs`: Defines domain types, data transfer objects (DTOs), validation logic, and error types.
- **Application Layer**:
  - `Services\Application.fs`: Implements the business logic for saving and updating trading strategies.
- **Infrastructure Layer**:
  - `Infrastructure\TradingStrategyInfra.fs`: Contains the file repository implementation for persisting trading strategies to the file system.
- **Presentation Layer**:
  - `Presentation.fs`: Defines the HTTP handlers for the REST API endpoints related to trading strategy management.
- **Program Entry Point**:
  - `Program.fs`: Configures and starts the web server, sets up routing, initializes logging, and manages WebSocket connections.

## Prerequisites
- .NET SDK (version 6.0 or above)
- Git (for cloning the repository)

## Installation Instructions

Follow these steps to set up the ArbitrageGainer project:

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

```makefile
API_KEY=<your_polygon_api_key>
```

This file is used to store your API key for the Polygon WebSocket.

4. **Run the Application**

To run the application, use the following command:

```sh
dotnet run
```

The application will start and listen on port 8000.

## Project Libraries

Below is a list of libraries installed and used in this project:

- **Giraffe** - Web framework for F# that integrates seamlessly with ASP.NET Core.
- **DotNetEnv** - Library to load environment variables from an `.env` file.
- **System.Text.Json** - For JSON serialization and deserialization.
- **FSharp.SystemTextJson** - For JSON serialization in F# with System.Text.Json.
- **Newtonsoft.Json** - Used for JSON manipulation and conversion.

## Usage

This application listens on port 8000 and provides the following functionalities:

### Trading Strategy Management

Manage trading strategies through REST API endpoints. Since there is no user interface, these endpoints can be invoked and tested using tools like curl.

- **Create/Update Trading Strategy**
  - **Endpoint**: `POST /trading-strategy`
  - **Description**: Submit trading strategy parameters to create a new strategy or update an existing one.
  
  Example:
  ```sh
  curl -X POST http://localhost:8000/trading-strategy   -H "Content-Type: application/json"   -d '{
        "NumberOfCurrencies": 5,
        "MinimalPriceSpread": 0.5,
        "MaximalTransactionValue": 10000.0,
        "MaximalTradingValue": 50000.0
      }'
  ```

- **Retrieve Current Trading Strategy**
  - **Endpoint**: `GET /trading-strategy`
  - **Description**: Fetch the currently active trading strategy.
  
  Example:
  ```sh
  curl http://localhost:8000/trading-strategy
  ```

### Annualized Return Calculation

get annualized return REST API endpoints. The endpoint accept a parameter with the initial investment from user.

- **Get Annualized Return**
  - **Endpoint**: `GET /annualized-return`
  - **Description**: get the current annualized return from initial investment

  Example:
  ```sh
  curl -X POST http://localhost:8000/trading-strategy?initInvest=4.45
  ```
  
  Response:
  ```
  {
    "status": "failed",
    "message": "12.883"
  }
  ```

## Unit Testing

Unit Testing is being done using a separate fsharp project `ArbitrageGainerTest` with NUnit Library

### How to use it

prepare the package installed with the following version
```
    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1"/>
        <PackageReference Include="NUnit" Version="4.2.2"/>
        <PackageReference Include="NUnit3TestAdapter" Version="4.6.0"/>
        <PackageReference Include="NUnit.Analyzers" Version="4.3.0"/>
        <PackageReference Include="coverlet.collector" Version="6.0.2"/>
    </ItemGroup>
```

Use the following command to run ArbitrageGainerTest unit testing framework

```sh
dotnet test
```

### Test Suite Explain
#### AnnualizedReturnCalcTest
This test suite is mainly tested for edge cases and error management for the annualized return calculation, 
including negative value validation for duration of years, P&L, and initial investment.

#### HistoryArbitrageOpportunityTest
This test suite is mainly tested for the functionality of calculating historicalArbitrage Opportunity, 
including if the algorithm can successfully separate the opportunities by buckets within 5ms. Also, 
the pair should be recognized as maximum pair from the bucket, and only the opportunities that has more
than 0.01$ profit can be recognized.


## Source Code Structure

Below is the mapping of functionalities to their corresponding source code files:

- **Domain Layer**:
  - File: `Core\Domain.fs`
    - **Functionality**: Defines trading strategy data types (TradingStrategy, TradingStrategyDto), validation logic, and error types.
- **Application Layer**:
  - File: `Services\Application.fs`
    - **Functionality**: Implements business logic for saving and updating trading strategies.
- **Infrastructure Layer**:
  - File: `Infrastructure\TradingStrategyInfra.fs`
    - **Functionality**: Provides file repository implementation for persisting trading strategies.
- **Presentation Layer**:
  - File: `Presentation.fs`
    - **Functionality**: Defines HTTP handlers for REST API endpoints (/trading-strategy).
- **Program Entry Point**:
  - File: `Program.fs`
    - **Functionality**: Configures and starts the web server, sets up routing for different API endpoints, initializes logging, and manages WebSocket connections with Polygon.

## Troubleshooting

- **Port Already in Use**:
  If you receive an error like `Failed to bind to address http://0.0.0.0:8000: address already in use`, it means another process is already using the port. You can either:

  - Terminate the Process Using Port 8000:
    ```sh
    lsof -i :8000
    kill -9 <PID>
    ```
  - Change the Port in Program.fs: Modify the port number in the `Program.fs` file to an available port, for example:
    ```fsharp
    .UseUrls("http://0.0.0.0:8080")
    ```

- **WebSocket Connection Issues**:
  Ensure that your `API_KEY` in the `.env` file is correctly configured and that your network allows connections to Polygon's WebSocket service.

- **JSON Deserialization Errors**:
  Ensure that the JSON payloads sent to the `POST /trading-strategy` endpoint are correctly formatted and include all required fields.

  Example of Correct JSON Payload:
  ```json
  {
    "NumberOfCurrencies": 5,
    "MinimalPriceSpread": 0.5,
    "MaximalTransactionValue": 10000.0,
    "MaximalTradingValue": 50000.0
  }
  ```

- **Undefined Modules or Functions**:
  If you encounter errors related to undefined modules or functions:

  - Verify Module Inclusion: Ensure all necessary `.fs` files are included in your `.fsproj` file in the correct order.
  - Check Namespace Imports: Confirm that namespaces are correctly opened (`open NamespaceName`) in each file.
  - Ensure Correct Function References: Functions should be referenced with their full paths if they belong to specific modules.

## License

This project is licensed under the MIT License.