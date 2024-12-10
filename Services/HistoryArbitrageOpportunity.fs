module ArbitrageGainer.HistoryArbitrageOpportunity

open System.IO
open Logging.Logger
open System
open Microsoft.Spark.Sql

let logger = createLogger

let calculateHistoryArbitrageOpportunity () =
    let baseDir = System.AppContext.BaseDirectory
    let filePath = Path.Combine(baseDir, "historicalData.txt")

    let spark: SparkSession =
        SparkSession.Builder()
                    .AppName("ArbitrageCalculation")
                    .Master("local[*]")
                    .GetOrCreate()

    // Load DataFrame
    let df: DataFrame =
        spark.Read()
             .Option("multiline","true")
             .Json(filePath)

    // Add bucket column
    let dfWithBucket: DataFrame =
        df.WithColumn("bucket", (Functions.Col("t") / Functions.Lit(5L)))

    // Group by bucket, pair and aggregate
    let grouped: DataFrame =
        dfWithBucket.GroupBy("bucket","pair")
                    .Agg(
                        (Functions.CollectList(
                            Functions.Struct(
                                Functions.Col("x"),
                                Functions.Col("bp"),
                                Functions.Col("ap")
                            )
                         )).Alias("exchanges")
                    )

    // distinct_exchanges calculation using an Expr
    // Using a SQL expression string to get the distinct exchange count
    let withDistinctExchanges: DataFrame =
        grouped.WithColumn(
            "distinct_exchanges",
            Functions.Expr("size(array_distinct(transform(exchanges, e -> e.x)))")
        )

    // Filter rows where distinct_exchanges > 1
    let filtered: DataFrame =
        withDistinctExchanges.Filter("distinct_exchanges > 1")

    // Arbitrage check using Expr
    let withArbitrageFlag: DataFrame =
        filtered.WithColumn(
            "arbitrage_found",
            Functions.Expr(
                "EXISTS(TRANSFORM(exchanges, e1 -> TRANSFORM(exchanges, e2 -> ((e1.bp - e2.ap) > 0.01 OR (e2.bp - e1.ap) > 0.01)), arr -> EXISTS(arr, x -> x)))"
            )
        )

    let withArbitrageInt: DataFrame =
        withArbitrageFlag.WithColumn(
            "arbitrage_count",
            Functions.When(Functions.Col("arbitrage_found"), Functions.Lit(1))
                                           .Otherwise(Functions.Lit(0))
        )

    let resultDf: DataFrame =
        withArbitrageInt.GroupBy("pair")
                        .Agg(Functions.Sum(Functions.Col("arbitrage_count"))
                                .Alias("num_opportunities"))

    let rows = resultDf.Collect()

    rows
    |> Seq.map (fun r -> sprintf "%s, %d opportunities" (r.GetAs<string>("pair")) (r.GetAs<int>("num_opportunities")))