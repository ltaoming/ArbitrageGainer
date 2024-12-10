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
   ```bash
   cd ..
   dotnet new console -lang "F#" -o ReduceProject
   cd ReduceProject
   dotnet add package FSharp.Data --version 4.2.7
   ```

2. **Compile and Publish the MapProject and ReduceProject**
publish the projects to publish folder, you will expect to get some library files and a .dll file under each published project folder:

   **Map Project:**
   ```bash
   dotnet publish -c Release -o ~/publish/MapProject/
   ```
   **Reduce Project:**
   ```bash
   dotnet publish -c Release -o ~/publish/ReduceProject/
   ```

3. **Verify Published dll file is working**
You can run the two dll files in a pipe to on bash to check if the program returns the desired results.
   ```bash
   dotnet ~/publish/MapProject/MapProject.dll < historicalData.txt \
   | dotnet ~/publish/ReduceProject/ReduceProject.dll > final_output.txt
   ```
   verify the result inside the `final_output.txt` file

4. **Set Up Hadoop Distributed Environment**
follow the instruction from [this blog](https://medium.com/codex/running-a-multi-node-hadoop-cluster-257068e5f276) to set up the Hadoop Distributed System.

   1. start the Hadoop HDFS and Yarn
      ```bash
      [hduser@master ~]# /opt/hadoop/sbin/start-dfs.sh
      [hduser@master ~]# /opt/hadoop/sbin/start-yarn.sh
      ```
   2. Inside the master node, upload the `historicalData.txt` to HDFS.
      ```bash
      [hduser@master ~]# hdfs dfs -put historicalData.txt hadoop-hdfs/
      ```

5. **Run Hadoop Pipeline**
Run the hadoop framework with the mapreduce program
   ```bash
   [hduser@master ~]# mapred streaming \
      -input ./hadoop-hdfs/historicalData.txt \
      -output ./output \
      -mapper "dotnet ./publish/MapProject/MapProject.dll" \
      -reducer "dotnet ./publish/ReduceProject/ReduceProject.dll" \
      -file publish
   ```

## Performance Evaluation
by using `time` in front of each command, we can get the execution time for each command.
From One single Node execution, we get an average time of 46s to finish the history arbitrage opportunities analysis.
However, using the 3 worker node hadoop system allow us to boost time to 26s.
