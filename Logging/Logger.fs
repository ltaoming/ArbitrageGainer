namespace Logging
    open System.IO
    open System

    module Logger = 
        // Define a function to create a logger function
        let createLogger =
            let filePath = "log.txt"
            // Logger function
            let logMessage (invokingWorkflowName:string) =
                // Prepare log entry with current date, time, and invoking workflow name
                let logEntry: string = sprintf "%s - %s called" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff")) invokingWorkflowName
                // Append log entry to the file
                File.AppendAllText(filePath, logEntry + "\n")
            
            // Return the logging function
            logMessage