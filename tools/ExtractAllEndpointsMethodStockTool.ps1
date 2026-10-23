param(
	[string]$pRootPath = "c:\TEST1\SolucionA\",
	[string]$pTotalJson = "c:\TEST1\Catalog\output\stockTool.inventory.endpoints.total.json",
	[string]$pTotalCsv = "c:\TEST1\Catalog\output\stockTool.inventory.endpoints.total.csv"
)

$ErrorActionPreference = 'Stop'

function Get-NormalizedParameters {
	param(
		[object[]]$pPathParameters,
		[object[]]$pOperationParameters,
		[bool]$pIsOpenApi3
	)

	$llstAllParameters = @($pPathParameters + $pOperationParameters)
	$llstNormalized = New-Object System.Collections.ArrayList

	foreach ($lParam in $llstAllParameters) {
		if ($null -eq $lParam) { continue }

		if ($null -ne $lParam.'$ref') {
			[void]$llstNormalized.Add([pscustomobject]@{
				Name     = ''
				In       = ''
				Required = $null
				Type     = 'ref'
				Ref      = [string]$lParam.'$ref'
			})
			continue
		}

		$lType = ''
		if ($pIsOpenApi3) {
			if ($null -ne $lParam.schema) {
				if ($null -ne $lParam.schema.'$ref') {
					$lType = [string]$lParam.schema.'$ref'
				}
				elseif ($null -ne $lParam.schema.type) {
					$lType = [string]$lParam.schema.type
					if ($null -ne $lParam.schema.format) {
						$lType = "$lType($($lParam.schema.format))"
					}
				}
			}
			if ([string]::IsNullOrWhiteSpace($lType) -and $null -ne $lParam.content) {
				$lFirstContent = $lParam.content.PSObject.Properties | Select-Object -First 1
				if ($null -ne $lFirstContent -and $null -ne $lFirstContent.Value.schema) {
					$lSchema = $lFirstContent.Value.schema
					if ($null -ne $lSchema.'$ref') { $lType = [string]$lSchema.'$ref' }
					elseif ($null -ne $lSchema.type) { $lType = [string]$lSchema.type }
				}
			}
		}
		else {
			if ($null -ne $lParam.schema) {
				if ($null -ne $lParam.schema.'$ref') { $lType = [string]$lParam.schema.'$ref' }
				elseif ($null -ne $lParam.schema.type) { $lType = [string]$lParam.schema.type }
			}
			elseif ($null -ne $lParam.type) {
				$lType = [string]$lParam.type
				if ($null -ne $lParam.format) { $lType = "$lType($($lParam.format))" }
			}
		}

		[void]$llstNormalized.Add([pscustomobject]@{
			Name     = [string]$lParam.name
			In       = [string]$lParam.in
			Required = $(if ($null -ne $lParam.required) { [bool]$lParam.required } else { $null })
			Type     = $lType
			Ref      = ''
		})
	}

	return @($llstNormalized)
}

