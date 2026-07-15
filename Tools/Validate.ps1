$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$xmlFiles = Get-ChildItem -Path $root -Recurse -File -Filter "*.xml" |
    Where-Object { $_.FullName -notmatch '[\\/](\.runtime-test|bin|obj)[\\/]' }

foreach ($file in $xmlFiles) {
    $document = New-Object System.Xml.XmlDocument
    $document.Load($file.FullName)
}

function Get-LanguageKeys([string]$languageFolder, [string]$subFolder) {
    $keys = New-Object 'System.Collections.Generic.HashSet[string]'
    $folder = Join-Path $root "1.6\Languages\$languageFolder\$subFolder"
    if (-not (Test-Path -LiteralPath $folder)) {
        return $keys
    }
    Get-ChildItem -LiteralPath $folder -Recurse -File -Filter "*.xml" | ForEach-Object {
        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($_.FullName)
        foreach ($node in $doc.DocumentElement.ChildNodes) {
            if ($node.NodeType -eq [System.Xml.XmlNodeType]::Element) {
                [void]$keys.Add($node.Name)
            }
        }
    }
    return $keys
}

$languageRoot = Join-Path $root "1.6\Languages"
$chineseLanguage = Get-ChildItem -LiteralPath $languageRoot -Directory |
    Where-Object { $_.Name -like "ChineseSimplified*" } |
    Select-Object -First 1 -ExpandProperty Name
if (-not $chineseLanguage) {
    throw "ChineseSimplified language folder is missing."
}
$englishKeys = @(Get-LanguageKeys "English" "Keyed")
$chineseKeys = @(Get-LanguageKeys $chineseLanguage "Keyed")
$missingEnglish = @($chineseKeys | Where-Object { $englishKeys -notcontains $_ })
$missingChinese = @($englishKeys | Where-Object { $chineseKeys -notcontains $_ })
if ($missingEnglish.Count -gt 0 -or $missingChinese.Count -gt 0) {
    throw "Keyed translation sets differ. Missing English: $($missingEnglish -join ', '); missing Chinese: $($missingChinese -join ', ')"
}

$referencedKeys = New-Object 'System.Collections.Generic.HashSet[string]'
Get-ChildItem -LiteralPath (Join-Path $root "1.6\Defs") -Recurse -File -Filter "*.xml" | ForEach-Object {
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($_.FullName)
    foreach ($node in $doc.SelectNodes("//*")) {
        if ($node.Name.EndsWith("Key") -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
            [void]$referencedKeys.Add($node.InnerText.Trim())
        }
    }
}
Get-ChildItem -LiteralPath (Join-Path $root "Source\1.6") -Recurse -File -Filter "*.cs" | ForEach-Object {
    $source = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
    foreach ($match in [regex]::Matches($source, '"(LCOrdeal_[A-Za-z0-9_]+)"\.Translate')) {
        [void]$referencedKeys.Add($match.Groups[1].Value)
    }
}
$missingReferenced = @($referencedKeys | Where-Object { $englishKeys -notcontains $_ -or $chineseKeys -notcontains $_ })
if ($missingReferenced.Count -gt 0) {
    throw "Referenced translation keys are missing: $($missingReferenced -join ', ')"
}

$chineseInjected = @(Get-LanguageKeys $chineseLanguage "DefInjected")
$missingInjected = New-Object 'System.Collections.Generic.List[string]'
Get-ChildItem -LiteralPath (Join-Path $root "1.6\Defs") -Recurse -File -Filter "*.xml" | ForEach-Object {
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($_.FullName)
    foreach ($def in $doc.DocumentElement.ChildNodes) {
        $defNameNode = $def.SelectSingleNode("defName")
        if ($def.NodeType -ne [System.Xml.XmlNodeType]::Element -or $null -eq $defNameNode) {
            continue
        }
        $defName = $defNameNode.InnerText
        foreach ($field in @("label", "description")) {
            if ($null -ne $def.SelectSingleNode($field)) {
                $translationKey = "$defName.$field"
                if ($chineseInjected -notcontains $translationKey) {
                    $missingInjected.Add($translationKey)
                }
            }
        }
    }
}
if ($missingInjected.Count -gt 0) {
    throw "Chinese DefInjected entries are missing: $($missingInjected -join ', ')"
}

$storyNodes = "D:\DevelopSpace\LC-Anomaly-Story\1.6\Defs\CompanyDevelopmentDefs\DevelopmentNodes.xml"
if (Test-Path -LiteralPath $storyNodes) {
    $document = New-Object System.Xml.XmlDocument
    $document.Load($storyNodes)
    $xpath = '/Defs/LCAnomalyStory.Defs.CompanyDevelopmentDef[defName="LCStory_BasicEnergyManagement"]/conditions/li[@Class="LCAnomalyStory.Conditions.DevelopmentCondition_ExaminationPassed"]/examination'
    if (-not $document.SelectSingleNode($xpath)) {
        throw "StoryDawnGate.xml no longer matches LCStory_BasicEnergyManagement."
    }
    $noonXpath = '/Defs/LCAnomalyStory.Defs.CompanyDevelopmentDef[defName="LCStory_OrganizationalIntegration"]/conditions/li[@Class="LCAnomalyStory.Conditions.DevelopmentCondition_StatisticAtLeast"]'
    if (-not $document.SelectSingleNode($noonXpath)) {
        throw "StoryNoonGate.xml no longer matches LCStory_OrganizationalIntegration."
    }
    $duskXpath = '/Defs/LCAnomalyStory.Defs.CompanyDevelopmentDef[defName="LCStory_InstitutionalCrisis"]/conditions/li[@Class="LCAnomalyStory.Conditions.DevelopmentCondition_StatisticAtLeast"]'
    if (-not $document.SelectSingleNode($duskXpath)) {
        throw "StoryDuskGate.xml no longer matches LCStory_InstitutionalCrisis."
    }
}

$forbidden = Get-ChildItem -Path $root -Recurse -File |
    Where-Object { $_.FullName -match '[\\/]1\.5[\\/]' -or $_.FullName -match '[\\/]Source[\\/]1\.5[\\/]' }
if ($forbidden) {
    throw "RimWorld 1.5 content is not allowed in this repository."
}

$assembly = Join-Path $root "1.6\Assemblies\LCAnomalyOrdeals.dll"
if (-not (Test-Path -LiteralPath $assembly)) {
    throw "LCAnomalyOrdeals.dll is missing. Run Tools/Build.ps1 first."
}

Write-Host "Validated $($xmlFiles.Count) XML files, $($englishKeys.Count) bilingual keyed translations, DefInjected coverage, and the RimWorld 1.6 assembly."
