@echo off
title Cliente de Impressao - Chase a Flare

REM =====================================================
REM   CONFIGURACOES - PREENCHA ANTES DE EXECUTAR
REM =====================================================

REM URL do servidor da API (obrigatorio)
set CAF_SERVER_URL=

REM ID unico deste cliente de impressao (obrigatorio)
set CAF_CLIENTE_ID=

REM Intervalo em segundos entre verificacoes (obrigatorio)
set CAF_INTERVALO=10

REM Nome da impressora (deixe vazio para usar a padrao do Windows)
set CAF_IMPRESSORA=

REM =====================================================

echo.
echo ============================================
echo   Cliente de Impressao - Chase a Flare
echo ============================================
echo.

REM Para desenvolvimento: usa dotnet run
REM Para producao: compile com "dotnet publish -c Release" e use o .exe

dotnet run --project "%~dp0PrintClient.csproj"

pause