function Get-ResultInfo {
	param(
		[object]$pOperation,
		[bool]$pIsOpenApi3
	)

	$lResultStatus = ''
	$lResultType = ''

	if ($null -eq $pOperation.responses) {
		return [pscustomobject]@{ ResultStatus = $lResultStatus; ResultType = $lResultType }
	}

	$llstPriority = @('200', '201', '202', '204', 'default')
	$llstStatuses = @($pOperation.responses.PSObject.Properties.Name)
	$lStatus = $llstPriority | Where-Object { $_ -in $llstStatuses } | Select-Object -First 1
	if ($null -eq $lStatus -and $llstStatuses.Count -gt 0) { $lStatus = $llstStatuses[0] }

	if ($null -eq $lStatus) {
		return [pscustomobject]@{ ResultStatus = $lResultStatus; ResultType = $lResultType }
	}

	$lResultStatus = [string]$lStatus
	$lResponse = $pOperation.responses.$lStatus

	if ($pIsOpenApi3) {
		if ($null -ne $lResponse.content) {
			$lContent = if ($null -ne $lResponse.content.'application/json') { $lResponse.content.'application/json' } else { ($lResponse.content.PSObject.Properties | Select-Object -First 1).Value }
			if ($null -ne $lContent -and $null -ne $lContent.schema) {
				$lSchema = $lContent.schema
				if ($null -ne $lSchema.'$ref') { $lResultType = [string]$lSchema.'$ref' }
				elseif ($lSchema.type -eq 'array' -and $null -ne $lSchema.items) {
					if ($null -ne $lSchema.items.'$ref') { $lResultType = "array[$($lSchema.items.'$ref')]" }
					elseif ($null -ne $lSchema.items.type) { $lResultType = "array[$($lSchema.items.type)]" }
					else { $lResultType = 'array' }
				}
				elseif ($null -ne $lSchema.type) {
					$lResultType = [string]$lSchema.type
					if ($null -ne $lSchema.format) { $lResultType = "$lResultType($($lSchema.format))" }
				}
			}
		}
	}
	else {
		if ($null -ne $lResponse.schema) {
			$lSchema = $lResponse.schema
			if ($null -ne $lSchema.'$ref') { $lResultType = [string]$lSchema.'$ref' }
			elseif ($lSchema.type -eq 'array' -and $null -ne $lSchema.items) {
				if ($null -ne $lSchema.items.'$ref') { $lResultType = "array[$($lSchema.items.'$ref')]" }
				elseif ($null -ne $lSchema.items.type) { $lResultType = "array[$($lSchema.items.type)]" }
				else { $lResultType = 'array' }
			}
			elseif ($null -ne $lSchema.type) {
				$lResultType = [string]$lSchema.type
				if ($null -ne $lSchema.format) { $lResultType = "$lResultType($($lSchema.format))" }
			}
		}
	}

	return [pscustomobject]@{ ResultStatus = $lResultStatus; ResultType = $lResultType }
}

function Get-Scope {
	param([string]$pPath)

	if ([string]::IsNullOrWhiteSpace($pPath)) { return '' }
	if ($pPath -match '^https?://') { return 'External' }
	return 'Internal'
}

function Test-IsLikelyApiPath {
	param([string]$pPath)

	if ([string]::IsNullOrWhiteSpace($pPath)) { return $false }
	if ($pPath -match '^https?://') { return $true }
	if ($pPath -match '/') { return $true }
	if ($pPath -match '(?i)api') { return $true }
	return $false
}

