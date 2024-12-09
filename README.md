
# ArbitrageGainer Project

ArbitrageGainer is a web application developed using F# and Giraffe, designed to manage trading strategies and integrate real-time market data from Polygon's WebSocket API. The application adheres to functional programming principles and the Onion Architecture, ensuring a clean, maintainable, and scalable codebase without relying on Object-Oriented Programming (OOP) constructs.

## Table of Contents
- Prerequisites
- Database Configuration
- Access the Application
- Installation Instructions
- Usage
  - Trading Strategy Management
  - Start Real-time Trading Algorithm
  - Cross-Traded Pairs Management
  - Annualized Return Metric Calculation
  - P&L Calculation
  - Order Management
- Performance Optimization and Testing
- Unit Testing
- Srouce Code Structure
- Troubleshooting
- License
- Future Improvements


## Prerequisites
- .NET SDK (version 6.0 or above)
- Git (for cloning the repository)

### Pull the Docker Image

Pull the pre-built Docker image from Docker Hub:

```bash
docker pull 0tt00t/arbitrage_app:latest
```
### Run the Docker Container
To start the application, use the following command:
```bash
docker run -d \
  --name arbitrage-gainer-container \
  -v /path/to/your/.env:/app/.env \
  -p 8080:8080 \
  0tt00t/arbitrage_app:latest
```
## Database Configuration

To connect the application to the database, you need to create a `.env` file in the root directory of the project. This file contains the necessary configuration details, including API keys and MongoDB connection settings.

### Creating the `.env` File

1. Navigate to the root directory of the project.
2. Create a new file named `.env`.
3. Add the following content to the file:

   ```env
   API_KEY=BKTRbIhK3OPX5Iptfh9pbpUlolQQMW2e
   MONGO_DB_NAME="your_db_name"
   MONGO_DB_URL="your_db_url"
   ```
## Access the Application
Once the container is running, the application will be accessible at:

arduino
```
http://localhost:8080
```
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

### Start Real-time Trading Algorithm
Get result of historical arbitrage analysis and cross traded paris identification. Fetch trading parameters and subscribe to real-time market data. Start the Arbitrage Gainer Trading process.

- **Start Trading Process**
  - **Endpoint**: `Post /start-trading`
  - **Description**: Start the real-time trading algorithm

  Example usage:
  ```
  curl -X POST http://localhost:8000/start-trading \
      -H "Content-Type: application/json" \
  ```

- **Stop Trading Process**
  - **Endpoiny**: `POST
   /stop-trading`
  - **Description**: Stop the real-time trading algorithm

  Example usage:
  ```
  curl -X POST http://localhost:8000/stop-trading \
      -H "Content-Type: application/json" \
  ```

### Cross-Traded Pairs Management
Manage cross-traded cryptocurrency pairs through REST API endpoints. These endpoints allow users to retrieve pairs that are traded on multiple exchanges (Bitfinex, Bitstamp, Kraken) and can be tested using curl.

- Retrieve Cross-Traded Pairs
  - Endpoint: GET /cross-traded-pairs
  - Description: Fetch a list of cryptocurrency pairs that are traded on at least two of the exchanges (Bitfinex, Bitstamp, Kraken).
  - Response: The response includes an array of cross-traded pairs, formatted as ["BTC-USD", "ETH-USD", ...].

Example Usage:

```sh
curl http://localhost:8000/cross-traded-pairs
```

- Save Cross-Traded Pairs to File
  - Description: When the cross-traded pairs are retrieved via the GET /cross-traded-pairs endpoint, they are also saved to a JSON file called cross_traded_pairs.json located in the root directory of the project.
  - File Content: The file cross_traded_pairs.json contains an array of currency pairs in JSON format, structured as ["BTC-USD", "ETH-USD", ...].
    Example File Content:

```json
[
  "BTC-USD",
  "ETH-USD",
  "LTC-USD"
]
```

<hr>

#### API Usage Guide
Once the application is running, you can interact with the cross-traded-pairs endpoint as follows:

1. Retrieve Cross-Traded Pairs: Use the following command to retrieve cross-traded pairs:

```sh
curl http://localhost:8000/cross-traded-pairs
```
This command will return a list of cross-traded pairs in JSON format, and also save them to cross_traded_pairs.json in the project's root directory.

