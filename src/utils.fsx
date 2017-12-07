open System.Net
open System.IO
open System.IO.Compression
open System.Threading.Tasks
open System.Diagnostics

#r "System.IO.Compression"
#r "System.IO.Compression.FileSystem"

type LogLevel = Info | Error

let log (level:LogLevel) msg =
    let write (pre:string) (msg:string) =
        System.Console.WriteLine("{0}: {1}", pre, msg)

    match level with
        | Info -> write "info" msg
        | Error -> write "error" msg

let dirExists dir = Directory.Exists(dir)
let createDir dir = Directory.CreateDirectory(dir) |> ignore

let ensureExists dir = 
    if not (dirExists dir) then createDir dir

let rmDir dir = 
    if (dirExists dir) then Directory.Delete(dir, true)

let extractZip zip outDir = 
    ensureExists outDir
    ZipFile.ExtractToDirectory(zip, outDir)
    outDir

let download (url : string) outDir outName = 
    let client = new WebClient()
    let outPath = Path.Combine(outDir, outName)
    ensureExists outDir
    client.DownloadFile(url, outPath)
    outPath

let getNssm outDir =
    let url = "http://www.nssm.cc/release/nssm-2.24.zip"
    download url outDir "nssm.zip"

let executeProc exe args = 
    log Info ("executing process: " + exe + " " + args)
    let p = Process.Start(exe,args)
    p.WaitForExit(int(10e3)) |> ignore
    let ec = p.ExitCode
    log Info ("process returned: " + ec.ToString())
    ec

let nssmInstall nssmPath (binPath:string) (svcName : string) =
    let args = System.String.Format("install {0} {1}", svcName, binPath)
    executeProc nssmPath args

let nssmRemove nssmPath (svcName : string) =
    let args = System.String.Format("remove {0} confirm",svcName)
    executeProc nssmPath args

let nssmStart nssmPath (svcName : string) =
    let args = System.String.Format("start {0}", svcName)
    executeProc nssmPath args

let nssmSet nssmPath (svcName : string) (key:string) (value:string) =
    let args = System.String.Format("set {0} {1} {2}", svcName, key, value)
    executeProc nssmPath args

let createBuildDir = fun baseDir ->
    rmDir baseDir
    ensureExists baseDir

let createTempDir baseDir =
    let p = Path.Combine(baseDir, "temp")
    ensureExists p
    p

let createPkgDir baseDir =
    let p = Path.Combine(baseDir, "pkg")
    ensureExists p
    p

let buildGrafanaPkg = fun() ->
    let baseDir =  "C:\d\grafana"
    createBuildDir baseDir
    let tempDir = createTempDir baseDir
    let pkgDir = createPkgDir baseDir

    let getGrafBins = fun() ->
        let url = "https://s3-us-west-2.amazonaws.com/grafana-releases/release/grafana-4.6.2.windows-x64.zip"
        let zip = download url tempDir "grafana.zip"
        extractZip zip pkgDir |> ignore

    let getNssmBins = fun() ->
        let zip = getNssm tempDir
        extractZip zip pkgDir |> ignore
    
    let gTask = Task.Run(getGrafBins)
    let nTask = Task.Run(getNssmBins)
    Task.WaitAll(gTask, nTask)

    rmDir tempDir

let installGrafana = fun() ->
    // default config for now

    // install as win service
    let nssmPath = @"C:\d\grafana\pkg\nssm-2.24\win64\nssm.exe"
    let binPath = @"C:\d\grafana\pkg\grafana-4.6.2\bin\grafana-server.exe"
    let svcName = "grafana"

    nssmRemove nssmPath svcName |> ignore
    nssmInstall nssmPath binPath svcName |> ignore
    nssmStart nssmPath svcName

let buildConsulPkg = fun() ->
    let baseDir =  "C:\d\consul"
    createBuildDir baseDir
    let tempDir = createTempDir baseDir
    let pkgDir = createPkgDir baseDir

    let getConsulBins = fun() ->
        let url = "https://releases.hashicorp.com/consul/1.0.1/consul_1.0.1_windows_amd64.zip"
        let zip = download url tempDir "consul.zip"
        extractZip zip pkgDir |> ignore
    
    let getNssmBins = fun() ->
        let zip = getNssm tempDir
        extractZip zip pkgDir |> ignore
    
    let consulDl = Task.Run(getConsulBins)
    let nssmDl = Task.Run(getNssmBins)
    Task.WaitAll(consulDl,nssmDl)

let installConsul = fun() -> 
    // default config for now

    // install as win service
    let nssmPath = @"C:\d\consul\pkg\nssm-2.24\win64\nssm.exe"
    let binPath = @"C:\d\consul\pkg\consul.exe"
    let svcName = "consul4"

    nssmRemove nssmPath svcName |> ignore
    nssmInstall nssmPath binPath svcName |> ignore
    nssmSet nssmPath svcName "AppParameters" "agent -dev --bind=127.0.0.1" |> ignore
    nssmSet nssmPath svcName "AppStdout" "C:\d\consul\pkg\consul.log" |> ignore
    nssmStart nssmPath svcName