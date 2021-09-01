﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Core;
using Nethereum.Web3;
using RedDuck.Candleswap.Candles.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using WebSocket.Uniswap.Infrastructure;  

namespace WebSocket.Uniswap.Background
{
    public class BlockchainListener: BackgroundService
    {
        private readonly ILogger<BlockchainListener> _logger;
        private readonly ILogicService _logicService;
        private readonly IIndexerService _indexerService;
        private readonly IWeb3 _web3;

        private readonly int[] _defaultPeriods = { 10, 15, 60, 240, 480, 960 };

        public BlockchainListener(ILogger<BlockchainListener> logger, ILogicService logicService,
                                  IIndexerService indexerService, IWeb3 web3)
        {
            _logger = logger;
            _logicService = logicService;
            _indexerService = indexerService;
            _web3 = web3;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Indexer running.");
            DoWork(cancellationToken);
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            var lastBlockInBlockchain = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            _indexerService.IndexInRangeParallel(lastBlockInBlockchain.Value,
                                                 0,
                                                 FSharpOption<BigInteger>.None);

            _indexerService.IndexNewBlockAsync(3);

            var startFrom = DateTimeOffset.UtcNow.DateTime;
            foreach(var period in _defaultPeriods)
            {
                await Task.Run(() => {
                    _logicService.GetCandles(_ => { }, TimeSpan.FromSeconds(period), cancellationToken, startFrom);
                    _logicService.GetCandle(WebSocketConnection.OnCandleUpdateReceived, TimeSpan.FromSeconds(period),
                                        cancellationToken);
                });
            }

            /*_logicService.GetCandles(_ => { }, TimeSpan.FromSeconds(period), cancellationToken,
                                         startFrom);*/
            /*_logicService.GetCandle(WebSocketConnection.OnCandleUpdateReceived, TimeSpan.FromSeconds(period), 
                                    cancellationToken);*/
        }

    }
}
