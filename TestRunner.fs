namespace ArbitrageGainer.Tests

module TestRunner =
    
    open ArbitrageGainer.Services.HistoryArbitrageOpportunity
    
    [<EntryPoint>]
    let main argv =
        // 运行测试函数
        runTest ()
        0
