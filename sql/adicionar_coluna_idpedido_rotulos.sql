-- Adicionar coluna IdPedido na tabela Rotulos (se já existir)
ALTER TABLE `Rotulos` 
ADD COLUMN `IdPedido` int NULL AFTER `Id`;

-- Criar índice para melhor performance nas buscas
CREATE INDEX `IX_Rotulos_IdPedido` ON `Rotulos` (`IdPedido`);
