$SolutionFile = Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".sln"}

foreach ($ProjectFile in Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".csproj"})
{
	if (Get-Content $ProjectFile.FullName | Select-String -Pattern "Authors")
	{
        Write-Host 'Building package for project: '$ProjectFile.FullName -foregroundcolor "green"

        $BuildArgs = @{
            FilePath = 'dotnet'
            ArgumentList =  "pack " + $ProjectFile.FullName + " -c Release -o " + $PSScriptRoot
            Wait = $true
        }	
        Start-Process @BuildArgs -NoNewWindow
	}
}