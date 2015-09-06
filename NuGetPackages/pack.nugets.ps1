$MsBuild = $env:SystemRoot + "\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"

foreach ($NugetSpec in Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".nuspec"})
{
	$ProjectFile = Get-ChildItem $NugetSpec.DirectoryName | Where-Object {$_.Extension -eq ".csproj"}
	$BuildArgs = @{
		FilePath = $MsBuild
		ArgumentList = $ProjectFile.FullName,  "/p:Configuration=Release"
		Wait = $true
	}
	
	## Start-Process @BuildArgs -NoNewWindow
	nuget pack $NugetSpec.FullName -Build -NonInteractive
}