2. File Output: After calling the GET /cross-traded-pairs endpoint, check the root directory of the project for the file cross_traded_pairs.json. This file should contain the cross-traded pairs in a JSON array format, allowing the user to access the retrieved data offline.

### Annualized Return Metric Calculation

Retrieve the annualized return using REST API endpoints. The endpoint accepts the initial investment amount from the user and calculates the annualized return.

- **Get Annualized Return**
  - **Endpoint**: `GET /annualized-return`
  - **Description**: Calculates and retrieves the current annualized return based on the initial investment and actual trading data.

  Example:
  ```sh
  curl -X GET http://localhost:8000/annualized-return?initialInvestment=10000.0
  ```

  Response:
  ```json
  {
    "annualizedReturn": 0.12883
  }
  ```

Here's the updated **P&L Calculation** section for your README, styled to match the existing structure with `*`, `#`, and `code` formatting:

### P&L Calculation

Manage P&L calculations through REST API endpoints. The endpoints allow users to configure P&L thresholds, retrieve current P&L status, and fetch historical P&L data.

#### *Set/Update P&L Threshold*
- **Endpoint**: `POST /set-pnl-threshold`
- **Description**: Allows users to set or update the P&L threshold. Providing a threshold of `0` cancels any existing threshold.

**Example:**
```sh
curl -X POST http://localhost:8000/set-pnl-threshold \
     -H "Content-Type: application/json" \
     -d '{
          "threshold": 1000.0
        }'
```

**Response:**
```json
{
  "status": "Threshold updated successfully"
}
```

#### *Retrieve Current P&L Status*
- **Endpoint**: `GET /current-pnl`
- **Description**: Fetches the current P&L status, including the current P&L, threshold (if any), and whether the threshold has been reached.

**Example:**
```sh
curl http://localhost:8000/current-pnl
```

**Response:**
```json
{
  "currentPNL": 0.0,
  "threshold": {
    "fields": [
      1000.0
    ]
  },
  "thresholdReached": false
}
```

#### *Retrieve Historical P&L*
- **Endpoint**: `GET /pnl/history`
- **Description**: Retrieves historical P&L values by providing a start and end date.

**Example:**
```sh
curl http://localhost:8000/historical-pnl?startDate=2023-01-01&endDate=2024-12-31
```

**Response:**
```json
{
  "totalPNL": 5.425000000
}
```

### Order Management
Emit but/sell orders to corresponding crypto currency exchange. \

Source file: 
```
/Services/OrderRepository.fs 
/Services/TransactionRepository.fs
/Infrastructure/OrderManagement.fs
```

- **Retrieve Current P&L Status**
  - **Endpoint**: `GET /pnl/status`
  - **Description**: Fetches the current P&L status, including the current P&L, threshold (if any), and whether the threshold has been reached.

  Example:
  ```sh
  curl http://localhost:8000/pnl/status
  ```

  Response:
  ```json
  {
    "currentPNL": 1500.0,
    "threshold": 1000.0,
    "thresholdReached": true
  }
  ```

- **Retrieve Historical P&L**
  - **Endpoint**: `GET /pnl/history`
  - **Description**: Retrieves historical P&L values by providing a start and end date.

  Example:
  ```sh
  curl http://localhost:8000/pnl/history?startDate=2023-01-01&endDate=2023-12-31
  ```

  Response:
  ```json
  {
    "historicalPNL": 5000.0
  }
  ```

## Performance Optimization and Testing
### Performance Testing
#### Historical Arbitrage Analysis Time Performance
For this performance test, our first checkpoint is called right before the historical arbitrage analysis is performed.
The second checkpoint is called right after the analysis is completed. These two checkpoint wrap up the `let historicalPairs = performHistoricalAnalysis()` function call
where the time difference between these two checkpoints is calculated to determine the time taken for the historical arbitrage analysis. 
For this analysis, our average time taken for the historical arbitrage analysis is around 2 seconds.

### Cross-Traded Currencies Identification Time
For this performance test, we have added two checkpoints around the `identifyCrossTradedPairs` call. 
The time difference between these two checkpoints is calculated to determine the time taken for identifying cross-traded currencies. The average time from our tests is around 1 second.


