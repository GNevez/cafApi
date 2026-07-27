-- Migration: Adiciona campos para sistema de carrinho abandonado
-- Data: 2024
-- Descrição: Adiciona campos EmailRecuperacaoEnviadoEm e EmailRecuperacaoCount na tabela Carrinhos

-- Adicionar campo para data do último email de recuperação
ALTER TABLE Carrinhos ADD COLUMN IF NOT EXISTS EmailRecuperacaoEnviadoEm DATETIME NULL;

-- Adicionar campo para contador de emails de recuperação enviados
ALTER TABLE Carrinhos ADD COLUMN IF NOT EXISTS EmailRecuperacaoCount INT NOT NULL DEFAULT 0;

-- Índice para otimizar busca de carrinhos abandonados
CREATE INDEX IF NOT EXISTS IX_Carrinhos_Abandono ON Carrinhos (
    Status, 
    ClienteId, 
    EmailRecuperacaoEnviadoEm, 
    EmailRecuperacaoCount, 
    DataAtualizacao
);
