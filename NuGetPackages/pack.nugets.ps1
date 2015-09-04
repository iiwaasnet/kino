foreach ($file in Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".nuspec"})
{
	nuget pack $file.FullName -Build -NonInteractive
}