### Time to First Order
For this performance test, our first checkpoint is called right after our api endpoint `/start-trading` is being called.
The second checkpoint is called right before the first order is being placed to exchange.
These two checkpoints wrap up the functionality including history performance analysis, get cross traded pairs from database,
track the pairs using websocket connectioned to the polygon, and the evaluation of a legit arbitrage opportunity.
The time difference between these two checkpoints is calculated to determine the time taken for the first order to be placed.
For this analysis, our average time taken for the first order to be placed is around 7 - 8 seconds.

### Performance Optimization
For the performance optimization, we have implemented the following strategies:
- find all the possible parallelism in the code and make sure that the code is running in parallel as much as possible.
- Use error tunning to retrieve errors as quickly as possible.
- parallelize the io operations and data calculations

From our analysis of the codebase, we found out there are two possible optimization we can do to improve the performance
of the application by using parallelism. We firstly parallelize the `connectionTasks` function to make sure that the server
can subscribe and track multiple pairs at the same time. This will help to reduce the time taken to track pairs sequentially,
and possibly leading to missing out on some arbitrage opportunities. Secondly, we parallelize the `emitOrder` function to make
sure that the server can place multiple orders at the same time. This will help to reduce the time as we are forced to emit
at least two orders (buy and sell) for each arbitrage opportunity.

We also optimized our error handing process to make sure that we can retrieve errors as quickly as possible. By using error handling,
the error will stop at a stage of the pipe such that it will not be propagated to the next stage of the pipe.

### Optimization Result
According to our performance testing, we have been able to reduce the time taken for the first time to order from 9 - 9.5 seconds to 7 - 8 seconds.

## Unit Testing

Unit Testing is being done using a separate F# project `ArbitrageGainerTest` with the NUnit Library.

### How to use it

Prepare the package installed with the following version:
```xml
<ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="NUnit.Analyzers" Version="4.3.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
</ItemGroup>
```

Use the following command to run the `ArbitrageGainerTest` unit testing framework:

```sh
dotnet test
```

### Test Suite Explanation

#### AnnualizedReturnCalcTest
This test suite validates the implementation of the annualized return metric calculation. Key aspects covered include:

- **Edge Cases**: Handles negative or zero values for parameters such as duration of years, cumulative P&L, and initial investment.
- **Error Management**: Ensures appropriate errors are raised when invalid input parameters are provided.
- **Correctness**: Validates the annualized return calculation against expected results for both complete and fractional years.

---

#### HistoryArbitrageOpportunityTest
This test suite ensures the historical arbitrage opportunity calculations are efficient and accurate. Key aspects covered include:

- **Bucket Management**: Validates that the algorithm correctly groups trades into buckets based on criteria within a 5ms execution time.
- **Maximal Pair Recognition**: Confirms that the most profitable pair is accurately identified from each bucket.
- **Profit Filtering**: Ensures only opportunities with more than $0.01 profit are included in the results.

---

#### PNLCalculationTest
This test suite validates the correctness and robustness of the Profit and Loss (P&L) calculation functionalities. Key aspects include:

- **Threshold Configuration and Validation**:
  - Ensures that setting a positive P&L threshold updates the system correctly.
  - Confirms that setting a threshold of `0` cancels the existing threshold.
  - Verifies that negative thresholds are rejected with appropriate error messages.

- **P&L Updates**:
  - Validates that cumulative P&L is updated accurately as trades are completed.
  - Ensures the system responds correctly when thresholds are reached, including disabling trading and resetting thresholds.

- **Historical P&L Retrieval**:
  - Tests historical P&L calculations based on user-specified date ranges.
  - Ensures that the sum of P&L for trades in the specified range matches expected values.

- **Current P&L Status Retrieval**:
  - Confirms that the current P&L status includes accurate values for cumulative P&L, threshold, and threshold-reached state.

Example Test Scenarios:
1. **Set Threshold**:
  - Input: Threshold = `1000.0`
  - Expected: Threshold set successfully.

2. **Update P&L**:
  - Input: Additional P&L = `500.0`
  - Expected: Cumulative P&L updated correctly.

3. **Threshold Trigger**:
  - Input: Cumulative P&L exceeds the set threshold.
  - Expected: Trading is disabled, threshold reset, and appropriate notification behavior simulated.

4. **Historical P&L**:
  - Input: Start Date = `2023-01-01`, End Date = `2023-12-31`
  - Expected: Total historical P&L matches the sum of relevant trades.

These unit tests ensure robust, reliable functionality for P&L calculation and threshold management.

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
