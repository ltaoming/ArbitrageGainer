# Arbitrage Gainer - Hadoop Streaming Setup

This guide explains how to run the provided map and reduce logic on Hadoop Streaming. We start from two `.fs` files (one for the mapper and one for the reducer), compile them into `.dll` executables, and then use these `.dll` files with Hadoop Streaming to process data stored in HDFS.

## Overview

We have two `.fs` files:

1. **Map file (`map.fs`)**:
   - Reads each input line (which may contain multiple JSON objects).
   - Extracts and parses all JSON objects from the line.
   - For each valid JSON object, computes a "bucket" value and outputs a record in the form of `bucket\tjson`.

2. **Reduce file (`reduce.fs`)**:
   - Takes the mapper's output (`bucket\tjson` lines) as input.
   - Groups records by their keys (buckets), parses JSON, and checks for arbitrage opportunities across multiple `X` values within the same pair.
   - Aggregates these results and outputs a final summarized list, such as:
     ```
     ["DOT-USD, 3 opportunities";
      "MKR-USD, 33 opportunities";
      "SOL-USD, 3 opportunities";
      "FET-USD, 5 opportunities"]
     ```

## Steps to Run

1. **Set Up F# Console Projects**  
   For each `.fs` file, create a separate F# console project:

   **Map Project:**
   ```bash
   dotnet new console -lang "F#" -o MapProject
   cd MapProject
   dotnet add package FSharp.Data --version 4.2.7
    ```
   **Reduce Project:**
    ```
    bash
    cd ..
    dotnet new console -lang "F#" -o ReduceProject
    cd ReduceProject
    dotnet add package FSharp.Data --version 4.2.7
    ```
    