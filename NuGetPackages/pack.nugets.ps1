$MsBuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\msbuild.exe"

$SolutionFile = Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".sln"}
nuget restore $SolutionFile.FullName

foreach ($NugetSpec in Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".nuspec"})
{
	$ProjectFile = Get-ChildItem $NugetSpec.DirectoryName | Where-Object {$_.Extension -eq ".csproj"}

	$BuildArgs = @{
		FilePath = $MsBuild
		ArgumentList = $ProjectFile.FullName,  "/p:Configuration=Release /t:Rebuild"
		Wait = $true
	}
	
	Start-Process @BuildArgs -NoNewWindow
	#nuget pack $ProjectFile.FullName -BasePath $NugetSpec.DirectoryName -Build -Prop Configuration=Release -Prop FilePath=$MsBuild -NonInteractive -IncludeReferencedProjects
	nuget pack $ProjectFile.FullName -BasePath $NugetSpec.DirectoryName -Prop Configuration=Release -NonInteractive -IncludeReferencedProjects
}