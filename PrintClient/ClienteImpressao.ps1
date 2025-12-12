# Cliente de Impressão Local - Chase a Flare
# Este script deve rodar no computador onde está a impressora
# Ele fica consultando a fila de impressão no servidor e imprimindo automaticamente

param(
    [string]$ServerUrl = "https://seu-servidor.com",
    [string]$ClienteId = "impressora-local-01",
    [int]$IntervaloSegundos = 10,
    [string]$ImpressoraPadrao = ""
)

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Cliente de Impressao - Chase a Flare  " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Servidor: $ServerUrl"
Write-Host "Cliente ID: $ClienteId"
Write-Host "Intervalo: $IntervaloSegundos segundos"
Write-Host ""

# Pasta temporaria para downloads
$TempFolder = "$env:TEMP\CAF_Rotulos"
if (-not (Test-Path $TempFolder)) {
    New-Item -ItemType Directory -Path $TempFolder | Out-Null
}

function Get-PendingJobs {
    try {
        $response = Invoke-RestMethod -Uri "$ServerUrl/api/FilaImpressao/pendentes?clienteId=$ClienteId&limite=5" -Method Get
        return $response
    }
    catch {
        Write-Host "[ERRO] Falha ao buscar fila: $($_.Exception.Message)" -ForegroundColor Red
        return @()
    }
}

function Reserve-Job {
    param([int]$JobId)
    try {
        $response = Invoke-RestMethod -Uri "$ServerUrl/api/FilaImpressao/$JobId/reservar?clienteId=$ClienteId" -Method Post
        return $response
    }
    catch {
        Write-Host "[ERRO] Falha ao reservar job $JobId : $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Download-Pdf {
    param([int]$JobId, [string]$NomeArquivo)
    try {
        $outputPath = Join-Path $TempFolder $NomeArquivo
        Invoke-WebRequest -Uri "$ServerUrl/api/FilaImpressao/$JobId/download" -OutFile $outputPath
        return $outputPath
    }
    catch {
        Write-Host "[ERRO] Falha ao baixar PDF: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Confirm-Job {
    param([int]$JobId)
    try {
        Invoke-RestMethod -Uri "$ServerUrl/api/FilaImpressao/$JobId/confirmar?clienteId=$ClienteId" -Method Post | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Report-Error {
    param([int]$JobId, [string]$Mensagem)
    try {
        $body = @{ mensagemErro = $Mensagem; clienteId = $ClienteId } | ConvertTo-Json
        Invoke-RestMethod -Uri "$ServerUrl/api/FilaImpressao/$JobId/erro" -Method Post -Body $body -ContentType "application/json" | Out-Null
    }
    catch {
        Write-Host "[ERRO] Falha ao reportar erro: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Print-Pdf {
    param([string]$FilePath, [string]$Impressora, [int]$Copias)
    
    try {
        # Verificar se o arquivo existe
        if (-not (Test-Path $FilePath)) {
            return @{ Success = $false; Message = "Arquivo nao encontrado: $FilePath" }
        }

        # Usar SumatraPDF para imprimir (recomendado para PDFs)
        # Se nao tiver, usar o leitor padrao do Windows
        
        $sumatraPath = "C:\Users\paode\AppData\Local\SumatraPDF\SumatraPDF.exe"
        
        if (Test-Path $sumatraPath) {
            # Usar SumatraPDF (melhor qualidade e controle)
            if ($Impressora) {
                $args = "-print-to `"$Impressora`" -print-settings `"$Copias`x`" `"$FilePath`""
            } else {
                $args = "-print-to-default -print-settings `"$Copias`x`" `"$FilePath`""
            }
            
            Start-Process -FilePath $sumatraPath -ArgumentList $args -Wait -NoNewWindow
        }
        else {
            # Fallback: Usar Adobe Reader ou visualizador padrao
            # Nota: Este metodo pode nao funcionar perfeitamente com todas as impressoras
            
            for ($i = 0; $i -lt $Copias; $i++) {
                Start-Process -FilePath $FilePath -Verb Print -Wait
                Start-Sleep -Seconds 2
            }
        }

        return @{ Success = $true; Message = "Impressao enviada com sucesso" }
    }
    catch {
        return @{ Success = $false; Message = $_.Exception.Message }
    }
}

Write-Host ""
Write-Host "Iniciando monitoramento da fila de impressao..." -ForegroundColor Green
Write-Host "Pressione Ctrl+C para parar"
Write-Host ""

while ($true) {
    try {
        $jobs = Get-PendingJobs
        
        if ($jobs -and $jobs.Count -gt 0) {
            Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Encontrados $($jobs.Count) job(s) pendente(s)" -ForegroundColor Yellow
            
            foreach ($job in $jobs) {
                Write-Host ""
                Write-Host "Processando Job #$($job.id) - $($job.nomeArquivo)" -ForegroundColor Cyan
                
                # Reservar o job
                $reserved = Reserve-Job -JobId $job.id
                if (-not $reserved) {
                    Write-Host "  [SKIP] Job ja reservado por outro cliente" -ForegroundColor Gray
                    continue
                }
                
                # Baixar o PDF
                Write-Host "  Baixando PDF..."
                $pdfPath = Download-Pdf -JobId $job.id -NomeArquivo $job.nomeArquivo
                if (-not $pdfPath) {
                    Report-Error -JobId $job.id -Mensagem "Falha ao baixar PDF"
                    continue
                }
                
                # Imprimir
                Write-Host "  Enviando para impressora..."
                $impressora = if ($job.impressoraDestino) { $job.impressoraDestino } else { $ImpressoraPadrao }
                $result = Print-Pdf -FilePath $pdfPath -Impressora $impressora -Copias $job.copias
                
                if ($result.Success) {
                    Write-Host "  [OK] Impressao enviada com sucesso!" -ForegroundColor Green
                    Confirm-Job -JobId $job.id | Out-Null
                    
                    # Limpar arquivo temporario
                    Remove-Item -Path $pdfPath -Force -ErrorAction SilentlyContinue
                }
                else {
                    Write-Host "  [ERRO] $($result.Message)" -ForegroundColor Red
                    Report-Error -JobId $job.id -Mensagem $result.Message
                }
            }
        }
        else {
            Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Fila vazia - aguardando..." -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "[ERRO] $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Seconds $IntervaloSegundos
}