function Get-RoutePrefixMap {
	param([string]$pRootPath)

	$lMap = @{}
	$llstConfigFiles = Get-ChildItem -Path $pRootPath -Recurse -File -Include *.config |
		Where-Object { $_.FullName -notmatch '\\node_modules\\|\\packages\\|\\.vs\\|\\bin\\|\\obj\\' }

	foreach ($lCfg in $llstConfigFiles) {
		try {
			$lCfgText = Get-Content $lCfg.FullName -Raw -Encoding UTF8 -ErrorAction Stop
		}
		catch {
			continue
		}

		$llstRouteMatches = [regex]::Matches($lCfgText, '<add\s+key="(routeWebAPI\w+)"\s+value="([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
		foreach ($lMatch in $llstRouteMatches) {
			$lKey = $lMatch.Groups[1].Value
			$lValue = $lMatch.Groups[2].Value
			if ([string]::IsNullOrWhiteSpace($lKey) -or [string]::IsNullOrWhiteSpace($lValue)) { continue }
			if (-not $lMap.ContainsKey($lKey)) {
				$lMap[$lKey] = $lValue
			}
		}
	}

	return $lMap
}

function Normalize-DetectedPath {
	param(
		[string]$pRawPath,
		[hashtable]$pRoutePrefixMap
	)

	if ([string]::IsNullOrWhiteSpace($pRawPath)) { return '' }

	$lPath = $pRawPath.Trim()
	$lPath = $lPath.Trim('"','''','`')
	$lPath = $lPath -replace '^\$"',''
	$lPath = $lPath -replace '^\$''',''

	if ($lPath -match '(?i)(this\.)?(routeWebAPI\w+)\s*\+\s*(\$?"[^"]+"|\$?''[^'']+'')') {
		$lRouteKey = $Matches[2]
		$lSuffix = $Matches[3].Trim('"','''','`')
		$lPrefix = ''
		if ($null -ne $pRoutePrefixMap -and $pRoutePrefixMap.ContainsKey($lRouteKey)) {
			$lPrefix = [string]$pRoutePrefixMap[$lRouteKey]
		}

		if (-not [string]::IsNullOrWhiteSpace($lPrefix)) {
			$lPrefix = '/' + $lPrefix.Trim('/')
			$lSuffix = '/' + $lSuffix.Trim('/')
			$lPath = ($lPrefix + $lSuffix) -replace '/{2,}', '/'
		}
		else {
			$lPath = $lSuffix
		}
	}

	if ($lPath -match '(?i)^\$?"([^"]+)"$') {
		$lPath = $Matches[1]
	}
	elseif ($lPath -match '(?i)^\$?''([^'']+)''$') {
		$lPath = $Matches[1]
	}

	return $lPath.Trim()
}

function Get-DetectedPrefix {
	param(
		[string]$pRawPath,
		[hashtable]$pRoutePrefixMap
	)

	if ([string]::IsNullOrWhiteSpace($pRawPath)) { return '' }
	$lMatch = [regex]::Match($pRawPath, '(?i)(?:this\.)?(routeWebAPI\w+)')
	if (-not $lMatch.Success) { return '' }

	$lRouteKey = $lMatch.Groups[1].Value
	if ($null -ne $pRoutePrefixMap -and $pRoutePrefixMap.ContainsKey($lRouteKey)) {
		return [string]$pRoutePrefixMap[$lRouteKey]
	}

	return ''
}

function Add-Record {
	param(
		[System.Collections.ArrayList]$pList,
		[hashtable]$pData
	)

	if ($null -eq $pData.Parameters) { $pData.Parameters = @() }
	if ($null -eq $pData.Prefix) { $pData.Prefix = '' }
	if ($null -eq $pData.Id -or [string]::IsNullOrWhiteSpace([string]$pData.Id)) {
		$pData.Id = [Guid]::NewGuid().ToString()
	}

	[void]$pList.Add([pscustomobject]$pData)
}

$lResolvedRoot = Resolve-Path $pRootPath
Set-Location $lResolvedRoot

$llstContractRecords = New-Object System.Collections.ArrayList
$llstTotalRecords = New-Object System.Collections.ArrayList
$lRoutePrefixMap = Get-RoutePrefixMap -pRootPath .

# 1) Contract records from OpenAPI/Swagger
$llstSpecCandidates = Get-ChildItem -Path . -Recurse -File -Include *.json |
	Where-Object { $_.FullName -match '\\OpenAPIs\\|swagger' -and $_.FullName -notmatch '\\node_modules\\|\\packages\\|\\.vs\\|\\bin\\|\\obj\\' }

Write-Output $llstSpecCandidates

foreach ($lSpec in $llstSpecCandidates) {

	try {
		$lDoc = Get-Content $lSpec.FullName -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 100
	}
	catch {
		continue
	}

	Write-Output $lDoc

	if ($null -eq $lDoc.paths) { continue }
	if ($null -eq $lDoc.swagger -and $null -eq $lDoc.openapi) { continue }

	$lVersion = if ($lDoc.swagger) { [string]$lDoc.swagger } elseif ($lDoc.openapi) { [string]$lDoc.openapi } else { '' }
	$lIsOpenApi3 = -not [string]::IsNullOrWhiteSpace([string]$lDoc.openapi)
	$lRelativeSpec = Resolve-Path -Relative $lSpec.FullName

	foreach ($lPathProp in $lDoc.paths.PSObject.Properties) {
		$lPath = [string]$lPathProp.Name
		$lPathNode = $lPathProp.Value
		
		$llstPathParameters = @()
		if ($lPathNode.PSObject.Properties.Name -contains 'parameters' -and $null -ne $lPathNode.parameters) {
			$llstPathParameters = @($lPathNode.parameters)
		}

		foreach ($lMethodProp in $lPathNode.PSObject.Properties) {
			$lMethod = [string]$lMethodProp.Name
			if ($lMethod -notin @('get', 'post', 'put', 'delete', 'patch', 'options', 'head', 'trace')) { continue }

			$lOperation = $lMethodProp.Value
			$llstOperationParameters = @()
			if ($lOperation.PSObject.Properties.Name -contains 'parameters' -and $null -ne $lOperation.parameters) {
				$llstOperationParameters = @($lOperation.parameters)
			}

			$llstParameters = Get-NormalizedParameters -pPathParameters $llstPathParameters -pOperationParameters $llstOperationParameters -pIsOpenApi3 $lIsOpenApi3
			$lResult = Get-ResultInfo -pOperation $lOperation -pIsOpenApi3 $lIsOpenApi3

			$lRecord = @{
				SourceSpec      = $lRelativeSpec.TrimStart('.','\','/')
				Version         = $lVersion
				Method          = $lMethod.ToUpperInvariant()
				HttpVerbReal    = $lMethod.ToUpperInvariant()
				Path            = $lPath
				Prefix          = ''
				OperationId     = [string]$lOperation.operationId
				Namespace       = ''
				ClassName       = ''
				MethodName      = [string]$lOperation.operationId
				ParametersCount = @($llstParameters).Count
				Parameters      = @($llstParameters)
				ResultStatus    = [string]$lResult.ResultStatus
				ResultType      = [string]$lResult.ResultType
				Direction       = 'Inbound'
				Origin          = 'Contract'
				Scope           = 'Internal'
				SourceFile      = $lRelativeSpec.TrimStart('.','\','/')
				Category        = 'OpenApiPath'
				Details         = ''
			}

			Add-Record -pList $llstContractRecords -pData $lRecord
			Add-Record -pList $llstTotalRecords -pData $lRecord
		}
	}
}

Write-Output "# 2) Inbound from controllers (C#/VB)"

# 2) Inbound from controllers (C#/VB)
$llstControllerFiles = Get-ChildItem -Path . -Recurse -File -Include *Controller.cs,*Controller.vb |
	Where-Object { $_.FullName -notmatch '\\node_modules\\|\\packages\\|\\.vs\\|\\bin\\|\\obj\\' }

foreach ($lFile in $llstControllerFiles) {
	$lContent = Get-Content $lFile.FullName -Raw -Encoding UTF8
	$llstLines = $lContent -split "`r?`n"
	$lControllerName = [System.IO.Path]::GetFileNameWithoutExtension($lFile.Name) -replace 'Controller$',''
	$lNamespace = ''
	$lClassName = [System.IO.Path]::GetFileNameWithoutExtension($lFile.Name)
	$lNamespaceMatch = [regex]::Match($lContent, '(?im)^\s*namespace\s+([\w\.]+)')
	if ($lNamespaceMatch.Success) { $lNamespace = $lNamespaceMatch.Groups[1].Value }
	$lClassMatch = [regex]::Match($lContent, '(?im)^\s*(?:public\s+)?class\s+(\w+)')
	if ($lClassMatch.Success) { $lClassName = $lClassMatch.Groups[1].Value }

	$lRoutePrefix = ''
	$lPrefixMatch = [regex]::Match($lContent, '\[RoutePrefix\("([^"]+)"\)\]')
	if ($lPrefixMatch.Success) {
		$lRoutePrefix = $lPrefixMatch.Groups[1].Value -replace '\[controller\]', $lControllerName
	}

	for ($lIndex = 0; $lIndex -lt $llstLines.Count; $lIndex++) {
		$lLine = $llstLines[$lIndex]
		$lVerbMatch = [regex]::Match($lLine, '\[(HttpGet|HttpPost|HttpPut|HttpDelete|HttpPatch)\b')
		if (-not $lVerbMatch.Success) { continue }

		$lVerb = ($lVerbMatch.Groups[1].Value -replace 'Http','').ToUpperInvariant()
		$lRoute = ''
		$lMethodName = ''
		$lResultType = ''
		$llstParameters = @()

		for ($lScan = $lIndex; $lScan -lt [Math]::Min($lIndex + 15, $llstLines.Count); $lScan++) {
			$lRouteMatch = [regex]::Match($llstLines[$lScan], '\[Route\("([^"]+)"\)\]')
			if ($lRouteMatch.Success) { $lRoute = $lRouteMatch.Groups[1].Value }

			$lMethodMatch = [regex]::Match($llstLines[$lScan], 'public\s+([\w<>,\.\[\]\?]+)\s+(\w+)\s*\(([^\)]*)\)')
			if ($lMethodMatch.Success) {
				$lResultType = $lMethodMatch.Groups[1].Value
				$lMethodName = $lMethodMatch.Groups[2].Value
				$lParamRaw = $lMethodMatch.Groups[3].Value
				if (-not [string]::IsNullOrWhiteSpace($lParamRaw)) {
					$llstSplitParams = $lParamRaw -split ','
					$llstParameters = @($llstSplitParams | ForEach-Object {
						$lTokens = ($_ -replace '\[.*?\]','').Trim() -split '\s+'
						if ($lTokens.Count -ge 2) {
							[pscustomobject]@{ Name = $lTokens[-1]; In = 'unknown'; Required = $null; Type = ($lTokens[0..($lTokens.Count-2)] -join ' '); Ref = '' }
						}
					})
				}
				break
			}
		}

		if ([string]::IsNullOrWhiteSpace($lMethodName)) { continue }

		$lFullPath = ''
		if (-not [string]::IsNullOrWhiteSpace($lRoutePrefix) -and -not [string]::IsNullOrWhiteSpace($lRoute)) {
			$lFullPath = "/$($lRoutePrefix.Trim('/'))/$($lRoute.Trim('/'))"
		}
		elseif (-not [string]::IsNullOrWhiteSpace($lRoutePrefix)) {
			$lFullPath = "/$($lRoutePrefix.Trim('/'))/$lMethodName"
		}
		else {
			$lFullPath = "/api/{apiVersion}/$lControllerName/$lMethodName"
		}

		Add-Record -pList $llstTotalRecords -pData @{
			SourceSpec      = ''
			Version         = ''
			Method          = $lVerb
			HttpVerbReal    = $lVerb
			Path            = $lFullPath
			Prefix          = ''
			OperationId     = $lMethodName
			Namespace       = $lNamespace
			ClassName       = $lClassName
			MethodName      = $lMethodName
			ParametersCount = @($llstParameters).Count
			Parameters      = @($llstParameters)
			ResultStatus    = ''
			ResultType      = $lResultType
			Direction       = 'Inbound'
			Origin          = 'Code'
			Scope           = 'Internal'
			SourceFile      = (Resolve-Path -Relative $lFile.FullName).TrimStart('.','\','/')
			Category        = 'ControllerRoute'
			Details         = ''
		}
	}
}

Write-Output "# 3) Outbound from code calls (C#, VB, TS)"

# 3) Outbound from code calls (C#, VB, TS)
$llstCodeFiles = Get-ChildItem -Path . -Recurse -File -Include *.cs,*.vb,*.ts |
	Where-Object { $_.FullName -notmatch '\\node_modules\\|\\packages\\|\\.vs\\|\\bin\\|\\obj\\' }

$lSeen = New-Object System.Collections.Generic.HashSet[string]

foreach ($lFile in $llstCodeFiles) {
	try {
		$lFileContent = Get-Content $lFile.FullName -Raw -Encoding UTF8 -ErrorAction Stop
	}
	catch {
		continue
	}

	if ($null -eq $lFileContent) { $lFileContent = '' }
	$llstLines = if ([string]::IsNullOrWhiteSpace($lFileContent)) { @() } else { $lFileContent -split "`r?`n" }
	$lRelativeFile = (Resolve-Path -Relative $lFile.FullName).TrimStart('.','\','/')
	$lNamespace = ''
	$lClassName = [System.IO.Path]::GetFileNameWithoutExtension($lFile.Name)
	$lNamespaceMatch = [regex]::Match($lFileContent, '(?im)^\s*namespace\s+([\w\.]+)')
	if ($lNamespaceMatch.Success) { $lNamespace = $lNamespaceMatch.Groups[1].Value }
	$lClassMatchCs = [regex]::Match($lFileContent, '(?im)^\s*(?:public\s+|private\s+|internal\s+|protected\s+)?(?:partial\s+)?class\s+(\w+)')
	if ($lClassMatchCs.Success) { $lClassName = $lClassMatchCs.Groups[1].Value }
	$lClassMatchVb = [regex]::Match($lFileContent, '(?im)^\s*Public\s+Class\s+(\w+)')
	if ($lClassMatchVb.Success) { $lClassName = $lClassMatchVb.Groups[1].Value }

	foreach ($lLine in $llstLines) {
		$lMethodName = ''
		$lCsMatch = [regex]::Match($lLine, '(?i)\b(Get|Post|Put|Delete|Patch)Async\s*\(\s*(@?"[^"]+"|''[^'']+'')')
		if ($lCsMatch.Success) {
			$lVerb = $lCsMatch.Groups[1].Value.ToUpperInvariant()
			$lPrefix = Get-DetectedPrefix -pRawPath $lCsMatch.Groups[2].Value -pRoutePrefixMap $lRoutePrefixMap
			$lPath = Normalize-DetectedPath -pRawPath $lCsMatch.Groups[2].Value -pRoutePrefixMap $lRoutePrefixMap
			$lMethodCtx = [regex]::Match($lLine, '(?i)\b([A-Za-z_][A-Za-z0-9_]*)\s*\(')
			if ($lMethodCtx.Success) { $lMethodName = $lMethodCtx.Groups[1].Value }
			if (-not (Test-IsLikelyApiPath -pPath $lPath)) { continue }
			$lKey = "$lRelativeFile|$lVerb|$lPath"
			if (-not $lSeen.Add($lKey)) { continue }

			$lCategory = if ($lRelativeFile -match '(?i)\.ts$') { 'TsHttpCall' } else { 'CSharpOrVbHttpCall' }

			Add-Record -pList $llstTotalRecords -pData @{
				SourceSpec      = ''
				Version         = ''
				Method          = $lVerb
				HttpVerbReal    = $lVerb
				Path            = $lPath
				Prefix          = $lPrefix
				OperationId     = ''
				Namespace       = $lNamespace
				ClassName       = $lClassName
				MethodName      = $lMethodName
				ParametersCount = 0
				Parameters      = @()
				ResultStatus    = ''
				ResultType      = ''
				Direction       = 'Outbound'
				Origin          = 'Code'
				Scope           = (Get-Scope -pPath $lPath)
				SourceFile      = $lRelativeFile
				Category        = $lCategory
				Details         = $lLine.Trim()
			}
			continue
		}

		$lSrvMatch = [regex]::Match($lLine, '(?i)\bthis\.(get|post|put|delete|patch)\s*\(\s*(.+?)\s*(?:,|\))')
		if ($lSrvMatch.Success) {
			$lVerb = $lSrvMatch.Groups[1].Value.ToUpperInvariant()
			$lPathRaw = $lSrvMatch.Groups[2].Value
			$lPrefix = Get-DetectedPrefix -pRawPath $lPathRaw -pRoutePrefixMap $lRoutePrefixMap
			$lPath = Normalize-DetectedPath -pRawPath $lPathRaw -pRoutePrefixMap $lRoutePrefixMap
			$lMethodCtx = [regex]::Match($lLine, '(?i)\b([A-Za-z_][A-Za-z0-9_]*)\s*\(')
			if ($lMethodCtx.Success) { $lMethodName = $lMethodCtx.Groups[1].Value }
			if (-not (Test-IsLikelyApiPath -pPath $lPath)) { continue }
			$lKey = "$lRelativeFile|$lVerb|$lPath"
			if (-not $lSeen.Add($lKey)) { continue }

			$lCategory = if ($lRelativeFile -match '(?i)\.ts$') { 'TsHttpCall' } else { 'CSharpOrVbHttpCall' }

			Add-Record -pList $llstTotalRecords -pData @{
				SourceSpec      = ''
				Version         = ''
				Method          = $lVerb
				HttpVerbReal    = $lVerb
				Path            = $lPath
				Prefix          = $lPrefix
				OperationId     = ''
				Namespace       = $lNamespace
				ClassName       = $lClassName
				MethodName      = $lMethodName
				ParametersCount = 0
				Parameters      = @()
				ResultStatus    = ''
				ResultType      = ''
				Direction       = 'Outbound'
				Origin          = 'Code'
				Scope           = (Get-Scope -pPath $lPath)
				SourceFile      = $lRelativeFile
				Category        = $lCategory
				Details         = $lLine.Trim()
			}
			continue
		}

		$lTsMatch = [regex]::Match($lLine, '(?i)\.(get|post|put|delete|patch)\s*(<[^>]+>)?\s*\(\s*([`"''][^`"'']+[`"''])')
		if ($lTsMatch.Success) {
			if ($lRelativeFile -notmatch '(?i)\.ts$') { continue }
			$lVerb = $lTsMatch.Groups[1].Value.ToUpperInvariant()
			$lPrefix = Get-DetectedPrefix -pRawPath $lTsMatch.Groups[3].Value -pRoutePrefixMap $lRoutePrefixMap
			$lPath = Normalize-DetectedPath -pRawPath $lTsMatch.Groups[3].Value -pRoutePrefixMap $lRoutePrefixMap
			$lMethodCtx = [regex]::Match($lLine, '(?i)\b([A-Za-z_][A-Za-z0-9_]*)\s*\(')
			if ($lMethodCtx.Success) { $lMethodName = $lMethodCtx.Groups[1].Value }
			if (-not (Test-IsLikelyApiPath -pPath $lPath)) { continue }
			$lKey = "$lRelativeFile|$lVerb|$lPath"
			if (-not $lSeen.Add($lKey)) { continue }

			Add-Record -pList $llstTotalRecords -pData @{
				SourceSpec      = ''
				Version         = ''
				Method          = $lVerb
				HttpVerbReal    = $lVerb
				Path            = $lPath
				Prefix          = $lPrefix
				OperationId     = ''
				Namespace       = $lNamespace
				ClassName       = $lClassName
				MethodName      = $lMethodName
				ParametersCount = 0
				Parameters      = @()
				ResultStatus    = ''
				ResultType      = ''
				Direction       = 'Outbound'
				Origin          = 'Code'
				Scope           = (Get-Scope -pPath $lPath)
				SourceFile      = $lRelativeFile
				Category        = 'TsHttpCall'
				Details         = $lLine.Trim()
			}
			continue
		}

		$lUrlMatch = [regex]::Match($lLine, '(?i)(https?://[^\s"''`]+|/api/[^\s"''`]+)')
		if ($lUrlMatch.Success -and $lLine -match '(?i)http|request|api|fetch|axios|rest') {
			$lPath = $lUrlMatch.Groups[1].Value
			$lMethodCtx = [regex]::Match($lLine, '(?i)\b([A-Za-z_][A-Za-z0-9_]*)\s*\(')
			if ($lMethodCtx.Success) { $lMethodName = $lMethodCtx.Groups[1].Value }
			$lKey = "$lRelativeFile|URL|$lPath"
			if (-not $lSeen.Add($lKey)) { continue }

			Add-Record -pList $llstTotalRecords -pData @{
				SourceSpec      = ''
				Version         = ''
				Method          = ''
				HttpVerbReal    = ''
				Path            = $lPath
				Prefix          = ''
				OperationId     = ''
				Namespace       = $lNamespace
				ClassName       = $lClassName
				MethodName      = $lMethodName
				ParametersCount = 0
				Parameters      = @()
				ResultStatus    = ''
				ResultType      = ''
				Direction       = 'Outbound'
				Origin          = 'Code'
				Scope           = (Get-Scope -pPath $lPath)
				SourceFile      = $lRelativeFile
				Category        = 'UrlReference'
				Details         = $lLine.Trim()
			}
		}
	}
}

# Normalize and export
$llstTotalOrdered = $llstTotalRecords | Sort-Object Direction, Origin, Scope, Path, Method

$llstTotalOrdered | ConvertTo-Json -Depth 100 | Set-Content -Path $pTotalJson -Encoding UTF8
$llstTotalOrdered |
	Select-Object Id, Direction, Origin, Scope, Category, Method, HttpVerbReal, Path, Prefix, OperationId, Namespace, ClassName, MethodName, ParametersCount, ResultStatus, ResultType, SourceFile, SourceSpec,
	@{ Name = 'Parameters'; Expression = { ($_.Parameters | ConvertTo-Json -Compress -Depth 20) } },
	Details |
	Export-Csv -Path $pTotalCsv -NoTypeInformation -Encoding UTF8

Write-Output "Contract records included in total: $($llstContractRecords.Count)"
Write-Output "Total records: $($llstTotalOrdered.Count)"
Write-Output "Generated: $pTotalJson, $pTotalCsv"
