﻿using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using BinanceBot.Market;
using BinanceBot.Market.Configurations;
using BinanceBot.Market.Strategies;
using CryptoExchange.Net.Authentication;

using static System.Console;



namespace BinanceWpfSkin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    
    public partial class MainWindow : Window
    {
        #region Bot Settings
        // WARN: set your credentials here here 
        private const string ApiKey = "lZ6M1NOtHHWTjXie3hj9gVPmeuCL5rrYHe0kbT17fjOmxf0Cdv9EZFT9pPXuKIAF";
        private const string Secret = "HYX2dY4SWHoKDUDHCuagXSQqwHHcxQdE4aTZCyD9g7jkxvablWgqfuhnxOjhWcya";

        // WARN: set necessary token here
        private const string Symbol = "XRPUSDT";
        private static readonly TimeSpan ReceiveWindow = TimeSpan.FromMilliseconds(1000);
        #endregion

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async Task gridEmployees_LoadedAsync(object sender, RoutedEventArgs e)
        {
            await CreateDataTableAsync();
           // gridEmployees.DataContext = employeeData.DefaultView;
        }
        private static async Task CreateDataTableAsync()
        {
            // 1. create connections with exchange
            var credentials = new ApiCredentials(ApiKey, Secret);
            using IBinanceClient binanceRestClient = new BinanceClient(new BinanceClientOptions { ApiCredentials = credentials });
            using IBinanceSocketClient binanceSocketClient = new BinanceSocketClient(new BinanceSocketClientOptions { ApiCredentials = credentials });


            // 2. test connection
            Logger.Info("Testing connection...");
            var pingAsyncResult = await binanceRestClient.PingAsync();
            Logger.Info($"Ping {pingAsyncResult.Data} ms");


            // 2.1. check permissions
            var permissionsResponse = await binanceRestClient.General.GetAPIKeyPermissionsAsync();
            if (!permissionsResponse.Success)
            {
                Logger.Error($"{permissionsResponse.Error?.Message}");
            }
            else if (permissionsResponse.Data.IpRestrict | !permissionsResponse.Data.EnableSpotAndMarginTrading)
            {
                Logger.Error("Insufficient API permissions");
            }
            ReadLine();

            // 3. set bot strategy config
            var exchangeInfoResult = binanceRestClient.Spot.System.GetExchangeInfoAsync(Symbol);

            var symbolInfo = exchangeInfoResult.Result.Data.Symbols
                .Single(s => s.Name.Equals(Symbol, StringComparison.InvariantCultureIgnoreCase));

           if (!(symbolInfo.Status == SymbolStatus.Trading && symbolInfo.OrderTypes.Contains(OrderType.Market)))
            {
                Logger.Error($"Symbol {symbolInfo.Name} doesn't suitable for this strategy");
                return;
            }

            if (symbolInfo.LotSizeFilter == null)
            {
                Logger.Error($"Cannot define risks strategy for {symbolInfo.Name}");
                return;
            }

            if (symbolInfo.PriceFilter == null)
            {
                Logger.Error($"Cannot define price precision for {symbolInfo.Name}. Please define it manually.");
                return;
            }
            int pricePrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(symbolInfo.PriceFilter.TickSize)));


            // WARN: set thresholds for strategy here
            var strategyConfig = new MarketStrategyConfiguration
            {
                TradeWhenSpreadGreaterThan = .02M, // or 0.02%, (price spread*min_volume) should be greater than broker's commissions for trade
                MinOrderVolume = symbolInfo.LotSizeFilter.MinQuantity * 10,
                MaxOrderVolume = symbolInfo.LotSizeFilter.MinQuantity * 100,
                QuoteAssetPrecision = symbolInfo.QuoteAssetPrecision,
                PricePrecision = pricePrecision,
                ReceiveWindow = ReceiveWindow
            };

            var marketStrategy = new NaiveMarketMakerStrategy(strategyConfig, Logger);


            // 3. start bot
            IMarketBot bot = new MarketMakerBot(Symbol, marketStrategy, binanceRestClient, binanceSocketClient, Logger);

            try
            {
                await bot.RunAsync();

                WriteLine($"Press Enter to stop {Symbol} bot...");
                ReadLine();
            }
            finally
            {
                bot.Stop();
            }

            WriteLine("Press Enter to exit...");
            ReadLine();
        }

        private void gridEmployees_Loaded(object sender, RoutedEventArgs e)
        {
            CreateDataTableAsync();
        }
    }
}

