$TargetFolder = "bin\release\";
$SearchFolder = $TargetFolder + $RelativeFolder;
$Include = '*.dll', '*.exe', '*.config*';
$Exclude = '*.vshost.*';
Out-File list.txt;
foreach ($file in Get-ChildItem .\$SearchFolder\. -Recurse -Include $Include  -Exclude $Exclude)
{
    $pattern = '(.*' + [regex]::escape($TargetFolder) + ')'
    $file = $file -replace  $pattern, "";
    Add-Content list.txt ("<file src=""" + $TargetFolder + $file `
                                                        + """ target="""  + $file  +  """ />");
}
