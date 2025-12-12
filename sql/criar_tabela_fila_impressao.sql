-- Criar tabela FilaImpressao
CREATE TABLE IF NOT EXISTS `FilaImpressao` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RotuloId` int NULL,
    `PedidoId` int NULL,
    `CodigoPedido` varchar(50) NULL,
    `NomeArquivo` varchar(500) NOT NULL,
    `CaminhoArquivo` varchar(500) NOT NULL,
    `Status` int NOT NULL DEFAULT 0,
    `Tentativas` int NOT NULL DEFAULT 0,
    `MaxTentativas` int NOT NULL DEFAULT 3,
    `MensagemErro` longtext NULL,
    `ImpressoraDestino` varchar(255) NULL,
    `Copias` int NOT NULL DEFAULT 1,
    `DataCriacao` datetime(6) NOT NULL,
    `DataProcessamento` datetime(6) NULL,
    `DataImpressao` datetime(6) NULL,
    `ClienteId` varchar(100) NULL,
    CONSTRAINT `PK_FilaImpressao` PRIMARY KEY (`Id`),
    INDEX `IX_FilaImpressao_Status` (`Status`),
    INDEX `IX_FilaImpressao_DataCriacao` (`DataCriacao`),
    INDEX `IX_FilaImpressao_RotuloId` (`RotuloId`),
    INDEX `IX_FilaImpressao_PedidoId` (`PedidoId`),
    CONSTRAINT `FK_FilaImpressao_Rotulos_RotuloId` FOREIGN KEY (`RotuloId`) REFERENCES `Rotulos` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

-- Adicionar comentários sobre os status
-- Status: 0 = Pendente, 1 = EmProcessamento, 2 = Impresso, 3 = Erro, 4 = Cancelado
