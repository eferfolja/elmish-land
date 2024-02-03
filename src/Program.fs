﻿module ElmishLand.Program

open System
open ElmishLand.Base
open ElmishLand.Init
open ElmishLand.Log
open ElmishLand.Server
open ElmishLand.Build
open ElmishLand.AddPage
open ElmishLand.AppError
open Orsak

let (|NotFlag|_|) (x: string) =
    if x.StartsWith("--") then None else Some x

let run argv =
    eff {
        let! log = Log().Get()

        return!
            match List.ofArray argv with
            | "init" :: _ -> init (AbsoluteProjectDir.create argv)
            | "server" :: _ -> server (AbsoluteProjectDir.create argv)
            | "build" :: _ -> build (AbsoluteProjectDir.create argv)
            | "add" :: "page" :: NotFlag url :: _ -> addPage (AbsoluteProjectDir.create argv) url
            | "routes" :: _ -> eff { return () }
            | _ ->
                $"""
    %s{getWelcomeTitle ()}
%s{help id}
"""
                |> log.Info

                eff { return () }

    }

type ConsoleLogger(memberName, path, line) =
    let logger = Logger(memberName, path, line)

    interface ILog with
        member _.Debug(message, [<ParamArray>] args: obj array) =
            if logger.IsVerbose then
                Console.ForegroundColor <- ConsoleColor.Gray
                logger.WriteLine Console.Out.WriteLine message args
                Console.ResetColor()

        member _.Info(message, [<ParamArray>] args: obj array) =
            Console.ForegroundColor <- ConsoleColor.Gray
            logger.WriteLine Console.Out.WriteLine message args
            Console.ResetColor()

        member _.Error(message, [<ParamArray>] args: obj array) =
            Console.ForegroundColor <- ConsoleColor.Red
            logger.WriteLine Console.Error.WriteLine message args
            Console.ResetColor()

let handleAppResult (log: ILog) onSuccess =
    function
    | Ok _ ->
        onSuccess ()
        0
    | Error e ->
        match e with
        | ProcessError(error) -> log.Error error
        | FsProjNotFound -> log.Error "No F# project file found."
        | MultipleFsProjFound -> log.Error "Multiple F# project files found."
        | FsProjValidationError errors ->
            for error in errors do
                log.Error error

        | DotnetSdkNotFound ->
            log.Error
                $"""You need to install .NET Core SDK version %s{DotnetSdkVersion.asString minimumRequiredDotnetSdk} or above
https://dotnet.microsoft.com/
"""

        | NodeNotFound ->
            log.Error
                $"""You need to install Node.js version %s{minimumRequiredNode.ToString()} or above
https://nodejs.org/
"""

        | DepsMissingFromPaket ->
            let dependenies =
                nugetDependencies
                |> Set.map (fun (name, version) -> let v = version.Replace("--version ", "") in $"nuget %s{name} %s{v}")
                |> String.concat "\n"

            log.Error
                $"""The following nuget dependencies are missing from paket.dependencies:

%s{dependenies}
"""
        | PaketNotInstalled ->
            log.Error
                """Found paket.dependencies but paket is not installed. Please install paket.
"""

        -1


[<EntryPoint>]
let main argv =
    (task {
        let! result =
            run argv
            |> Effect.run
                { new ILogProvider with
                    member _.GetLogger(memberName, path, line) = ConsoleLogger(memberName, path, line)
                }

        return handleAppResult (ConsoleLogger("", "", 0)) ignore result
    })
        .Result
