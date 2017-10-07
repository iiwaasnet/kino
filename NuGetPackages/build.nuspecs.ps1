param(
    [string]$version
)

if (!$version)
{
	#Define based on RB number
    throw '$version parameter is not provided!'
}
#================================================================================
function Get-ProjectFileContent($projectFile)
{
    return [xml] $projectXml = Get-Content ($projectFile) -Encoding UTF8
}


function Get-NuGetDependencies([string]$projectFile, [string]$basePath)
{
    $nugetRefs = @()
    $projectFile = [IO.Path]::GetFullPath([IO.Path]::Combine($basePath,$projectFile))

    [xml] $projectXml = (Get-ProjectFileContent $projectFile)

    $tmp = $projectXml.SelectSingleNode('//ItemGroup/PackageReference')

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

function Get-ProjectDependencies([string]$projectFile, [string]$basePath)
{
    $projectRefs = @()

    $projectFile = [IO.Path]::GetFullPath([IO.Path]::Combine($basePath,$projectFile))
    [xml] $projectXml = (Get-ProjectFileContent $projectFile)
    foreach ($ref in $projectXml.Project.ItemGroup.ProjectReference.Include)
    {
        if ($projectXml.Project.ItemGroup.Condition -match "(?:==['](.*)['])")
        {            
            $platform = $Matches[1]
        }        

        $projRef = New-Object System.Object
        $projRef | Add-Member -type NoteProperty -name Name -value $ref
        $projRef | Add-Member -type NoteProperty -name Platform -value $platform
        $projectRefs += $projRef
    }

    foreach ($ref in $projectXml.Project.ItemGroup.ProjectReference.Include)
    {
        $basePath = (Split-Path -parent $projectFile)        
        $projectRefs += (Get-ProjectDependencies $ref $basePath)
    }

    return $projectRefs
}

function Get-TargetFrameworks([xml]$projectXml)
{
    if ($projectXml.Project.PropertyGroup.TargetFrameworks)
    {
        return $projectXml.Project.PropertyGroup.TargetFrameworks -split ';'
    }
    else
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
	
	# add other FWs, i.e. core, net461, etc

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

function Add-PackageDependencies([xml]$nuSpec, $projectFile, $nugetRefs, $frameworks)
{
    [xml] $projectXml = (Get-ProjectFileContent $projectFile.FullName)
    $meta = $nuSpec.CreateElement('metadata', (Get-XmlNs))        
    $root.AppendChild($meta)

    $id = $nuSpec.CreateElement('id', (Get-XmlNs))
    $id.InnerText = $projectFile.BaseName
    $meta.AppendChild($id)

    $ver = $nuSpec.CreateElement('version', (Get-XmlNs))
    $ver.InnerText = $version
    $meta.AppendChild($ver)

    $title = $nuSpec.CreateElement('title', (Get-XmlNs))
    $title.InnerText = $projectFile.BaseName
    $meta.AppendChild($title)

    (Copy-ProjectNuGetAttributes $projectXml $meta $nuSpec)
    $dependencies = $nuSpec.CreateElement('dependencies', (Get-XmlNs))
    $meta.AppendChild($dependencies)
    foreach ($fw in $frameworks)
    {        
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
}

function Add-FileDependencies([xml]$nuSpec, [xml]$projectXml, $projectRefs, $frameworks)
{
    $files = $nuSpec.CreateElement('files', (Get-XmlNs))        
    $root.AppendChild($files)    
    foreach ($fw in $frameworks)
    {
        foreach ($ref in $projectRefs)
        {
            if ($ref.Platform -eq $fw -or !$ref.Platform)
            {				
                # Configuration, i.e. Debug or Release can come with params to script
                $fileName = [io.path]::GetFileNameWithoutExtension($ref.Name)
                $source = 'bin\Release\' + $fw + '\' + $fileName + '.dll'
                $target = 'lib\' + $fw + '\' + $fileName + '.dll'
                $file = $nuSpec.CreateElement('file', (Get-XmlNs))
                $file.SetAttribute('src', $source)
                $file.SetAttribute('target', $target)

                $files.AppendChild($file)
            }
        }        
    }
}


function Get-SelfAsProjectDenendency([string]$projectFile, $frameworks)
{
    $projectRefs = @()
    foreach ($fw in $frameworks)
    {
        $projRef = New-Object System.Object
        $projRef | Add-Member -type NoteProperty -name Name -value $projectFile
        $projRef | Add-Member -type NoteProperty -name Platform -value $fw
        $projectRefs += $projRef
    }

    return $projectRefs
}


# Entry point
.{
    $solutionFile = Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".sln"}

    foreach ($projectFile in Get-ChildItem ..\src -Recurse | Where-Object {$_.Extension -eq ".csproj"})
    {    

        #Get file contents
        [xml] $projectXml = (Get-ProjectFileContent $projectFile.FullName)
        if ((Should-BuildNuGetPackage $projectXml))
        {                
            $frameworks = (Get-TargetFrameworks $projectXml)
    
            $nugetRefs = @()
            [array]$projectRefs = @()

        
            $projectRefs = (Get-ProjectDependencies $projectFile.FullName '.')
            $projectRefs += , @(Get-SelfAsProjectDenendency $projectFile.FullName $frameworks)
            $projectRefs = $projectRefs |Sort-Object Name -Unique
        
            $basePath = (Split-Path -parent $projectFile.FullName)
            $nugetRefs = (Get-NuGetDependencies $projectFile.FullName '.')        
            $nugetRefs = $nugetRefs |Sort-Object Name -Unique        

            #Build NuSpec file            
            [xml]$nuSpec = New-Object System.Xml.XmlDocument
            $nuSpec.AppendChild($nuSpec.CreateXmlDeclaration("1.0","UTF-8",$null))
            $root = $nuSpec.CreateElement('package', (Get-XmlNs))
            $nuSpec.AppendChild($root)
            
        
            (Add-PackageDependencies $nuSpec $projectFile $nugetRefs $frameworks)
            (Add-FileDependencies $nuSpec $projectXml $projectRefs $frameworks)            

            #Save Nuspec file
            $nuSpecFile = (Join-Path $projectFile.DirectoryName $projectFile.BaseName) + '.' + $version + '.nuspec'
            $nuSpec.Save($nuSpecFile)

            Write-Host $nuSpecFile -ForegroundColor Green
        }
    }
} | Out-Null