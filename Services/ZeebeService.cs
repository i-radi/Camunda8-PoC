using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Zeebe.Client.Impl.Builder;
using System.Text.Json;
using System.Threading;

namespace Cloudstarter.Services
{
    public class ZeebeService : IZeebeService
    {
        private readonly IZeebeClient _client;
        private readonly ILogger<ZeebeService> _logger;
        private readonly IWebHostEnvironment _env;

        public ZeebeService(IConfiguration config, ILogger<ZeebeService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
            var ZbClinetType = config["ZeebeClientType"];

            var authServer = config["ZeebeClientCloudSettings:OAuthURL"];
            var clientId = config["ZeebeClientCloudSettings:ClientId"];
            var clientSecret = config["ZeebeClientCloudSettings:ClientSecret"];
            var zeebeUrl = config["ZeebeClientCloudSettings:ClusterURL"];
            char[] port = { '4', '3', ':' };
            var audience = zeebeUrl?.TrimEnd(port);

            if (ZbClinetType == "1")
            {
                _client = ZeebeClient.Builder()
                   .UseGatewayAddress(config["CamundaServer:Url"])
                   .UsePlainText()
                   .Build();
            }
            else
            {
                _client = ZeebeClient.Builder()
                    .UseGatewayAddress(zeebeUrl)
                    .UseTransportEncryption()
                    .UseAccessTokenSupplier(
                    CamundaCloudTokenProvider.Builder()
                    .UseAuthServer(authServer)
                    .UseClientId(clientId)
                    .UseClientSecret(clientSecret)
                    .UseAudience(audience)
                    .Build()).Build();
            }
        }

        public Task<ITopology> Status()
        {
            return _client.TopologyRequest().Send();
        }

        public async Task<IDeployResourceResponse> Deploy(string modelFile)
        {
            var filename = Path.Combine(_env.ContentRootPath, "Resources", modelFile);
            var deployment = await _client.NewDeployCommand().AddResourceFile(filename).Send();
            var res = deployment.Processes[0];
            _logger.LogInformation("Deployed BPMN Model: " + res?.BpmnProcessId +
                        " v." + res?.Version);
            return deployment;
        }

        public async Task<string> CreateWorkflowInstance(string bpmProcessId)
        {
            var instance = await _client.NewCreateProcessInstanceCommand()
                .BpmnProcessId(bpmProcessId)
                .LatestVersion()
                .Send();

            return JsonSerializer.Serialize(instance);
        }

        public async Task StartWorkers()
        {
            await CreateTestWorker();
            await Test1Worker();
            await Test2Worker();
        }

        public async Task CreateTestWorker()
        {
            await CreateWorker("test_service_task_def", async (client, job) =>
            {
                try
                {
                    //throw new Exception();
                    Console.WriteLine($"CreateTestWorker Started {DateTime.Now}");
                    //Task.Delay(5000);
                    await client.NewCompleteJobCommand(job.Key)
                    .Variables(JsonSerializer.Serialize(new { ErrorCode = 9999, ErrorMsg = "test message" }))
                    .Send();
                    Console.WriteLine($"CreateTestWorker completed {DateTime.Now}");
                }
                catch (Exception ex)
                {
                    await client.NewThrowErrorCommand(job.Key)
                    .ErrorCode("9999")
                    .Variables(JsonSerializer.Serialize(new { ErrorCode = 9999, ErrorMsg = ex.Message }))
                    .Send();
                }
            },1);
        }

        public async Task Test1Worker()
        {
            await CreateWorker("test1_def", async (client, job) =>
            {
                try
                {
                    //throw new Exception();
                    //Task.Delay(5000);
                    await client.NewCompleteJobCommand(job.Key)
                    .Variables(JsonSerializer.Serialize(new { ErrorCode = 9999, ErrorMsg = "test message" }))
                    .Send();
                }
                catch (Exception ex)
                {
                    await client.NewThrowErrorCommand(job.Key)
                    .ErrorCode("9999")
                    .Variables(JsonSerializer.Serialize(new { ErrorCode = 9999, ErrorMsg = ex.Message }))
                    .Send();
                }
            });
        }

        public async Task Test2Worker()
        {
            await CreateWorker("test2_def", async (client, job) =>
            {
                try
                {
                    //throw new Exception();
                    //Task.Delay(5000);
                    await client.NewCompleteJobCommand(job.Key)
                    .Variables(JsonSerializer.Serialize(new { ErrorCode = 9999, ErrorMsg = "test message" }))
                    .Send();
                }
                catch (Exception ex)
                {
                    await client.NewThrowErrorCommand(job.Key)
                    .ErrorCode("9999")
                    .Variables(JsonSerializer.Serialize(new { ErrorCode = 9999, ErrorMsg = ex.Message }))
                    .Send();
                }
            });
        }

        private Task CreateWorker(string jobType, AsyncJobHandler handleJob, int maxJobsActive = 5)
        {
            _client.NewWorker()
                    .JobType(jobType)
                    .Handler(handleJob)
                    .MaxJobsActive(maxJobsActive)
                    .Name(jobType)
                    .PollInterval(TimeSpan.FromSeconds(60))
                    .PollingTimeout(TimeSpan.FromSeconds(60))
                    .Timeout(TimeSpan.FromSeconds(60))
                    .Open();

            return Task.CompletedTask;
        }
    }
}
