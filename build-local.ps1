param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build\app'))
$ErrorActionPreference = 'Stop'
# Offline build using Roslyn shipped with PowerShell 7 and Windows .NET Framework 4.8.
# No installer, package restore, network, elevation, or machine configuration changes.
if (-not (Test-Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll'))) {
    throw 'Use PowerShell 7 with its bundled Roslyn assemblies, or use build.ps1 with a .NET SDK.'
}
Add-Type -AssemblyName Microsoft.CodeAnalysis
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$compiler = @'
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
public static class OfflineBuild
{
    public static void Build(string[] sources,string[] references,string output,string main,string manifest,string icon)
    {
        var trees=sources.Select(p=>CSharpSyntaxTree.ParseText(File.ReadAllText(p),path:p,encoding:Encoding.UTF8));
        var refs=references.Select(p=>MetadataReference.CreateFromFile(p));
        var options=new CSharpCompilationOptions(main.StartsWith("PocketGrid.")?OutputKind.WindowsApplication:OutputKind.ConsoleApplication)
            .WithPlatform(Platform.X64).WithOptimizationLevel(OptimizationLevel.Release).WithDeterministic(true).WithMainTypeName(main);
        var compilation=CSharpCompilation.Create(Path.GetFileNameWithoutExtension(output),trees,refs,options);
        using(var stream=File.Create(output))
        using(var man=File.OpenRead(manifest))
        using(var ico=File.OpenRead(icon))
        using(var resources=compilation.CreateDefaultWin32Resources(true,false,man,ico))
        {
            var result=compilation.Emit(stream,win32Resources:resources);
            foreach(var diagnostic in result.Diagnostics.Where(d=>d.Severity>=DiagnosticSeverity.Warning))Console.WriteLine(diagnostic.ToString());
            if(!result.Success)throw new Exception("Compilation failed.");
        }
    }
}
'@
Add-Type -TypeDefinition $compiler -CompilerOptions /nowarn:1701 -ReferencedAssemblies @(
    (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll'),
    (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll'),
    (Join-Path $PSHOME 'System.Collections.Immutable.dll'),
    (Join-Path $PSHOME 'ref\System.Runtime.dll'),
    (Join-Path $PSHOME 'ref\System.Linq.dll'),
    (Join-Path $PSHOME 'ref\System.Collections.dll'),
    (Join-Path $PSHOME 'ref\System.Console.dll'))
$fx=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { Join-Path $fx $_ }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$src=@(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
[OfflineBuild]::Build($src,$refs,(Join-Path $OutputDirectory 'M-Capsule.exe'),'PocketGrid.Program',(Join-Path $PSScriptRoot 'src\app.manifest'),(Join-Path $PSScriptRoot 'src\app.ico'))
Copy-Item (Join-Path $PSScriptRoot 'src\app.config') (Join-Path $OutputDirectory 'M-Capsule.exe.config')
foreach($test in @('CoreTests','PortableTests','HeadlessPortableTests','GridSnapTests','StartupTests','GlassBackdropTests','ReferenceTests','CustomIconTests','UltimateOrganizerTests')) {
    $file=Join-Path $PSScriptRoot "tests\$test.cs"
    if(Test-Path $file) {
        [OfflineBuild]::Build(($src+@($file)),$refs,(Join-Path $OutputDirectory "$test.exe"),$test,(Join-Path $PSScriptRoot 'src\app.manifest'),(Join-Path $PSScriptRoot 'src\app.ico'))
        Copy-Item (Join-Path $PSScriptRoot 'src\app.config') (Join-Path $OutputDirectory "$test.exe.config")
    }
}
Write-Output "Built $OutputDirectory"


