using Infrastructure.Broker;
using Infrastructure.Broker.Kafka.Contracts.Interfaces;
using Infrastructure.Broker.Kafka.Rpc.Commandor;
using Infrastructure.Broker.Kafka.Rpc.Interfaces;
using Infrastructure.Database;
using SmtpConnector.Api;
using SmtpConnector.Api.Options;
using SmtpConnector.Dal;
using SmtpConnector.Dal.History;
using SmtpConnector.Dal.History.Interfaces;
using SmtpConnector.Dal.History.Services;
using SmtpConnector.Logic.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection(nameof(GmailOptions)));
builder.Services.Configure<PubSubOptions>(builder.Configuration.GetSection(nameof(PubSubOptions)));
builder.Services.Configure<HistoryStateOptions>(builder.Configuration.GetSection(nameof(HistoryStateOptions)));

builder.Services.AddPostgres();
builder.Services.AddSmtpConnectorDal();
builder.Services.AddKafka();

builder.Services.AddSingleton<IKafkaBrokerCommandor, KafkaBrokerCommandor>();

builder.Services.AddSingleton<IEmailIngestor, EmailIngestor>();
builder.Services.AddSingleton<IHistoryStateStore, FileHistoryStateStore>();

builder.Services.AddHostedService<GmailInboundHostedService>();

await builder.Build().RunAsync();