-- Script para adicionar tabela NotasFiscais
-- Execute este script no banco de dados chase-a-flare

CREATE TABLE IF NOT EXISTS `NotasFiscais` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PedidoId` int NOT NULL,
    `Numero` int NOT NULL,
    `Serie` int NOT NULL,
    `ChaveAcesso` varchar(44) NOT NULL,
    `ProtocoloAutorizacao` varchar(50) NULL,
    `XmlNfe` longtext NULL,
    `XmlProtocolo` longtext NULL,
    `Status` int NOT NULL DEFAULT 0,
    `CodigoStatus` int NOT NULL DEFAULT 0,
    `MensagemStatus` varchar(500) NULL,
    `Ambiente` int NOT NULL DEFAULT 2,
    `ValorTotal` decimal(10,2) NOT NULL,
    `DataEmissao` datetime(6) NOT NULL,
    `DataAutorizacao` datetime(6) NULL,
    `MotivoCancelamento` varchar(500) NULL,
    `DataCancelamento` datetime(6) NULL,
    `ProtocoloCancelamento` varchar(50) NULL,
    CONSTRAINT `PK_NotasFiscais` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_NotasFiscais_Pedidos_PedidoId` FOREIGN KEY (`PedidoId`) REFERENCES `Pedidos` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

-- Índice único para ChaveAcesso
CREATE UNIQUE INDEX `IX_NotasFiscais_ChaveAcesso` ON `NotasFiscais` (`ChaveAcesso`);

-- Índice para busca por PedidoId
CREATE INDEX `IX_NotasFiscais_PedidoId` ON `NotasFiscais` (`PedidoId`);

-- Índice para busca por Status
CREATE INDEX `IX_NotasFiscais_Status` ON `NotasFiscais` (`Status`);

-- Índice para busca por Data
CREATE INDEX `IX_NotasFiscais_DataEmissao` ON `NotasFiscais` (`DataEmissao`);
