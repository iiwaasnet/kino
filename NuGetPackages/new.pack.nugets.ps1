function Get-NuGetDependencies([string]$projectFile, [string]$basePath)
{
    $nugetRefs = @()
    $projectFile = [IO.Path]::GetFullPath([IO.Path]::Combine($basePath,$projectFile))

    [xml] $projectXml = Get-Content ($projectFile)

    $tmp = $projectXml.SelectSingleNode('//ItemGroup/PackageReference')
    Write-Host $tmp.InnerText

    foreach ($package in $projectXml.Project.ItemGroup.PackageReference)
    {
        if ($package.Include)
        {
            $nugetRef = New-Object System.Object
            $nugetRef | Add-Member -type NoteProperty -name Name -value $package.Include
            $nugetRef | Add-Member -type NoteProperty -name Version -value $package.Version

            $nugetRefs += $nugetRef
        }
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

function Get-NuGetTargetPlatform([string]$fw)
{
    if ($fw -eq 'netstandard2.0')
    {
        return '.NETStandard2.0'
    }
    if ($fw -eq 'net47')
    {
        return '.NETFramework4.7'
    }

    throw 'Framework is not supported: ' + $fw
}

function Copy-ProjectNuGetAttributes([xml]$projectXml, [System.Xml.XmlNode]$meta, [System.Xml.XmlDocument]$doc)
{
    foreach ($element in (Get-ProjectNuGetAttributesMap))
    {
        $node = $doc.CreateElement($element.Destination, (Get-XmlNs))
        $node.InnerText = $projectXml.SelectSingleNode('//PropertyGroup/' + $element.Source).InnerText
        $meta.AppendChild($node)
    }
}

function Get-ProjectNuGetAttributesMap()
{
    $attrs = @()
    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'Copyright'
    $attr | Add-Member -type NoteProperty -name Destination -value 'copyright'
    $attrs += $attr

    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'PackageLicenseUrl'
    $attr | Add-Member -type NoteProperty -name Destination -value 'licenseUrl'
    $attrs += $attr

    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'PackageProjectUrl'
    $attr | Add-Member -type NoteProperty -name Destination -value 'projectUrl'
    $attrs += $attr

    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'PackageIconUrl'
    $attr | Add-Member -type NoteProperty -name Destination -value 'iconUrl'
    $attrs += $attr

    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'Description'
    $attr | Add-Member -type NoteProperty -name Destination -value 'description'
    $attrs += $attr

    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'PackageTags'
    $attr | Add-Member -type NoteProperty -name Destination -value 'tags'
    $attrs += $attr

    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'Authors'
    $attr | Add-Member -type NoteProperty -name Destination -value 'authors'
    $attrs += $attr

    $attr = New-Object System.Object
    $attr | Add-Member -type NoteProperty -name Source -value 'Authors'
    $attr | Add-Member -type NoteProperty -name Destination -value 'owners'
    $attrs += $attr

    return $attrs
}

function Get-XmlNs()
{
    return 'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd'
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
        $root = $nuSpec.CreateElement('package', (Get-XmlNs))
        $nuSpec.AppendChild($root)
        $meta = $nuSpec.CreateElement('metadata', (Get-XmlNs))        
        $root.AppendChild($meta)
        (Copy-ProjectNuGetAttributes $projectXml $meta $nuSpec)
        $dependencies = $nuSpec.CreateElement('dependencies', (Get-XmlNs))
        $meta.AppendChild($dependencies)
        foreach ($fw in $frameworks)
        {
            Write-Host $fw -ForegroundColor Yellow
            $grp = $nuSpec.CreateElement('group', (Get-XmlNs))
            $grp.SetAttribute('targetFramework', (Get-NuGetTargetPlatform $fw))
            foreach ($nugetRef in $nugetRefs)
            {
                $ref = $nuSpec.CreateElement('dependency', (Get-XmlNs))
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