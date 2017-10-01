function Get-NuGetDependencies([string]$projectFile, [string]$basePath)
{
    $nugetRefs = @()
    $projectFile = [IO.Path]::GetFullPath([IO.Path]::Combine($basePath,$projectFile))

    [xml] $projectXml = Get-Content ($projectFile)
    foreach ($package in $projectXml.Project.ItemGroup.PackageReference)
    {
        $nugetRef = New-Object System.Object
        $nugetRef | Add-Member -type NoteProperty -name Name -value $package.Include
        $nugetRef | Add-Member -type NoteProperty -name Version -value $package.Version

        $nugetRefs += $nugetRef
    }

    foreach ($ref in $projectXml.Project.ItemGroup.ProjectReference.Include)
    {
        $basePath = (Split-Path -parent $projectFile)        
        $nugetRefs += (Get-NuGetDependencies $ref $basePath)
    }

    return $nugetRefs
}

function Get-ProjectDependencies([string]$projectFile)
{
    $projectRefs = @()
    [xml] $projectXml = Get-Content $projectFile
    foreach ($ref in $projectXml.Project.ItemGroup.ProjectReference.Include)
    {
        $projectRefs += $ref
    }

    return $projectRefs
}

function Get-TargetFrameworks([xml]$projectXml)
{
    try
    {
        return $projectXml.Project.PropertyGroup.TargetFrameworks -split ';'
    }
    catch
    {
        return @($projectXml.Project.PropertyGroup.TargetFramework)
    }
}

function Should-BuildNuGetPackage([xml]$projectXml)
{
    return $projectXml.SelectSingleNode('//PropertyGroup/BuildNuGetPackage')
}

$solutionFile = Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".sln"}

foreach ($projectFile in Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".csproj"})
{    

    #Get file contents
    [xml] $projectXml = Get-Content $projectFile.FullName
    if ((Should-BuildNuGetPackage $projectXml))
    {        
        #Read supported .NET Frameworks
        $frameworks = (Get-TargetFrameworks $projectXml)
    
        $nugetRefs = @()
        $projectRefs = @()

        #Read Project references
        $projectRefs = Get-ProjectDependencies($projectFile.FullName)
        #Read referenced NuGet packages
        $basePath = (Split-Path -parent $projectFile.FullName)
        $nugetRefs += (Get-NuGetDependencies $projectFile.FullName '.')        
        $nugetRefs = $nugetRefs |Sort-Object Name -Unique        

        Write-Host $projectFile.FullName -ForegroundColor Yellow
        foreach ($nugetRef in $nugetRefs)
        {
            Write-Host $nugetRef.Name $nugetRef.Version -ForegroundColor Green           
        }

        foreach ($projectRef in $projectRefs)
        {
            Write-Host $projectRef -ForegroundColor Green           
        }

        #Build NuSpec file
        [xml]$nuSpec = New-Object System.Xml.XmlDocument
        $nuSpec.AppendChild($nuSpec.CreateXmlDeclaration("1.0","UTF-8",$null))
        $root = $nuSpec.CreateNode('element', 'package', $null)
        $meta = $nuSpec.CreateNode('element', 'metadata', $root)
        $meta.SetAttribute('xmlns', 'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd')
        $nuSpec.AppendChild($meta)
        $dependencies = $nuSpec.CreateNode('element', 'dependencies', $null)
        $meta.AppendChild($dependencies)
        foreach ($fw in $frameworks)
        {
            Write-Host $fw -ForegroundColor Yellow
            $grp = $nuSpec.CreateNode('element', 'group', $null)
            $grp.SetAttribute('targetFramework', $fw)
            foreach ($nugetRef in $nugetRefs)
            {
                $ref = $nuSpec.CreateNode('element', 'dependency', $null)
                $ref.SetAttribute('id', $nugetRef.Name)
                $ref.SetAttribute('version', $nugetRef.Version)
                $grp.AppendChild($ref)
            }
            $dependencies.AppendChild($grp)
        }

        Write-Host $nuSpec.InnerXml
    
    }
    #foreach ($child in $nuspecXml.package.files.file)
    #{
    #    $nuspecXml.package.files.RemoveChild($child)
    #}

    #$nuspecXml.Save($nuSpecFile.FullName)
}