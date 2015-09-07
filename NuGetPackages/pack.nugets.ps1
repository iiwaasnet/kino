$MsBuild = "C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe"

foreach ($NugetSpec in Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".nuspec"})
{
	$ProjectFile = Get-ChildItem $NugetSpec.DirectoryName | Where-Object {$_.Extension -eq ".csproj"}

	$BuildArgs = @{
		FilePath = $MsBuild
		ArgumentList = $ProjectFile.FullName,  "/p:Configuration=Release /t:Rebuild"
		Wait = $true
	}
	
	Start-Process @BuildArgs
	nuget pack $NugetSpec.FullName -Build -NonInteractive
}