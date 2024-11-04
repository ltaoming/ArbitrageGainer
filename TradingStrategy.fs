module TradingStrategy

open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors
open System.Collections.Concurrent
open System.Text
open Newtonsoft.Json

// 定义交易策略参数的数据结构
type TradingStrategy = {
    NumberOfCurrencies: int
    MinimalPriceSpread: float
    MinimalProfit: float
    MaxTransactionValue: float
    MaxTradingValue: float
}

// 创建一个可变字典来存储策略参数（为了简化使用 ConcurrentDictionary）
let strategyParameters: ConcurrentDictionary<string,TradingStrategy> = ConcurrentDictionary<string, TradingStrategy>()

// 获取交易策略参数
let getStrategy (ctx: HttpContext) =
    match strategyParameters.TryGetValue("default") with
    | true, strategy -> OK (sprintf "Strategy: %A" strategy) ctx
    | _ -> NOT_FOUND "Strategy not found" ctx

// 更新交易策略参数
let updateStrategy (ctx: HttpContext) =
    async {
        // 从请求中获取原始表单数据
        let body = ctx.request.rawForm |> Encoding.UTF8.GetString
        try
            // 使用 Newtonsoft.Json 反序列化
            let newStrategy = JsonConvert.DeserializeObject<TradingStrategy>(body)
            strategyParameters.["default"] <- newStrategy
            return! OK "Strategy updated successfully!" ctx
        with
        | ex -> return! BAD_REQUEST (sprintf "Error: %s" ex.Message) ctx
    }

// 路由配置
let app =
    choose [
        GET >=> path "/strategy" >=> getStrategy
        POST >=> path "/update" >=> request updateStrategy
    ]
