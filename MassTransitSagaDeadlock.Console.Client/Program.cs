﻿using System;
using System.Threading.Tasks;
using MassTransit;
using MassTransitSagaDeadlock.Console.Client.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MassTransitSagaDeadlock.Console.Client
{
    class Program
    {
        static void Main(string[] args)
        {

            // add the framework services
            var services = new ServiceCollection()
                .AddLogging();

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            services.AddMassTransitHostedService();

            services.Configure<MessagingSettings>(options =>
                config.GetSection("MessagingSettings").Bind(options));

            services.AddMassTransit(_ =>
            {
                _.UsingRabbitMq((context, config) =>
                {
                    var appSettings = context.GetService<IOptions<MessagingSettings>>().Value;
                    config.Host(appSettings.RabbitMq.Hostname, (ushort)appSettings.RabbitMq.Port, "/",
                        hostConfig =>
                        {
                            hostConfig.Username(appSettings.RabbitMq.Username);
                            hostConfig.Password(appSettings.RabbitMq.Password);
                        });
                });
            });



            var serviceProvider = services.BuildServiceProvider(true);
            var bus = serviceProvider.GetService<IBus>();

            var messagesCount = int.Parse(Environment.GetEnvironmentVariable("MESSAGES_COUNT"));
            var tasks = new Task[messagesCount];
            for (var i = 0; i < messagesCount; i++)
            {
                var endpoint = bus.GetSendEndpoint(new Uri($"queue:transfer-funds")).Result;
                tasks[i] = SendMessage(endpoint);

            }

            Task.WaitAll(tasks);

        }

        private static Task SendMessage(ISendEndpoint endpoint)
        {
            var message = new TransferFundsCommand
            {
                TransferFundsId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                FromWalletId = 1,
                ToWalletId = 2,
                Amount = 100,
                Comment = "Test"
            };
            return endpoint.Send(message);
        }


    }
}
