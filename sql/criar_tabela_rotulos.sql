-- Criar tabela Rotulos
CREATE TABLE IF NOT EXISTS `Rotulos` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `IdPedido` int NULL,
    `IdRecibo` varchar(255) NOT NULL,
    `IdAtendimento` varchar(255) NULL,
    `NomeArquivo` varchar(500) NOT NULL,
    `CaminhoArquivo` varchar(500) NOT NULL,
    `DataGeracao` datetime(6) NOT NULL,
    `QuantidadeRotulos` int NOT NULL DEFAULT 0,
    `CodigosObjeto` longtext NULL,
    `IdsPrePostagem` longtext NULL,
    `TipoRotulo` varchar(10) NOT NULL DEFAULT 'P',
    `FormatoRotulo` varchar(10) NOT NULL DEFAULT 'ET',
    `TamanhoBytes` bigint NOT NULL DEFAULT 0,
    `Observacao` longtext NULL,
    CONSTRAINT `PK_Rotulos` PRIMARY KEY (`Id`),
    INDEX `IX_Rotulos_IdPedido` (`IdPedido`),
    INDEX `IX_Rotulos_IdRecibo` (`IdRecibo`),
    INDEX `IX_Rotulos_DataGeracao` (`DataGeracao`)
) CHARACTER SET=utf8mb4;
