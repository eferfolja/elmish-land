module ElmishLand.Base

open System
open System.Diagnostics
open System.Text
open System.IO
open System.Reflection
open System.Threading
open System.Threading.Tasks

module String =
    let asLines (s: string) =
        s.Split(Environment.NewLine)

let appTitle = "Elmish Land"
let cliName = "elmish-land"
let version = "0.0.1"
let welcomeTitle = $"Welcome to %s{appTitle}! (v%s{version})"
let help eachLine =
    $"""
    %s{welcomeTitle}
    %s{String.init welcomeTitle.Length (fun _ -> "-")}

    Here are the available commands:

    %s{cliName} init <project-dir> ............. create a new project
    %s{cliName} server <working-directory> ... run a local dev server
    %s{cliName} build ................. build your app for production
    %s{cliName} add page <url> ....................... add a new page
    %s{cliName} add layout <name> .................. add a new layout
    %s{cliName} routes .................. list all routes in your app

    Want to learn more? Visit https://github.com/reaptor/elmish-land
    """
    |> fun s -> s.Split(Environment.NewLine)
    |> Array.map eachLine
    |> String.concat Environment.NewLine

let disclaimer = $"""// THIS FILE IS AUTO GENERATED. ALL CONTENTS WILL BE OVERWRITTEN ON BUILD
%s{help (fun s -> $"// %s{s}")}
// THIS FILE IS AUTO GENERATED. ALL CONTENTS WILL BE OVERWRITTEN ON BUILD"""

let getTemplatesDir =
    Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "src", "templates")

let getProjectDir (projectName: string) =
    Path.Combine(Environment.CurrentDirectory, projectName)

let getProjectPath workingDirectory =
    let projectDir =
        match workingDirectory with
        | Some workingDirectory' -> Path.Combine(Environment.CurrentDirectory, workingDirectory')
        | None -> Environment.CurrentDirectory

    Path.ChangeExtension(Path.Combine(projectDir, DirectoryInfo(projectDir).Name), "fsproj")

let commandHeader s =
    let header = $"%s{appTitle} (v%s{version}) %s{s}"

    $"""
    %s{header}
    %s{String.init header.Length (fun _ -> "-")}
"""

let private runProcessInternal
    (workingDirectory: string option)
    (command: string)
    (args: string array)
    (cancellation: CancellationToken)
    =
    let p =
        ProcessStartInfo(
            command,
            args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = defaultArg workingDirectory Environment.CurrentDirectory
        )
        |> Process.Start

    p.OutputDataReceived.Add(fun args -> Console.WriteLine(args.Data))

    p.ErrorDataReceived.Add(fun args ->
        Console.ForegroundColor <- ConsoleColor.Red
        Console.WriteLine(args.Data)
        Console.ResetColor())

    p.BeginOutputReadLine()
    p.BeginErrorReadLine()

    while not cancellation.IsCancellationRequested && not p.HasExited do
        Thread.Sleep(100)

    if cancellation.IsCancellationRequested then
        p.Kill(true)
        p.Dispose()
        -1
    else
        p.ExitCode


let rec runProcess
    (workingDirectory: string option)
    (command: string)
    (args: string array)
    cancel
    (completed: unit -> unit)
    =
    let exitCode = runProcessInternal workingDirectory command args cancel

    if exitCode = 0 then
        completed ()

    exitCode

let runProcesses
    (processes: (string option * string * string array * CancellationToken) list)
    (completed: unit -> unit)
    =
    let exitCode =
        processes
        |> List.fold
            (fun previousExitCode (workingDirectory, command, args, cancellation) ->
                if previousExitCode = 0 then
                    runProcessInternal workingDirectory command args cancellation
                else
                    previousExitCode)
            0

    if exitCode = 0 then
        completed ()

    exitCode
